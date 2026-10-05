namespace Isle.Gameplay.Combat
{
    /// <summary>SYS-COMBAT-01 §Power. Static pure, no Unity refs — EditMode test target.</summary>
    public static class PowerCalculator
    {
        /// <summary>SYS-COMBAT-01: "SkillFloor = 0.50, MaxLevel = 50".</summary>
        public const float SkillFloor = 0.5f;
        public const int MaxLevel = 50;

        public static float FinalPower(float basePower, int skillLevel, float qualityMult = 1f, float enchantMult = 1f, float situationalMult = 1f)
        {
            var skillFactor = SkillFloor + (1f - SkillFloor) * skillLevel / MaxLevel;
            return basePower * skillFactor * qualityMult * enchantMult * situationalMult;
        }
    }
}
