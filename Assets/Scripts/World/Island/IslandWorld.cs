using System.Collections.Generic;
using Isle.Core;
using Isle.Core.Ids;
using Isle.Core.Util;
using Isle.Data;
using Isle.Modding.Defs;
using Isle.Networking;
using Isle.World.Chunks;
using Isle.World.Generation;
using Isle.World.Objects;
using UnityEngine;

namespace Isle.World.Island
{
    /// <summary>One placed resource node or water source (SYS-WORLD-03). Plain data — the map paints it
    /// and the gameplay layer reads/writes it through <see cref="IslandWorld"/>.</summary>
    public sealed class ResourceNode
    {
        public WorldObjectDef Def { get; init; }
        public Vec2Int Tile { get; init; }
        public Vector2 Position { get; init; }
        public int UsesLeft { get; set; }

        /// <summary>Real-time second at which a depleted node comes back.</summary>
        public float RespawnAt { get; set; }

        /// <summary>Placeholder shape for harvestable nodes; water spots have none and stay painted on the map.</summary>
        public SpriteRenderer View { get; set; }

        public bool IsHarvestable => Def.Gather != null && UsesLeft > 0;
        public bool IsDrinkable => Def.Drink != null;
    }

    /// <summary>
    /// Owns the island's generated layout: tiles, nodes and their respawn clock. Presentation is one
    /// painted texture (each tile is one pixel, so a 384×384 island is a single draw call). Server
    /// authority: only the server's <see cref="Update"/> advances respawns, and only the server calls
    /// <see cref="TryHarvest"/> — same host-process reasoning as <c>WorldObjectRegistry</c>.
    /// <para>
    /// Coordinates: tile (0,0) is the island's corner, and the island is centred on world origin, so
    /// <c>world = tile − IslandGenerator.Size / 2</c>. The player spawns at origin, inside the island.
    /// </para>
    /// </summary>
    public sealed class IslandWorld : MonoBehaviour
    {
        public static IslandWorld Instance { get; private set; }

        static readonly Color32 CoastColour = new(0xE8, 0xD9, 0xA0, 0xFF);
        static readonly Color32 ForestColour = new(0x3E, 0x7A, 0x3A, 0xFF);
        static readonly Color32 MarshColour = new(0x5B, 0x6F, 0x4A, 0xFF);

        /// <summary>Open sea. Public so the camera background can match the map edge.</summary>
        public static readonly Color32 SeaColour = new(0x2E, 0x6F, 0x9E, 0xFF);

        readonly List<ResourceNode> _nodes = new();
        readonly Dictionary<Vec2Int, ResourceNode> _nodeByTile = new();
        Texture2D _map;
        float _realSeconds;

        public IslandGenerator Island { get; private set; }
        public int Seed { get; private set; }
        public IReadOnlyList<ResourceNode> Nodes => _nodes;

        /// <summary>The painted island, one pixel per tile — the HUD reuses it as the minimap.</summary>
        public Texture2D Map => _map;

        /// <summary>Set before this component is added to resume a saved island; consumed once.</summary>
        public static int? NextSeed { get; set; }

        /// <summary>Seconds this island has been simulated — the clock node respawns are measured against.</summary>
        public float Now => _realSeconds;

        void Awake()
        {
            Instance = this;
            Seed = NextSeed ?? Random.Range(1, int.MaxValue);
            NextSeed = null;
            Island = new IslandGenerator(Seed);
            PlaceNodes();
            BuildMap();
            PlayerMovement.IsWalkable = IsWalkable;
        }

        /// <summary>Land only — the sea is the island's wall.</summary>
        public bool IsWalkable(Vector2 world)
        {
            var tile = WorldToTile(world);
            return Island.IsLand(tile.X, tile.Y);
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            PlayerMovement.IsWalkable = null;
        }

        /// <summary>Respawn runs server-side only (Absolute Rule 2). A solo host is also the server, so
        /// this runs for the one player who exists.</summary>
        void Update()
        {
            if (!FishNet.InstanceFinder.IsServerStarted) return;
            _realSeconds += UnityEngine.Time.deltaTime;
            foreach (var node in _nodes)
            {
                if (node.Def.Gather == null || node.UsesLeft > 0 || _realSeconds < node.RespawnAt) continue;
                node.UsesLeft = node.Def.Gather.Uses;
                Paint(node);
                _map.Apply();
            }
        }

        public ResourceNode NodeAt(Vec2Int tile) => _nodeByTile.GetValueOrDefault(tile);

        /// <summary>Puts a node back the way a save recorded it.</summary>
        public void RestoreNode(ResourceNode node, int usesLeft, float respawnInSeconds)
        {
            node.UsesLeft = usesLeft;
            node.RespawnAt = _realSeconds + respawnInSeconds;
            Paint(node);
            _map.Apply();
        }

        /// <summary>Centre of a tile in world space. The map sprite spans ±Size/2 with one pixel per tile, so
        /// tile 0 covers [−Size/2, −Size/2 + 1) and its centre is half a tile in.</summary>
        public static Vector2 TileToWorld(Vec2Int tile) =>
            new(tile.X - IslandGenerator.Size / 2f + 0.5f, tile.Y - IslandGenerator.Size / 2f + 0.5f);

