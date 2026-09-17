using System.Collections.Generic;
using Isle.Data;

namespace Isle.Gameplay.Inventory
{
    /// <summary>
    /// SYS-INV-01 §Containers: the eight equip slots. One item per slot. Equipping a bag-type item
    /// (backpack, belt pouch — <see cref="ItemDef.BagGrid"/> set) additionally opens a separate
    /// <see cref="GridInventory"/>; the base carry grid itself never grows.
    /// </summary>
    public sealed class EquipSlots
    {
        /// <summary>The fixed slot names, exactly as SYS-INV-01 §Containers lists them.</summary>
        public static readonly string[] All =
        {
            "head", "chest", "legs", "feet", "back", "belt", "main_hand", "off_hand"
        };

        readonly Dictionary<string, ItemDef> _equipped = new();
        readonly Dictionary<string, GridInventory> _bags = new();

        public ItemDef Get(string slot) => _equipped.GetValueOrDefault(slot);

        /// <summary>The grid a bag opened, or null if nothing's equipped there or it isn't a bag.</summary>
        public GridInventory BagFor(string slot) => _bags.GetValueOrDefault(slot);

        /// <summary>Fails if <paramref name="item"/>'s own <see cref="ItemDef.EquipSlot"/> doesn't match
        /// <paramref name="slot"/>, or the slot is already occupied — no implicit swap, call
        /// <see cref="Unequip"/> first.</summary>
        public bool TryEquip(string slot, ItemDef item)
        {
            if (item.EquipSlot != slot) return false;
            if (_equipped.ContainsKey(slot)) return false;

            _equipped[slot] = item;
            if (item.BagGrid is { } bagGrid) _bags[slot] = new GridInventory(bagGrid.W, bagGrid.H);
            return true;
        }

        /// <summary>Fails — leaving the item equipped — if the slot's bag still holds items. An item
        /// must never disappear because its container got unequipped.</summary>
        public bool Unequip(string slot)
        {
            if (!_equipped.ContainsKey(slot)) return false;
            if (_bags.TryGetValue(slot, out var bag) && bag.Placements.Count > 0) return false;

            _equipped.Remove(slot);
            _bags.Remove(slot);
            return true;
        }

        /// <summary>Drops everything equipped at once (SYS-SURV-01 §Death — "drop entire inventory
        /// including equipment"), bypassing <see cref="Unequip"/>'s "bag still has items" guard —
        /// on death nothing is coming back into these slots, so nothing to protect.</summary>
        public void Clear()
        {
            _equipped.Clear();
            _bags.Clear();
        }
    }
}
