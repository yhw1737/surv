namespace Isle.Gameplay.Crafting
{
    /// <summary>
    /// T-151 playtest: crafting takes time, like gathering and cooking. A recipe's <c>time_sec</c> at no skill, down to
    /// half of it at level 50 in the recipe's primary skill — the same shape as SYS-COOK-01 §Cook time. [invented]
    /// Static pure.
    /// </summary>
    public static class CraftTimeCalculator
    {
        public const float MaxSkillReduction = 0.5f;
        public const int SkillCap = 50;

        public static float Seconds(float timeSec, int level)
        {
            if (timeSec <= 0f) return 0f;
            var t = System.Math.Clamp(level, 0, SkillCap) / (float)SkillCap;
            return timeSec * (1f - MaxSkillReduction * t);
        }
    }
}
