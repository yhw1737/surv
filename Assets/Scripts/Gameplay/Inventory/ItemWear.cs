using Isle.Data;

namespace Isle.Gameplay.Inventory
{
    /// <summary>
    /// SYS-CRAFT-02: how worn one item is. It travels with the item — on its equip slot, its bag placement, a loot pile,
    /// a save — and is null for items that can't wear out. A null wear on a durable item means "brand new"; one is
    /// created the first time the item is used. Server-side state.
    /// </summary>
    public sealed class ItemWear
    {
        public int Current { get; private set; }
        public int Max { get; private set; }

        /// <summary>SYS-HUNT-01: for a carried carcass, the body it holds (weight, condition, spoilage). The wear numbers
        /// are unused then. Same travel path as wear: slot, bag, pile, save.</summary>
        public Hunting.CarcassState Carcass { get; init; }

        /// <summary>SYS-CRAFT-01 §Quality: the tier it was crafted at; null for items that weren't crafted (start gear,
        /// salvage), which count as Common. Kept through repairs.</summary>
        public Crafting.QualityTier? Quality { get; init; }

        public bool Broken => Current <= 0;
        public bool NeedsRepair => Current < Max;
        public float Fraction => Max > 0 ? Current / (float)Max : 0f;

        public ItemWear(int current, int max)
        {
            Max = max < 1 ? 1 : max;
            Current = current < 0 ? 0 : current > Max ? Max : current;
        }

        /// <summary>A new item's wear, or null if <paramref name="item"/> can't wear out.</summary>
        public static ItemWear Fresh(ItemDef item) =>
            item?.Durability is { } durability && durability > 0 ? new ItemWear(durability, durability) : null;

        public void Use(int amount = 1)
        {
            Current -= amount;
            if (Current < 0) Current = 0;
        }

        /// <summary>SYS-CRAFT-01 §Repair: the ceiling erodes, then it's restored up to the new ceiling.</summary>
        public void Repair()
        {
            Max = Crafting.RepairCalculator.NewMax(Max);
            Current = Max;
        }
    }
}
