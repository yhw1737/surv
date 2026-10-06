using Isle.Gameplay.Character;
using Isle.World.Generation;
using Isle.World.Island;
using UnityEngine;

namespace Isle.UI.Prototype
{
    /// <summary>
    /// SYS-MAP-01: the local player's map knowledge — explored fog cells and their markers — and the fog drawn over
    /// the world. Revealing runs 4× a second around the player; explored stays explored. Per player, host only in
    /// the prototype.
    /// </summary>
    public sealed class MapState : MonoBehaviour
    {
        /// <summary>Fog resolution: 2×2 tiles per cell (was 4×4 — the developer found the map's fog blocky).</summary>
        public const int CellTiles = 2;

        /// <summary>The revealed edge fades over this many tiles instead of stepping cell by cell. Presentation.</summary>
        const float EdgeFadeTiles = 5f;
        public const float RevealRadiusTiles = 16f;
        public const int MarkerCapacity = 32;

        static readonly Color32 Hidden = new(10, 12, 18, 255);
    
        public static MapState Instance { get; private set; }

        public FogGrid Fog { get; private set; }
        public Texture2D FogTexture { get; private set; }
        public MapMarkers Markers { get; } = new(MarkerCapacity);

        float _nextRevealAt;
        Color32[] _pixels;

        void Awake()
        {
            Instance = this;
            Fog = new FogGrid(IslandGenerator.Size, CellTiles);
            _pixels = new Color32[Fog.Cells * Fog.Cells];
            FogTexture = new Texture2D(Fog.Cells, Fog.Cells, TextureFormat.RGBA32, mipChain: false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            Repaint();

            // Drawn over everything in the world so nothing in unexplored ground shows. One texel per fog cell,
            // stretched over the whole map; bilinear filtering softens the edge.
            var overlay = new GameObject("FogOfWar").AddComponent<SpriteRenderer>();
            overlay.sprite = Sprite.Create(FogTexture, new Rect(0, 0, Fog.Cells, Fog.Cells), new Vector2(0.5f, 0.5f), 1f / CellTiles);
            overlay.sortingOrder = 100;
            overlay.transform.SetParent(transform, worldPositionStays: false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (Time.time < _nextRevealAt) return;
            _nextRevealAt = Time.time + 0.25f;
            foreach (var player in PlayerInteraction.All)
            {
                if (!player.IsOwner) continue;
                var tile = IslandWorld.WorldToTile(player.transform.position);
                Fog.Reveal(tile, RevealRadiusTiles);
                if (PaintReveal(tile)) Upload();
            }
        }

        public bool IsExplored(Vector2 world) => Fog.IsExplored(IslandWorld.WorldToTile(world));

        /// <summary>Loading a save: take its explored cells and redraw the whole fog.</summary>
        public void Load(string explored)
        {
            Fog.Deserialize(explored);
            Repaint();
        }

        /// <summary>Clears the fog in a soft disc around a tile: fully clear inside, fading out over the edge, never
        /// darkening what's already been seen. True when any texel changed.</summary>
        bool PaintReveal(Isle.Core.Vec2Int tile)
        {
            var cells = Fog.Cells;
            var inner = (RevealRadiusTiles - EdgeFadeTiles) / CellTiles;
            var outer = RevealRadiusTiles / CellTiles;
            var cx = (tile.X + 0.5f) / CellTiles;
            var cy = (tile.Y + 0.5f) / CellTiles;
            var reach = Mathf.CeilToInt(outer) + 1;
            var changed = false;
            for (var y = Mathf.Max(0, (int)cy - reach); y <= Mathf.Min(cells - 1, (int)cy + reach); y++)
            for (var x = Mathf.Max(0, (int)cx - reach); x <= Mathf.Min(cells - 1, (int)cx + reach); x++)
            {
                var d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                if (d >= outer) continue;
                var alpha = (byte)Mathf.RoundToInt(Hidden.a * Mathf.Clamp01((d - inner) / (outer - inner)));
                var i = y * cells + x;
                if (alpha >= _pixels[i].a) continue;
                _pixels[i] = new Color32(Hidden.r, Hidden.g, Hidden.b, alpha);
                changed = true;
            }
            return changed;
        }

        void Upload()
        {
            FogTexture.SetPixels32(_pixels);
            FogTexture.Apply();
        }

        /// <summary>Full redraw from the explored cells (start, load), softened with one blur pass so loaded edges
        /// match freshly revealed ones.</summary>
        void Repaint()
        {
            var cells = Fog.Cells;
            var hard = new byte[cells * cells];
            for (var y = 0; y < cells; y++)
            for (var x = 0; x < cells; x++)
                hard[y * cells + x] = Fog.IsCellExplored(x, y) ? (byte)0 : Hidden.a;
            for (var y = 0; y < cells; y++)
            for (var x = 0; x < cells; x++)
            {
                int sum = 0, n = 0;
                for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    int sx = x + dx, sy = y + dy;
                    if (sx < 0 || sy < 0 || sx >= cells || sy >= cells) continue;
                    sum += hard[sy * cells + sx];
                    n++;
                }
                // Unexplored stays fully hidden; only explored cells soften toward their hidden neighbours.
                var a = hard[y * cells + x] == 0 ? (byte)(sum / n) : Hidden.a;
                _pixels[y * cells + x] = new Color32(Hidden.r, Hidden.g, Hidden.b, a);
            }
            Upload();
        }
    }
}
