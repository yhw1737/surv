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
            var timer = System.Diagnostics.Stopwatch.StartNew();
            Island = new IslandGenerator(Seed);
            Island.IsLand(0, 0); // force the cached mask now, so the timing below is honest
            var shapeMs = timer.ElapsedMilliseconds;
            GenerateWater();
            var waterMs = timer.ElapsedMilliseconds;
            PlaceNodes();
            var nodesMs = timer.ElapsedMilliseconds;
            BuildMap();
            Debug.Log($"[Isle] Island {Seed} built in {timer.ElapsedMilliseconds} ms (shape {shapeMs}, water {waterMs - shapeMs}, nodes {nodesMs - waterMs}, map {timer.ElapsedMilliseconds - nodesMs}); {_nodes.Count} nodes, {_water.Count} water tiles, {Island.BlobCount} landmasses");
            _shoreAt = ShoreAtTile;
            PlayerMovement.IsWalkable = IsWalkable;
        }

        /// <summary>Dry land only — the sea, ponds and rivers are walls (SYS-WORLD-03 §Water bodies).</summary>
        /// <summary>Areas outside the island that have their own ground — the dungeons' underground layer (SYS-DUNG-01).
        /// Returns null for a point it doesn't own, else whether that point can be stood on.</summary>
        public static System.Func<Vector2, bool?> ExtraWalkable { get; set; }

        /// <summary>Dry land, outside every standing node's trunk. The water's edge is the smooth drawn shoreline
        /// (<see cref="ShoreField"/>), not the tile grid.</summary>
        public bool IsWalkable(Vector2 world)
        {
            if (ExtraWalkable?.Invoke(world) is { } underground) return underground;
            var half = IslandGenerator.Size / 2f;
            if (ShoreField.IsWater(ShoreField.At(_shoreAt, world.x + half - 0.5f, world.y + half - 0.5f))) return false;
            return !BlockedByNode(world);
        }

        /// <summary>A standing node (a tree, a rock) whose <c>block_radius</c> covers this point. Depleted nodes (felled,
        /// mined out) don't block.</summary>
        public bool BlockedByNode(Vector2 world)
        {
            var centre = WorldToTile(world);
            for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
            {
                if (!_nodeByTile.TryGetValue(new Vec2Int(centre.X + dx, centre.Y + dy), out var node)) continue;
                var radius = node.Def.BlockRadius;
                if (radius <= 0f || (node.Def.Gather != null && node.UsesLeft <= 0)) continue;
                if ((node.Position - world).sqrMagnitude < radius * radius) return true;
            }
            return false;
        }

        /// <summary>Blurred water per tile, filled as it's asked for (main thread only).</summary>
        readonly Dictionary<Vec2Int, float> _shore = new();
        System.Func<int, int, float> _shoreAt;

        float ShoreAtTile(int x, int y)
        {
            var tile = new Vec2Int(x, y);
            if (_shore.TryGetValue(tile, out var value)) return value;
            return _shore[tile] = ShoreField.AtTile((tx, ty) => !Island.IsLand(tx, ty) || _water.ContainsKey(new Vec2Int(tx, ty)), x, y);
        }

        readonly Dictionary<Vec2Int, WorldObjectDef> _water = new();

        /// <summary>Pond and river tiles (the sea isn't counted).</summary>
        public int WaterTileCount => _water.Count;
        Dictionary<Vec2Int, int> _depthTiles = new();
        WorldObjectDef _ocean;

        /// <summary>The water body at a tile — a pond or river, the sea for anything off the island, or null on dry land.</summary>
        public WorldObjectDef WaterAt(Vec2Int tile)
        {
            if (_water.TryGetValue(tile, out var def)) return def;
            return Island.IsLand(tile.X, tile.Y) ? null : _ocean;
        }

        /// <summary>SYS-WORLD-03: depth = tiles from the nearest land × the body's depth_per_tile. Off-island tiles
        /// measure their distance to the island's silhouette.</summary>
        public float DepthAt(Vec2Int tile)
        {
            var def = WaterAt(tile);
            if (def == null) return 0f;
            var perTile = def.Fishing?.DepthPerTile ?? 1f;
            if (_depthTiles.TryGetValue(tile, out var inland)) return inland * perTile;
            return OceanDistance(tile) * perTile;
        }

        /// <summary>Nearest water tile within <paramref name="reachTiles"/> of a world point, or null.</summary>
        public Vec2Int? NearestWater(Vector2 from, float reachTiles)
        {
            var centre = WorldToTile(from);
            var r = Mathf.CeilToInt(reachTiles);
            Vec2Int? best = null;
            var bestDistance = reachTiles;
            for (var dx = -r; dx <= r; dx++)
            for (var dy = -r; dy <= r; dy++)
            {
                var tile = new Vec2Int(centre.X + dx, centre.Y + dy);
                if (WaterAt(tile) == null) continue;
                var distance = Vector2.Distance(from, TileToWorld(tile));
                if (distance > bestDistance) continue;
                best = tile;
                bestDistance = distance;
            }
            return best;
        }

        /// <summary>Distance from an off-island tile to the nearest land, searched outward a ring at a time (capped —
        /// beyond the cap the sea is simply "deep").</summary>
        int OceanDistance(Vec2Int tile)
        {
            const int Cap = 12;
            for (var r = 1; r <= Cap; r++)
                for (var dx = -r; dx <= r; dx++)
                for (var dy = -r; dy <= r; dy++)
                {
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;
                    if (Island.IsLand(tile.X + dx, tile.Y + dy) && !_water.ContainsKey(new Vec2Int(tile.X + dx, tile.Y + dy))) return r;
                }
            return Cap;
        }

        /// <summary>Ponds and rivers from every def with a <c>water_body</c> block; the def with <c>ocean</c> owns the sea.</summary>
        void GenerateWater()
        {
            var defs = DefRegistry.All<WorldObjectDef>();
            var plans = new List<WaterBodyPlan>();
            for (var i = 0; i < defs.Count; i++)
            {
                if (defs[i].Ocean) _ocean = defs[i];
                var body = defs[i].WaterBody;
                if (body != null) plans.Add(new WaterBodyPlan(i, body.Kind, body.Count, body.MinSize, body.MaxSize));
            }
            foreach (var (tile, index) in WaterGenerator.Generate(Island, Seed, plans)) _water[tile] = defs[index];
            _depthTiles = WaterGenerator.DistanceToLand(_water.Keys);
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
                _mapDirty = true;
            }
        }

        /// <summary>Draws every def's placeholder silhouette ahead of time, one per frame, so the first sight of a new
        /// creature or node doesn't stall a frame on rasterising its 256-px shape.</summary>
        System.Collections.IEnumerator WarmShapes()
        {
            foreach (var def in DefRegistry.All<WorldObjectDef>())
            {
                if (def.Visual == null) continue;
                ShapeLibrary.Sprite(def.Visual.Shape, NodeColour(def));
                yield return null;
            }
            foreach (var def in DefRegistry.All<CreatureDef>())
            {
                if (def.Visual == null) continue;
                ShapeLibrary.Sprite(def.Visual.Shape, ShapeLibrary.ParseColour(def.Visual.Color, PlaceholderVisuals.ColorForTags(def.Tags)));
                yield return null;
            }
        }

        /// <summary>Map repaints are batched: the 1152² texture is uploaded at most once per frame.</summary>
        bool _mapDirty;

        /// <summary>Node sprites are grouped per chunk and only chunks near the camera are switched on — the island
        /// holds over ten thousand nodes.</summary>
        readonly Dictionary<Vec2Int, List<SpriteRenderer>> _viewsByChunk = new();
        readonly HashSet<Vec2Int> _activeChunks = new();
        float _nextCullAt;

        /// <summary>Chunks around the camera kept drawn, in each direction. [invented] presentation value.</summary>
        const int VisibleChunkRadius = 2;

        void LateUpdate()
        {
            if (_mapDirty)
            {
                _map.Apply();
                _mapDirty = false;
            }
            if (UnityEngine.Time.time < _nextCullAt) return;
            _nextCullAt = UnityEngine.Time.time + 0.25f;
            var camera = Camera.main;
            if (camera == null) return;

            var centre = Chunk.CoordFromTilePosition(WorldToTile(Isle.Core.Util.ViewTilt.FocusOr(camera)));
            var wanted = new HashSet<Vec2Int>();
            for (var dx = -VisibleChunkRadius; dx <= VisibleChunkRadius; dx++)
            for (var dy = -VisibleChunkRadius; dy <= VisibleChunkRadius; dy++)
                wanted.Add(new Vec2Int(centre.X + dx, centre.Y + dy));
            foreach (var chunk in _activeChunks)
                if (!wanted.Contains(chunk)) SetChunkVisible(chunk, false);
            foreach (var chunk in wanted)
                if (!_activeChunks.Contains(chunk)) SetChunkVisible(chunk, true);
            _activeChunks.Clear();
            _activeChunks.UnionWith(wanted);
        }

        void SetChunkVisible(Vec2Int chunk, bool visible)
        {
            if (!_viewsByChunk.TryGetValue(chunk, out var views))
            {
                if (!visible || !_nodesByChunk.TryGetValue(chunk, out var nodes)) return;
                foreach (var node in nodes)
                {
                    CreateNodeView(node);
                    Paint(node);
                }
                return;
            }
            foreach (var view in views) view.gameObject.SetActive(visible);
        }

        public ResourceNode NodeAt(Vec2Int tile) => _nodeByTile.GetValueOrDefault(tile);

        /// <summary>Puts a node back the way a save recorded it.</summary>
        public void RestoreNode(ResourceNode node, int usesLeft, float respawnInSeconds)
        {
            node.UsesLeft = usesLeft;
            node.RespawnAt = _realSeconds + respawnInSeconds;
            Paint(node);
            _mapDirty = true;
        }

        /// <summary>Centre of a tile in world space. The map sprite spans ±Size/2 with one pixel per tile, so
        /// tile 0 covers [−Size/2, −Size/2 + 1) and its centre is half a tile in.</summary>
        /// <summary>
        /// SYS-START-01: a beach to wake up on — a dry, walkable tile of <paramref name="biome"/> a couple of tiles in
        /// from the open sea, found by walking out from the island's middle in a seed-chosen direction (other directions
        /// are tried in turn). Seed-deterministic. The middle of the island if no shore qualifies.
        /// </summary>
        public Vector2 ShoreStart(Biome biome, int salt = 0)
        {
            const int stepBack = 2;
            var centre = new Vec2Int(IslandGenerator.Size / 2, IslandGenerator.Size / 2);
            var start = (int)(IslandGenerator.Hash(Seed, 4242, salt) % 360u);
            for (var turn = 0; turn < 24; turn++)
            {
                var angle = (start + turn * 15) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                for (var r = 1; r < IslandGenerator.Size / 2; r++)
                {
                    var tile = new Vec2Int(centre.X + Mathf.RoundToInt(dir.x * r), centre.Y + Mathf.RoundToInt(dir.y * r));
                    if (WaterAt(tile)?.Ocean != true) continue;
                    // The first open sea on this line: step back onto the sand.
                    var shore = new Vec2Int(centre.X + Mathf.RoundToInt(dir.x * (r - stepBack)), centre.Y + Mathf.RoundToInt(dir.y * (r - stepBack)));
                    if (IsWalkable(TileToWorld(shore)) && WaterAt(shore) == null && Island.BiomeAt(shore.X, shore.Y) == biome) return TileToWorld(shore);
                    break;
                }
            }
            return TileToWorld(centre);
        }

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
            foreach (var node in NodesNear(from, reachTiles))
            {
                if (!filter(node)) continue;
                var distance = Vector2.Distance(from, node.Position);
                if (distance > bestDistance) continue;
                best = node;
                bestDistance = distance;
            }
            return best;
        }

        /// <summary>Nodes in the chunks overlapping a circle — the spatial index behind every "what's near me" query,
        /// so none of them walk all twelve thousand nodes. Callers still check the exact distance.</summary>
        public IEnumerable<ResourceNode> NodesNear(Vector2 from, float radiusTiles)
        {
            var min = Chunk.CoordFromTilePosition(WorldToTile(from - Vector2.one * radiusTiles));
            var max = Chunk.CoordFromTilePosition(WorldToTile(from + Vector2.one * radiusTiles));
            for (var cx = min.X; cx <= max.X; cx++)
            for (var cy = min.Y; cy <= max.Y; cy++)
                if (_nodesByChunk.TryGetValue(new Vec2Int(cx, cy), out var nodes))
                    foreach (var node in nodes) yield return node;
        }

        readonly Dictionary<Vec2Int, List<ResourceNode>> _nodesByChunk = new();

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
                _mapDirty = true;
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
                    if (_nodeByTile.ContainsKey(tile) || _water.ContainsKey(tile)) continue;
                    var node = new ResourceNode
                    {
                        Def = def,
                        Tile = tile,
                        // Standing nodes sit off the tile centre (a hashed, fixed offset), so woods don't grow on a grid.
                        Position = TileToWorld(tile) + (def.Gather != null ? NodeOffset(tile) : Vector2.zero),
                        UsesLeft = def.Gather?.Uses ?? 0,
                    };
                    _nodes.Add(node);
                    _nodeByTile[tile] = node;
                    var chunk = Chunk.CoordFromTilePosition(tile);
                    if (!_nodesByChunk.TryGetValue(chunk, out var inChunk)) _nodesByChunk[chunk] = inChunk = new List<ResourceNode>();
                    inChunk.Add(node);
                }
            }
        }

        /// <summary>How far a standing node may sit from its tile's centre, each axis. [invented look]</summary>
        const float NodeJitterTiles = 0.3f;

        static Vector2 NodeOffset(Vec2Int tile)
        {
            var h = NodeHash(tile);
            return new Vector2(((h & 0xFFFF) / 65535f - 0.5f) * 2f * NodeJitterTiles, (((h >> 16) & 0xFFFF) / 65535f - 0.5f) * 2f * NodeJitterTiles);
        }

        /// <summary>A stable per-tile hash for placement and look variety.</summary>
        public static uint NodeHash(Vec2Int tile)
        {
            unchecked
            {
                var h = (uint)(tile.X * 73856093) ^ (uint)(tile.Y * 19349663) ^ 0x9E3779B9u;
                h ^= h >> 15;
                h *= 0x2C1B3C6Du;
                h ^= h >> 12;
                h *= 0x297A2D39u;
                h ^= h >> 15;
                return h;
            }
        }

        void BuildMap()
        {
            var size = IslandGenerator.Size;
            _map = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
            {
                // Smoothed when the minimap zooms in, instead of showing square tiles.
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            // One array upload, not 1.3 M SetPixel calls.
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                pixels[y * size + x] = BiomeColour(new Vec2Int(x, y));
            _map.SetPixels32(pixels);
            // Sprites are made per chunk the first time it comes into view (SetChunkVisible), not twelve thousand at once.
            foreach (var node in _nodes) Paint(node);
            _map.Apply();

            // The one-texel map is the minimap's source and a low-res fallback under the high-res ground chunks.
            var sprite = Sprite.Create(_map, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), pixelsPerUnit: 1f);
            var renderer = new GameObject("IslandMap").AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = -10;

            var waterDefs = new List<WorldObjectDef>();
            foreach (var def in DefRegistry.All<WorldObjectDef>())
                if (def.WaterBody != null || def.Ocean) waterDefs.Add(def);
            gameObject.AddComponent<GroundRenderer>().Initialize(this, SeaColour, CoastColour, ForestColour, MarshColour, waterDefs);
            StartCoroutine(WarmShapes());
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

        /// <summary>One of three shades of a node's colour: darker and cooler, as is, lighter and warmer.</summary>
        static Color Shade(Color c, int shade, float jitter)
        {
            var k = (shade - 1) * jitter;
            var warm = new Color(Mathf.Clamp01(c.r * (1f + k * 1.2f)), Mathf.Clamp01(c.g * (1f + k)), Mathf.Clamp01(c.b * (1f + k * 0.6f)), c.a);
            return warm;
        }

        static Color NodeColour(WorldObjectDef def) => ShapeLibrary.ParseColour(def.Visual?.Color, PlaceholderVisuals.ColorForTags(def.Tags));

        static readonly Color DepletedTint = new(1f, 1f, 1f, 0.25f);

        /// <summary>Presentation: harvestable node diameter in tiles.</summary>
        const float NodeDiameterTiles = 0.85f;

        void CreateNodeView(ResourceNode node)
        {
            var view = new GameObject(node.Def.Id.Name).AddComponent<SpriteRenderer>();
            // Water lies flat under everything. Standing things (trees, rocks, bushes) are placed by their foot and sort
            // by it against the player and creatures (same order, custom-axis sort), so you walk behind a trunk.
            var standing = node.Def.Gather != null;
            // Variety (presentation): a drawing variant, one of three shades, a size and a mirror, all from the tile's
            // hash, so the same island always looks the same.
            var hash = NodeHash(node.Tile);
            var jitter = node.Def.Visual?.Jitter ?? 0f;
            var colour = NodeColour(node.Def);
            if (jitter > 0f) colour = Shade(colour, (int)(hash >> 8) % 3, jitter);
            view.sprite = standing ? ShapeLibrary.StandingSprite(node.Def.Visual?.Shape, colour, (int)(hash % 7919u))
                : ShapeLibrary.Sprite(node.Def.Visual?.Shape, colour);
            if (standing && jitter > 0f) view.flipX = ((hash >> 24) & 1u) == 1u;
            view.spriteSortPoint = SpriteSortPoint.Pivot;
            view.sortingOrder = standing ? 2 : 1;
            view.transform.SetParent(transform, worldPositionStays: false);
            view.transform.position = node.Position;
            var sizeJitter = jitter > 0f ? 1f + jitter * (((hash >> 16) & 0xFF) / 127.5f - 1f) : 1f;
            view.transform.localScale = Vector3.one * ((node.Def.Visual?.Size ?? NodeDiameterTiles) * sizeJitter);
            if (standing) Isle.Core.Util.ViewTilt.Stand(view.transform);
            node.View = view;
            var chunk = Chunk.CoordFromTilePosition(node.Tile);
            if (!_viewsByChunk.TryGetValue(chunk, out var list)) _viewsByChunk[chunk] = list = new List<SpriteRenderer>();
            list.Add(view);
            // Thousands of these: keep them out of the Editor's Hierarchy, whose refresh cost was a lag source.
            view.gameObject.hideFlags = HideFlags.HideInHierarchy;
        }

        Color32 BiomeColour(Vec2Int tile) =>
            _water.TryGetValue(tile, out var water) ? (Color32)ShapeLibrary.ParseColour(water.Visual?.Color, SeaColour)
            : !Island.IsLand(tile.X, tile.Y) ? SeaColour : Island.BiomeAt(tile.X, tile.Y) switch
        {
            Biome.Forest => ForestColour,
            Biome.Marsh => MarshColour,
            _ => CoastColour,
        };
    }
}
