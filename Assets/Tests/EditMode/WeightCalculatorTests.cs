using Isle.Gameplay.Inventory;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-INV-01 §Weight, verification table (±0.001).</summary>
    public sealed class WeightCalculatorTests
    {
        const float Tolerance = 0.001f;

        [TestCase(10.0f, 1.000f, true)]
        [TestCase(15.0f, 1.000f, true)]
        [TestCase(30.0f, 0.750f, true)]
        [TestCase(45.0f, 0.500f, true)]
        [TestCase(50.0f, 0.350f, false)]
        public void SpeedMultiplier_VerificationTable_MatchesSpec(float totalKg, float expectedSpeedMult, bool expectedCanRoll)
        {
            Assert.AreEqual(expectedSpeedMult, WeightCalculator.SpeedMultiplier(totalKg), Tolerance);
            Assert.AreEqual(expectedCanRoll, !WeightCalculator.IsOverloaded(totalKg));
        }

        [TestCase(10.0f, 1.0f)]
        [TestCase(15.0f, 1.0f)]
        [TestCase(30.0f, 1.4f)]
        [TestCase(45.0f, 1.8f)]
        [TestCase(50.0f, 1.8f)] // clamped: over MaxWeightKg no longer widens the (total - Free) / range term
        public void StaminaDrainMultiplier_FollowsFormula_NotClampedByOverload(float totalKg, float expected)
        {
            Assert.AreEqual(expected, WeightCalculator.StaminaDrainMultiplier(totalKg), Tolerance);
        }
    }
}
