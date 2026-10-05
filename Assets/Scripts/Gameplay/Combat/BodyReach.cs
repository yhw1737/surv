using System;

namespace Isle.Gameplay.Combat
{
    /// <summary>Body-edge reach for melee, arrows and creature strikes. Static pure. The body radius is creature def
    /// data (<c>combat.body_radius_tiles</c>), never derived from a sprite (Absolute Rule 7).</summary>
    public static class BodyReach
    {
        public static float SurfaceDistance(float centreDistance, float radius) => Math.Max(0f, centreDistance - radius);

        public static bool InReach(float centreDistance, float radius, float reach) => SurfaceDistance(centreDistance, radius) <= reach;

        /// <summary>Radius at a rolled weight: the def's radius at its mean weight, scaled by the cube root of the
        /// weight ratio (a body's volume follows its mass).</summary>
        public static float RadiusForWeight(float baseRadius, float weightKg, float meanKg) =>
            meanKg <= 0f ? baseRadius : baseRadius * (float)Math.Pow(weightKg / meanKg, 1.0 / 3.0);
    }
}
