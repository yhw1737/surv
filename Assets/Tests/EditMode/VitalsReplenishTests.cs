using Isle.Gameplay.Character;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>Food and drink restore a gauge, clamped to the 0–100 range (SYS-SURV-01 §Gauges).</summary>
    public sealed class VitalsReplenishTests
    {
        const float Tolerance = 0.001f;

        [Test]
        public void Replenish_WithinRange_AddsAmount()
        {
            Assert.AreEqual(62f, VitalsCalculator.Replenish(50f, 12f), Tolerance);
        }

        [Test]
        public void Replenish_OverflowsMax_ClampsTo100()
        {
            Assert.AreEqual(100f, VitalsCalculator.Replenish(95f, 10f), Tolerance);
        }
    }
}
