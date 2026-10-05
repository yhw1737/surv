using Isle.Gameplay.Combat;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-COMBAT-01 §Ranged: 0.8 s full charge, uncharged ×0.4, effective 12 tiles / max 20 with ×0.6
    /// beyond, and sway = 1.8 × (1 − Lv/50) × stanceMult.</summary>
    public sealed class RangedCalculatorTests
    {
        const float Tolerance = 0.001f;

        [TestCase(0.8f, 1f)]
        [TestCase(1.5f, 1f)]
        [TestCase(0.79f, 0.4f)]
        [TestCase(0f, 0.4f)]
        public void ChargeMult_Seconds_FullOrUncharged(float seconds, float expected)
        {
            Assert.AreEqual(expected, RangedCalculator.ChargeMult(seconds), Tolerance);
        }

        [TestCase(5f, 1f)]
        [TestCase(12f, 1f)]
        [TestCase(12.1f, 0.6f)]
        [TestCase(20f, 0.6f)]
        public void RangeMult_Travelled_FallsOffPastEffective(float travelled, float expected)
        {
            Assert.AreEqual(expected, RangedCalculator.RangeMult(travelled), Tolerance);
        }

        [Test]
        public void SwayRadius_Lv0Standing_Returns1_8()
        {
            Assert.AreEqual(1.8f, RangedCalculator.SwayRadiusTiles(0, Stance.Standing), Tolerance);
        }

        [Test]
        public void SwayRadius_Lv50Crouched_ReturnsZero()
        {
            Assert.AreEqual(0f, RangedCalculator.SwayRadiusTiles(50, Stance.Crouched), Tolerance);
        }

        [Test]
        public void SwayRadius_Lv0Moving_Returns3_96()
        {
            Assert.AreEqual(3.96f, RangedCalculator.SwayRadiusTiles(0, Stance.Moving), Tolerance);
        }

        [Test]
        public void CanAim_Sprinting_ReturnsFalse()
        {
            Assert.IsFalse(RangedCalculator.CanAim(Stance.Sprinting));
            Assert.IsTrue(RangedCalculator.CanAim(Stance.Moving));
        }
    }
}
