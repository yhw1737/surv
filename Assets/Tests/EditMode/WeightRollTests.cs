using Isle.Gameplay.Combat;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-HUNT-01 §Weight: lognormal per individual, clamped to the def's min/max.
    /// Uniform inputs are passed in directly so the Box-Muller step is deterministic.</summary>
    public sealed class WeightRollTests
    {
        const float Tolerance = 0.01f;

        [Test]
        public void Sample_ZeroNoise_ReturnsMean()
        {
            // u1 = 0.5, u2 = 0.25 → z = 0 exactly, so no spread at all.
            Assert.AreEqual(62f, WeightRoll.Sample(62f, 0.28f, 30f, 120f, u1: 0.5, u2: 0.25), Tolerance);
        }

        [Test]
        public void Sample_ExtremeLow_ClampsToMin()
        {
            Assert.AreEqual(30f, WeightRoll.Sample(62f, 1f, 30f, 120f, u1: 0.0001, u2: 0.5), Tolerance);
        }

        [Test]
        public void Sample_ExtremeHigh_ClampsToMax()
        {
            Assert.AreEqual(120f, WeightRoll.Sample(62f, 1f, 30f, 120f, u1: 0.0001, u2: 0.0), Tolerance);
        }
    }
}
