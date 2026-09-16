using System;

namespace Isle.Gameplay.Inventory
{
    /// <summary>SYS-INV-01 §Weight. Static pure, no Unity refs — EditMode test target.</summary>
    public static class WeightCalculator
    {
        public const float FreeWeightKg = 15.0f;
        public const float MaxWeightKg = 45.0f;
        public const float MaxSpeedPenalty = 0.50f;
        public const float OverloadSpeedMult = 0.35f;

        /// <summary>Past this, rolling is disabled and stamina regen halts (caller's responsibility).</summary>
        public static bool IsOverloaded(float totalKg) => totalKg > MaxWeightKg;

        public static float SpeedMultiplier(float totalKg)
        {
            if (IsOverloaded(totalKg)) return OverloadSpeedMult;
            if (totalKg <= FreeWeightKg) return 1.0f;

            var over = totalKg - FreeWeightKg;
            var range = MaxWeightKg - FreeWeightKg;
            return 1.0f - Clamp01(over / range) * MaxSpeedPenalty;
        }

        /// <summary>Unlike <see cref="SpeedMultiplier"/>, overload doesn't override this — it's the same formula throughout.</summary>
        public static float StaminaDrainMultiplier(float totalKg)
        {
            var range = MaxWeightKg - FreeWeightKg;
            return 1.0f + Clamp01((totalKg - FreeWeightKg) / range) * 0.8f;
        }

        static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);
    }
}
