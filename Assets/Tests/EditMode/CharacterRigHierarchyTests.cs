using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Isle.Gameplay.Editor;

namespace Isle.Tests.EditMode
{
    /// <summary>
    /// SYS-CHAR-01 §Verification is manual, but the rig's <i>shape</i> is not: T-002 hangs IK
    /// solvers on these exact paths and T-005 parents weapons under WeaponSocket. If a part is
    /// renamed or reparented, those tasks break silently — so assert the hierarchy here.
    /// </summary>
    public sealed class CharacterRigHierarchyTests
    {
        // SYS-CHAR-01 §Rig, using ART_PIPELINE §Rig's part names. Legs are Front/Back rather than
        // the spec's L/R because a flippable side view orders limbs by depth, not anatomy.
        static readonly string[] ExpectedPaths =
        {
            "Hip",
            "Hip/Torso",
            "Hip/Torso/Head",
            "Hip/Torso/Arm_Back_Upper",
            "Hip/Torso/Arm_Back_Upper/Arm_Back_Lower",
            "Hip/Torso/Arm_Back_Upper/Arm_Back_Lower/Hand_Back",
            "Hip/Torso/Arm_Front_Upper",
            "Hip/Torso/Arm_Front_Upper/Arm_Front_Lower",
            "Hip/Torso/Arm_Front_Upper/Arm_Front_Lower/Hand_Front",
            "Hip/Torso/Arm_Front_Upper/Arm_Front_Lower/Hand_Front/WeaponSocket",
            "Hip/Leg_Back_Upper",
            "Hip/Leg_Back_Upper/Leg_Back_Lower",
            "Hip/Leg_Back_Upper/Leg_Back_Lower/Foot_Back",
            "Hip/Leg_Front_Upper",
            "Hip/Leg_Front_Upper/Leg_Front_Lower",
            "Hip/Leg_Front_Upper/Leg_Front_Lower/Foot_Front",
        };

        static GameObject LoadRig()
        {
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(PlaceholderRigBuilder.PrefabPath);
            Assert.IsNotNull(rig,
                $"No rig prefab at {PlaceholderRigBuilder.PrefabPath}. Run ISLE > Rebuild placeholder character rig.");
            return rig;
        }

        [Test]
        public void Rig_HasEverySpecifiedBone()
        {
            var root = LoadRig().transform;
            foreach (var path in ExpectedPaths)
            {
                Assert.IsNotNull(root.Find(path), $"Missing bone: {path}");
            }
        }

        [Test]
        public void Rig_HasNoBonesBeyondTheSpec()
        {
            // Counted from Hip, not Root: the IK solvers and their targets also live under Root
            // (T-002) and are rigging, not skeleton.
            var hip = LoadRig().transform.Find("Hip");
            Assert.AreEqual(ExpectedPaths.Length, hip.GetComponentsInChildren<Transform>(true).Length);
        }

        [Test]
        public void FrontArm_IsATwoBoneChain()
        {
            // SYS-CHAR-01 §Rig: "Arms use the Limb solver (2-bone)". Fewer or more bones between
            // the shoulder and the hand and the solver cannot bind.
            var upper = LoadRig().transform.Find("Hip/Torso/Arm_Front_Upper");
            Assert.AreEqual(1, upper.childCount);
            Assert.AreEqual("Arm_Front_Lower", upper.GetChild(0).name);
        }

        [Test]
        public void WeaponSocket_CarriesNoSprite()
        {
            // It is a mount point, not a body part; a renderer here would draw over the hand.
            var socket = LoadRig().transform.Find(
                "Hip/Torso/Arm_Front_Upper/Arm_Front_Lower/Hand_Front/WeaponSocket");
            Assert.IsNull(socket.GetComponent<SpriteRenderer>());
        }

        [TestCase("Hip/Torso/Arm_Back_Upper", "Hip/Torso", "Hip/Torso/Arm_Front_Upper")]
        [TestCase("Hip/Leg_Back_Upper", "Hip/Torso", "Hip/Leg_Front_Upper")]
        public void BackLimbs_DrawBehindTorso_FrontLimbsInFront(string back, string torso, string front)
        {
            var root = LoadRig().transform;
            int Order(string path) => root.Find(path).GetComponent<SpriteRenderer>().sortingOrder;
            Assert.Less(Order(back), Order(torso));
            Assert.Less(Order(torso), Order(front));
        }

        [Test]
        public void EverySpritePart_HasASpriteAssigned()
        {
            foreach (var renderer in LoadRig().GetComponentsInChildren<SpriteRenderer>(true))
            {
                Assert.IsNotNull(renderer.sprite, $"{renderer.name} has no sprite");
            }
        }
    }
}
