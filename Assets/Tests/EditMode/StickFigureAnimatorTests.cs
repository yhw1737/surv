using Isle.UI.Art;
using NUnit.Framework;
using UnityEngine;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-CHAR-02 §Animation: the procedural animator settles into the poses its states describe.</summary>
    public sealed class StickFigureAnimatorTests
    {
        static StickFigureAnimator Run(FigureInput input, float seconds, float dt = 1f / 60f)
        {
            var animator = new StickFigureAnimator();
            for (var t = 0f; t < seconds; t += dt)
            {
                input.ActionTime = t;
                animator.Step(input, dt);
            }
            return animator;
        }

        [Test]
        public void Animator_Idle_StandsUprightWithFeetOnGround()
        {
            var pose = Run(new FigureInput(), 2f).Pose;
            Assert.AreEqual(0f, pose.FootFront.y, 1e-4f);
            Assert.AreEqual(0f, pose.FootBack.y, 1e-4f);
            Assert.Greater(pose.Head.y, pose.Neck.y);
            Assert.Greater(pose.Neck.y, pose.Hip.y);
            Assert.AreEqual(0f, pose.Rotation, 1e-3f);
        }

        [Test]
        public void Animator_Limbs_KeepTheirLengths()
        {
            var pose = Run(new FigureInput { Speed = 3f, Distance = 3f / 60f }, 1.3f).Pose;
            Assert.AreEqual(StickFigureAnimator.Thigh, Vector2.Distance(pose.Hip, pose.KneeFront), 1e-3f);
            Assert.AreEqual(StickFigureAnimator.UpperArm, Vector2.Distance(pose.Shoulder, pose.ElbowBack), 1e-3f);
            Assert.AreEqual(StickFigureAnimator.ForeArm, Vector2.Distance(pose.ElbowFront, pose.HandFront), 1e-3f);
        }

        [Test]
        public void Animator_Walking_LiftsOneFootAtATime()
        {
            var animator = new StickFigureAnimator();
            float maxFront = 0f, maxBack = 0f;
            for (var i = 0; i < 240; i++)
            {
                animator.Step(new FigureInput { Speed = 3f, Distance = 3f / 60f }, 1f / 60f);
                var p = animator.Pose;
                maxFront = Mathf.Max(maxFront, p.FootFront.y);
                maxBack = Mathf.Max(maxBack, p.FootBack.y);
                Assert.IsFalse(p.FootFront.y > 0.01f && p.FootBack.y > 0.01f, "both feet off the ground");
            }
            Assert.Greater(maxFront, 0.05f);
            Assert.Greater(maxBack, 0.05f);
        }

        [Test]
        public void Animator_FacingLeft_SettlesToMirrored()
        {
            var pose = Run(new FigureInput { FacingTarget = -1f }, 1f).Pose;
            Assert.AreEqual(-1f, pose.FacingScale, 0.01f);
        }

        [Test]
        public void Animator_Dead_LiesFlat()
        {
            var pose = Run(new FigureInput { Action = FigureAction.Dead }, 3f).Pose;
            Assert.AreEqual(90f * Mathf.Deg2Rad, pose.Rotation, 0.02f);
            Assert.IsTrue(pose.Dead);
        }

        [Test]
        public void Animator_Roll_SpinsOnceOverTheRoll()
        {
            var animator = new StickFigureAnimator();
            animator.Step(new FigureInput { Action = FigureAction.Roll, ActionTime = 0.3f, RollDuration = 0.6f }, 1f / 60f);
            Assert.AreEqual(-Mathf.PI, animator.Pose.Rotation, 1e-3f);
        }

        [Test]
        public void Animator_HoldingAWeapon_PointsItAtTheAim()
        {
            foreach (var aim in new[] { -0.8f, 0f, 0.6f })
            {
                var pose = Run(new FigureInput { HasAim = true, AimAngle = aim, HoldsItem = true }, 1.5f).Pose;
                Assert.AreEqual(aim, pose.ItemAngleFront, 0.05f, $"item angle for aim {aim}");
                var hand = (pose.HandFront - pose.Shoulder).normalized;
                Assert.Greater(Vector2.Dot(hand, new Vector2(Mathf.Cos(aim), Mathf.Sin(aim))), 0.9f, "hand not reaching along the aim");
            }
        }

        [Test]
        public void Animator_HeadIsSmall()
        {
            // Fancy Pants proportions: the head (neck to crown) is well under a fifth of the figure's height.
            var pose = Run(new FigureInput(), 1f).Pose;
            Assert.Less(pose.Head.y - pose.Neck.y, 0.25f * pose.Head.y);
        }
    }
}
