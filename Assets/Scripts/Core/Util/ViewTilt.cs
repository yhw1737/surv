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

        /// <summary>The rotation a standing thing (and the camera) takes: its up leans toward the camera.</summary>
        public static Quaternion Standing => Quaternion.Euler(-PitchDegrees, 0f, 0f);

        /// <summary>Stands <paramref name="t"/> up, facing the camera, pivoting at its feet.</summary>
        public static void Stand(Transform t)
        {
            if (t != null) t.rotation = Standing;
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
