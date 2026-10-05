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
            // Resolved once per spawn, not per tile: the per-tile string compare allocated tens of millions of
            // strings on the 1152-tile map and was most of the island's start-up time.
            var allowed = AllowedBiomes(spawn);
            for (var x = 0; x < IslandGenerator.Size; x++)
            for (var y = 0; y < IslandGenerator.Size; y++)
            {
                if (!island.IsLand(x, y)) continue;
                if (!allowed[(int)island.BiomeAt(x, y)]) continue;
                if (IslandGenerator.Hash(seed ^ salt, x, y) % RollScale < threshold) tiles.Add(new Vec2Int(x, y));
            }
            return tiles;
        }

        /// <summary>Matches a biome against <c>"isle:forest"</c>-style ids. Biomes are a fixed set
        /// (<see cref="Biome"/>), so the name is compared as text rather than registered as a def.</summary>
        static bool[] AllowedBiomes(SpawnSpec spawn)
        {
            var biomes = (Biome[])System.Enum.GetValues(typeof(Biome));
            var allowed = new bool[biomes.Length];
            if (spawn.Biomes == null) return allowed;
            foreach (var biome in biomes)
            {
                var name = biome.ToString().ToLowerInvariant();
                foreach (var id in spawn.Biomes)
                    if (id.Name == name) allowed[(int)biome] = true;
            }
            return allowed;
        }
    }
}
