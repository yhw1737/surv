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
        public const int CellTiles = 4;
        public const float RevealRadiusTiles = 16f;
        public const int MarkerCapacity = 32;

        static readonly Color32 Hidden = new(10, 12, 18, 255);
        static readonly Color32 Seen = new(0, 0, 0, 0);

        public static MapState Instance { get; private set; }

        public FogGrid Fog { get; private set; }
        public Texture2D FogTexture { get; private set; }
        public MapMarkers Markers { get; } = new(MarkerCapacity);

        float _nextRevealAt;

        void Awake()
        {
            Instance = this;
            Fog = new FogGrid(IslandGenerator.Size, CellTiles);
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
                var changed = Fog.Reveal(IslandWorld.WorldToTile(player.transform.position), RevealRadiusTiles);
                if (changed.Count == 0) continue;
                foreach (var (x, y) in changed) FogTexture.SetPixel(x, y, Seen);
                FogTexture.Apply();
            }
        }

        public bool IsExplored(Vector2 world) => Fog.IsExplored(IslandWorld.WorldToTile(world));

        /// <summary>Loading a save: take its explored cells and redraw the whole fog.</summary>
        public void Load(string explored)
        {
            Fog.Deserialize(explored);
            Repaint();
        }

        void Repaint()
        {
            var pixels = new Color32[Fog.Cells * Fog.Cells];
            for (var y = 0; y < Fog.Cells; y++)
            for (var x = 0; x < Fog.Cells; x++)
                pixels[y * Fog.Cells + x] = Fog.IsCellExplored(x, y) ? Seen : Hidden;
            FogTexture.SetPixels32(pixels);
            FogTexture.Apply();
        }
    }
}
