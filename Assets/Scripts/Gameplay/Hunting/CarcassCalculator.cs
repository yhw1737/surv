using System;

namespace Isle.Gameplay.Hunting
{
    /// <summary>SYS-HUNT-01 §Carrying: how a carcass of a given weight can be moved.</summary>
    public enum CarryClass
    {
        /// <summary>Under 5 kg: a 1×2 item in the bag.</summary>
        Small,
        /// <summary>5–15 kg: a 2×3 item in the bag.</summary>
        Medium,
        /// <summary>Over 15 kg: stays in the world; dragged alone or carried by two.</summary>
        WorldOnly,
    }

    /// <summary>SYS-HUNT-01 §Spoilage and scent, §Carrying. Static pure.</summary>
    public static class CarcassCalculator
    {
        // SYS-HUNT-01 constants.
        public const float SpoilRatePerMinute = 0.00069f;
        public const float SpoilHalfYield = 0.5f;
        public const float SpoilHalfYieldMult = 0.6f;
        public const float SpoilRotten = 0.8f;
        public const float ScentBase = 6.0f;
        public const float ScentPerKg = 0.12f;
        public const float ScentRollMinutes = 5f;
        public const float PredatorChance = 0.15f;
        public const float SmallMaxKg = 5f;
        public const float MediumMaxKg = 15f;
        public const float DragPenalty = 0.60f;
        public const float CoopPenalty = 0.20f;
        public const int CoopMinPlayers = 2;

        /// <summary>Spoilage past which a carcass is gone — only bones left. [invented]</summary>
        public const float SpoilGone = 1.5f;

        public static float DragSpeedMult => 1f - DragPenalty;
        public static float CoopSpeedMult => 1f - CoopPenalty;

        public static float TempFactor(float ambientTemp) => 1f + Math.Max(0f, (ambientTemp - 15f) / 15f);

        public static float Spoil(float spoilage, float minutes, float ambientTemp) =>
            spoilage + minutes * SpoilRatePerMinute * TempFactor(ambientTemp);

        /// <summary>Yield multiplier once it's going off (above 0.5 → ×0.6).</summary>
        /// <summary>Spoilage as a share of the way to gone (<see cref="SpoilGone"/>), 0..1 — what the UI shows as a
        /// percentage. Going off from 0.33, rotten from 0.53.</summary>
        public static float SpoilShare(float spoilage) => Math.Clamp(spoilage / SpoilGone, 0f, 1f);

        public static float YieldMult(float spoilage) => spoilage >= SpoilHalfYield ? SpoilHalfYieldMult : 1f;

        /// <summary>Above 0.8 only rotten meat comes off it.</summary>
        public static bool IsRotten(float spoilage) => spoilage >= SpoilRotten;

        public static float ScentRadiusTiles(float bodyWeightKg) => ScentBase + bodyWeightKg * ScentPerKg;

        public static CarryClass ClassFor(float bodyWeightKg) =>
            bodyWeightKg < SmallMaxKg ? CarryClass.Small : bodyWeightKg <= MediumMaxKg ? CarryClass.Medium : CarryClass.WorldOnly;
    }
}
