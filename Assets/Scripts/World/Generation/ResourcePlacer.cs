using System.Collections.Generic;
using Isle.Core;
using Isle.Core.Ids;
using Isle.Data;
using Isle.World.Chunks;

namespace Isle.World.Generation
{
    /// <summary>
    /// SYS-WORLD-03 §Placement. Static pure: given an island and a <see cref="SpawnSpec"/>, returns
    /// every tile that spawn rolled onto. Used for both resource nodes and creatures (same spawn
    /// shape, SCHEMA §Creatures), keyed by a per-def <paramref name="salt"/> so two defs with the same
    /// biome don't land on identical tiles.
    /// </summary>
    public static class ResourcePlacer
    {
        const uint RollScale = 10000;

        public static IReadOnlyList<Vec2Int> Roll(IslandGenerator island, SpawnSpec spawn, int seed, int salt)
        {
            var tiles = new List<Vec2Int>();
            var threshold = (uint)(spawn.Density * RollScale);
            for (var x = 0; x < IslandGenerator.Size; x++)
            for (var y = 0; y < IslandGenerator.Size; y++)
            {
                if (!island.IsLand(x, y)) continue;
                if (!AllowsBiome(spawn, island.BiomeAt(x, y))) continue;
                if (IslandGenerator.Hash(seed ^ salt, x, y) % RollScale < threshold) tiles.Add(new Vec2Int(x, y));
            }
            return tiles;
        }

        /// <summary>Matches a biome against <c>"isle:forest"</c>-style ids. Biomes are a fixed set
        /// (<see cref="Biome"/>), so the name is compared as text rather than registered as a def.</summary>
        static bool AllowsBiome(SpawnSpec spawn, Biome biome)
        {
            if (spawn.Biomes == null) return false;
            var name = biome.ToString().ToLowerInvariant();
            foreach (var id in spawn.Biomes)
                if (id.Name == name) return true;
            return false;
        }
    }
}
