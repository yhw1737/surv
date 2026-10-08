using System;

namespace Isle.Gameplay.Hunting
{
    /// <summary>How the killing blow landed — SYS-HUNT-01 §damageFactor.</summary>
    public enum KillMethod
    {
        /// <summary>Trap or a precise shot to a weak point.</summary>
        Trap,
        /// <summary>Bow or dagger (knife).</summary>
        Precise,
        /// <summary>Ordinary cutting or stabbing melee.</summary>
        Melee,
        /// <summary>Blunt trauma.</summary>
        Blunt,
    }

    /// <summary>
    /// SYS-HUNT-01 §Yield, §Cuts, §Carrying: how much meat and hide a carcass gives — the hunter's kill sets the bulk
    /// (<see cref="DamageFactor"/>), the butcher's Cooking level and knife decide how much of it is realised. Static pure.
    /// </summary>
    public static class ButcheryCalculator
    {
        // SYS-HUNT-01 constants.
        public const float ButcherBase = 0.30f;
        public const float ButcherSkillWeight = 0.70f;
        public const float QualityFloor = 0.15f;
        public const float QualityCeil = 1.15f;
        public const float BareHandsToolFactor = 0.40f;
        public const float OverkillPenalty = 0.85f;
        public const float OverkillExcessShare = 0.5f;
        public const float DamageFactorMin = 0.50f;
        public const float DamageFactorMax = 1.00f;
        public const float BaseButcherSeconds = 12f;
        public const int MaxButchers = 3;
        const float MaxLevel = 50f;

        public static float DamageFactor(KillMethod method, bool overkill)
        {
            var b = method switch
            {
                KillMethod.Trap => 1.00f,
                KillMethod.Precise => 0.95f,
                KillMethod.Melee => 0.85f,
                _ => 0.70f,
            };
            return Math.Clamp(b * (overkill ? OverkillPenalty : 1f), DamageFactorMin, DamageFactorMax);
        }

        /// <summary>Overkill: the blow's excess past the HP that was left is more than half the max HP.</summary>
        public static bool IsOverkill(float blowDamage, float healthBefore, float maxHealth) =>
            maxHealth > 0f && blowDamage - Math.Max(0f, healthBefore) > OverkillExcessShare * maxHealth;

        public static float Quality(int cookingLevel, float toolFactor, float damageFactor) =>
            Math.Clamp(ButcherBase + ButcherSkillWeight * (cookingLevel / MaxLevel) * toolFactor * damageFactor, QualityFloor, QualityCeil);

        public static float EdibleKg(float bodyWeightKg, float edibleRatio, float condition, int cookingLevel, float toolFactor, float damageFactor) =>
            bodyWeightKg * edibleRatio * condition * Quality(cookingLevel, toolFactor, damageFactor);

        /// <summary>Share of a damage-sensitive cut (hide, offal, bone) that survives.</summary>
        public static float SurvivalRate(float damageFactor, int cookingLevel) =>
            Math.Clamp(damageFactor * (0.4f + 0.6f * cookingLevel / MaxLevel), 0f, 1f);

        /// <summary>Whole items from <paramref name="kg"/> at <paramref name="unitKg"/> each, fractions dropped, at least
        /// <paramref name="min"/>.</summary>
        public static int Pieces(float kg, float unitKg, int min) =>
            Math.Max(min, unitKg > 0f ? (int)Math.Floor(kg / unitKg + 1e-4f) : 0);

        /// <summary>Seconds to butcher, split among up to <see cref="MaxButchers"/> people working together.</summary>
        public static float ButcherSeconds(float bodyWeightKg, int cookingLevel, int butchers = 1) =>
            BaseButcherSeconds * (bodyWeightKg / 50f) * (1.5f - 0.5f * cookingLevel / MaxLevel) / Math.Clamp(butchers, 1, MaxButchers);
    }
}
