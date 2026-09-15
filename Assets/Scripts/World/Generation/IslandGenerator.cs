using System;
using Isle.World.Chunks;

namespace Isle.World.Generation
{
    /// <summary>
    /// SYS-WORLD-01 §Island generation. Seeded and deterministic: <see cref="BiomeAt"/> is a pure
    /// function of (seed, world tile), so it paints the same biome regardless of which order chunks
    /// happen to load in — no whole-island array to generate or store up front.
    ///
    /// Shape, per the developer: a different island every game start, but never a formless blob —
    /// a rough ellipse as the main axis, edges always coast, interior a clumped patchwork of forest
    /// and marsh (not concentric rings, not a clean split) moving inward. Ponds/rivers aren't a
    /// fourth biome (the spec fixes exactly three: coast/forest/marsh) — marsh patches double as
    /// the wet, boggy ground the developer meant by that.
    /// </summary>
    public sealed class IslandGenerator
    {
        /// <summary>Full island span in tiles — 12×12 chunks (SYS-WORLD-01 §Island generation).</summary>
        public const int Size = Chunk.Size * 12;

        /// <summary>Normalized ellipse distance (0 = centre, 1 = ellipse edge) beyond which a tile is coast.</summary>
        const float CoastRingStart = 0.82f;

        /// <summary>Interior patches are sampled on this grid, not per-tile, so neighbouring tiles agree (a patch, not speckle).</summary>
        const int PatchCellSize = 24;

        readonly int _seed;
        readonly float _semiAxisX;
        readonly float _semiAxisY;

        public IslandGenerator(int seed)
        {
            _seed = seed;
            // Semi-axes at 0.82..0.95 of the half-width: big enough that the island fills most of
            // its bounding box, varied enough that no two seeds look the same.
            _semiAxisX = (0.82f + Frac01(seed, 1) * 0.13f) * (Size / 2f);
            _semiAxisY = (0.82f + Frac01(seed, 2) * 0.13f) * (Size / 2f);
        }

        /// <summary>Biome at one world tile position.</summary>
        public Biome BiomeAt(int worldX, int worldY)
        {
            if (EllipseDistance(worldX, worldY) >= CoastRingStart) return Biome.Coast;

            var cellX = FloorDiv(worldX, PatchCellSize);
            var cellY = FloorDiv(worldY, PatchCellSize);
            return (Hash(_seed, cellX, cellY) & 1) == 0 ? Biome.Forest : Biome.Marsh;
        }

        /// <summary>True for any tile inside the island's silhouette (including its coast ring) — the
        /// candidate space <see cref="LandmarkPlacer"/> places into.</summary>
        public bool IsLand(int worldX, int worldY) => EllipseDistance(worldX, worldY) < 1f;

        float EllipseDistance(int worldX, int worldY)
        {
            var dx = worldX - Size / 2f;
            var dy = worldY - Size / 2f;
            return (float)Math.Sqrt(dx * dx / (_semiAxisX * _semiAxisX) + dy * dy / (_semiAxisY * _semiAxisY));
        }

        static float Frac01(int seed, int salt) => (Hash(seed, salt, 0) % 10000) / 10000f;

        static int FloorDiv(int a, int b) => (a >= 0 ? a : a - (b - 1)) / b;

        // ponytail: a hashed grid, not real value/Perlin noise — good enough for a clumped
        // placeholder patchwork. Swap for real noise if the patches read too blocky in a playtest.
        internal static uint Hash(int seed, int x, int y)
        {
            unchecked
            {
                var h = (uint)(seed * 374761393 + x * 668265263 + y * 2246822519);
                h = (h ^ (h >> 15)) * 2246822519u;
                h = (h ^ (h >> 13)) * 3266489917u;
                return h ^ (h >> 16);
            }
        }
    }
}
