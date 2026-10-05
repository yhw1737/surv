using System;
using System.Collections.Generic;
using UnityEngine;

namespace Isle.Gameplay.Building
{
    /// <summary>Structure placement and rain-catcher filling. Static pure — EditMode test target. No spec covers
    /// building; the distances here are prototype values (PROJECT_STATE.md §Decided without a spec).</summary>
    public static class BuildingCalculator
    {
        /// <summary>Same reach as every other interaction (<c>PlayerInteraction.ReachTiles</c>).</summary>
        public const float ReachTiles = 2f;

        /// <summary>Two structures can't stand closer than this, centre to centre. [invented]</summary>
        public const float MinSpacingTiles = 1f;

        /// <summary>Nothing is built on the player's own tile. [invented]</summary>
        public const float MinDistanceFromPlayerTiles = 0.6f;

        public static bool CanPlace(Vector2 player, Vector2 target, Func<Vector2, bool> walkable, IEnumerable<Vector2> structures)
        {
            var distance = Vector2.Distance(player, target);
            if (distance > ReachTiles || distance < MinDistanceFromPlayerTiles) return false;
            if (!walkable(target)) return false;
            foreach (var other in structures)
                if (Vector2.Distance(other, target) < MinSpacingTiles) return false;
            return true;
        }

        public static float Fill(float current, float capacity, float perSecond, float seconds, bool raining) =>
            raining ? Mathf.Min(capacity, current + perSecond * seconds) : current;
    }
}
