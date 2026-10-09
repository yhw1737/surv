using System.Collections.Generic;
using System.Threading.Tasks;
using Isle.Core;
using Isle.Core.Util;
using Isle.Data;
using Isle.World.Chunks;
using Isle.World.Generation;
using UnityEngine;

namespace Isle.World.Island
{
    /// <summary>
    /// High-resolution ground around the camera (developer: "모든 이미지는 픽셀이 아니라 고해상도"). The island map is one
    /// texel per tile, which reads as big blocks up close; this paints each nearby chunk at <see cref="PixelsPerTile"/>
    /// with smooth shorelines (bilinear water weight, thresholded) and soft biome blending plus fine noise. Texels are
    /// computed on a worker thread from read-only island data; only the texture upload is on the main thread. The
    /// one-texel map stays underneath as a fallback until a chunk is ready, and for the minimap.
    /// </summary>
    public sealed class GroundRenderer : MonoBehaviour
    {
        const int PixelsPerTile = 16;
        const int ChunkPixels = Chunk.Size * PixelsPerTile;
        /// <summary>Pencil paper cells per tile on the ground — coarser than on figures, so ground strokes read as loose
        /// shading. [invented look]</summary>
        const float PaperCellsPerTile = 6f;

        /// <summary>Chunks around the camera to draw, and how far out to keep them before freeing. Presentation values.</summary>
        const int DrawRadius = 1;
        const int KeepRadius = 3;

        IslandWorld _world;
        Color32 _sea, _coast, _forest, _marsh;
        readonly Dictionary<WorldObjectDef, Color32> _waterColours = new();
        readonly Dictionary<Vec2Int, SpriteRenderer> _chunks = new();
        readonly Dictionary<Vec2Int, Task<Color32[]>> _pending = new();
        readonly List<Vec2Int> _scratch = new();

        public void Initialize(IslandWorld world, Color32 sea, Color32 coast, Color32 forest, Color32 marsh, IEnumerable<WorldObjectDef> waterDefs)
        {
            _world = world;
            (_sea, _coast, _forest, _marsh) = (sea, coast, forest, marsh);
            // Resolved here, on the main thread: colour parsing is a Unity API and the workers mustn't call it.
            foreach (var def in waterDefs) _waterColours[def] = ShapeLibrary.ParseColour(def.Visual?.Color, sea);
        }

        void Update()
        {
            var camera = Camera.main;
            if (_world == null || camera == null) return;
            var centre = Chunk.CoordFromTilePosition(IslandWorld.WorldToTile(Isle.Core.Util.ViewTilt.FocusOr(camera)));

            for (var dx = -DrawRadius - 1; dx <= DrawRadius + 1; dx++)
            for (var dy = -DrawRadius - 1; dy <= DrawRadius + 1; dy++)
            {
                var chunk = new Vec2Int(centre.X + dx, centre.Y + dy);
                if (_chunks.ContainsKey(chunk) || _pending.ContainsKey(chunk)) continue;
                _pending[chunk] = Task.Run(() => Compute(chunk));
            }

            _scratch.Clear();
            foreach (var (chunk, task) in _pending)
                if (task.IsCompleted) _scratch.Add(chunk);
            foreach (var chunk in _scratch)
            {
                var task = _pending[chunk];
                _pending.Remove(chunk);
                if (task.Status == TaskStatus.RanToCompletion) Build(chunk, task.Result);
            }

            _scratch.Clear();
            foreach (var chunk in _chunks.Keys)
                if (Mathf.Abs(chunk.X - centre.X) > KeepRadius || Mathf.Abs(chunk.Y - centre.Y) > KeepRadius) _scratch.Add(chunk);
            foreach (var chunk in _scratch)
            {
                var renderer = _chunks[chunk];
                _chunks.Remove(chunk);
                if (renderer == null) continue;
                Destroy(renderer.sprite.texture);
                Destroy(renderer.gameObject);
            }
        }

