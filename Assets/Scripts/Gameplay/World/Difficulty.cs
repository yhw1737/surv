namespace Isle.Gameplay.World
{
    public enum DifficultyLevel { Easy, Normal, Hard, Unlimited }

    /// <summary>
    /// SYS-DIFF-01: the world's difficulty, picked at creation and saved with it. Scales hunger/thirst drain and the damage
    /// creatures deal to players; Unlimited is the creative mode (no needs, no damage, the item palette). Server-side
    /// world state; host-only until co-op syncs world settings.
    /// </summary>
    public static class Difficulty
    {
        public const float EasyMult = 0.50f;
        public const float NormalMult = 1.00f;
        public const float HardMult = 1.25f;

        public static DifficultyLevel Current { get; set; } = DifficultyLevel.Normal;

        public static bool IsUnlimited => Current == DifficultyLevel.Unlimited;

        /// <summary>The tier's DifficultyMult; Unlimited is 0 (nothing drains, nothing hurts).</summary>
        public static float Mult(DifficultyLevel tier) => tier switch
        {
            DifficultyLevel.Easy => EasyMult,
            DifficultyLevel.Hard => HardMult,
            DifficultyLevel.Unlimited => 0f,
            _ => NormalMult,
        };

        /// <summary>A hunger or thirst drain from SYS-SURV-01, scaled for the current tier.</summary>
        public static float NeedsDrain(float drain) => drain * Mult(Current);

        /// <summary>A creature attack's damage to a player, scaled for the current tier (before block and armor).</summary>
        public static float CreatureDamage(float damage) => damage * Mult(Current);

        /// <summary>The id a save stores ("easy", "normal", "hard", "unlimited").</summary>
        public static string IdOf(DifficultyLevel tier) => tier.ToString().ToLowerInvariant();

        /// <summary>A saved id back to a tier; anything unknown (an old save) is Normal.</summary>
        public static DifficultyLevel Parse(string id) => id switch
        {
            "easy" => DifficultyLevel.Easy,
            "hard" => DifficultyLevel.Hard,
            "unlimited" => DifficultyLevel.Unlimited,
            _ => DifficultyLevel.Normal,
        };
    }
}
