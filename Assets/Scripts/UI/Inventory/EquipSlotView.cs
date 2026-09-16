using Isle.Core.Util;
using Isle.Data;
using Isle.Gameplay.Inventory;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Isle.UI.Inventory
{
    /// <summary>
    /// SYS-INV-01 §Containers: one equip slot (a name from <see cref="EquipSlots.All"/>). Drag an
    /// inventory item icon here to equip it (<see cref="DragHandler"/> routes the drop); drag the
    /// equipped icon back out (<see cref="EquipDragHandler"/>) to unequip.
    /// <para>
    /// Doesn't spawn or bind a bag's <see cref="GridView"/> itself — that's scene wiring this class
    /// doesn't own. Subscribe to <see cref="Changed"/> to show/bind one when a bag gets equipped or
    /// unequipped.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(RectTransform), typeof(Image))]
    public sealed class EquipSlotView : MonoBehaviour
    {
        // ponytail: every slot drawn at a fixed 2x2-cell frame regardless of what actually fits in
        // it — a placeholder dimension (Absolute Rule 7), not a spec value.
        const int FrameSizePx = GridView.CellSizePx * 2;

        public string SlotName { get; private set; }
        public EquipSlots Slots { get; private set; }

        /// <summary>Where <see cref="EquipDragHandler"/>'s Ctrl+click quick-unequip sends the item
        /// (e.g. the player's bag). Null just disables Ctrl+click for this slot — dragging out still
        /// always works.</summary>
        public GridView PairedView { get; set; }

        [SerializeField] RectTransform _itemLayer;

        /// <summary>Fires after any successful equip/unequip through this slot.</summary>
        public UnityEvent Changed = new();

        GameObject _icon;

        public void Bind(EquipSlots slots, string slotName)
        {
            Slots = slots;
            SlotName = slotName;

            var rect = GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(FrameSizePx, FrameSizePx);
            GetComponent<Image>().sprite = PlaceholderVisuals.AsSprite(
                PlaceholderVisuals.RoundedRect(FrameSizePx, FrameSizePx, new Color(0.12f, 0.12f, 0.12f)), GridView.CellSizePx);

            // No hand-placed layer (no baked prefab exists) — build our own, same as GridView.
            if (_itemLayer == null)
            {
                _itemLayer = new GameObject("Items", typeof(RectTransform)).GetComponent<RectTransform>();
                _itemLayer.SetParent(transform, false);
                _itemLayer.anchorMin = _itemLayer.anchorMax = _itemLayer.pivot = new Vector2(0f, 1f);
                _itemLayer.anchoredPosition = Vector2.zero;
            }

            Redraw();
        }

        /// <summary>Called by <see cref="DragHandler"/> when an inventory item icon is dropped here.</summary>
        public bool TryEquipDrop(ItemDef item)
        {
            if (!Slots.TryEquip(SlotName, item)) return false;

            Redraw();
            Changed?.Invoke();
            return true;
        }

        public void Redraw()
        {
            if (_icon != null) Destroy(_icon);

            var item = Slots.Get(SlotName);
            if (item == null) return;

            _icon = new GameObject($"Equipped_{item.Id}", typeof(RectTransform), typeof(Image),
                typeof(CanvasGroup), typeof(EquipDragHandler), typeof(ItemTooltip));
            var rect = (RectTransform)_icon.transform;
            rect.SetParent(_itemLayer, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(item.Grid.W * GridView.CellSizePx, item.Grid.H * GridView.CellSizePx);

            var color = PlaceholderVisuals.ColorForTags(item.Tags);
            _icon.GetComponent<Image>().sprite = PlaceholderVisuals.AsSprite(
                PlaceholderVisuals.RoundedRect((int)rect.sizeDelta.x, (int)rect.sizeDelta.y, color), GridView.CellSizePx);

            _icon.GetComponent<EquipDragHandler>().Bind(this, item);
            // No Placement exists for an equipped item (no grid position/rotation) — build a bare one
            // just to feed ItemTooltip's existing name/weight/size display.
            _icon.GetComponent<ItemTooltip>().Bind(new Placement(item, default, false));
            GridView.BuildLabel(rect, item.Name, 1);
        }
    }
}
