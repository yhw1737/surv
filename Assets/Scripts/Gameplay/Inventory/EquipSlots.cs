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
        readonly Dictionary<string, ItemWear> _wear = new();

        public ItemDef Get(string slot) => _equipped.GetValueOrDefault(slot);

        /// <summary>The item in the slot if it does its job — null when empty or broken (SYS-CRAFT-02: a broken
        /// weapon fights as bare hands, a broken tool counts as no tool, broken armor gives no armor).</summary>
        public ItemDef Working(string slot)
        {
            var item = Get(slot);
            return item != null && _wear.TryGetValue(slot, out var wear) && wear.Broken ? null : item;
        }

        /// <summary>The slot's wear, or null (nothing there, can't wear out, or untouched).</summary>
        public ItemWear WearOf(string slot) => _wear.GetValueOrDefault(slot);

        /// <summary>Wears the slot's item by <paramref name="amount"/> (creating its wear on first use). Returns true
        /// when this use broke it.</summary>
        public bool Wear(string slot, int amount)
        {
            var item = Get(slot);
            if (item == null) return false;
            if (!_wear.TryGetValue(slot, out var wear))
            {
                wear = ItemWear.Fresh(item);
                if (wear == null) return false;
                _wear[slot] = wear;
            }
            var wasBroken = wear.Broken;
            wear.Use(amount);
            return !wasBroken && wear.Broken;
        }

        /// <summary>The grid a bag opened, or null if nothing's equipped there or it isn't a bag.</summary>
        public GridInventory BagFor(string slot) => _bags.GetValueOrDefault(slot);

        /// <summary>Fails if <paramref name="item"/>'s own <see cref="ItemDef.EquipSlot"/> doesn't match
        /// <paramref name="slot"/>, or the slot is already occupied — no implicit swap, call
        /// <see cref="Unequip"/> first.</summary>
        public bool TryEquip(string slot, ItemDef item, ItemWear wear = null)
        {
            if (item.EquipSlot != slot) return false;
            if (_equipped.ContainsKey(slot)) return false;

            _equipped[slot] = item;
            if (wear != null) _wear[slot] = wear;
            if (item.BagGrid is { } bagGrid) _bags[slot] = new GridInventory(bagGrid.W, bagGrid.H);
            return true;
        }

        /// <summary>Fails — leaving the item equipped — if the slot's bag still holds items. An item
        /// must never disappear because its container got unequipped.</summary>
        public bool Unequip(string slot) => Unequip(slot, out _);

        /// <summary>As <see cref="Unequip(string)"/>, handing back the item's wear so it can travel with the item.</summary>
        public bool Unequip(string slot, out ItemWear wear)
        {
            wear = null;
            if (!_equipped.ContainsKey(slot)) return false;
            if (_bags.TryGetValue(slot, out var bag) && bag.Placements.Count > 0) return false;

            wear = _wear.GetValueOrDefault(slot);
            _equipped.Remove(slot);
            _bags.Remove(slot);
            _wear.Remove(slot);
            return true;
        }

        /// <summary>Drops everything equipped at once (SYS-SURV-01 §Death — "drop entire inventory
        /// including equipment"), bypassing <see cref="Unequip"/>'s "bag still has items" guard —
        /// on death nothing is coming back into these slots, so nothing to protect.</summary>
        public void Clear()
        {
            _equipped.Clear();
            _bags.Clear();
            _wear.Clear();
        }
    }
}
