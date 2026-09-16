using Isle.Core;
using Isle.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Isle.UI.Inventory
{
    /// <summary>
    /// Drags an equipped item back out into a grid. Mirrors <see cref="DragHandler"/>'s
    /// rotate-while-dragging and drop-to-grid logic, but the source is an <see cref="EquipSlotView"/>
    /// slot rather than a <c>Placement</c> — no split (equip items are single units), so this stays
    /// its own small class rather than sharing a base with <see cref="DragHandler"/> for two call
    /// sites. Also handles Ctrl+click quick-unequip, mirroring <c>DragHandler.OnPointerClick</c>'s
    /// bulk-move.
    /// </summary>
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public sealed class EquipDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerClickHandler
    {
        EquipSlotView _owner;
        ItemDef _item;
        RectTransform _rect;
        CanvasGroup _canvasGroup;
        Canvas _rootCanvas;

        Vector2 _dragStartPosition;
        Transform _dragStartParent;
        bool _dragRotated;
        bool _dragging;

        public void Bind(EquipSlotView owner, ItemDef item)
        {
            _owner = owner;
            _item = item;
            _rect = (RectTransform)transform;
            _canvasGroup = GetComponent<CanvasGroup>();
            _rootCanvas = GetComponentInParent<Canvas>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragStartPosition = _rect.anchoredPosition;
            _dragStartParent = _rect.parent;
            _dragRotated = false;
            _dragging = true;

            // See DragHandler.OnBeginDrag — reparent to canvas root so this renders above every
            // panel, not just siblings within its own slot.
            if (_rootCanvas != null)
            {
                _rect.SetParent(_rootCanvas.transform, true);
                _rect.SetAsLastSibling();
            }

            _canvasGroup.alpha = 0.6f;
            _canvasGroup.blocksRaycasts = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            var scale = _rootCanvas != null ? _rootCanvas.scaleFactor : 1f;
            _rect.anchoredPosition += eventData.delta / scale;
        }

        // ponytail: see DragHandler.Update — rotate keys must be polled every frame, not only on
        // frames OnDrag fires (pointer movement), or a key press while the mouse sits still is missed.
        void Update()
        {
            if (!_dragging) return;

            var kb = Keyboard.current;
            if (kb == null || !(kb.rKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame)) return;
            if (_item.Grid.W == _item.Grid.H) return; // spec: square items can't meaningfully rotate

            _dragRotated = !_dragRotated;
            var size = _dragRotated ? new GridSize { W = _item.Grid.H, H = _item.Grid.W } : _item.Grid;
            _rect.sizeDelta = new Vector2(size.W * GridView.CellSizePx, size.H * GridView.CellSizePx);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _dragging = false;

            foreach (var result in eventData.hovered)
            {
                var target = result.GetComponentInParent<GridView>();
                if (target != null && TryDrop(target)) return;
            }

            // See DragHandler.OnEndDrag — reset here, not up front, so a pending networked request
            // (T-045) stays dimmed until its ack's Redraw() replaces this icon.
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
            _rect.SetParent(_dragStartParent, false);
            _rect.anchoredPosition = _dragStartPosition;
        }

        /// <summary>Ctrl+click: quick-unequip into <see cref="EquipSlotView.PairedView"/> (its first
        /// free spot), no drag needed. Mirrors <c>DragHandler.OnPointerClick</c>'s bulk-move.</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.ctrlKey.isPressed) return;
            if (_owner.PairedView == null) return;

            var spot = _owner.PairedView.Inventory.FindPlacementSpot(_item, rotated: false);
            if (spot == null) return;

            UnequipInto(_owner.PairedView, spot.Value.position, spot.Value.rotated);
        }

        bool TryDrop(GridView target)
        {
            var camera = target.GetComponentInParent<Canvas>()?.worldCamera;
            // ponytail: snap by the icon's own visual top-left corner — see DragHandler.TryDrop.
            var screenPosition = RectTransformUtility.WorldToScreenPoint(camera, _rect.position);
            var cell = target.ScreenToCell(screenPosition, camera);
            return UnequipInto(target, cell, _dragRotated);
        }

        /// <summary>Unequips into <paramref name="target"/> at <paramref name="position"/>, rolling
        /// the equip back if the destination doesn't fit. Shared by drag-drop and Ctrl+click.</summary>
        bool UnequipInto(GridView target, Vec2Int position, bool rotated)
        {
            if (_owner.Network != null) return UnequipIntoNetworked(target, position, rotated);

            // Unequip first: fails safely (item stays put) if the slot's bag still holds items.
            if (!_owner.Slots.Unequip(_owner.SlotName)) return false;

            if (target.Inventory.TryPlace(_item, position, rotated))
            {
                _owner.Redraw();
                _owner.Changed?.Invoke();
                target.Redraw();
                return true;
            }

            // Didn't fit at the destination — put it back on the slot exactly as it was.
            _owner.Slots.TryEquip(_owner.SlotName, _item);
            _owner.Redraw();
            return false;
        }

        /// <summary>T-045: only unequipping into the same player's own networked bag has server
        /// authority (see <c>InventoryNetwork</c>'s class remarks) — reject a drop onto anything
        /// else (e.g. a warehouse) rather than silently bypass it locally.</summary>
        bool UnequipIntoNetworked(GridView target, Vec2Int position, bool rotated)
        {
            if (target.Network != _owner.Network) return false;

            var owner = _owner;
            owner.Network.RequestUnequip(owner.SlotName, position, rotated, ok =>
            {
                owner.Redraw();
                target.Redraw();
                if (ok) owner.Changed?.Invoke();
            });
            return true;
        }
    }
}
