namespace Isle.Gameplay.Combat
{
    public enum Stance { Standing, Moving, Crouched, Sprinting }

    /// <summary>SYS-COMBAT-01 §Ranged. Static pure, no Unity refs.</summary>
    public static class RangedCalculator
    {
        public const float FullChargeSeconds = 0.8f;
        public const float UnchargedMult = 0.4f;
        public const float EffectiveRangeTiles = 12f;
        public const float MaxRangeTiles = 20f;

        /// <summary>SYS-COMBAT-01 situationalMult: "beyond max range 0.60" — applied past the effective range,
        /// where the spec says falloff starts.</summary>
        public const float BeyondEffectiveMult = 0.6f;

        public const float SwayBase = 1.8f;

        /// <summary>The spec gives full charge and "uncharged"; anything short of full counts as uncharged.</summary>
        public static float ChargeMult(float chargeSeconds) => chargeSeconds >= FullChargeSeconds ? 1f : UnchargedMult;

        public static float RangeMult(float travelledTiles) => travelledTiles > EffectiveRangeTiles ? BeyondEffectiveMult : 1f;

        public static float SwayRadiusTiles(int skillLevel, Stance stance) =>
            SwayBase * (1f - (float)skillLevel / PowerCalculator.MaxLevel) * StanceMult(stance);

        /// <summary>SYS-COMBAT-01: "sprinting = cannot aim".</summary>
        public static bool CanAim(Stance stance) => stance != Stance.Sprinting;

        static float StanceMult(Stance stance) => stance switch
        {
            Stance.Moving => 2.2f,
            Stance.Crouched => 0.5f,
            _ => 1f,
        };
    }
}
