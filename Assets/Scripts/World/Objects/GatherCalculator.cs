using System;
using Isle.World.Time;

namespace Isle.World.Objects
{
    /// <summary>SYS-WORLD-03 §Gathering. Static pure.</summary>
    public static class GatherCalculator
    {
        /// <summary>Respawn is written in in-game minutes; the world ticks in real seconds, so the
        /// conversion goes through the same rate <see cref="WorldClock"/> uses (1 real s = 1.2 in-game min).</summary>
        /// <summary>SYS-WORLD-03 §Gathering: Lv 1 takes the full time, Lv 50 takes 30% of it, never under 1 s.</summary>
        public static float GatherSeconds(float timeSec, int level)
        {
            var t = Math.Clamp((level - 1) / 49f, 0f, 1f);
            return Math.Max(MinGatherSeconds, timeSec * (1f - MaxSpeedup * t));
        }

        /// <summary>[invented] — SYS-WORLD-03 revision 2026-10-05.</summary>
        public const float MaxSpeedup = 0.7f;
        public const float MinGatherSeconds = 1f;

        public static float RespawnRealSeconds(float respawnMinutes) => (float)(respawnMinutes / WorldClock.MinutesPerRealSecond);
    }
}
