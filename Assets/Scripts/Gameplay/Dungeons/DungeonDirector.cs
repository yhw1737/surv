using System.Collections.Generic;
using System.Linq;
using Isle.Core;
using Isle.Core.Ids;
using Isle.Core.Util;
using Isle.Data;
using Isle.Gameplay.Character;
using Isle.Gameplay.Combat;
using Isle.Gameplay.Feedback;
using Isle.Gameplay.Hunting;
using Isle.Modding.Defs;
using Isle.World.Chunks;
using Isle.World.Generation;
using Isle.World.Island;
using UnityEngine;

namespace Isle.Gameplay.Dungeons
{
    /// <summary>
    /// SYS-DUNG-01 runtime (T-201): places each dungeon's entrance on the island, builds a floor the first time someone
    /// goes down (seed-deterministic), and runs its doors — stairs, locks and their keys, the vault's soft gate — plus the
    /// creatures on its spawn marks. Floors live in an underground layer far outside the island's tiles; the island's
    /// walkability asks <see cref="Walkable"/> first. Server-side, like the other directors.
    /// </summary>
    public sealed class DungeonDirector : MonoBehaviour
    {
        // Layout of the underground layer, world units (tiles). The island spans ±576.
        const float RegionOrigin = 2000f;
        const float FloorSpacing = 200f;
        const int PixelsPerTile = 8;

        // SYS-DUNG-01 decided values.
        const float SpecialistGateSeconds = 3f;
        const float AnyoneGateSeconds = 30f;
        const float NoiseWakeTiles = 25f;

        /// <summary>How far from a door you land after using it, so you don't stand on it. Presentation/feel.</summary>
        const float ArrivalOffsetTiles = 2f;

        /// <summary>Walking this far from a soft gate stops clearing it.</summary>
        const float ClearLeashTiles = 2f;

        public static DungeonDirector Instance { get; private set; }

        public sealed class Site
        {
            public DungeonDef Def;
            public int Index;
            public Vector2 Entrance;
            public GameObject View;
        }

        public enum DoorKind { Down, Up, Exit }

        public sealed class Portal
        {
            public Vector2 Position;
            public DoorKind Kind;
            public GameObject View;
        }

        public sealed class Key
        {
            public int LockId;
            public Vector2 Position;
            public bool Taken;
            public GameObject View;
        }

        public sealed class Barrier
        {
            public int Edge;
            public int LockId = -1;   // a locked door; -1 = the soft gate
            public Vector2 Position;
            public HashSet<Vec2Int> Tiles = new();
            public bool Open;
            public GameObject View;
        }

        public sealed class Floor
        {
            public Site Site;
            public int Index;
            public DungeonFloor Layout;
            public DungeonTileMap Tiles;
            public Vector2 Origin;
            public readonly List<Portal> Portals = new();
            public readonly List<Key> Keys = new();
            public readonly List<Barrier> Barriers = new();
            public GameObject Root;

            public Vector2 TileToWorld(Vec2Int t) => Origin + new Vector2(t.X + 0.5f, t.Y + 0.5f);
            public bool Contains(Vector2 p) => p.x >= Origin.x && p.y >= Origin.y && p.x < Origin.x + Tiles.Size && p.y < Origin.y + Tiles.Size;
        }

        readonly List<Site> _sites = new();
        readonly Dictionary<(int Site, int Floor), Floor> _floors = new();
        readonly Dictionary<PlayerInteraction, (Barrier Gate, Floor Floor, float StartedAt, float Seconds, Vector2 From)> _clearing = new();
        bool _placed;

        public IReadOnlyList<Site> Sites => _sites;
        public IEnumerable<Floor> Floors => _floors.Values;

        void Awake()
        {
            Instance = this;
            IslandWorld.ExtraWalkable = Walkable;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (IslandWorld.ExtraWalkable == (System.Func<Vector2, bool?>)Walkable) IslandWorld.ExtraWalkable = null;
        }

