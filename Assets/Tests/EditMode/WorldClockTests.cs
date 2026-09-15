using Isle.World.Time;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-WORLD-01 §Time.</summary>
    public sealed class WorldClockTests
    {
        [TestCase(0, DayPhase.Night)]
        [TestCase(299, DayPhase.Night)]
        [TestCase(300, DayPhase.Dawn)]
        [TestCase(419, DayPhase.Dawn)]
        [TestCase(420, DayPhase.Day)]
        [TestCase(1019, DayPhase.Day)]
        [TestCase(1020, DayPhase.Dusk)]
        [TestCase(1139, DayPhase.Dusk)]
        [TestCase(1140, DayPhase.Night)]
        [TestCase(1439, DayPhase.Night)]
        public void PhaseAt_MatchesSpecTable(long minuteOfDay, DayPhase expected)
        {
            Assert.AreEqual(expected, WorldClock.PhaseAt(minuteOfDay));
        }

        [Test]
        public void Tick_OneRealSecond_AdvancesOnePointTwoMinutes()
        {
            var clock = new WorldClock();
            clock.Tick(1.0);
            Assert.AreEqual(1, clock.TotalMinutes);
        }

        [Test]
        public void Tick_TwentyRealMinutes_AdvancesOneFullDay()
        {
            var clock = new WorldClock();
            clock.Tick(20 * 60);
            Assert.AreEqual(WorldClock.MinutesPerDay, clock.TotalMinutes);
        }

        [Test]
        public void MinuteOfDay_AfterMultipleDays_WrapsToRemainder()
        {
            var clock = new WorldClock(WorldClock.MinutesPerDay * 3 + 500);
            Assert.AreEqual(500, clock.MinuteOfDay);
        }

        [Test]
        public void Phase_AfterMultipleDays_UsesWrappedMinuteOfDay()
        {
            var clock = new WorldClock(WorldClock.MinutesPerDay * 2 + 300);
            Assert.AreEqual(DayPhase.Dawn, clock.Phase);
        }
    }
}
