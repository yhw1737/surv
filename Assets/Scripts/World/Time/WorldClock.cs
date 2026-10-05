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

        /// <summary>Loading a save: put the clock back where it was.</summary>
        public void SetTotalMinutes(long totalMinutes) => _totalMinutes = totalMinutes;

        public void Tick(double realSeconds) => _totalMinutes += realSeconds * MinutesPerRealSecond;

        /// <summary>Minute of day a rest at a campfire wakes you at (07:00, the start of Day). Prototype
        /// value — SYS-WORLD-01 specs no sleep (PROJECT_STATE.md §Decided without a spec).</summary>
        public const long RestWakeMinute = 420;

        /// <summary>Jumps forward to the next occurrence of <paramref name="minuteOfDay"/>. Never moves backward:
        /// if that minute already passed today, the jump goes to tomorrow's.</summary>
        public void AdvanceToNextMinuteOfDay(long minuteOfDay)
        {
            var delta = minuteOfDay - MinuteOfDay;
            if (delta <= 0) delta += MinutesPerDay;
            _totalMinutes += delta;
        }

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
