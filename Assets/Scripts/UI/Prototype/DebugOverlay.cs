using System;
using System.Collections.Generic;
using System.Linq;
using FishNet;
using Isle.Core.Util;
using Isle.Data;
using Isle.Gameplay.Character;
using Isle.Gameplay.Combat;
using Isle.Gameplay.Crafting;
using Isle.Gameplay.Dungeons;
using Isle.Gameplay.Hunting;
using Isle.Gameplay.Inventory;
using Isle.Modding.Defs;
using Isle.Networking;
using Isle.World.Chunks;
using Isle.World.Generation;
using Isle.World.Island;
using Isle.World.Objects;
using Isle.World.Time;
using Isle.World.Weather;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Isle.UI.Prototype
{
    /// <summary>Rolling frame-time window for the debug screen: average and worst over the last N frames. Pure.</summary>
    public sealed class FrameStats
    {
        readonly float[] _frames;
        int _count, _next;

        public FrameStats(int window) => _frames = new float[Math.Max(1, window)];

        public void Add(float seconds)
        {
            _frames[_next] = seconds;
            _next = (_next + 1) % _frames.Length;
            if (_count < _frames.Length) _count++;
        }

        float Sum { get { var s = 0f; for (var i = 0; i < _count; i++) s += _frames[i]; return s; } }
        float Worst { get { var w = 0f; for (var i = 0; i < _count; i++) w = Math.Max(w, _frames[i]); return w; } }

        public float AverageFps => _count == 0 || Sum <= 0f ? 0f : _count / Sum;
        public float AverageMs => _count == 0 ? 0f : Sum / _count * 1000f;
        public float WorstMs => Worst * 1000f;

        /// <summary>The frame rate the slowest frame in the window ran at.</summary>
        public float LowFps => Worst <= 0f ? 0f : 1f / Worst;
    }

    /// <summary>
    /// T-303 developer debug screen. F3 or F12 shows every live number a developer needs on top of the game: build and
    /// frame time, network, world clock and weather, position/tile/chunk/biome, the dungeon underfoot, vitals, buffs,
    /// skills, what's held and worn, nearby creatures and nodes, what's under the mouse. Ctrl+F3/F12 adds world gizmos
    /// (reach, assist and vision rings, creature hitboxes, node labels, chunk borders); Shift+F3/F12 copies the text to
    /// the clipboard for a bug report. Developer-facing, so its labels are English and not in the language files.
    /// </summary>
    public sealed class DebugOverlay : MonoBehaviour
    {
        const float LineHeight = 15f;
        const float NearbyCreatureTiles = 20f;
        const float NearbyNodeTiles = 10f;
        const int MaxListed = 6;

        public static DebugOverlay Instance { get; private set; }

        /// <summary>The text screen is up.</summary>
        public bool Visible { get; set; }

        /// <summary>The world gizmos are drawn.</summary>
        public bool Gizmos { get; set; }

        readonly FrameStats _frames = new(120);
        GUIStyle _style;
        Texture2D _pixel;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            _frames.Add(Time.unscaledDeltaTime);
            var kb = Keyboard.current;
            if (kb == null || !(kb.f3Key.wasPressedThisFrame || kb.f12Key.wasPressedThisFrame)) return;
            var ctrl = kb.ctrlKey.isPressed || kb.leftCommandKey.isPressed || kb.rightCommandKey.isPressed;
            if (kb.shiftKey.isPressed)
            {
                var (left, right) = Lines();
                GUIUtility.systemCopyBuffer = string.Join("\n", left.Concat(new[] { "" }).Concat(right));
                Isle.Gameplay.Feedback.GameFeed.RaiseNotice("@ui.debug_copied");
            }
            else if (ctrl)
            {
                Gizmos = !Gizmos;
                if (Gizmos) Visible = true;
            }
            else
            {
                Visible = !Visible;
                if (!Visible) Gizmos = false;
            }
        }

        /// <summary>World minutes as "day N hh:mm" (day 1 is the first).</summary>
        public static string ClockText(long totalMinutes)
        {
            var day = totalMinutes / WorldClock.MinutesPerDay + 1;
            var minute = totalMinutes % WorldClock.MinutesPerDay;
            return $"day {day} {minute / 60:00}:{minute % 60:00}";
        }

        // ------------------------------------------------------------------ text

        /// <summary>Both columns as text. Each section is guarded, so one broken read shows as an error line instead of
        /// taking the screen down.</summary>
        public (List<string> Left, List<string> Right) Lines()
        {
            var left = new List<string>();
            var right = new List<string>();
            var player = PlayerInteraction.Local;
            Section(left, "build", Build);
            Section(left, "world", World);
            left.Add("");
            Section(left, "position", l => Position(l, player));
            Section(left, "mouse", Mouse);
            left.Add("");
            Section(left, "vitals", l => Vitals(l, player));
            Section(left, "skills", l => Skills(l, player));

            Section(right, "defs", Defs);
            right.Add("");
            Section(right, "gear", r => Gear(r, player));
            Section(right, "activity", r => Activity(r, player));
            right.Add("");
            Section(right, "creatures", r => Creatures(r, player));
            Section(right, "nodes", r => Nodes(r, player));
            Section(right, "dungeons", Dungeons);
            return (left, right);
        }

        static void Section(List<string> lines, string name, Action<List<string>> fill)
        {
            try { fill(lines); }
            catch (Exception e) { lines.Add($"[{name}: {e.GetType().Name}: {e.Message}]"); }
        }

        void Build(List<string> l)
        {
            l.Add($"ISLE {Application.version} · Unity {Application.unityVersion} · {(Application.isEditor ? "editor" : Debug.isDebugBuild ? "dev build" : "release")}");
            l.Add($"{_frames.AverageFps:0} fps ({_frames.AverageMs:0.0} ms avg · worst {_frames.WorstMs:0.0} ms = {_frames.LowFps:0} fps) · vSync {QualitySettings.vSyncCount} · cap {Application.targetFrameRate}");
            l.Add($"Mem: managed {GC.GetTotalMemory(false) / 1048576f:0} MB · Unity {UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576f:0} / {UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / 1048576f:0} MB · GC #{GC.CollectionCount(0)}");
            var role = InstanceFinder.IsHostStarted ? "host" : InstanceFinder.IsServerStarted ? "server" : InstanceFinder.IsClientStarted ? "client" : "offline";
            var clients = InstanceFinder.ServerManager != null && InstanceFinder.IsServerStarted ? InstanceFinder.ServerManager.Clients.Count : 0;
            var tm = InstanceFinder.TimeManager;
            l.Add($"Net: {role} · clients {clients} · players {PlayerInteraction.All.Count} · tick {(tm != null ? tm.Tick : 0)} @ {(tm != null ? tm.TickRate : 0)} Hz · RTT {(tm != null ? tm.RoundTripTime : 0)} ms");
        }

        static void World(List<string> l)
        {
            var world = IslandWorld.Instance;
            var clock = WorldTime.Instance != null ? WorldTime.Instance.Clock : null;
            l.Add($"Island: seed {(world != null ? world.Seed : 0)} · save slot {SaveGame.Slot} · difficulty {Isle.Gameplay.World.Difficulty.IdOf(Isle.Gameplay.World.Difficulty.Current)}"
                  + (clock != null ? $" · {ClockText(clock.TotalMinutes)} ({clock.Phase}) · t={clock.TotalMinutes} min" : ""));
            if (WeatherController.Instance is { } weather)
                l.Add($"Weather: {weather.State} · {weather.Season} · event {weather.TempEvent} · ambient {weather.AmbientTemp:0.0}°");
        }

        static void Position(List<string> l, PlayerInteraction player)
        {
            if (player == null) { l.Add("No local player"); return; }
            Vector2 p = player.transform.position;
            var underground = DungeonDirector.Instance != null ? DungeonDirector.Instance.FloorAt(p) : null;
            l.Add($"XY: {p.x:0.000} / {p.y:0.000}");
            if (underground == null)
            {
                var tile = IslandWorld.WorldToTile(p);
                var chunk = Chunk.CoordFromTilePosition(tile);
                l.Add($"Tile: {tile.X}, {tile.Y} · chunk {chunk.X}, {chunk.Y} (in-chunk {tile.X - chunk.X * Chunk.Size}, {tile.Y - chunk.Y * Chunk.Size})");
                if (IslandWorld.Instance is { } world)
                    l.Add($"Biome: {world.Island.BiomeAt(tile.X, tile.Y)} · depth {world.DepthAt(tile):0.00} · walkable {Yes(world.IsWalkable(p))}");
            }
            else
            {
                var tile = underground.WorldToTile(p);
                var site = underground.Site;
                var d = DungeonDirector.Instance;
                l.Add($"Underground: {site.Def.Id} floor {underground.Index + 1}/{Math.Max(1, site.Def.Floors)} · tile {tile.X}, {tile.Y} · rooms {underground.Layout?.Rooms.Count ?? 0}");
                l.Add($"  light ×{d.LightMultAt(p):0.00} · darkness {d.DarknessAt(p):0.00} · stamina regen ×{d.StaminaRegenMultAt(p):0.00} · curses {site.Curses} · sigils {site.SigilsPlaced.Count}"
                      + $" · miasma {Yes(d.IsMiasma(p))} · water {Yes(d.IsWater(p))} · walkable {Yes(d.Walkable(p) ?? false)}");
            }

            var move = player.GetComponent<PlayerMovement>();
            var terrain = PlayerMovement.TerrainSpeed?.Invoke(p) ?? 1f;
            if (move != null)
                l.Add($"Move: dir {move.LastDirection.x:0.00}, {move.LastDirection.y:0.00} · {(move.IsRolling ? "rolling" : move.IsSprinting ? "sprinting" : move.IsMoving ? "walking" : "still")}"
                      + $" · terrain ×{terrain:0.00} · weight ×{move.WeightMultiplier:0.00}{(move.Frozen ? " · FROZEN" : "")}{(move.IsInvulnerable ? " · i-frames" : "")}");
        }

        static void Mouse(List<string> l)
        {
            var camera = Camera.main;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (camera == null || mouse == null) return;
            if (ViewTilt.ScreenToGround(camera, mouse.position.ReadValue()) is not { } at) return;
            var text = $"Mouse: {at.x:0.00} / {at.y:0.00}";
            var player = PlayerInteraction.Local;
            if (player != null)
            {
                var delta = at - (Vector2)player.transform.position;
                text += $" · {delta.magnitude:0.00} tiles · aim {Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg:0}°";
            }
            if (IslandWorld.Instance is { } world && (DungeonDirector.Instance == null || !DungeonDirector.Instance.IsUnderground(at)))
            {
                var tile = IslandWorld.WorldToTile(at);
                text += $" · tile {tile.X}, {tile.Y} {world.Island.BiomeAt(tile.X, tile.Y)} · walkable {Yes(world.IsWalkable(at))}";
            }
            l.Add(text);
        }

        static void Vitals(List<string> l, PlayerInteraction player)
        {
            if (player == null || !player.TryGetComponent<Vitals>(out var v)) return;
            l.Add($"HP {v.Health:0.0} · hunger {v.Hunger:0.0} · thirst {v.Thirst:0.0} · stamina {v.Stamina:0.0}{(v.Exhausted ? " (exhausted)" : "")} · mana {v.Mana:0.0}/{v.ManaMax:0}{(v.EffectsRunning > 0 ? $" · {v.EffectsRunning} over-time" : "")}");
            l.Add($"Body {v.Temperature:0.00}° · ambient {v.AmbientTemp:0.0}° · clothing +{v.ClothingBonus:0.0} · fire {(float.IsInfinity(v.NearestCampfireDistanceTiles) ? "-" : v.NearestCampfireDistanceTiles.ToString("0.0"))} t"
                  + $" · wet {v.WetPenalty:0.00} · hypothermia {v.HypothermiaSeverity:0.00}");
            var dots = string.Join(" ", new[] { DamageTypes.Toxic, DamageTypes.Slash, DamageTypes.Heat }.Select(t => (t, n: v.Status.Stacks(t))).Where(x => x.n > 0).Select(x => $"{x.t}×{x.n}"));
            if (v.Injuries.Wounds.Count > 0)
                l.Add($"Wounds {v.Injuries.Wounds.Count} · max HP {v.MaxHealth:0} · bleed {v.Injuries.BleedPerSecond:0.00}/s · legs ×{v.Injuries.LegMult:0.00} · arms ×{v.Injuries.ArmMult:0.00}");
            l.Add($"Armor {v.WornArmor():0.0} · activity {v.CurrentActivity}{(v.Overloaded ? " · OVERLOADED" : "")}{(dots.Length > 0 ? " · dots " + dots : "")}");
            var now = Isle.Gameplay.Character.Vitals.NowMinutes;
            var buffs = v.Buffs.Entries().Where(b => b.ExpiresAt == 0 || b.ExpiresAt > now)
                .Select(b => b.Id.Value.Replace("isle:", "") + (b.ExpiresAt > 0 ? $" {b.ExpiresAt - now}m" : "")).ToList();
            if (buffs.Count > 0) l.Add($"Buffs: {string.Join(", ", buffs)}");
            if (player.Survivor is { } survivor)
                l.Add($"Survivor: {survivor.Name} · traits {(survivor.Traits.Count == 0 ? "-" : string.Join(", ", survivor.Traits.Select(t => t.Id.Value.Replace("isle:", ""))))}");
        }

        static void Skills(List<string> l, PlayerInteraction player)
        {
            if (player == null) return;
            var parts = player.Skills.Skills.Select(s => $"{s.Id.Value.Replace("isle:", "")} {player.Skills.Level(s.Id)}").ToList();
            for (var i = 0; i < parts.Count; i += 6) l.Add((i == 0 ? "Skills: " : "        ") + string.Join(" · ", parts.Skip(i).Take(6)));
        }

        static void Defs(List<string> r)
        {
            r.Add($"Defs: items {DefRegistry.All<ItemDef>().Count} · recipes {DefRegistry.All<CraftRecipeDef>().Count} · creatures {DefRegistry.All<CreatureDef>().Count}"
                  + $" · objects {DefRegistry.All<WorldObjectDef>().Count} · cook {DefRegistry.All<CookMethodDef>().Count} · enchants {DefRegistry.All<EnchantDef>().Count} · dungeons {DefRegistry.All<DungeonDef>().Count}");
            var creatures = CreatureDirector.Instance;
            r.Add($"Live: creatures {(creatures != null ? creatures.Creatures.Count(c => !c.Dead) : 0)} · carcasses {(creatures != null ? creatures.Carcasses.Count : 0)}"
                  + $" · nodes {(IslandWorld.Instance != null ? IslandWorld.Instance.Nodes.Count : 0)} · structures {WorldObjectRegistry.All.Count} · piles {LootPiles.All.Count}");
        }

        static void Gear(List<string> r, PlayerInteraction player)
        {
            if (player == null || !player.TryGetComponent<InventoryNetwork>(out var inventory)) return;
            foreach (var slot in EquipSlots.All)
            {
                if (inventory.Slots.Get(slot) is not { } item) continue;
                r.Add($"{slot}: {item.Id.Value.Replace("isle:", "")}{WearText(inventory.Slots.WearOf(slot))}");
            }
            var bag = inventory.Containers();
            r.Add($"Carrying: {bag.Sum(c => c.Placements.Count)} stacks in {bag.Count} containers");
        }

        static string WearText(ItemWear wear)
        {
            if (wear == null) return "";
            if (wear.Carcass is { } body) return $" · carcass {body.Def.Id.Value} {body.WeightKg:0.0} kg spoil {body.Spoilage:0.00}";
            var text = $" · {wear.Current}/{wear.Max}";
            if (wear.Quality is { } q) text += $" {q} ×{QualityCalculator.PowerMult(q):0.##}";
            if (wear.Enchants.Count > 0) text += " [" + string.Join(",", wear.Enchants.GroupBy(e => e).Select(g => g.Key.Value.Replace("isle:", "") + (g.Count() > 1 ? "×" + g.Count() : ""))) + "]";
            if (!string.IsNullOrEmpty(wear.Crafter)) text += $" by {wear.Crafter}";
            return text;
        }

        static void Activity(List<string> r, PlayerInteraction player)
        {
            if (player == null) return;
            if (player.Gathering is { } node) r.Add($"Gathering {node.Def.Id.Value} {player.GatherProgress:P0}");
            if (player.Butchering is { } carcass) r.Add($"Butchering {carcass.Def.Id.Value} {player.ButcherProgress:P0}");
            if (player.Hauling is { } hauled) r.Add($"Hauling {hauled.Def.Id.Value} {(player.HaulHelping ? "(helping)" : "")}");
            if (player.Forge is { } forge) r.Add($"Forging heat {forge.Heat:0.00} · strikes {forge.Strikes}/{forge.Required}");
        }

        static void Creatures(List<string> r, PlayerInteraction player)
        {
            if (player == null || CreatureDirector.Instance == null) return;
            Vector2 p = player.transform.position;
            var near = CreatureDirector.Instance.Creatures.Where(c => !c.Dead && Vector2.Distance(c.Position, p) <= NearbyCreatureTiles)
                .OrderBy(c => Vector2.Distance(c.Position, p)).ToList();
            r.Add($"Creatures within {NearbyCreatureTiles:0} tiles: {near.Count}");
            foreach (var c in near.Take(MaxListed))
                r.Add($"  {c.Def.Id.Value.Replace("isle:", "")} {c.State}{(c.Asleep ? " asleep" : "")}{(c.IsWindingUp ? " windup" : "")}{(c.IsStaggered ? " stagger" : "")}"
                      + $" HP {c.Health:0}/{c.MaxHealth:0} · {Vector2.Distance(c.Position, p):0.0} t · {c.Weight:0.0} kg");
        }

        static void Nodes(List<string> r, PlayerInteraction player)
        {
            if (player == null || IslandWorld.Instance == null) return;
            Vector2 p = player.transform.position;
            var near = IslandWorld.Instance.NodesNear(p, NearbyNodeTiles).OrderBy(n => Vector2.Distance(n.Position, p)).Take(MaxListed).ToList();
            if (near.Count == 0) return;
            r.Add("Nodes:");
            foreach (var n in near)
                r.Add($"  {n.Def.Id.Value.Replace("isle:", "")} uses {n.UsesLeft}/{n.Def.Gather?.Uses ?? 0}"
                      + $"{(n.UsesLeft <= 0 ? $" respawn {Mathf.Max(0f, n.RespawnAt - IslandWorld.Instance.Now):0}s" : "")} · {Vector2.Distance(n.Position, p):0.0} t");
        }

        static void Dungeons(List<string> r)
        {
            if (DungeonDirector.Instance is not { } d || d.Sites.Count == 0) return;
            r.Add("");
            r.Add($"Dungeons ({d.Floors.Count()} floors built):");
            foreach (var site in d.Sites)
                r.Add($"  {site.Def.Id.Value.Replace("isle:", "")} @ {site.Entrance.x:0}, {site.Entrance.y:0} · boss {(site.BossDefeated ? "slain" : site.Boss != null ? $"{site.Boss.Health:0}/{site.Boss.MaxHealth:0}" : "-")}"
                      + (site.Def.Seal != null ? $" · sigils {site.SigilsPlaced.Count}" : ""));
        }

        static string Yes(bool value) => value ? "yes" : "no";

        // ------------------------------------------------------------------ drawing

        void OnGUI()
        {
            if (!Visible) return;
            GUI.depth = -100;
            _style ??= new GUIStyle(GUI.skin.label) { fontSize = 12, richText = false, wordWrap = false, normal = { textColor = new Color(0.95f, 0.95f, 0.95f) } };
            _pixel ??= Texture2D.whiteTexture;
            if (Gizmos) DrawGizmos();
            var (left, right) = Lines();
            Column(left, 6f, alignRight: false);
            Column(right, Screen.width - 6f, alignRight: true);
        }

        void Column(List<string> lines, float x, bool alignRight)
        {
            var y = 6f;
            foreach (var line in lines)
            {
                if (line.Length > 0)
                {
                    var size = _style.CalcSize(new GUIContent(line));
                    var rect = new Rect(alignRight ? x - size.x - 4f : x, y, size.x + 4f, LineHeight);
                    GUI.color = new Color(0f, 0f, 0f, 0.55f);
                    GUI.DrawTexture(rect, _pixel);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(rect.x + 2f, rect.y - 1f, size.x + 2f, LineHeight + 2f), line, _style);
                }
                y += LineHeight;
            }
        }

        void DrawGizmos()
        {
            var camera = Camera.main;
            var player = PlayerInteraction.Local;
            if (camera == null || player == null) return;
            Vector2 p = player.transform.position;
            var underground = DungeonDirector.Instance != null && DungeonDirector.Instance.IsUnderground(p);

            // Chunk borders around the player (the surface only — dungeons aren't chunked).
            if (!underground)
            {
                var chunk = Chunk.CoordFromTilePosition(IslandWorld.WorldToTile(p));
                var half = IslandGenerator.Size / 2f;
                var colour = new Color(0.3f, 0.8f, 1f, 0.6f);
                for (var i = -1; i <= 2; i++)
                {
                    float x = (chunk.X + i) * Chunk.Size - half, y = (chunk.Y + i) * Chunk.Size - half;
                    float x0 = (chunk.X - 1) * Chunk.Size - half, x1 = (chunk.X + 2) * Chunk.Size - half;
                    float y0 = (chunk.Y - 1) * Chunk.Size - half, y1 = (chunk.Y + 2) * Chunk.Size - half;
                    WorldLine(camera, new Vector2(x, y0), new Vector2(x, y1), colour);
                    WorldLine(camera, new Vector2(x0, y), new Vector2(x1, y), colour);
                }
            }

            // The player: interaction reach and the adjacent-assist radius.
            Ring(camera, p, PlayerInteraction.ReachTiles, new Color(0.4f, 1f, 0.4f, 0.9f));
            Ring(camera, p, AdjacentAssist.RadiusTiles, new Color(0.4f, 0.9f, 1f, 0.6f));

            // Creatures: body (hitbox), attack range, vision; state and HP above.
            if (CreatureDirector.Instance != null)
                foreach (var c in CreatureDirector.Instance.Creatures)
                {
                    if (c.Dead || Vector2.Distance(c.Position, p) > NearbyCreatureTiles) continue;
                    var stateColour = c.State switch
                    {
                        CreatureState.Engage => new Color(1f, 0.3f, 0.25f),
                        CreatureState.Flee => new Color(0.4f, 0.7f, 1f),
                        CreatureState.Alert => new Color(1f, 0.85f, 0.3f),
                        _ => new Color(0.85f, 0.85f, 0.85f),
                    };
                    Ring(camera, c.Position, Mathf.Max(0.1f, c.Radius), stateColour);
                    if (c.Def.Combat is { } combat)
                    {
                        if (combat.AttackRangeTiles > 0f) Ring(camera, c.Position, combat.AttackRangeTiles, new Color(1f, 0.35f, 0.3f, 0.45f));
                        if (combat.VisionTiles > 0f) Ring(camera, c.Position, combat.VisionTiles, new Color(1f, 0.9f, 0.4f, 0.25f));
                    }
                    WorldLabel(camera, c.Position + Vector2.up * (c.Radius + 0.6f), $"{c.Def.Id.Value.Replace("isle:", "")} {c.State} {c.Health:0}/{c.MaxHealth:0}", stateColour);
                }

            // Nodes: a cross and uses left.
            if (!underground && IslandWorld.Instance != null)
                foreach (var n in IslandWorld.Instance.NodesNear(p, NearbyNodeTiles))
                {
                    var colour = n.UsesLeft > 0 ? new Color(0.7f, 1f, 0.6f, 0.9f) : new Color(0.7f, 0.7f, 0.7f, 0.7f);
                    WorldLine(camera, n.Position - new Vector2(0.2f, 0.2f), n.Position + new Vector2(0.2f, 0.2f), colour);
                    WorldLine(camera, n.Position - new Vector2(0.2f, -0.2f), n.Position + new Vector2(0.2f, -0.2f), colour);
                    WorldLabel(camera, n.Position + Vector2.down * 0.35f, $"{n.Def.Id.Value.Replace("isle:", "")} {n.UsesLeft}/{n.Def.Gather?.Uses ?? 0}", colour);
                }

            // Structures: their id.
            foreach (var s in WorldObjectRegistry.All)
            {
                if (s == null || s.Def == null || Vector2.Distance(s.transform.position, p) > NearbyNodeTiles) continue;
                WorldLabel(camera, (Vector2)s.transform.position + Vector2.down * 0.4f, $"{s.Def.Id.Value.Replace("isle:", "")}{(s.IsActive ? " (on)" : "")}", new Color(1f, 0.8f, 0.5f));
            }
        }

        static bool ToGui(Camera camera, Vector2 world, out Vector2 gui)
        {
            var screen = camera.WorldToScreenPoint(new Vector3(world.x, world.y, 0f));
            gui = new Vector2(screen.x, Screen.height - screen.y);
            return screen.z > 0f;
        }

        void WorldLine(Camera camera, Vector2 a, Vector2 b, Color colour)
        {
            if (!ToGui(camera, a, out var ga) || !ToGui(camera, b, out var gb)) return;
            var delta = gb - ga;
            var length = delta.magnitude;
            if (length < 0.5f) return;
            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, ga);
            GUI.color = colour;
            GUI.DrawTexture(new Rect(ga.x, ga.y - 0.75f, length, 1.5f), _pixel);
            GUI.matrix = matrix;
            GUI.color = Color.white;
        }

        void Ring(Camera camera, Vector2 centre, float radius, Color colour)
        {
            const int segments = 32;
            var previous = centre + new Vector2(radius, 0f);
            for (var i = 1; i <= segments; i++)
            {
                var angle = i * Mathf.PI * 2f / segments;
                var next = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                WorldLine(camera, previous, next, colour);
                previous = next;
            }
        }

        void WorldLabel(Camera camera, Vector2 world, string text, Color colour)
        {
            if (!ToGui(camera, world, out var gui)) return;
            var size = _style.CalcSize(new GUIContent(text));
            var rect = new Rect(gui.x - size.x / 2f, gui.y - size.y / 2f, size.x, size.y);
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(rect, _pixel);
            GUI.color = colour;
            GUI.Label(rect, text, _style);
            GUI.color = Color.white;
        }
    }
}
