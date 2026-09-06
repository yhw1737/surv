using Isle.Gameplay.Character;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D.IK;

namespace Isle.Gameplay.Editor
{
    /// <summary>
    /// Wires IK Manager 2D onto a rig built to SYS-CHAR-01 §Rig. Separate from
    /// <see cref="PlaceholderRigBuilder"/> because the placeholder sprites are throwaway and this
    /// wiring is not — the real <c>player_rig.psd</c> gets the same treatment.
    /// </summary>
    public static class CharacterRigIk
    {
        /// <summary>Both arms take the Limb solver: SYS-CHAR-01 §Rig, "Arms use the Limb solver (2-bone)".</summary>
        static readonly (string Name, string EffectorPath)[] Arms =
        {
            ("IK_Arm_Front", "Hip/Torso/Arm_Front_Upper/Arm_Front_Lower/Hand_Front"),
            ("IK_Arm_Back",  "Hip/Torso/Arm_Back_Upper/Arm_Back_Lower/Hand_Back"),
        };

        /// <summary>
        /// Adds an <see cref="IKManager2D"/> and one <see cref="LimbSolver2D"/> per arm to
        /// <paramref name="root"/>, plus a <see cref="CharacterRig"/> bound to the head.
        /// </summary>
        public static void Apply(GameObject root)
        {
            var manager = root.GetComponent<IKManager2D>() ?? root.AddComponent<IKManager2D>();

            foreach (var (name, effectorPath) in Arms)
            {
                var effector = root.transform.Find(effectorPath);
                if (effector == null)
                {
                    Debug.LogError($"{name}: no effector at {effectorPath}");
                    continue;
                }

                var solverObject = new GameObject(name);
                solverObject.transform.SetParent(root.transform, worldPositionStays: false);

                // The target must sit outside the chain it drives — IKChain2D.Validate rejects a
                // target descended from the chain root, since moving it would move itself.
                var target = new GameObject("Target").transform;
                target.SetParent(solverObject.transform, worldPositionStays: false);
                target.position = effector.position;

                var solver = solverObject.AddComponent<LimbSolver2D>();
                var chain = solver.GetChain(0);
                chain.effector = effector;
                chain.target = target;
                // transformCount is forced to 3 by LimbSolver2D.DoInitialize: upper, lower, hand.
                solver.Initialize();

                manager.AddSolver(solver);
            }

            var rig = root.GetComponent<CharacterRig>() ?? root.AddComponent<CharacterRig>();
            var serialized = new SerializedObject(rig);
            serialized.FindProperty("_head").objectReferenceValue = root.transform.Find("Hip/Torso/Head");
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
