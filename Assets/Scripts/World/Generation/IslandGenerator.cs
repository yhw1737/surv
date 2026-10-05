using System;
using Isle.World.Chunks;

namespace Isle.World.Generation
{
    /// <summary>
    /// SYS-WORLD-01 §Island generation, SYS-WORLD-03 §Island shape (2026-10-05 revision). Seeded and
    /// deterministic: the same seed always draws the same island.
    ///
    /// Per the developer: three times the old size, no need to stay round — one landmass or several, never pure
    /// noise, with different terrain per island. The shape is 1–4 noisy rotated ellipses ("blobs") joined to the
    /// centre blob by land bridges, so every part is walkable from the spawn; a sea border keeps the edges water.
    /// The forest/marsh patchwork is smooth value noise whose marsh share varies per island. Ponds and rivers
    /// are cut in afterwards by <see cref="WaterGenerator"/>.
    ///
    /// The whole map is computed once on first use and cached, so <see cref="BiomeAt"/> and
    /// <see cref="IsLand"/> are array lookups.
    /// </summary>
    public sealed class IslandGenerator
    {
        /// <summary>Full map span in tiles — 36×36 chunks (developer revision, 2026-10-05).</summary>
        public const int Size = Chunk.Size * 36;

        // SYS-WORLD-03 §Island shape — all [invented].
        const int SeaBorder = 32;
        const float NoiseAmplitude = 0.35f;
        const float NoiseScale = 1f / 140f;
        const float CoastBand = 0.06f;
        const float BiomeNoiseScale = 1f / 70f;
        const float MinBridgeWidth = 10f, MaxBridgeWidth = 16f;

        readonly int _seed;
        readonly Blob[] _blobs;
        readonly float _marshRatio;
        readonly float _bridgeHalfWidth;
        byte[] _cells; // 0 sea, 1 coast, 2 forest, 3 marsh

        public IslandGenerator(int seed)
        {
            _seed = seed;
            BlobCount = 1 + (int)(Hash(seed, 101, 0) % 4);
            _blobs = new Blob[BlobCount];
            var centre = Size / 2f;
            for (var i = 0; i < BlobCount; i++)
            {
                float cx = centre, cy = centre, radius;
                if (i == 0) radius = Lerp(0.26f, 0.34f, Frac01(seed, 200)) * Size;
                else
                {
                    var angle = Frac01(seed, 300 + i) * MathF.PI * 2f;
                    var distance = Lerp(0.24f, 0.36f, Frac01(seed, 400 + i)) * Size;
                    cx = centre + MathF.Cos(angle) * distance;
                    cy = centre + MathF.Sin(angle) * distance;
                    radius = Lerp(0.10f, 0.20f, Frac01(seed, 500 + i)) * Size;
                }
                var aspect = Lerp(0.65f, 1.35f, Frac01(seed, 600 + i));
                _blobs[i] = new Blob(cx, cy, radius * aspect, radius / aspect, Frac01(seed, 700 + i) * MathF.PI);
            }
            _marshRatio = Lerp(0.30f, 0.55f, Frac01(seed, 800));
            _bridgeHalfWidth = Lerp(MinBridgeWidth, MaxBridgeWidth, Frac01(seed, 900)) / 2f;
        }

        /// <summary>How many landmasses this island has (1–4).</summary>
        public int BlobCount { get; }

        /// <summary>Biome at one tile. Off the map and open sea read as coast, as before.</summary>
        public Biome BiomeAt(int worldX, int worldY) => Cell(worldX, worldY) switch
        {
            2 => Biome.Forest,
            3 => Biome.Marsh,
            _ => Biome.Coast,
        };

        /// <summary>True for any tile inside the island's silhouette (including its coast).</summary>
        public bool IsLand(int worldX, int worldY) => Cell(worldX, worldY) != 0;

        byte Cell(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Size || y >= Size) return 0;
            _cells ??= Build();
            return _cells[y * Size + x];
        }

        byte[] Build()
        {
            var cells = new byte[Size * Size];
            for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
            {
                if (x < SeaBorder || y < SeaBorder || x >= Size - SeaBorder || y >= Size - SeaBorder) continue;
                var field = Field(x, y);
                var land = field > 0f || OnBridge(x, y);
                if (!land) continue;
                // Beaches vary in width along the coast instead of a uniform ring.
                var band = CoastBand * (0.35f + 1.3f * ValueNoise(x * NoiseScale * 2f, y * NoiseScale * 2f, 557));
                if (field < band && !OnBridge(x, y)) cells[y * Size + x] = 1;
                else
                {
                    // Two octaves, so marsh/forest patches have ragged edges rather than round blobs.
                    var biome = 0.7f * ValueNoise(x * BiomeNoiseScale, y * BiomeNoiseScale, 977)
                              + 0.3f * ValueNoise(x * BiomeNoiseScale * 3f, y * BiomeNoiseScale * 3f, 983);
                    cells[y * Size + x] = biome < _marshRatio ? (byte)3 : (byte)2;
                }
            }
            DropUnreachableLand(cells);
            return cells;
        }

