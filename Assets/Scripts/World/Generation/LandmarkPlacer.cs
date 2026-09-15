using System;
using System.Collections.Generic;
using Isle.Core;
using Isle.World.Chunks;

namespace Isle.World.Generation
{
    /// <summary>
    /// SYS-WORLD-01 §Island generation (T-036). Places landmarks on the island <see
    /// cref="IslandGenerator"/> painted, spread as far apart as the island's size physically
    /// allows.
    ///
    /// The developer's target is "≥2 minutes walking apart" — ~256 tiles, derived from this same
    /// spec's own crossing pace (see PROJECT_STATE.md §Decided without a spec, T-036). That number
    /// isn't reachable for every pair of 6 landmarks at once on a 384-tile island (the best any 6
    /// mutually-spread points can do on a disk this size tops out well under 256 — geometry, not a
    /// bug). Rather than loop forever chasing an unreachable target, this maximizes the achieved
    /// minimum separation instead (greedy farthest-point placement): landmarks end up as spread out
    /// as the island allows, which is the actual intent behind the number.
    /// </summary>
    public static class LandmarkPlacer
    {
        /// <summary>Coarse candidate grid — 8 tiles apart is plenty of resolution for placing 6 points.</summary>
        const int CandidateStep = 8;

        /// <summary>Places every landmark, keyed by which chunk it lands in (ready to append to that <see cref="Chunk"/>'s Objects when it's first generated).</summary>
        public static Dictionary<Vec2Int, List<WorldObject>> Place(IslandGenerator island, IReadOnlyList<LandmarkDef> landmarks, int seed)
        {
            var candidates = BuildCandidates(island);
            var placed = new List<Vec2Int>();
            var byChunk = new Dictionary<Vec2Int, List<WorldObject>>();

            foreach (var landmark in landmarks)
            {
                for (var i = 0; i < landmark.Count; i++)
                {
                    var pos = PickFarthest(candidates, placed, seed, placed.Count);
                    placed.Add(pos);

                    var chunkCoord = Chunk.CoordFromTilePosition(pos);
                    var local = new Vec2Int(pos.X - chunkCoord.X * Chunk.Size, pos.Y - chunkCoord.Y * Chunk.Size);
                    if (!byChunk.TryGetValue(chunkCoord, out var objects))
                        byChunk[chunkCoord] = objects = new List<WorldObject>();
                    objects.Add(new WorldObject { DefId = landmark.DefId, LocalPosition = local });
                }
            }

            return byChunk;
        }

        static List<Vec2Int> BuildCandidates(IslandGenerator island)
        {
            var candidates = new List<Vec2Int>();
            for (var x = 0; x < IslandGenerator.Size; x += CandidateStep)
            for (var y = 0; y < IslandGenerator.Size; y += CandidateStep)
                if (island.IsLand(x, y))
                    candidates.Add(new Vec2Int(x, y));
            return candidates;
        }

        /// <summary>Picks the candidate farthest from every already-placed landmark (ties broken deterministically per seed).</summary>
        static Vec2Int PickFarthest(List<Vec2Int> candidates, List<Vec2Int> placed, int seed, int index)
        {
            var best = candidates[0];
            var bestScore = double.MinValue;
            foreach (var candidate in candidates)
            {
                var score = placed.Count == 0 ? 0.0 : MinDistanceTo(candidate, placed);
                // Tiny deterministic tiebreak so the first landmark (no placed points yet to
                // maximize against) doesn't always land on the same candidate every seed.
                score += IslandGenerator.Hash(seed ^ index, candidate.X, candidate.Y) % 1000 / 1000.0 * 1e-3;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }
            return best;
        }

        static double MinDistanceTo(Vec2Int point, List<Vec2Int> others)
        {
            var min = double.MaxValue;
            foreach (var other in others)
            {
                var dx = point.X - other.X;
                var dy = point.Y - other.Y;
                var distance = Math.Sqrt(dx * dx + dy * dy);
                if (distance < min) min = distance;
            }
            return min;
        }
    }
}
