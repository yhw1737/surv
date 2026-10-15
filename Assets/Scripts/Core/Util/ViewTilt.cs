using System.Collections.Generic;
using UnityEngine;

namespace Isle.Core.Util
{
    /// <summary>
    /// The camera view (2026-10-09): a perspective camera looking down at the ground plane from the south at an angle,
    /// with everything that stands up — people, animals, trees, structures — drawn upright, facing the camera. The world
    /// itself stays in the XY plane (gameplay positions don't change); "height" is toward the camera (−Z). Presentation
    /// only: gameplay never reads this.
    /// </summary>
    public static class ViewTilt
    {
        /// <summary>How far the view leans from straight down. [invented look]</summary>
        public const float PitchDegrees = 40f;

        /// <summary>Vertical field of view. Narrow, so the perspective is gentle. [invented look]</summary>
        public const float FieldOfView = 35f;

        /// <summary>T-307: Q / E turn the view this far around the vertical, a step at a time. [proposed]</summary>
        public const float TurnStepDegrees = 45f;

        /// <summary>T-307: how long one turn step takes to swing round. [proposed]</summary>
        public const float TurnSeconds = 0.25f;

        /// <summary>T-307: which way the view faces, degrees around the vertical (0 = from the south, looking north). It
        /// eases toward <see cref="TargetYawDegrees"/>.</summary>
        public static float YawDegrees { get; private set; }

        public static float TargetYawDegrees { get; private set; }

        /// <summary>The rotation a standing thing (and the camera) takes: its up leans toward the camera, turned with the
        /// view.</summary>
        public static Quaternion Standing => Quaternion.Euler(0f, 0f, YawDegrees) * Quaternion.Euler(-PitchDegrees, 0f, 0f);

        /// <summary>T-307: the ground direction that points up the screen (north when the view isn't turned).</summary>
        public static Vector2 ScreenUp => Quaternion.Euler(0f, 0f, YawDegrees) * Vector2.up;

        /// <summary>T-307: a ground direction as it looks on screen (x right, y up) — for figures facing left or right.</summary>
        public static Vector2 ToScreen(Vector2 ground) => Quaternion.Euler(0f, 0f, -YawDegrees) * ground;

        static readonly HashSet<Transform> StandingThings = new();
        static readonly List<Transform> Scratch = new();

        /// <summary>Stands <paramref name="t"/> up, facing the camera, pivoting at its feet — and keeps it facing the camera
        /// as the view turns.</summary>
        public static void Stand(Transform t)
        {
            if (t == null) return;
            t.rotation = Standing;
            // Views come and go as chunks stream; sweep out the destroyed ones now and then.
            if (StandingThings.Add(t) && ++_added % 2048 == 0) StandingThings.RemoveWhere(x => x == null);
        }

        static int _added;

        /// <summary>T-307: starts turning the view by <paramref name="steps"/> × <see cref="TurnStepDegrees"/>.</summary>
        public static void Turn(int steps) => TargetYawDegrees = Mathf.Repeat(TargetYawDegrees + steps * TurnStepDegrees, 360f);

        /// <summary>T-307: back to facing north at once (a new game, the menu).</summary>
        public static void ResetYaw()
        {
            TargetYawDegrees = 0f;
            Apply(0f);
        }

        /// <summary>T-307: eases the view toward its target; true while it's still turning.</summary>
        public static bool StepTurn(float deltaSeconds)
        {
            if (Mathf.Approximately(Mathf.DeltaAngle(YawDegrees, TargetYawDegrees), 0f)) return false;
            var speed = TurnStepDegrees / TurnSeconds;
            Apply(Mathf.MoveTowardsAngle(YawDegrees, TargetYawDegrees, speed * deltaSeconds));
            return true;
        }

        static void Apply(float yaw)
        {
            YawDegrees = Mathf.Repeat(yaw, 360f);
            var rotation = Standing;
            Scratch.Clear();
            foreach (var t in StandingThings)
                if (t == null) Scratch.Add(t);
                else t.rotation = rotation;
            foreach (var gone in Scratch) StandingThings.Remove(gone);
        }

        /// <summary>Where on the ground the camera is looking (set by the camera each frame). Null before a game camera
        /// runs; chunk streaming falls back to the camera's own position then.</summary>
        public static Vector2? Focus { get; set; }

        /// <summary>The point on the ground (z = 0) under a screen position, or null when the ray misses the ground.</summary>
        public static Vector2? ScreenToGround(Camera camera, Vector2 screen)
        {
            if (camera == null) return null;
            if (camera.orthographic)
            {
                var p = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -camera.transform.position.z));
                return new Vector2(p.x, p.y);
            }
            var ray = camera.ScreenPointToRay(new Vector3(screen.x, screen.y, 0f));
            if (Mathf.Abs(ray.direction.z) < 1e-5f) return null;
            var t = -ray.origin.z / ray.direction.z;
            if (t < 0f) return null;
            var hit = ray.origin + ray.direction * t;
            return new Vector2(hit.x, hit.y);
        }

        /// <summary>The camera's ground focus, or its position when no game camera has set one.</summary>
        public static Vector2 FocusOr(Camera camera) => Focus ?? (camera != null ? (Vector2)camera.transform.position : Vector2.zero);
    }
}
