using System.Collections.Generic;
using UnityEngine;

namespace Isle.World.Island
{
    /// <summary>A pin a player put on the map (SYS-MAP-01 §Markers).</summary>
    public readonly struct MapMarker
    {
        public MapMarker(Vector2 position, int colour)
        {
            Position = position;
            Colour = colour;
        }

        public Vector2 Position { get; }

        /// <summary>Index into the presentation's palette; cycles as markers are placed.</summary>
        public int Colour { get; }
    }

    /// <summary>SYS-MAP-01 §Markers: a capped list where the oldest pin goes when the cap is hit.</summary>
    public sealed class MapMarkers
    {
        public const int ColourCount = 4;

        readonly List<MapMarker> _markers = new();
        readonly int _capacity;
        int _nextColour;

        public MapMarkers(int capacity) => _capacity = capacity;

        public IReadOnlyList<MapMarker> All => _markers;

        public void Add(Vector2 position, int? colour = null)
        {
            if (_markers.Count >= _capacity) _markers.RemoveAt(0);
            _markers.Add(new MapMarker(position, colour ?? _nextColour));
            if (colour == null) _nextColour = (_nextColour + 1) % ColourCount;
        }

        /// <summary>Removes the nearest marker within <paramref name="radius"/>; false when none is that close.</summary>
        public bool RemoveNear(Vector2 position, float radius)
        {
            var best = -1;
            var bestDistance = radius;
            for (var i = 0; i < _markers.Count; i++)
            {
                var distance = Vector2.Distance(position, _markers[i].Position);
                if (distance > bestDistance) continue;
                best = i;
                bestDistance = distance;
            }
            if (best < 0) return false;
            _markers.RemoveAt(best);
            return true;
        }

        public void Clear() => _markers.Clear();
    }
}
