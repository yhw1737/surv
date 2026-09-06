using UnityEngine;

namespace Isle.Gameplay.Character
{
    /// <summary>
    /// Drives the rig's aimed parts. SYS-CHAR-01 §Angles.
    ///
    /// The head is a <b>look-at</b>, not an IK chain: Unity's 2D IK package ships Limb, CCD and
    /// FABRIK solvers but no look-at, so the spec says to implement it directly. Arms are the
    /// opposite — they are solved by <c>LimbSolver2D</c> and this component never touches them.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterRig : MonoBehaviour
    {
        [SerializeField] Transform _head;

        // SYS-CHAR-01 §Angles.
        public const float HeadMaxAngle = 70f;
        public const float HeadSmoothTime = 0.08f;

        float _headAngle;
        float _headVelocity;

        /// <summary>
        /// Where the head wants to look, in degrees relative to facing forward. Clamped to
        /// <see cref="HeadMaxAngle"/> and eased over <see cref="HeadSmoothTime"/>; callers set it
        /// raw and let the rig do the limiting. T-003's AimController is the intended caller.
        /// </summary>
        public float HeadTargetAngle { get; set; }

        /// <summary>The eased angle actually applied this frame. Exposed for tests and debug UI.</summary>
        public float HeadAngle => _headAngle;

        /// <summary>
        /// Snap the head to its target with no easing — for spawn and teleport only.
        /// <b>Not for flips:</b> SYS-CHAR-01 §Angles requires the upper body to interpolate across
        /// the 0.13 s transition, "never snap".
        /// </summary>
        public void SnapHead()
        {
            _headAngle = Mathf.Clamp(HeadTargetAngle, -HeadMaxAngle, HeadMaxAngle);
            _headVelocity = 0f;
            ApplyHead();
        }

        void LateUpdate()
        {
            // After the Animator has written its pose, so an animated head does not fight the aim.
            Step(Time.deltaTime);
        }

        /// <summary>Advance the easing by <paramref name="deltaTime"/>. Separated so tests can step it.</summary>
        public void Step(float deltaTime)
        {
            var target = Mathf.Clamp(HeadTargetAngle, -HeadMaxAngle, HeadMaxAngle);
            _headAngle = Mathf.SmoothDampAngle(_headAngle, target, ref _headVelocity, HeadSmoothTime, Mathf.Infinity, deltaTime);
            ApplyHead();
        }

        void ApplyHead()
        {
            if (_head == null) return;

            // HeadTargetAngle is relative to facing forward, but the head hangs off the torso, which
            // twists on its own (±20°, T-003). Cancelling the parent's local rotation keeps the head
            // at the requested angle instead of adding the two together.
            _head.localRotation = Quaternion.Inverse(_head.parent.localRotation) * Quaternion.Euler(0f, 0f, _headAngle);
        }
    }
}
