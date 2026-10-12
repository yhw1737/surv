using System.Collections.Generic;

namespace Isle.Gameplay.Combat
{
    /// <summary>SYS-MAGIC-01 §Mana and SYS-BREW-01 §Effects. Static pure: capacity and regen from the skills' per-level
    /// growth (data on each skill), and how an over-time effect spreads its amount.</summary>
    public static class ManaCalculator
    {
        public const float ManaBase = 100f;
        public const float ManaRegenBase = 0.5f;

        public const string Heal = "heal";
        public const string HealOverTime = "heal_over_time";
        public const string Mana = "mana";
        public const string ManaOverTime = "mana_over_time";

        /// <summary>manaMax = ManaBase + Σ mana_per_level × level, over (perLevel, level) pairs.</summary>
        public static float ManaMax(IEnumerable<(float PerLevel, int Level)> skills)
        {
            var max = ManaBase;
            foreach (var (perLevel, level) in skills) max += perLevel * level;
            return max;
        }

        /// <summary>manaRegen (per second) = ManaRegenBase + Σ mana_regen_per_level × level.</summary>
        public static float ManaRegen(IEnumerable<(float PerLevel, int Level)> skills)
        {
            var regen = ManaRegenBase;
            foreach (var (perLevel, level) in skills) regen += perLevel * level;
            return regen;
        }

        /// <summary>What an over-time effect gives during <paramref name="dt"/> seconds, given how long is left — never more
        /// than what remains of its amount.</summary>
        public static float OverTimeStep(float amount, float seconds, float remainingSeconds, float dt)
        {
            if (seconds <= 0f || remainingSeconds <= 0f) return 0f;
            return amount / seconds * System.Math.Min(dt, remainingSeconds);
        }

        public static bool IsKnownEffect(string type) => type is Heal or HealOverTime or Mana or ManaOverTime;
        public static bool IsOverTime(string type) => type is HealOverTime or ManaOverTime;
    }
}
