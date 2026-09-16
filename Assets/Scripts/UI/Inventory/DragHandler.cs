using Isle.Core;
using Isle.Data;
using Isle.Gameplay.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Isle.UI.Inventory
{
    /// <summary>
    /// SYS-INV-01 §Required UX: drag to move, R/Q/E to rotate while dragging, Ctrl+click to
    /// bulk-move into <see cref="GridView.PairedView"/>, Shift+drag to split a stack. One instance
    /// per spawned item icon (<see cref="GridView.Redraw"/>).
    /// <para>
    /// Rotating only while dragging (not while hovering a stored item) matches how extraction-shooter
    /// grid inventories (e.g. Escape from Tarkov) handle it — rotating an item that's already settled
    /// into a spot has nothing to preview against, since it isn't moving.
    /// </para>
    /// <para>
    /// Uses <see cref="Keyboard.current"/> (Input System) — the project's established input
    /// convention, see <c>PlayerMovement.cs</c>.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public sealed class DragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerClickHandler
    {
        GridView _owner;
        Placement _placement;
        RectTransform _rect;
        CanvasGroup _canvasGroup;
        Canvas _rootCanvas;

        Vector2 _dragStartPosition;
        Transform _dragStartParent;
        bool _dragRotated;
        int _dragCount;
        GameObject _splitGhost;
        bool _dragging;

        public void Bind(GridView owner, Placement placement)
        {
            _owner = owner;
            _placement = placement;
            _rect = (RectTransform)transform;
            _canvasGroup = GetComponent<CanvasGroup>();
            _rootCanvas = GetComponentInParent<Canvas>();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.ctrlKey.isPressed) return;
            if (_owner.PairedView == null) return;

            if (!_owner.Inventory.TryMoveTo(_owner.PairedView.Inventory, _placement)) return;
            _owner.Redraw();
            _owner.PairedView.Redraw();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragStartPosition = _rect.anchoredPosition;
            _dragStartParent = _rect.parent;
            _dragRotated = _placement.Rotated;
            _dragging = true;

            var kb = Keyboard.current;
            _dragCount = kb != null && kb.shiftKey.isPressed && _placement.Count > 1
                ? _placement.Count / 2
                : _placement.Count;

            // A split drag moves this exact icon away with the pointer, so without a stand-in the
            // remainder just looks like it vanished from its original spot for the whole drag.
            if (_dragCount < _placement.Count)
                _splitGhost = _owner.SpawnGhostIcon(_placement, _placement.Count - _dragCount);

            // Reparent to the canvas root and put it last in sibling order — otherwise the icon
            // stays nested under its own GridView, so hovering a different, later-drawn panel (e.g.
            // the warehouse) draws that panel's cells on top of the icon still following the pointer.
            if (_rootCanvas != null)
            {
                _rect.SetParent(_rootCanvas.transform, true);
                _rect.SetAsLastSibling();
            }

            // Dim the dragged icon and let raycasts pass through it so drop targets underneath are hit-testable.
            _canvasGroup.alpha = 0.6f;
            _canvasGroup.blocksRaycasts = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            var scale = _rootCanvas != null ? _rootCanvas.scaleFactor : 1f;
            _rect.anchoredPosition += eventData.delta / scale;
        }

        // ponytail: rotate keys are polled here, not in OnDrag — OnDrag only fires on frames the
        // pointer actually moves, so a key pressed while the mouse sits still would be missed or
        // read a frame late. Update() runs every frame regardless, matching PlayerMovement's WASD.
        void Update()
        {
            if (!_dragging) return;

            var kb = Keyboard.current;
            if (kb == null || !(kb.rKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame)) return;
            if (_placement.Item.Grid.W == _placement.Item.Grid.H) return; // spec: square items can't meaningfully rotate

            _dragRotated = !_dragRotated;
            var size = _dragRotated
                ? new GridSize { W = _placement.Item.Grid.H, H = _placement.Item.Grid.W }
                : _placement.Item.Grid;
            _rect.sizeDelta = new Vector2(size.W * GridView.CellSizePx, size.H * GridView.CellSizePx);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
            _dragging = false;

            if (_splitGhost != null)
            {
                Destroy(_splitGhost);
                _splitGhost = null;
            }

            var gridTarget = FindInHovered<GridView>(eventData);
            if (gridTarget != null && TryDrop(gridTarget)) return;

            // T-044: equip slots are a second kind of drop target, separate from a GridView's cells.
            var slotTarget = FindInHovered<EquipSlotView>(eventData);
            if (slotTarget != null && TryEquip(slotTarget)) return;

            // Snap back on any failure — an item must never disappear on a bad drop. On success the
            // icon gets destroyed by Redraw() regardless of its (reparented) current parent, so only
            // the failure path needs to undo the OnBeginDrag reparent.
            _rect.SetParent(_dragStartParent, false);
            _rect.anchoredPosition = _dragStartPosition;
        }

        static T FindInHovered<T>(PointerEventData eventData) where T : Component
        {
            foreach (var result in eventData.hovered)
            {
                var component = result.GetComponentInParent<T>();
                if (component != null) return component;
            }
            return null;
        }

        /// <summary>Ignores Shift/split state — equip items are single units, there's no "equip half a stack".</summary>
        bool TryEquip(EquipSlotView slotTarget)
        {
            if (!slotTarget.TryEquipDrop(_placement.Item)) return false;

            _owner.Inventory.Remove(_placement);
            _owner.Redraw();
            return true;
        }

        bool TryDrop(GridView target)
        {
            var camera = target.GetComponentInParent<Canvas>()?.worldCamera;
            // ponytail: snap by the icon's own visual top-left corner (its pivot, GridView.Redraw),
            // not the raw pointer position — the pointer can be grabbed anywhere over the icon, but
            // what the player sees snapping into a cell is the icon itself.
            var screenPosition = RectTransformUtility.WorldToScreenPoint(camera, _rect.position);
            var cell = target.ScreenToCell(screenPosition, camera);

            if (_dragCount < _placement.Count)
                return _owner.Inventory.TrySplit(_placement, _dragCount, target.Inventory, cell, _dragRotated)
                       && FinishDrop(target);

            if (target == _owner) return MoveWithinSameView(cell);

            if (!target.Inventory.TryPlace(_placement.Item, cell, _dragRotated, _placement.Count)) return false;

            _owner.Inventory.Remove(_placement);
            return FinishDrop(target);
        }

        bool MoveWithinSameView(Vec2Int cell)
        {
            _owner.Inventory.Remove(_placement);
            if (_owner.Inventory.TryPlace(_placement.Item, cell, _dragRotated, _placement.Count))
                return FinishDrop(_owner);

            // Failed at the new spot — put it back exactly where it was.
            _owner.Inventory.TryPlace(_placement.Item, _placement.Position, _placement.Rotated, _placement.Count);
            return false;
        }

        bool FinishDrop(GridView target)
        {
            _owner.Redraw();
            if (target != _owner) target.Redraw();
            return true;
        }
    }
}