        void Update()
        {
            var world = IslandWorld.Instance;
            if (!_placed && world != null && world.Island != null)
            {
                PlaceEntrances(world);
                _placed = true;
            }
            TickClearing();
        }

        // ------------------------------------------------------------------ entrances

        void PlaceEntrances(IslandWorld world)
        {
            var defs = DefRegistry.All<DungeonDef>().OrderBy(d => d.Id.Value).ToList();
            var placed = new List<Vec2Int>();
            for (var i = 0; i < defs.Count; i++)
            {
                var def = defs[i];
                var tile = PickEntranceTile(world, def, placed, i);
                if (tile == null) continue;
                placed.Add(tile.Value);
                var site = new Site { Def = def, Index = i, Entrance = IslandWorld.TileToWorld(tile.Value) };
                site.View = Prop($"{def.Id.Name}_entrance", def.Entrance?.Shape ?? "ruin_gate", def.Entrance?.Color, site.Entrance, def.Entrance?.Size ?? 2.6f, transform);
                _sites.Add(site);
            }
        }

        /// <summary>A walkable tile in the def's biome, as far as possible from the entrances already placed (spread over
        /// the island like the landmarks). Seed-deterministic through the island's hash.</summary>
        static Vec2Int? PickEntranceTile(IslandWorld world, DungeonDef def, List<Vec2Int> placed, int salt)
        {
            const int step = 8;
            var biomeName = def.Entrance?.Biome.IsValid == true ? def.Entrance.Biome.Name : null;
            Vec2Int? best = null;
            var bestScore = double.MinValue;
            for (var x = step; x < IslandGenerator.Size - step; x += step)
            for (var y = step; y < IslandGenerator.Size - step; y += step)
            {
                var tile = new Vec2Int(x, y);
                if (!world.IsWalkable(IslandWorld.TileToWorld(tile))) continue;
                if (biomeName != null && world.Island.BiomeAt(x, y).ToString().ToLowerInvariant() != biomeName) continue;
                var score = placed.Count == 0 ? 0.0 : placed.Min(p => Mathf.Sqrt((p.X - x) * (p.X - x) + (p.Y - y) * (p.Y - y)));
                score += Hash(world.Seed ^ (salt * 977), x, y) % 1000 / 1000.0;
                if (score <= bestScore) continue;
                bestScore = score;
                best = tile;
            }
            return best;
        }

        // ------------------------------------------------------------------ floors

        /// <summary>The floor, built on first use.</summary>
        public Floor EnsureFloor(Site site, int index)
        {
            if (_floors.TryGetValue((site.Index, index), out var floor)) return floor;
            var seed = (IslandWorld.Instance != null ? IslandWorld.Instance.Seed : 1) ^ (site.Index * 7919);
            var layout = DungeonGenerator.Generate(seed, index, site.Def.Floors, site.Def.Danger);
            var templates = DefRegistry.All<RoomTemplateDef>()
                .Where(t => t.Dungeons == null || t.Dungeons.Contains(site.Def.Id))
                .Select(t => new RoomTemplate { Id = t.Id.Value, Tags = t.Tags, Rows = t.Rows }).ToList();
            var tiles = DungeonTiles.Build(layout, templates, seed + index);
            floor = new Floor
            {
                Site = site,
                Index = index,
                Layout = layout,
                Tiles = tiles,
                Origin = new Vector2(RegionOrigin + site.Index * FloorSpacing, RegionOrigin + index * FloorSpacing),
            };
            floor.Root = new GameObject($"{site.Def.Id.Name}_floor{index}");
            floor.Root.transform.SetParent(transform, false);
            _floors[(site.Index, index)] = floor;
            BuildView(floor);
            PlaceDoors(floor);
            SpawnCreatures(floor);
            return floor;
        }

