using System;

namespace Isle.World.Time
{
    /// <summary>SYS-WORLD-01 §Time. Named phases of the in-game day.</summary>
    public enum DayPhase
    {
        Dawn,
        Day,
        Dusk,
        Night
    }

    /// <summary>
    /// SYS-WORLD-01 §Time. Accumulates in-game minutes at a fixed rate against real seconds.
    /// Plain C#, no Unity refs — server holds the authoritative instance (Absolute rule 2);
    /// clients receive WorldTime, never advance their own.
    /// </summary>
    public sealed class WorldClock
    {
        public const long MinutesPerDay = 1440;

        /// <summary>1 real second = 1.2 in-game minutes (1 in-game day = 20 real minutes).</summary>
        public const double MinutesPerRealSecond = 1.2;

        private double _totalMinutes;

        public WorldClock(long startMinutes = 0) => _totalMinutes = startMinutes;

        /// <summary>Accumulated in-game minutes since world start.</summary>
        public long TotalMinutes => (long)_totalMinutes;

        /// <summary>Minutes since local midnight (0–1439).</summary>
        public long MinuteOfDay => TotalMinutes % MinutesPerDay;

        public DayPhase Phase => PhaseAt(MinuteOfDay);

        public void Tick(double realSeconds) => _totalMinutes += realSeconds * MinutesPerRealSecond;

        /// <summary>SYS-WORLD-01 §Time phase table. <paramref name="minuteOfDay"/> must be 0–1439.</summary>
        public static DayPhase PhaseAt(long minuteOfDay)
        {
            if (minuteOfDay is >= 300 and < 420) return DayPhase.Dawn;
            if (minuteOfDay is >= 420 and < 1020) return DayPhase.Day;
            if (minuteOfDay is >= 1020 and < 1140) return DayPhase.Dusk;
            return DayPhase.Night;
        }
    }
}
