using Isle.Gameplay.Skills;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-SKILL-01 §Focus — 7-day median distinct active players.</summary>
    public sealed class ActivityTrackerTests
    {
        [Test]
        public void MedianActivePlayers_NoDaysRecorded_DefaultsToSolo()
        {
            var tracker = new ActivityTracker();
            Assert.AreEqual(1, tracker.MedianActivePlayers());
        }

        [Test]
        public void MedianActivePlayers_OddCountRecorded_ReturnsMiddleValue()
        {
            var tracker = new ActivityTracker();
            foreach (var day in new[] { 1, 4, 2 }) tracker.RecordDay(day);

            Assert.AreEqual(2, tracker.MedianActivePlayers());
        }

        [Test]
        public void MedianActivePlayers_FullWindow_IgnoresDaysOlderThanSeven()
        {
            var tracker = new ActivityTracker();
            // Fill the window with a run of 4s, then push one day of 1 off the back of the ring.
            for (var i = 0; i < 7; i++) tracker.RecordDay(4);
            tracker.RecordDay(1);

            // 6 days of 4 and 1 day of 1 -> median is still 4.
            Assert.AreEqual(4, tracker.MedianActivePlayers());
        }
    }
}
