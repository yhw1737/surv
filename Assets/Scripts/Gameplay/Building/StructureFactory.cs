using System.Collections.Generic;
using Isle.Core.Util;
using Isle.Data;
using Isle.Gameplay.World;
using Isle.World.Objects;
using UnityEngine;

namespace Isle.Gameplay.Building
{
    /// <summary>
    /// Builds a placed structure from its <see cref="WorldObjectDef"/>. What it does is decided by the def's own
    /// data (Absolute Rule 4): a <c>station/campfire</c> tag lights and warms, a <c>storage</c> block holds items, a
    /// <c>rain_catcher</c> block collects water. Server-side and not network-spawned — a code-built object has no
    /// prefab id for clients to instantiate, so like creatures these are host-only until Phase 10.
    /// </summary>
    public static class StructureFactory
    {
        const string CampfireTag = "station/campfire";

        static readonly List<WorldObjectInstance> _built = new();

        /// <summary>Everything placed this session — the save reads it, the placement check spaces against it.</summary>
        public static IReadOnlyList<WorldObjectInstance> Built => _built;

        /// <summary>Nearest placed container within reach of <paramref name="from"/>, or null.</summary>
        public static StorageBox NearestStorage(Vector2 from, float reachTiles)
        {
            StorageBox best = null;
            var bestDistance = reachTiles;
            foreach (var instance in _built)
            {
                if (instance == null || !instance.TryGetComponent<StorageBox>(out var box)) continue;
                var distance = Vector2.Distance(from, instance.transform.position);
                if (distance > bestDistance) continue;
                best = box;
                bestDistance = distance;
            }
            return best;
        }

        public static WorldObjectInstance Build(WorldObjectDef def, Vector2 position)
        {
            var go = new GameObject(def.Id.Name);
            go.SetActive(false);
            go.transform.position = position;

            var instance = go.AddComponent<WorldObjectInstance>();
            instance.Initialize(def.Id.Value);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = SpriteFor(def);
            // Stands on its foot and sorts by it, like trees, so the player can walk behind a workbench.
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;
            renderer.sortingOrder = 2;
            go.transform.localScale = Vector3.one * (def.Visual?.Size ?? 1f);

            // Awake hasn't run yet (the object is inactive), so read the def's tags directly.
            if (def.Tags != null && System.Array.IndexOf(def.Tags, CampfireTag) >= 0) go.AddComponent<CampfireInteraction>();
            if (def.Storage is { } grid) go.AddComponent<StorageBox>().Initialize(grid.W, grid.H);
            if (def.RainCatcher != null) go.AddComponent<RainCatcher>().Initialize(def.RainCatcher);

            go.SetActive(true);
            // A campfire starts unlit and is lit by hand; every other station (a workbench) works as soon as it's built.
            instance.IsActive = !go.TryGetComponent<CampfireInteraction>(out _);
            _built.Add(instance);
            return instance;
        }

        /// <summary>Removes one structure (a harvested crop plot).</summary>
        public static void Remove(WorldObjectInstance instance)
        {
            _built.Remove(instance);
            if (instance != null) Object.Destroy(instance.gameObject);
        }

        static Sprite _sprout;

        /// <summary>Plants <paramref name="crop"/> on a freshly built plot.</summary>
        public static CropPlot Plant(WorldObjectInstance plot, CropDef crop, long plantedAtMinutes)
        {
            _sprout ??= PlaceholderVisuals.AsSprite(PlaceholderVisuals.Circle(24, Color.white));
            var cropPlot = plot.gameObject.AddComponent<CropPlot>();
            cropPlot.Initialize(crop, plantedAtMinutes, _sprout);
            return cropPlot;
        }

        /// <summary>Scene reloads and tests.</summary>
        public static void Clear()
        {
            foreach (var instance in _built)
                if (instance != null) Object.Destroy(instance.gameObject);
            _built.Clear();
        }

        static Sprite SpriteFor(WorldObjectDef def) =>
            ShapeLibrary.StandingSprite(def.Visual?.Shape, ShapeLibrary.ParseColour(def.Visual?.Color, PlaceholderVisuals.ColorForTags(def.Tags)));
    }
}
