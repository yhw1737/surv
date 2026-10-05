using Isle.Networking;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-CHAR-01 §Movement: moveSpeed = BaseSpeed × weightMult × stanceMult, sprint 1.65, and the
    /// dodge roll's 0.6 s with i-frames from 0.1 to 0.45 s.</summary>
    public sealed class MovementCalculatorTests
    {
        const float Tolerance = 0.001f;

        [Test]
        public void Speed_Walk_ReturnsBase()
        {
            Assert.AreEqual(4.2f, MovementCalculator.Speed(weightMult: 1f, sprinting: false), Tolerance);
        }

        [Test]
        public void Speed_Sprint_Returns6_93()
        {
            Assert.AreEqual(6.93f, MovementCalculator.Speed(weightMult: 1f, sprinting: true), Tolerance);
        }

        [Test]
        public void Speed_HalfWeightPenaltyWalking_Returns3_15()
        {
            Assert.AreEqual(3.15f, MovementCalculator.Speed(weightMult: 0.75f, sprinting: false), Tolerance);
        }

        [TestCase(0.05f, false)]
        [TestCase(0.1f, true)]
        [TestCase(0.3f, true)]
        [TestCase(0.45f, true)]
        [TestCase(0.5f, false)]
        public void IsRollInvulnerable_ElapsedSeconds_MatchesWindow(float elapsed, bool expected)
        {
            Assert.AreEqual(expected, MovementCalculator.IsRollInvulnerable(elapsed));
        }

        [Test]
        public void RollSeconds_Is0_6()
        {
            Assert.AreEqual(0.6f, MovementCalculator.RollSeconds, Tolerance);
        }
    }
}
