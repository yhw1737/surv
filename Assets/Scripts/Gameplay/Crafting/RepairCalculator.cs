using System;
using Isle.Data;

namespace Isle.Gameplay.Crafting
{
    /// <summary>SYS-CRAFT-01 §Repair and SYS-CRAFT-02: each repair lowers the maximum to ×0.92 (rounded) and costs half
    /// the item's recipe, rounded up. Static pure.</summary>
    public static class RepairCalculator
    {
        public const float RepairDecay = 0.92f;

        /// <summary>Share of the recipe's base XP a repair grants. [invented]</summary>
        public const float RepairXpShare = 0.5f;

        public static int NewMax(int max) => Math.Max(1, (int)Math.Floor(max * RepairDecay + 0.5f));

        public static IngredientRef[] Cost(IngredientRef[] recipe)
        {
            if (recipe == null) return Array.Empty<IngredientRef>();
            var cost = new IngredientRef[recipe.Length];
            for (var i = 0; i < recipe.Length; i++)
                cost[i] = new IngredientRef { Item = recipe[i].Item, Tag = recipe[i].Tag, Count = (Math.Max(1, recipe[i].Count) + 1) / 2 };
            return cost;
        }
    }

    /// <summary>SYS-CRAFT-02 tool tiers: which tool may work which node, and how much faster a better one is.</summary>
    public static class ToolTiers
    {
        public static bool CanWork(int toolTier, int requiredTier) => requiredTier <= 0 || toolTier >= requiredTier;

        public static float HarvestSeconds(float seconds, float toolPower) => toolPower > 0f ? seconds / toolPower : seconds;
    }
}
