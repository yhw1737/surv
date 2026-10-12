using Isle.UI.Prototype;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>T-303 developer debug screen: frame statistics and the world-time text it shows.</summary>
    public sealed class DebugOverlayTests
    {
        [Test]
        public void FrameStats_SteadyFrames_ReportsFpsAndMs()
        {
            var stats = new FrameStats(10);
            for (var i = 0; i < 10; i++) stats.Add(1f / 50f);
            Assert.AreEqual(50f, stats.AverageFps, 0.01f);
            Assert.AreEqual(20f, stats.AverageMs, 0.01f);
            Assert.AreEqual(20f, stats.WorstMs, 0.01f);
            Assert.AreEqual(50f, stats.LowFps, 0.01f);
        }

        [Test]
        public void FrameStats_OneSpike_ShowsInWorstAndLow()
        {
            var stats = new FrameStats(4);
            stats.Add(0.01f);
            stats.Add(0.01f);
            stats.Add(0.01f);
            stats.Add(0.05f);
            Assert.AreEqual(50f, stats.WorstMs, 0.01f);
            Assert.AreEqual(20f, stats.LowFps, 0.01f);
            Assert.AreEqual(1f / 0.02f, stats.AverageFps, 0.01f, "average over the window: 80 ms / 4 frames");
        }

        [Test]
        public void FrameStats_WindowRollsOver_ForgetsOldFrames()
        {
            var stats = new FrameStats(2);
            stats.Add(0.1f);
            stats.Add(0.01f);
            stats.Add(0.01f);
            Assert.AreEqual(10f, stats.WorstMs, 0.01f);
        }

        [Test]
        public void FrameStats_Empty_IsZero()
        {
            var stats = new FrameStats(8);
            Assert.AreEqual(0f, stats.AverageFps);
            Assert.AreEqual(0f, stats.WorstMs);
        }

        [TestCase(0L, "day 1 00:00")]
        [TestCase(1440L + 6 * 60 + 5, "day 2 06:05")]
        [TestCase(3 * 1440L + 23 * 60 + 59, "day 4 23:59")]
        public void Clock_Minutes_FormatsDayAndTime(long minutes, string expected) =>
            Assert.AreEqual(expected, DebugOverlay.ClockText(minutes));
    }
}
