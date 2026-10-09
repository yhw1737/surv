using System;

namespace Isle.Gameplay.Cooking
{
    /// <summary>
    /// SYS-COOK-01 §Cook time (developer, 2026-10-09): cooking takes time, like gathering —
    /// <c>(base + perIngredient × n) × method time_mult × (1 − 0.5 × level / 50)</c>. Static pure.
    /// </summary>
    public static class CookTimeCalculator
    {
        public const float BaseSeconds = 4f;
        public const float PerIngredientSeconds = 2f;

        /// <summary>Share of the time a Lv 50 cook saves.</summary>
        public const float MasterSaving = 0.5f;

        const float MaxLevel = 50f;

        public static float Seconds(int ingredients, float timeMult, int cookingLevel) =>
            Math.Max(0f, (BaseSeconds + PerIngredientSeconds * Math.Max(0, ingredients)) * Math.Max(0f, timeMult)
                         * (1f - MasterSaving * Math.Clamp(cookingLevel, 0, (int)MaxLevel) / MaxLevel));
    }
}
