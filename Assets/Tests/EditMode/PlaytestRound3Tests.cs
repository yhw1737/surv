using Isle.Core.Util;
using Isle.Networking;
using NUnit.Framework;
using UnityEngine;

namespace Isle.Tests.EditMode
{
    /// <summary>T-307 playtest round 3: walking that winds up and slows down, and the view that turns.</summary>
    public sealed class PlaytestRound3Tests
    {
        [TearDown]
        public void TearDown() => ViewTilt.ResetYaw();

        [Test]
        public void Accelerate_FromStanding_ReachesFullSpeedInAccelSeconds()
        {
            var top = MovementCalculator.Speed(1f, false);
            var v = (X: 0f, Y: 0f);
            const float dt = 0.05f;
            v = MovementCalculator.Accelerate(v.X, v.Y, top, 0f, dt);
            Assert.Greater(v.X, 0f, "doesn't start");
            Assert.Less(v.X, top * 0.5f, "starts at full speed");
            for (var t = dt; t < MovementCalculator.AccelSeconds + 1e-4f; t += dt) v = MovementCalculator.Accelerate(v.X, v.Y, top, 0f, dt);
            Assert.AreEqual(top, v.X, 1e-3f, "not at full speed after the wind-up");
        }

        [Test]
        public void Accelerate_LettingGo_StopsWithinDecelSeconds()
        {
            var top = MovementCalculator.Speed(1f, true);
            var v = (X: top, Y: 0f);
            const float dt = 0.02f;
            v = MovementCalculator.Accelerate(v.X, v.Y, 0f, 0f, dt);
            Assert.Greater(v.X, 0f, "stopped dead");
            for (var t = dt; t < MovementCalculator.DecelSeconds + 1e-4f; t += dt) v = MovementCalculator.Accelerate(v.X, v.Y, 0f, 0f, dt);
            Assert.AreEqual(0f, v.X, 1e-3f, "still sliding");
        }

        [Test]
        public void Accelerate_NeverOvershootsTheTarget()
        {
            var v = MovementCalculator.Accelerate(0f, 0f, 1f, 0f, 1f);
            Assert.AreEqual(1f, v.X, 1e-5f);
        }

        [Test]
        public void Turn_StepsAndEasesToTheTarget()
        {
            ViewTilt.Turn(1);
            Assert.AreEqual(ViewTilt.TurnStepDegrees, ViewTilt.TargetYawDegrees, 1e-3f);
            Assert.AreEqual(0f, ViewTilt.YawDegrees, 1e-3f, "jumped instead of easing");
            Assert.IsTrue(ViewTilt.StepTurn(ViewTilt.TurnSeconds * 0.5f));
            Assert.Greater(ViewTilt.YawDegrees, 0f);
            ViewTilt.StepTurn(ViewTilt.TurnSeconds);
            Assert.AreEqual(ViewTilt.TurnStepDegrees, ViewTilt.YawDegrees, 1e-3f);
            Assert.IsFalse(ViewTilt.StepTurn(0.1f), "still turning once there");
        }

        [Test]
        public void Turn_Left_WrapsAround()
        {
            ViewTilt.Turn(-1);
            Assert.AreEqual(360f - ViewTilt.TurnStepDegrees, ViewTilt.TargetYawDegrees, 1e-3f);
        }

        [Test]
        public void ToScreen_TurnedAQuarter_WestIsUp()
        {
            ViewTilt.Turn(2); // 90°
            ViewTilt.StepTurn(10f);
            var west = ViewTilt.ToScreen(Vector2.left);
            Assert.AreEqual(0f, west.x, 1e-4f);
            Assert.AreEqual(1f, west.y, 1e-4f, "turned 90° the view looks west");
            Assert.AreEqual(Vector2.left.x, ViewTilt.ScreenUp.x, 1e-4f);
        }

        [Test]
        public void Stand_KeepsFacingTheCameraAsTheViewTurns()
        {
            var go = new GameObject("standing");
            try
            {
                ViewTilt.Stand(go.transform);
                ViewTilt.Turn(1);
                ViewTilt.StepTurn(10f);
                Assert.Less(Quaternion.Angle(go.transform.rotation, ViewTilt.Standing), 0.01f, "a standing thing didn't turn with the view");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
