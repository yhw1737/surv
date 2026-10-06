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
            // Resolved once per spawn, not per tile: the per-tile string compare allocated tens of millions of
            // strings on the 1152-tile map and was most of the island's start-up time.
            var allowed = AllowedBiomes(spawn);
            var cluster = spawn.Cluster;
            var maxMult = cluster != null ? System.Math.Max(cluster.Inside, cluster.Outside) : 1f;
            var elsewhere = System.Math.Max(0f, spawn.Elsewhere);
            for (var x = 0; x < IslandGenerator.Size; x++)
            for (var y = 0; y < IslandGenerator.Size; y++)
            {
                if (!island.IsLand(x, y)) continue;
                var biomeMult = allowed[(int)island.BiomeAt(x, y)] ? 1f : elsewhere;
                if (biomeMult <= 0f) continue;
                var roll = IslandGenerator.Hash(seed ^ salt, x, y) % RollScale;
                // Cheap reject first: the noise is only sampled for tiles that could pass at the densest.
                if (roll >= spawn.Density * biomeMult * maxMult * RollScale) continue;
                var mult = cluster != null ? ClusterMult(cluster, x, y, seed ^ (salt * 7919)) : 1f;
                if (roll < spawn.Density * biomeMult * mult * RollScale) tiles.Add(new Vec2Int(x, y));
            }
            return tiles;
        }

        /// <summary>Density multiplier from the patch field at a tile: <c>Inside</c> in a patch, <c>Outside</c> between,
        /// with a short soft edge so patches don't end on a hard line. Pure.</summary>
        public static float ClusterMult(ClusterSpec cluster, int x, int y, int seed)
        {
            var scale = System.Math.Max(4f, cluster.ScaleTiles);
            var n = 0.65f * ValueNoise(x / scale, y / scale, seed) + 0.35f * ValueNoise(x / (scale * 0.45f), y / (scale * 0.45f), seed + 101);
            // Two-octave value noise is roughly bell-shaped around 0.5; this maps coverage to a cut-off on it.
            var cut = 0.5f + (0.5f - System.Math.Clamp(cluster.Coverage, 0.02f, 0.98f)) * 0.55f;
            var t = System.Math.Clamp((n - (cut - 0.03f)) / 0.06f, 0f, 1f);
            t = t * t * (3f - 2f * t);
            return cluster.Outside + (cluster.Inside - cluster.Outside) * t;
        }

        static float ValueNoise(float x, float y, int seed)
        {
            var x0 = (int)System.Math.Floor(x);
            var y0 = (int)System.Math.Floor(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float H(int i, int j) => IslandGenerator.Hash(seed, i, j) % 10000 / 10000f;
            var a = H(x0, y0) + (H(x0 + 1, y0) - H(x0, y0)) * fx;
            var b = H(x0, y0 + 1) + (H(x0 + 1, y0 + 1) - H(x0, y0 + 1)) * fx;
            return a + (b - a) * fy;
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
