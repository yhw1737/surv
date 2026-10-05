using Isle.Gameplay.Building;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-WORLD-01 §Deferred simulation: <c>growth += elapsed / growthMinutes</c>, capped at 1.0, where
    /// growthMinutes = growth_days × 1440. Computed from the planting time, so a clock skip counts in full.</summary>
    public sealed class CropCalculatorTests
    {
        [Test]
        public void Growth_HalfOfGrowthDays_ReturnsHalf()
        {
            Assert.AreEqual(0.5f, CropCalculator.Growth(plantedAtMinutes: 1000, nowMinutes: 1000 + 720, growthDays: 1), 1e-4f);
        }

        [Test]
        public void Growth_PastGrowthDays_CapsAtOne()
        {
            Assert.AreEqual(1f, CropCalculator.Growth(0, 5000, growthDays: 1), 1e-4f);
        }

        [Test]
        public void Growth_ClockBehindPlanting_IsZero()
        {
            Assert.AreEqual(0f, CropCalculator.Growth(plantedAtMinutes: 500, nowMinutes: 400, growthDays: 1), 1e-4f);
        }

        [Test]
        public void Growth_ZeroGrowthDays_IsRipe()
        {
            Assert.AreEqual(1f, CropCalculator.Growth(0, 0, growthDays: 0), 1e-4f);
        }

        [TestCase(0.999f, false)]
        [TestCase(1f, true)]
        public void IsRipe_AtOne(float growth, bool expected)
        {
            Assert.AreEqual(expected, CropCalculator.IsRipe(growth));
        }
    }
}
