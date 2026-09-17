using System.Collections.Generic;
using UnityEngine;

namespace Isle.World.Objects
{
    /// <summary>Every spawned <see cref="WorldObjectInstance"/>, queried by tag for proximity checks
    /// (e.g. SYS-SURV-01's fire bonus). Plain static list, not a <c>NetworkBehaviour</c> — the
    /// instances themselves are already networked; this is just a same-process index of them, pulled
    /// from rather than pushed to, same shape as <see cref="Isle.World.Weather.WeatherController"/>.</summary>
    public static class WorldObjectRegistry
    {
        static readonly List<WorldObjectInstance> _instances = new();

        public static void Register(WorldObjectInstance instance) => _instances.Add(instance);
        public static void Unregister(WorldObjectInstance instance) => _instances.Remove(instance);

        /// <summary>Tile distance (1 Unity unit = 1 tile, GLOSSARY §Units) to the nearest active,
        /// <paramref name="tag"/>-tagged instance, or <see cref="float.PositiveInfinity"/> if none —
        /// <see cref="WorldObjectInstance.IsActive"/> gates it, same reasoning as an unlit campfire
        /// giving no fire bonus (PROJECT_STATE.md §Decided without a spec).</summary>
        public static float NearestDistanceTiles(Vector2 fromPosition, string tag)
        {
            var nearest = float.PositiveInfinity;
            foreach (var instance in _instances)
            {
                if (!instance.IsActive || !instance.HasTag(tag)) continue;
                var distance = Vector2.Distance(fromPosition, instance.transform.position);
                if (distance < nearest) nearest = distance;
            }
            return nearest;
        }

        /// <summary>Nearest instance within <paramref name="maxDistanceTiles"/> carrying an
        /// <see cref="IInteractable"/> component (regardless of <see cref="WorldObjectInstance.IsActive"/> —
        /// interacting is how a campfire gets lit in the first place), or null if none. Client-side
        /// pre-filter for <c>Isle.Gameplay.Character.PlayerInteraction</c>; the server re-checks
        /// reach itself before calling <see cref="IInteractable.Interact"/>.</summary>
        public static WorldObjectInstance NearestInteractable(Vector2 fromPosition, float maxDistanceTiles)
        {
            WorldObjectInstance nearest = null;
            var nearestDistance = maxDistanceTiles;
            foreach (var instance in _instances)
            {
                if (!instance.TryGetComponent<IInteractable>(out _)) continue;
                var distance = Vector2.Distance(fromPosition, instance.transform.position);
                if (distance > nearestDistance) continue;
                nearest = instance;
                nearestDistance = distance;
            }
            return nearest;
        }
    }
}