        /// <summary>Inverse of <see cref="TileToWorld"/>: the tile a world point lies in.</summary>
        public static Vec2Int WorldToTile(Vector2 world) =>
            new(Mathf.FloorToInt(world.x + IslandGenerator.Size / 2f), Mathf.FloorToInt(world.y + IslandGenerator.Size / 2f));

        /// <summary>Nearest node within reach that passes <paramref name="filter"/>, or null.</summary>
        public ResourceNode NearestNode(Vector2 from, float reachTiles, System.Func<ResourceNode, bool> filter)
        {
            ResourceNode best = null;
            var bestDistance = reachTiles;
            foreach (var node in _nodes)
            {
                if (!filter(node)) continue;
                var distance = Vector2.Distance(from, node.Position);
                if (distance > bestDistance) continue;
                best = node;
                bestDistance = distance;
            }
            return best;
        }

        /// <summary>Server-side harvest: one use, returns the yield. Drinkable nodes never deplete.</summary>
        public bool TryHarvest(ResourceNode node, out NamespacedId item, out int count)
        {
            item = default;
            count = 0;
            if (!node.IsHarvestable) return false;

            node.UsesLeft--;
            item = node.Def.Gather.Item;
            count = node.Def.Gather.Count;
            if (node.UsesLeft == 0)
            {
                node.RespawnAt = _realSeconds + GatherCalculator.RespawnRealSeconds(node.Def.Gather.RespawnMinutes);
                Paint(node);
                _map.Apply();
            }
            return true;
        }

        void PlaceNodes()
        {
            var defs = DefRegistry.All<WorldObjectDef>();
            for (var i = 0; i < defs.Count; i++)
            {
                var def = defs[i];
                if (def.Spawn == null) continue;
                foreach (var tile in ResourcePlacer.Roll(Island, def.Spawn, Seed, salt: i + 1))
                {
                    if (_nodeByTile.ContainsKey(tile)) continue;
                    var node = new ResourceNode
                    {
                        Def = def,
                        Tile = tile,
                        Position = TileToWorld(tile),
                        UsesLeft = def.Gather?.Uses ?? 0,
                    };
                    _nodes.Add(node);
                    _nodeByTile[tile] = node;
                }
            }
        }

        void BuildMap()
        {
            var size = IslandGenerator.Size;
            _map = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            for (var x = 0; x < size; x++)
            for (var y = 0; y < size; y++)
                _map.SetPixel(x, y, BiomeColour(new Vec2Int(x, y)));
            foreach (var node in _nodes)
            {
                CreateNodeView(node);
                Paint(node);
            }
            _map.Apply();

            var sprite = Sprite.Create(_map, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), pixelsPerUnit: 1f);
            var renderer = new GameObject("IslandMap").AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = -10;
        }

        /// <summary>Repaints one tile: a node's colour while it has something to give, the biome
        /// underneath once it's depleted (a stump, an empty bush). Caller applies the texture — one
        /// upload per batch, not per node.</summary>
        void Paint(ResourceNode node)
        {
            // Every node is drawn as its own shape; a depleted one fades to a ghost of itself (a stump, a stripped
            // bush). The map pixel underneath keeps the node's colour for the minimap while it has something to give.
            var depleted = node.Def.Gather != null && node.UsesLeft <= 0;
            if (node.View != null) node.View.color = depleted ? DepletedTint : Color.white;
            _map.SetPixel(node.Tile.X, node.Tile.Y, depleted ? BiomeColour(node.Tile) : NodeColour(node.Def));
        }

        static Color NodeColour(WorldObjectDef def) => ShapeLibrary.ParseColour(def.Visual?.Color, PlaceholderVisuals.ColorForTags(def.Tags));

        static readonly Color DepletedTint = new(1f, 1f, 1f, 0.25f);

        /// <summary>Presentation: harvestable node diameter in tiles.</summary>
        const float NodeDiameterTiles = 0.85f;

        void CreateNodeView(ResourceNode node)
        {
            var view = new GameObject(node.Def.Id.Name).AddComponent<SpriteRenderer>();
            view.sprite = ShapeLibrary.Sprite(node.Def.Visual?.Shape, NodeColour(node.Def));
            // Water lies flat under everything; trees stand over creatures' feet.
            view.sortingOrder = node.Def.Gather == null ? 1 : 2;
            view.transform.SetParent(transform, worldPositionStays: false);
            view.transform.position = node.Position;
            view.transform.localScale = Vector3.one * (node.Def.Visual?.Size ?? NodeDiameterTiles);
            node.View = view;
        }

        Color32 BiomeColour(Vec2Int tile) => !Island.IsLand(tile.X, tile.Y) ? SeaColour : Island.BiomeAt(tile.X, tile.Y) switch
        {
            Biome.Forest => ForestColour,
            Biome.Marsh => MarshColour,
            _ => CoastColour,
        };
    }
}