        void BuildView(Floor floor)
        {
            var size = floor.Tiles.Size;
            var px = size * PixelsPerTile;
            var floorColour = ShapeLibrary.ParseColour(floor.Site.Def.FloorColor, new Color(0.45f, 0.42f, 0.38f));
            var wallColour = ShapeLibrary.ParseColour(floor.Site.Def.WallColor, new Color(0.18f, 0.16f, 0.14f));
            var ink = new Color(0.086f, 0.075f, 0.06f);
            var pixels = new Color32[px * px];
            for (var y = 0; y < px; y++)
            for (var x = 0; x < px; x++)
            {
                int tx = x / PixelsPerTile, ty = y / PixelsPerTile;
                int lx = x % PixelsPerTile, ly = y % PixelsPerTile;
                Color c;
                if (!floor.Tiles.IsFloor(tx, ty))
                {
                    // Wall: dark, with a thick ink edge where it meets floor — the cartoon line work.
                    var edge = (lx < 2 && floor.Tiles.IsFloor(tx - 1, ty)) || (lx >= PixelsPerTile - 2 && floor.Tiles.IsFloor(tx + 1, ty))
                               || (ly < 2 && floor.Tiles.IsFloor(tx, ty - 1)) || (ly >= PixelsPerTile - 2 && floor.Tiles.IsFloor(tx, ty + 1));
                    c = edge ? ink : wallColour;
                }
                else
                {
                    // Floor: flagstones — a faint seam every 2 tiles and a little per-tile variation.
                    var seam = (tx % 2 == 0 && lx == 0) || (ty % 2 == 0 && ly == 0);
                    var shade = (Hash(floor.Site.Index + floor.Index * 31, tx, ty) % 100) / 100f * 0.08f - 0.04f;
                    c = Color.Lerp(floorColour, Color.black, seam ? 0.18f : 0f) + new Color(shade, shade, shade, 0f);
                }
                c.a = 1f;
                pixels[y * px + x] = c;
            }
            var texture = new Texture2D(px, px, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(pixels);
            texture.Apply();
            var go = new GameObject("Tiles");
            go.transform.SetParent(floor.Root.transform, false);
            go.transform.position = floor.Origin;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = Sprite.Create(texture, new Rect(0, 0, px, px), Vector2.zero, PixelsPerTile);
            renderer.sortingOrder = -9;

            // Solid rock past the floor's edge, so the camera never shows the sea's clear colour underground.
            var rock = new Texture2D(1, 1) { filterMode = FilterMode.Point };
            rock.SetPixel(0, 0, Color.Lerp(wallColour, Color.black, 0.35f));
            rock.Apply();
            var backdrop = new GameObject("Backdrop");
            backdrop.transform.SetParent(floor.Root.transform, false);
            backdrop.transform.position = floor.Origin + Vector2.one * (size / 2f);
            backdrop.transform.localScale = Vector3.one * FloorSpacing;
            var backdropRenderer = backdrop.AddComponent<SpriteRenderer>();
            backdropRenderer.sprite = Sprite.Create(rock, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            backdropRenderer.sortingOrder = -10;
        }

        void PlaceDoors(Floor floor)
        {
            var layout = floor.Layout;
            var site = floor.Site;
            var entrance = floor.TileToWorld(floor.Tiles.Features[layout.Entrance]);
            AddPortal(floor, entrance, floor.Index == 0 ? DoorKind.Exit : DoorKind.Up);

            var exitRoom = layout.Rooms[layout.Exit];
            var exitAt = floor.TileToWorld(floor.Tiles.Features[layout.Exit]);
            // The boss floor's way out until the boss exists (T-202): a passage back to the surface.
            AddPortal(floor, exitAt, exitRoom.Kind == RoomKind.Stairs ? DoorKind.Down : DoorKind.Exit);

            foreach (var lk in layout.Locks)
            {
                var keyAt = floor.TileToWorld(floor.Tiles.KeySpots[lk.KeyRoom]);
                var key = new Key { LockId = lk.Id, Position = keyAt };
                key.View = Prop("key", "key", "#E2B84A", keyAt, 0.7f, floor.Root.transform);
                floor.Keys.Add(key);
            }

            for (var e = 0; e < layout.Edges.Count; e++)
            {
                var edge = layout.Edges[e];
                if (edge.Gate == GateKind.None) continue;
                var a = layout.Rooms[edge.A].Cell;
                var b = layout.Rooms[edge.B].Cell;
                var barrier = new Barrier { Edge = e, LockId = edge.Gate == GateKind.Lock ? edge.LockId : -1 };
                foreach (var t in DungeonTiles.DoorwayTiles(a, b)) barrier.Tiles.Add(t);
                var centre = DungeonTiles.DoorwayCentre(a, b);
                barrier.Position = floor.Origin + new Vector2(centre.X, centre.Y + 0.5f) + (a.Y == b.Y ? new Vector2(0f, 0f) : new Vector2(0.5f, -0.5f));
                barrier.View = edge.Gate == GateKind.Lock
                    ? Prop("locked_door", "locked_door", "#8A6A48", barrier.Position, 2.4f, floor.Root.transform)
                    : Prop("gate", site.Def.Gate?.Shape ?? "gate_bars", site.Def.Gate?.Color, barrier.Position, 2.6f, floor.Root.transform);
                floor.Barriers.Add(barrier);
            }

            foreach (var (tile, _) in floor.Tiles.Chests) Prop("chest", "chest", "#9A6B42", floor.TileToWorld(tile), 0.9f, floor.Root.transform);
            foreach (var room in layout.Rooms.Where(r => r.Kind == RoomKind.Rest))
                Prop("campfire", "campfire", "#FFFFFF", floor.TileToWorld(floor.Tiles.Campfires[room.Index]), 0.9f, floor.Root.transform);
        }

        void AddPortal(Floor floor, Vector2 at, DoorKind kind)
        {
            var shape = kind == DoorKind.Down ? "stairs_down" : "stairs_up";
            var portal = new Portal { Position = at, Kind = kind, View = Prop($"portal_{kind}", shape, floor.Site.Def.WallColor, at, 1.6f, floor.Root.transform) };
            floor.Portals.Add(portal);
        }

        void SpawnCreatures(Floor floor)
        {
            var director = CreatureDirector.Instance;
            var ids = floor.Site.Def.Creatures;
            if (director == null || ids == null || ids.Length == 0) return;
            var i = 0;
            foreach (var (tile, room) in floor.Tiles.Spawns)
            {
                var kind = floor.Layout.Rooms[room].Kind;
                if (kind is RoomKind.Entrance or RoomKind.Rest or RoomKind.Vault or RoomKind.Boss) continue; // safe rooms; the boss is T-202
                var id = ids[(i++ + floor.Index) % ids.Length];
                if (!DefRegistry.TryGet<CreatureDef>(id, out var def)) continue;
                director.SpawnAt(def, floor.TileToWorld(tile), inDungeon: true);
            }
        }

        static uint Hash(int seed, int x, int y)
        {
            unchecked
            {
                var h = (uint)seed * 0x9E3779B1u ^ (uint)x * 0x85EBCA77u ^ (uint)y * 0xC2B2AE3Du;
                h ^= h >> 15;
                h *= 0x2C1B3C6Du;
                h ^= h >> 12;
                return h;
            }
        }

        static GameObject Prop(string name, string shape, string colour, Vector2 foot, float size, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = foot;
            go.transform.localScale = Vector3.one * size;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = ShapeLibrary.StandingSprite(shape, ShapeLibrary.ParseColour(colour, Color.gray));
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;
            renderer.sortingOrder = 2;
            return go;
        }

        // ------------------------------------------------------------------ walkability

        /// <summary>Null outside the underground layer; else floor tiles minus closed doors and uncleared gates.</summary>
        public bool? Walkable(Vector2 p)
        {
            if (p.x < RegionOrigin - 1f || p.y < RegionOrigin - 1f) return null;
            foreach (var floor in _floors.Values)
            {
                if (!floor.Contains(p)) continue;
                var tile = new Vec2Int(Mathf.FloorToInt(p.x - floor.Origin.x), Mathf.FloorToInt(p.y - floor.Origin.y));
                if (!floor.Tiles.IsFloor(tile.X, tile.Y)) return false;
                foreach (var barrier in floor.Barriers)
                    if (!barrier.Open && barrier.Tiles.Contains(tile)) return false;
                return true;
            }
            return p.x >= RegionOrigin ? false : null;
        }

        public Floor FloorAt(Vector2 p) => _floors.Values.FirstOrDefault(f => f.Contains(p));

        public bool IsUnderground(Vector2 p) => FloorAt(p) != null;

        // ------------------------------------------------------------------ interaction

        /// <summary>What E would do here, for the HUD: world point, key cap, label. Null when nothing dungeon-related is in reach.</summary>
        public (Vector2 At, string Text)? Prompt(PlayerInteraction player)
        {
            Vector2 at = player.transform.position;
            if (_clearing.TryGetValue(player, out var clearing))
                return (clearing.Gate.Position + Vector2.up * 1.6f, $"{GateName(clearing.Floor)} {Mathf.Clamp01((Time.time - clearing.StartedAt) / clearing.Seconds) * 100f:0}%");
            var target = Nearest(at);
            return target switch
            {
                Site site => (site.Entrance + Vector2.up * 2.4f, string.Format(Lang("@ui.dungeon_enter"), Lang(site.Def.Name))),
                (Floor f, Portal portal) => (portal.Position + Vector2.up * 1.4f, Lang(portal.Kind switch { DoorKind.Down => "@ui.dungeon_down", DoorKind.Up => "@ui.dungeon_up", _ => "@ui.dungeon_exit" })),
                (Floor f, Key key) => (key.Position + Vector2.up * 0.8f, Lang("@ui.dungeon_key")),
                (Floor f, Barrier barrier) when barrier.LockId >= 0 => (barrier.Position + Vector2.up * 2.2f, Lang("@ui.dungeon_locked")),
                (Floor f, Barrier barrier) => (barrier.Position + Vector2.up * 2.2f, string.Format(Lang("@ui.dungeon_clear"), GateName(f))),
                _ => null,
            };
        }

        /// <summary>Uses whatever dungeon thing is in reach. True when something handled the press.</summary>
        public bool TryInteract(PlayerInteraction player)
        {
            if (_clearing.ContainsKey(player)) return true;
            Vector2 at = player.transform.position;
            switch (Nearest(at))
            {
                case Site site:
                    Arrive(player, EnsureFloor(site, 0), atExit: false);
                    return true;
                case (Floor floor, Portal portal):
                    UsePortal(player, floor, portal);
                    return true;
                case (Floor floor, Key key):
                    key.Taken = true;
                    if (key.View != null) key.View.SetActive(false);
                    foreach (var barrier in floor.Barriers.Where(b => b.LockId == key.LockId))
                    {
                        barrier.Open = true;
                        if (barrier.View != null) barrier.View.SetActive(false);
                    }
                    GameFeed.RaiseNotice("@ui.dungeon_unlocked");
                    return true;
                case (Floor floor, Barrier barrier) when barrier.LockId >= 0:
                    GameFeed.RaiseNotice("@ui.dungeon_need_key");
                    return true;
                case (Floor floor, Barrier barrier):
                    StartClearing(player, floor, barrier);
                    return true;
            }
            return false;
        }

        object Nearest(Vector2 at)
        {
            object best = null;
            var bestDistance = PlayerInteraction.ReachTiles + 0.5f;
            void Consider(object thing, Vector2 position, float extra = 0f)
            {
                var d = Vector2.Distance(at, position) - extra;
                if (d >= bestDistance) return;
                bestDistance = d;
                best = thing;
            }
            if (!IsUnderground(at))
            {
                foreach (var site in _sites) Consider(site, site.Entrance, 0.8f);
                return best;
            }
            var floor = FloorAt(at);
            foreach (var portal in floor.Portals) Consider((floor, portal), portal.Position);
            foreach (var key in floor.Keys) if (!key.Taken) Consider((floor, key), key.Position);
            foreach (var barrier in floor.Barriers) if (!barrier.Open) Consider((floor, barrier), barrier.Position, 1.2f);
            return best;
        }

        void UsePortal(PlayerInteraction player, Floor floor, Portal portal)
        {
            switch (portal.Kind)
            {
                case DoorKind.Down:
                    Arrive(player, EnsureFloor(floor.Site, floor.Index + 1), atExit: false);
                    break;
                case DoorKind.Up:
                    Arrive(player, EnsureFloor(floor.Site, floor.Index - 1), atExit: true);
                    break;
                default:
                    Teleport(player, floor.Site.Entrance + Vector2.down * ArrivalOffsetTiles);
                    break;
            }
        }

        /// <summary>Lands next to a floor's entrance stairs (coming down) or its exit stairs (coming back up).</summary>
        void Arrive(PlayerInteraction player, Floor floor, bool atExit)
        {
            var room = atExit ? floor.Layout.Exit : floor.Layout.Entrance;
            var feature = floor.TileToWorld(floor.Tiles.Features[room]);
            var centre = floor.TileToWorld(DungeonTiles.RoomCentre(floor.Layout.Rooms[room].Cell));
            var away = (centre - feature).sqrMagnitude > 0.5f ? (centre - feature).normalized : Vector2.down;
            var target = feature + away * ArrivalOffsetTiles;
            if (Walkable(target) != true) target = feature + Vector2.down;
            Teleport(player, target);
        }

        static void Teleport(PlayerInteraction player, Vector2 to) =>
            player.transform.position = new Vector3(to.x, to.y, player.transform.position.z);

        // ------------------------------------------------------------------ soft gates

        int GateLevel(Floor floor) => floor.Site.Def.Danger switch { 1 => 10, 2 => 20, _ => 30 };

        void StartClearing(PlayerInteraction player, Floor floor, Barrier gate)
        {
            var skill = floor.Site.Def.Gate?.Skill ?? default;
            var specialist = skill.IsValid && player.LevelOf(skill) >= GateLevel(floor);
            var seconds = specialist ? SpecialistGateSeconds : AnyoneGateSeconds;
            _clearing[player] = (gate, floor, Time.time, seconds, player.transform.position);
            if (!specialist) WakeNear(gate.Position);
            GameFeed.RaiseNotice(specialist ? "@ui.dungeon_clear_fast" : "@ui.dungeon_clear_slow");
        }

        /// <summary>The slow path is loud: creatures nearby come running (prey flees).</summary>
        static void WakeNear(Vector2 at)
        {
            var director = CreatureDirector.Instance;
            if (director == null) return;
            foreach (var creature in director.Creatures)
            {
                if (!creature.InDungeon || Vector2.Distance(creature.Position, at) > NoiseWakeTiles) continue;
                var prey = creature.Def.Ai.Value == CreatureBrain.Skittish || creature.Def.Ai.Value == CreatureBrain.AlertThenFlee;
                creature.State = prey ? CreatureState.Flee : CreatureState.Engage;
                creature.LastHitAt = Time.time;
            }
        }

        void TickClearing()
        {
            if (_clearing.Count == 0) return;
            foreach (var player in _clearing.Keys.ToList())
            {
                var (gate, floor, started, seconds, from) = _clearing[player];
                if (player == null || Vector2.Distance(player.transform.position, from) > ClearLeashTiles)
                {
                    _clearing.Remove(player);
                    if (player != null) GameFeed.RaiseNotice("@ui.dungeon_clear_cancelled");
                    continue;
                }
                if (Time.time - started < seconds) continue;
                gate.Open = true;
                if (gate.View != null) gate.View.SetActive(false);
                _clearing.Remove(player);
                GameFeed.RaiseNotice("@ui.dungeon_cleared");
            }
        }

        public bool IsClearing(PlayerInteraction player) => _clearing.ContainsKey(player);

        string GateName(Floor floor) => Lang(floor.Site.Def.Gate?.Name ?? "@gate.rubble");

        /// <summary>Gameplay can't see the UI's language table; the HUD sets this to its lookup.</summary>
        public static System.Func<string, string> Localize { get; set; }

        static string Lang(string key) => Localize != null ? Localize(key) : key;
    }
}
