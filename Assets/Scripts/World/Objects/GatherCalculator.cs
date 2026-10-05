using Isle.World.Time;

namespace Isle.World.Objects
{
    /// <summary>SYS-WORLD-03 §Gathering. Static pure.</summary>
    public static class GatherCalculator
    {
        /// <summary>Respawn is written in in-game minutes; the world ticks in real seconds, so the
        /// conversion goes through the same rate <see cref="WorldClock"/> uses (1 real s = 1.2 in-game min).</summary>
        public static float RespawnRealSeconds(float respawnMinutes) => (float)(respawnMinutes / WorldClock.MinutesPerRealSecond);
    }
}
