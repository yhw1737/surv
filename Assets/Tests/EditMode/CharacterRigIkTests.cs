using Isle.Gameplay.Character;
using Isle.Gameplay.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D.IK;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-CHAR-01 §Rig and §Angles — the parts of T-002 that are not a matter of taste.</summary>
    public sealed class CharacterRigIkTests
    {
        GameObject _instance;

        [SetUp]
        public void SetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlaceholderRigBuilder.PrefabPath);
            Assert.IsNotNull(prefab, "Run ISLE > Rebuild placeholder character rig.");
            _instance = Object.Instantiate(prefab);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_instance);

        [Test]
        public void Manager_DrivesOneSolverPerArm()
        {
            var manager = _instance.GetComponent<IKManager2D>();
            Assert.IsNotNull(manager);
            Assert.AreEqual(2, manager.solvers.Count);
            CollectionAssert.AllItemsAreInstancesOfType(manager.solvers, typeof(LimbSolver2D));
        }

        [TestCase("IK_Arm_Front", "Hand_Front")]
        [TestCase("IK_Arm_Back", "Hand_Back")]
        public void EachArmSolver_IsValidAndEndsAtItsHand(string solverName, string hand)
        {
            var solver = _instance.transform.Find(solverName).GetComponent<LimbSolver2D>();
            var chain = solver.GetChain(0);

            Assert.AreEqual(hand, chain.effector.name);
            // Limb is a 2-bone solver: upper, lower, hand.
            Assert.AreEqual(3, chain.transformCount);
            Assert.IsTrue(solver.isValid, $"{solverName} chain did not validate");
            Assert.IsNotNull(chain.target);
        }

        [TestCase("IK_Arm_Front")]
        [TestCase("IK_Arm_Back")]
        public void SolverTarget_LivesOutsideTheChainItDrives(string solverName)
        {
            // A target parented inside its own chain moves when the chain solves, so the solver
            // chases itself. IKChain2D.Validate rejects it outright.
            var chain = _instance.transform.Find(solverName).GetComponent<LimbSolver2D>().GetChain(0);
            Assert.IsFalse(chain.target.IsChildOf(chain.rootTransform));
        }

        [Test]
        public void HeadAngle_ClampsToSeventyDegrees()
        {
            var rig = _instance.GetComponent<CharacterRig>();
            rig.HeadTargetAngle = 120f;
            rig.SnapHead();
            Assert.AreEqual(CharacterRig.HeadMaxAngle, rig.HeadAngle, 0.01f);

            rig.HeadTargetAngle = -120f;
            rig.SnapHead();
            Assert.AreEqual(-CharacterRig.HeadMaxAngle, rig.HeadAngle, 0.01f);
        }

        [Test]
        public void HeadAngle_EasesRatherThanSnapping()
        {
            var rig = _instance.GetComponent<CharacterRig>();
            rig.HeadTargetAngle = 60f;

            rig.Step(1f / 60f);
            Assert.Less(rig.HeadAngle, 30f, "one frame moved more than half the arc — that is a snap");

            // SmoothDamp is within a few percent after roughly three time constants.
            for (var i = 0; i < 60; i++) rig.Step(1f / 60f);
            Assert.AreEqual(60f, rig.HeadAngle, 0.5f);
        }

        [Test]
        public void HeadRotation_IgnoresTorsoTwist()
        {
            // Head hangs off the torso, so without cancelling the parent the two angles add up and
            // the head overshoots its ±70° limit. T-003 twists the torso; this is the guard.
            var rig = _instance.GetComponent<CharacterRig>();
            var torso = _instance.transform.Find("Hip/Torso");
            var head = _instance.transform.Find("Hip/Torso/Head");

            rig.HeadTargetAngle = 40f;
            rig.SnapHead();
            var withoutTwist = head.rotation.eulerAngles.z;

            torso.localRotation = Quaternion.Euler(0f, 0f, 20f);
            rig.SnapHead();
            Assert.AreEqual(withoutTwist, head.rotation.eulerAngles.z, 0.01f);
        }
    }
}