        /// <summary>Noise can leave small islets just off a blob's edge; anything not reachable on foot from the centre
        /// goes back to sea, so every land tile is walkable from the spawn (verification 15).</summary>
        static void DropUnreachableLand(byte[] cells)
        {
            var reached = new bool[cells.Length];
            var stack = new System.Collections.Generic.Stack<int>();
            var start = Size / 2 * Size + Size / 2;
            if (cells[start] == 0) cells[start] = 2;
            reached[start] = true;
            stack.Push(start);
            while (stack.Count > 0)
            {
                var i = stack.Pop();
                int x = i % Size, y = i / Size;
                Visit(x + 1, y);
                Visit(x - 1, y);
                Visit(x, y + 1);
                Visit(x, y - 1);
            }
            for (var i = 0; i < cells.Length; i++)
                if (!reached[i]) cells[i] = 0;

            void Visit(int x, int y)
            {
                if (x < 0 || y < 0 || x >= Size || y >= Size) return;
                var j = y * Size + x;
                if (reached[j] || cells[j] == 0) return;
                reached[j] = true;
                stack.Push(j);
            }
        }

        float Field(int x, int y)
        {
            var best = float.MinValue;
            foreach (var blob in _blobs) best = MathF.Max(best, 1f - blob.Distance(x, y));
            // Noise moves the field by at most ±NoiseAmplitude/2, so far inside or far outside it can't change the
            // answer — only tiles near a coastline pay for the 4-octave noise.
            var reach = NoiseAmplitude * 0.5f + CoastBand * 1.7f;
            if (best > reach) return best;
            if (best < -reach) return best;
            return best + NoiseAmplitude * (Fbm(x * NoiseScale, y * NoiseScale) - 0.5f);
        }

        /// <summary>Land strips from the centre blob to every other, so nothing is cut off from the spawn.</summary>
        bool OnBridge(int x, int y)
        {
            for (var i = 1; i < _blobs.Length; i++)
                if (SegmentDistance(x, y, _blobs[0].X, _blobs[0].Y, _blobs[i].X, _blobs[i].Y) <= _bridgeHalfWidth) return true;
            return false;
        }

        static float SegmentDistance(float px, float py, float ax, float ay, float bx, float by)
        {
            float vx = bx - ax, vy = by - ay;
            var t = Math.Clamp(((px - ax) * vx + (py - ay) * vy) / (vx * vx + vy * vy), 0f, 1f);
            float dx = px - (ax + vx * t), dy = py - (ay + vy * t);
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Four octaves of value noise, roughly 0..1.</summary>
        float Fbm(float x, float y)
        {
            float sum = 0f, amplitude = 0.5f, total = 0f;
            for (var octave = 0; octave < 4; octave++)
            {
                sum += ValueNoise(x, y, 31 + octave * 17) * amplitude;
                total += amplitude;
                x *= 2f;
                y *= 2f;
                amplitude *= 0.5f;
            }
            return sum / total;
        }

        /// <summary>Smoothly interpolated hashed lattice noise, 0..1.</summary>
        float ValueNoise(float x, float y, int salt)
        {
            var ix = (int)MathF.Floor(x);
            var iy = (int)MathF.Floor(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            var a = Lattice(ix, iy, salt);
            var b = Lattice(ix + 1, iy, salt);
            var c = Lattice(ix, iy + 1, salt);
            var d = Lattice(ix + 1, iy + 1, salt);
            return Lerp(Lerp(a, b, fx), Lerp(c, d, fx), fy);
        }

        float Lattice(int x, int y, int salt) => (Hash(_seed ^ salt, x, y) % 10000) / 10000f;

        static float Lerp(float a, float b, float t) => a + (b - a) * t;

        static float Frac01(int seed, int salt) => (Hash(seed, salt, 0) % 10000) / 10000f;

        readonly struct Blob
        {
            readonly float _rx, _ry, _cos, _sin;

            public Blob(float x, float y, float rx, float ry, float angle)
            {
                X = x;
                Y = y;
                _rx = rx;
                _ry = ry;
                _cos = MathF.Cos(angle);
                _sin = MathF.Sin(angle);
            }

            public float X { get; }
            public float Y { get; }

            /// <summary>Normalised ellipse distance: 0 at the centre, 1 on the ellipse edge.</summary>
            public float Distance(int x, int y)
            {
                float dx = x - X, dy = y - Y;
                var u = (dx * _cos + dy * _sin) / _rx;
                var v = (-dx * _sin + dy * _cos) / _ry;
                return MathF.Sqrt(u * u + v * v);
            }
        }

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
