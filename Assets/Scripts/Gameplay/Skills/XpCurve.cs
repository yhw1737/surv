using System;

namespace Isle.Gameplay.Skills
{
    /// <summary>SYS-SKILL-01 §XP curve. Static, no Unity refs — EditMode test target.</summary>
    public static class XpCurve
    {
        public const int MaxLevel = 50;

        /// <summary>XP required to go from <paramref name="level"/> to <paramref name="level"/> + 1.</summary>
        public static int XpToNext(int level) =>
            (int)Math.Round(80.0 * Math.Pow(level, 1.6), MidpointRounding.AwayFromZero);

        /// <summary>Total XP earned from level 1 up to (not including) <paramref name="level"/>.</summary>
        public static long TotalXpTo(int level)
        {
            long total = 0;
            for (var l = 1; l < level; l++) total += XpToNext(l);
            return total;
        }
    }
}
