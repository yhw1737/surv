namespace Isle.Gameplay.Crafting
{
    /// <summary>
    /// SYS-CRAFT-01 §Adjacent assist: a nearby ally lends part of their level toward a recipe's skill requirements —
    /// requirements only; quality always uses the crafter's own level. Static pure.
    /// </summary>
    public static class AdjacentAssist
    {
        public const float Ratio = 0.60f;
        public const float RadiusTiles = 3.0f;

        /// <summary><c>max(own, own + (bestNearbyLevel - own) × Ratio)</c>; 0 for <paramref name="bestNearby"/> = nobody near.</summary>
        public static float EffectiveLevel(int own, int bestNearby) =>
            System.Math.Max(own, own + (bestNearby - own) * Ratio);

        /// <summary>Whether a requirement of <paramref name="required"/> is met; a recipe with assist off uses only <paramref name="own"/>.</summary>
        public static bool Meets(int own, int bestNearby, int required, bool allowed) =>
            (allowed ? EffectiveLevel(own, bestNearby) : own) >= required - 1e-4f;
    }
}
