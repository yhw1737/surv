using UnityEngine;
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

        [Test]
        public void SteerRoll_NoInput_KeepsDirection()
        {
            var (x, y) = MovementCalculator.SteerRoll(1f, 0f, 0f, 0f);
            Assert.AreEqual(1f, x, 1e-5f);
            Assert.AreEqual(0f, y, 1e-5f);
        }

        [Test]
        public void SteerRoll_HeldInput_TurnsTheRollWithinAFewTicks()
        {
            float x = 1f, y = 0f;
            (x, y) = MovementCalculator.SteerRoll(x, y, 0f, 1f);
            Assert.Greater(y, 0f, "first tick already bends toward the input");
            Assert.Greater(x, 0f, "but doesn't snap");
            for (var i = 0; i < 12; i++) (x, y) = MovementCalculator.SteerRoll(x, y, 0f, 1f);
            Assert.AreEqual(1f, y, 0.02f);
            Assert.AreEqual(1f, Mathf.Sqrt(x * x + y * y), 1e-4f, "stays unit length");
        }
    }
}