        void Build(Vec2Int chunk, Color32[] pixels)
        {
            var texture = new Texture2D(ChunkPixels, ChunkPixels, TextureFormat.RGBA32, mipChain: true)
            {
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: true, makeNoLongerReadable: true);

            var renderer = new GameObject("Ground").AddComponent<SpriteRenderer>();
            renderer.gameObject.hideFlags = HideFlags.HideInHierarchy;
            renderer.sprite = Sprite.Create(texture, new Rect(0, 0, ChunkPixels, ChunkPixels), Vector2.zero, PixelsPerTile, 0, SpriteMeshType.FullRect);
            renderer.sortingOrder = -9; // over the one-texel map, under everything else
            renderer.transform.SetParent(transform, worldPositionStays: false);
            var origin = IslandWorld.TileToWorld(new Vec2Int(chunk.X * Chunk.Size, chunk.Y * Chunk.Size)) - new Vector2(0.5f, 0.5f);
            renderer.transform.position = origin;
            _chunks[chunk] = renderer;
        }

        /// <summary>Worker thread. Reads only data that's fixed after generation (island cells, water tiles).</summary>
        Color32[] Compute(Vec2Int chunk)
        {
            // Tile samples for the chunk plus a border, blurred twice (≈5×5 Gaussian) before interpolation: binary
            // per-tile water and biome values otherwise draw stair-stepped shorelines and borders.
            const int border = 3;
            const int grid = Chunk.Size + border * 2;
            var colours = new Color[grid * grid];
            var water = new float[grid * grid];
            var baseX = chunk.X * Chunk.Size - border;
            var baseY = chunk.Y * Chunk.Size - border;
            for (var gy = 0; gy < grid; gy++)
            for (var gx = 0; gx < grid; gx++)
            {
                var tile = new Vec2Int(baseX + gx, baseY + gy);
                var body = _world.WaterAt(tile);
                var i = gy * grid + gx;
                if (body != null)
                {
                    colours[i] = _waterColours.TryGetValue(body, out var c) ? c : _sea;
                    water[i] = 1f;
                    continue;
                }
                colours[i] = _world.Island.BiomeAt(tile.X, tile.Y) switch
                {
                    Biome.Forest => (Color)_forest,
                    Biome.Marsh => (Color)_marsh,
                    _ => (Color)_coast,
                };
            }
            var isWater = (float[])water.Clone();
            for (var pass = 0; pass < 2; pass++)
            {
                water = Blur(water, grid);
                colours = BlurSide(colours, isWater, grid);
            }

            var pixels = new Color32[ChunkPixels * ChunkPixels];
            var shoreLight = new Color(0.93f, 0.95f, 0.85f);
            for (var py = 0; py < ChunkPixels; py++)
            for (var px = 0; px < ChunkPixels; px++)
            {
                // Position in the sample grid, where integer coordinates are tile centres.
                var fx = (px + 0.5f) / PixelsPerTile + border - 0.5f;
                var fy = (py + 0.5f) / PixelsPerTile + border - 0.5f;
                var ix = Mathf.Clamp((int)fx, 0, grid - 2);
                var iy = Mathf.Clamp((int)fy, 0, grid - 2);
                var tx = fx - ix;
                var ty = fy - iy;
                var i00 = iy * grid + ix;
                var i10 = i00 + 1;
                var i01 = i00 + grid;
                var i11 = i01 + 1;

                var w = Bilinear(water[i00], water[i10], water[i01], water[i11], tx, ty);
                var wet = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.44f, 0.56f, w));
                var land = BlendSide(colours, isWater, false, i00, i10, i01, i11, tx, ty);
                var sea = BlendSide(colours, isWater, true, i00, i10, i01, i11, tx, ty);
                // Deeper water (well past the shoreline) reads a little darker.
                sea *= Mathf.Lerp(1f, 0.82f, Mathf.InverseLerp(0.6f, 1f, w));
                var colour = Color.Lerp(land, sea, wet);

                var shore = 1f - Mathf.Abs(w - 0.5f) / 0.1f;
                if (shore > 0f) colour = Color.Lerp(colour, shoreLight, shore * 0.3f);

