using Isle.World.Time;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>Resting at a campfire skips to the next morning (prototype, see PROJECT_STATE.md
    /// §Decided without a spec). Target minute 420 = 07:00, the start of Day in <see cref="WorldClock.PhaseAt"/>.</summary>
    public sealed class WorldClockRestTests
    {
        const long MorningMinute = 420;

        [Test]
        public void AdvanceToNextMinuteOfDay_FromNight_LandsOnNextMorning()
        {
            var clock = new WorldClock(startMinutes: 1200);
            clock.AdvanceToNextMinuteOfDay(MorningMinute);
            Assert.AreEqual(MorningMinute, clock.MinuteOfDay);
            Assert.AreEqual(WorldClock.MinutesPerDay + MorningMinute, clock.TotalMinutes);
        }

        [Test]
        public void AdvanceToNextMinuteOfDay_EarlyNight_StaysSameDay()
        {
            var clock = new WorldClock(startMinutes: 300);
            clock.AdvanceToNextMinuteOfDay(MorningMinute);
            Assert.AreEqual(MorningMinute, clock.TotalMinutes);
        }

        [Test]
        public void AdvanceToNextMinuteOfDay_AlreadyPastTarget_WrapsToNextDay()
        {
            var clock = new WorldClock(startMinutes: 500);
            clock.AdvanceToNextMinuteOfDay(MorningMinute);
            Assert.AreEqual(WorldClock.MinutesPerDay + MorningMinute, clock.TotalMinutes);
        }
    }
}
