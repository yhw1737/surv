using System;
using System.Collections.Generic;
using Isle.Core;
using Isle.World.Chunks;

namespace Isle.World.Generation
{
    /// <summary>One water-body def to generate: its index in the caller's def list, and its <c>water_body</c> block.</summary>
    public readonly struct WaterBodyPlan
    {
        public WaterBodyPlan(int index, string kind, int count, int minSize, int maxSize)
        {
            Index = index;
            Kind = kind;
            Count = count;
            MinSize = minSize;
            MaxSize = maxSize;
        }

        public int Index { get; }
        public string Kind { get; }
        public int Count { get; }
        public int MinSize { get; }
        public int MaxSize { get; }
    }

    /// <summary>
    /// SYS-WORLD-03 §Water bodies (SYS-WORLD-01 §Shape: "scattered ponds and rivers cut into it"). Static pure and
    /// deterministic per seed, like <see cref="IslandGenerator"/>: ponds are rounded blobs inland, rivers wind from
    /// the interior out to the sea. The island centre (the spawn point) is kept dry.
    /// </summary>
    public static class WaterGenerator
    {
        /// <summary>No water this close to the island centre, where players spawn. [invented]</summary>
        const int SpawnClearance = 12;

        const int MaxPlacementTries = 200;
        const int MaxRiverSteps = 1600;

        public static Dictionary<Vec2Int, int> Generate(IslandGenerator island, int seed, IReadOnlyList<WaterBodyPlan> plans)
        {
            var water = new Dictionary<Vec2Int, int>();
            foreach (var plan in plans)
                for (var n = 0; n < plan.Count; n++)
                {
                    var salt = seed ^ (plan.Index * 7919 + n * 104729 + 17);
                    if (plan.Kind == "pond") Pond(island, salt, plan, water);
                    else if (plan.Kind == "river") River(island, salt, plan, water);
                }
            return water;
        }

        /// <summary>Breadth-first distance from land: a water tile touching land is 1, the next ring 2, and so on.</summary>
        public static Dictionary<Vec2Int, int> DistanceToLand(ICollection<Vec2Int> water)
        {
            var distance = new Dictionary<Vec2Int, int>();
            var frontier = new Queue<Vec2Int>();
            foreach (var tile in water)
                foreach (var next in Neighbours(tile))
                    if (!water.Contains(next))
                    {
                        distance[tile] = 1;
                        frontier.Enqueue(tile);
                        break;
                    }
            while (frontier.Count > 0)
            {
                var tile = frontier.Dequeue();
                foreach (var next in Neighbours(tile))
                {
                    if (!water.Contains(next) || distance.ContainsKey(next)) continue;
                    distance[next] = distance[tile] + 1;
                    frontier.Enqueue(next);
                }
            }
            return distance;
        }

        static void Pond(IslandGenerator island, int salt, WaterBodyPlan plan, Dictionary<Vec2Int, int> water)
        {
            var radius = plan.MinSize + (int)(IslandGenerator.Hash(salt, 1, 0) % (uint)(plan.MaxSize - plan.MinSize + 1));
            for (var attempt = 0; attempt < MaxPlacementTries; attempt++)
            {
                var cx = (int)(IslandGenerator.Hash(salt, attempt, 11) % IslandGenerator.Size);
                var cy = (int)(IslandGenerator.Hash(salt, attempt, 13) % IslandGenerator.Size);
                if (!Inland(island, cx, cy) || NearSpawn(cx, cy, radius)) continue;

                for (var dx = -radius - 2; dx <= radius + 2; dx++)
                for (var dy = -radius - 2; dy <= radius + 2; dy++)
                {
                    // A lumpy edge: the radius wobbles by ±25% around the circle.
                    var angle = Math.Atan2(dy, dx);
                    var bucket = (int)((angle + Math.PI) / (Math.PI / 4));
                    var wobble = 0.75 + (IslandGenerator.Hash(salt, bucket, 29) % 1000) / 2000.0;
                    if (dx * dx + dy * dy > radius * radius * wobble * wobble) continue;
                    var x = cx + dx;
                    var y = cy + dy;
                    if (Inland(island, x, y)) water[new Vec2Int(x, y)] = plan.Index;
                }
                return;
            }
        }

        static void River(IslandGenerator island, int salt, WaterBodyPlan plan, Dictionary<Vec2Int, int> water)
        {
            var width = plan.MinSize + (int)(IslandGenerator.Hash(salt, 2, 0) % (uint)(plan.MaxSize - plan.MinSize + 1));
            var centre = IslandGenerator.Size / 2.0;
            for (var attempt = 0; attempt < MaxPlacementTries; attempt++)
            {
                double x = IslandGenerator.Hash(salt, attempt, 31) % IslandGenerator.Size;
                double y = IslandGenerator.Hash(salt, attempt, 37) % IslandGenerator.Size;
                if (!Inland(island, (int)x, (int)y) || NearSpawn((int)x, (int)y, SpawnClearance * 2)) continue;

                // Head away from the centre, wandering: rivers run from the interior out to the sea.
                var heading = Math.Atan2(y - centre, x - centre);
                for (var step = 0; step < MaxRiverSteps; step++)
                {
                    var tx = (int)Math.Round(x);
                    var ty = (int)Math.Round(y);
                    if (!island.IsLand(tx, ty)) return;
                    // A width × width square brush, starting half a width to the left/below.
                    var from = -width / 2;
                    for (var dx = from; dx < from + width; dx++)
                    for (var dy = from; dy < from + width; dy++)
                        if (island.IsLand(tx + dx, ty + dy) && !NearSpawn(tx + dx, ty + dy, 0))
                            water[new Vec2Int(tx + dx, ty + dy)] = plan.Index;

                    var outward = Math.Atan2(y - centre, x - centre);
                    var jitter = ((IslandGenerator.Hash(salt, step, 41) % 1000) / 1000.0 - 0.5) * 0.9;
                    heading += jitter + (outward - heading) * 0.15;
                    x += Math.Cos(heading);
                    y += Math.Sin(heading);
                }
                return;
            }
        }

        static bool Inland(IslandGenerator island, int x, int y) => island.IsLand(x, y) && island.BiomeAt(x, y) != Biome.Coast;

        static bool NearSpawn(int x, int y, int radius)
        {
            var dx = x - IslandGenerator.Size / 2;
            var dy = y - IslandGenerator.Size / 2;
            var clear = SpawnClearance + radius;
            return dx * dx + dy * dy < clear * clear;
        }

        static IEnumerable<Vec2Int> Neighbours(Vec2Int tile)
        {
            yield return new Vec2Int(tile.X + 1, tile.Y);
            yield return new Vec2Int(tile.X - 1, tile.Y);
            yield return new Vec2Int(tile.X, tile.Y + 1);
            yield return new Vec2Int(tile.X, tile.Y - 1);
        }
    }
}