                // Smooth, multi-scale variation (no per-texel grain — that reads as pixels).
                var wx = (chunk.X * Chunk.Size) + (px + 0.5f) / PixelsPerTile;
                var wy = (chunk.Y * Chunk.Size) + (py + 0.5f) / PixelsPerTile;
                var fine = SmoothNoise(wx / 0.7f, wy / 0.7f, 911) - 0.5f;
                var broad = SmoothNoise(wx / 4f, wy / 4f, 733) - 0.5f;
                var shade = 1f + fine * 0.07f + broad * 0.09f * (1f - wet * 0.6f);
                // Pencil pass, in world space so strokes run on across chunk seams.
                pixels[py * ChunkPixels + px] = PencilLook.Shade(new Color(colour.r * shade, colour.g * shade, colour.b * shade, 1f),
                    wx * PaperCellsPerTile, wy * PaperCellsPerTile);
            }
            return pixels;
        }

        /// <summary>One [1 2 1]² / 16 pass; the outermost ring is left as is.</summary>
        static float[] Blur(float[] src, int grid)
        {
            var dst = (float[])src.Clone();
            for (var y = 1; y < grid - 1; y++)
            for (var x = 1; x < grid - 1; x++)
            {
                var sum = 0f;
                for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                    sum += src[(y + dy) * grid + x + dx] * Kernel(dx, dy);
                dst[y * grid + x] = sum / 16f;
            }
            return dst;
        }

        /// <summary>The same blur on colours, but only among tiles on the same side of the shore.</summary>
        static Color[] BlurSide(Color[] src, float[] isWater, int grid)
        {
            var dst = (Color[])src.Clone();
            for (var y = 1; y < grid - 1; y++)
            for (var x = 1; x < grid - 1; x++)
            {
                var centreWet = isWater[y * grid + x] > 0.5f;
                var sum = Color.clear;
                var total = 0f;
                for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    var j = (y + dy) * grid + x + dx;
                    if ((isWater[j] > 0.5f) != centreWet) continue;
                    var k = Kernel(dx, dy);
                    sum += src[j] * k;
                    total += k;
                }
                if (total > 0f) dst[y * grid + x] = sum / total;
            }
            return dst;
        }

        static float Kernel(int dx, int dy) => (dx == 0 ? 2f : 1f) * (dy == 0 ? 2f : 1f);

        /// <summary>Bilinear value noise on a hashed lattice, 0..1.</summary>
        static float SmoothNoise(float x, float y, int salt)
        {
            var ix = Mathf.FloorToInt(x);
            var iy = Mathf.FloorToInt(y);
            var tx = x - ix;
            var ty = y - iy;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            float L(int a, int b) => (IslandGenerator.Hash(salt, a, b) % 1000) / 1000f;
            return Bilinear(L(ix, iy), L(ix + 1, iy), L(ix, iy + 1), L(ix + 1, iy + 1), tx, ty);
        }

        static float Bilinear(float a, float b, float c, float d, float tx, float ty) =>
            Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);

        /// <summary>Blends the four corner colours of one side (land or water) only, so a shoreline never mixes sand
        /// into the sea or blue into the beach; falls back to any corner when no corner is on that side.</summary>
        static Color BlendSide(Color[] colours, float[] water, bool wet, int i00, int i10, int i01, int i11, float tx, float ty)
        {
            float w00 = (1 - tx) * (1 - ty), w10 = tx * (1 - ty), w01 = (1 - tx) * ty, w11 = tx * ty;
            if ((water[i00] > 0.5f) != wet) w00 = 0f;
            if ((water[i10] > 0.5f) != wet) w10 = 0f;
            if ((water[i01] > 0.5f) != wet) w01 = 0f;
            if ((water[i11] > 0.5f) != wet) w11 = 0f;
            var total = w00 + w10 + w01 + w11;
            if (total <= 0f) return colours[i00];
            return (colours[i00] * w00 + colours[i10] * w10 + colours[i01] * w01 + colours[i11] * w11) / total;
        }
    }
}
