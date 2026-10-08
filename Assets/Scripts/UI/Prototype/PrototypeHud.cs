using System.Collections.Generic;
using System.Linq;
using Isle.Data;
using Isle.Gameplay.Building;
using Isle.Gameplay.Character;
using Isle.Gameplay.Cooking;
using Isle.Gameplay.Crafting;
using Isle.Gameplay.Feedback;
using Isle.Gameplay.Inventory;
using Isle.Gameplay.Skills;
using Isle.Modding.Defs;
using Isle.World.Island;
using Isle.World.Objects;
using Isle.World.Time;
using Isle.World.Weather;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Isle.UI.Prototype
{
    /// <summary>
    /// Immediate-mode HUD for the solo prototype: gauges, clock and weather, interaction prompt, equipment, bag
    /// (use / equip / place), craft window (C), container panel, and the placement preview. Reads the local
    /// player's own components and sends requests through <see cref="PlayerInteraction"/>; it never changes state
    /// itself (Absolute Rule 2). IMGUI on purpose — it needs no prefab or canvas wiring.
    /// </summary>
    public sealed partial class PrototypeHud : MonoBehaviour
    {
        // Layout constants — pixels, not content.
        const float Margin = 12f;
        const float GaugeWidth = 200f;
        const float GaugeHeight = 16f;
        const float RowHeight = 24f;
        const float PanelWidth = 250f;
        const float ButtonWidth = 64f;

        const float TemperatureMin = 20f;
        const float TemperatureMax = 45f;

        /// <summary>True while a structure is following the mouse — Esc cancels it rather than opening the menu.</summary>
        public static bool IsPlacing { get; private set; }

        public static PrototypeHud Instance { get; private set; }

        ItemDef _hover;
        GridInventory _hoverContainer;
        bool _skillsOpen;
        string _skillsTab = "production";
        bool _craftOpen;
        bool _cookOpen;
        string _cookMethod;
        readonly List<string> _cookPicks = new();
        bool _helpOpen = true;
        StorageBox _openBox;
        string _placing;
        DeathHandler _death;
        PlayerInteraction _player;
        Vitals _vitals;
        InventoryNetwork _inventory;
        GUIStyle _label;
        GUIStyle _title;
        GUIStyle _helpStyle;
        readonly List<Rect> _panels = new();

        void Awake() => Instance = this;

        void OnEnable() => GameFeed.StorageOpened += OnStorageOpened;
        void OnDisable() => GameFeed.StorageOpened -= OnStorageOpened;
        void OnStorageOpened(object box) => _openBox = box as StorageBox;

        void Update()
        {
            if (_player == null || !_player.IsOwner) FindLocalPlayer();

            var kb = Keyboard.current;
            if (kb != null && kb.cKey.wasPressedThisFrame) _craftOpen = !_craftOpen;
            if (kb != null && kb.kKey.wasPressedThisFrame) _cookOpen = !_cookOpen;
            if (kb != null && kb.pKey.wasPressedThisFrame) _skillsOpen = !_skillsOpen;
            if (kb != null && kb.hKey.wasPressedThisFrame) _helpOpen = !_helpOpen;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) _placing = null;

            // Say why a roll didn't happen instead of silently ignoring Space.
            if (kb != null && kb.spaceKey.wasPressedThisFrame && _vitals != null && _player != null
                && _player.TryGetComponent<Isle.Networking.PlayerMovement>(out var movement) && !movement.RollAllowed && !movement.IsRolling)
                GameFeed.RaiseNotice(_vitals.Overloaded ? "@ui.roll_overloaded" : "@ui.roll_tired");

            if (_openBox != null && (_player == null || Vector2.Distance(_player.transform.position, _openBox.transform.position) > PlayerInteraction.ReachTiles))
                _openBox = null;

            UpdatePlacing();
            IsPlacing = _placing != null;
        }

        void UpdatePlacing()
        {
            if (_placing == null || _player == null) return;
            var mouse = Mouse.current;
            if (mouse == null) return;
            if (mouse.rightButton.wasPressedThisFrame)
            {
                _placing = null;
                return;
            }
            if (!mouse.leftButton.wasPressedThisFrame || OverPanel()) return;

            var at = MouseWorld();
            if (at == null) return;
            _player.RequestPlace(_placing, at.Value);
            _placing = null;
        }

        void FindLocalPlayer()
        {
            _player = null;
            foreach (var candidate in PlayerInteraction.All)
            {
                if (!candidate.IsOwner) continue;
                _player = candidate;
                candidate.TryGetComponent(out _vitals);
                candidate.TryGetComponent(out _inventory);
                candidate.TryGetComponent(out _death);
                return;
            }
        }

        void OnGUI()
        {
            if (_player == null || _vitals == null || _inventory == null) return;
            EnsureStyles();
            if (Event.current.type == EventType.Layout) _panels.Clear();

            // The full-screen inventory covers the world; only windows and tooltips draw over it.
            var inventoryOpen = Isle.UI.Inventory.InventoryScreen.Instance is { Open: true };
            if (!inventoryOpen)
            {
                DrawGauges();
                DrawClock();
                DrawPrompt();
            }
            _hover = null;
            if (_craftOpen) DrawCraftWindow();
            if (_cookOpen) DrawCookWindow();
            if (_skillsOpen) DrawSkills();
            DrawBuffs();
            if (_openBox != null) DrawBox();
            if (_placing != null) DrawPlacementGhost();
            if (_player.DrawStartedAt >= 0f) DrawChargeBar();
            if (_player.Fight != null) DrawFight();
            if (_helpOpen) DrawHelp();
            if (_death != null ? _death.IsDead : _vitals.Health <= 0f) DrawDeath();
            if (_hover != null) DrawTooltip(_hover, _hoverContainer);
            else if (Isle.UI.Inventory.ItemTooltip.Hovered is { } hovered) DrawTooltip(hovered.Item, hovered.Container);

            if (Event.current.type == EventType.Repaint)
                PointerGate.Captured = _placing != null || OverPanel() || Isle.UI.Inventory.InventoryScreen.PointerOverUi;
        }

        bool OverPanel()
        {
            var mouse = Mouse.current;
            if (mouse == null) return false;
            var p = mouse.position.ReadValue();
            var gui = new Vector2(p.x, Screen.height - p.y);
            return _panels.Any(r => r.Contains(gui));
        }

        Rect Panel(Rect rect)
        {
            if (Event.current.type == EventType.Layout) _panels.Add(rect);
            GUI.Box(rect, GUIContent.none, UiTheme.Styles.Window);
            return rect;
        }

        void DrawGauges()
        {
            var y = Margin;
            DrawGauge(ref y, Lang.Get("@ui.health"), _vitals.Health, Color.red);
            DrawGauge(ref y, Lang.Get("@ui.hunger"), _vitals.Hunger, new Color(0.95f, 0.6f, 0.2f));
            DrawGauge(ref y, Lang.Get("@ui.thirst"), _vitals.Thirst, new Color(0.3f, 0.6f, 1f));
            var staminaLabel = Lang.Get("@ui.stamina");
            if (_vitals.Overloaded) staminaLabel += $"  {Lang.Get("@ui.overloaded")}";
            else if (_vitals.Exhausted) staminaLabel += $"  {Lang.Get("@ui.exhausted")}";
            DrawGauge(ref y, staminaLabel, _vitals.Stamina, _vitals.Exhausted || _vitals.Overloaded ? new Color(0.85f, 0.45f, 0.25f) : new Color(0.95f, 0.85f, 0.3f));

            var warmth = Mathf.InverseLerp(TemperatureMin, TemperatureMax, _vitals.Temperature) * VitalsCalculator.GaugeMax;
            var temperature = $"{Lang.Get("@ui.temperature")} {_vitals.Temperature:0.0}°";
            if (_vitals.HypothermiaSeverity > 0.01f) temperature += $"  {Lang.Get("@ui.hypothermia")} {_vitals.HypothermiaSeverity * 100f:0}%";
            DrawGauge(ref y, temperature, warmth, new Color(0.9f, 0.5f, 0.5f));
        }

        void DrawGauge(ref float y, string text, float value, Color colour)
        {
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(Margin, y, GaugeWidth + 2f, GaugeHeight + 2f), Texture2D.whiteTexture);
            GUI.color = colour;
            GUI.DrawTexture(new Rect(Margin + 1f, y + 1f, GaugeWidth * Mathf.Clamp01(value / VitalsCalculator.GaugeMax), GaugeHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(Margin + GaugeWidth + 8f, y - 2f, 260f, RowHeight), text, _label);
            y += RowHeight;
        }

        void DrawClock()
        {
            var clock = WorldTime.Instance != null ? WorldTime.Instance.Clock : null;
            if (clock == null) return;

            var minute = clock.MinuteOfDay;
            var weather = WeatherController.Instance;
            var line = $"{Lang.Get("@ui.day")} {clock.TotalMinutes / WorldClock.MinutesPerDay + 1}  {minute / 60:00}:{minute % 60:00}  {Lang.Get("@ui.phase." + clock.Phase.ToString().ToLowerInvariant())}";
            if (weather != null) line += $"\n{Lang.Get("@ui.season." + weather.Season.ToString().ToLowerInvariant())} · {Lang.Get("@ui.weather." + weather.State.ToString().ToLowerInvariant())} · {weather.AmbientTemp:0}°";

            GUI.Label(new Rect(Screen.width - PanelWidth - Margin, Margin, PanelWidth, 48f), line, _label);
        }

        /// <summary>Interaction prompts float over the thing they act on — a key cap and a label in a small bubble —
        /// instead of a line at the bottom of the screen. Fishing and gathering status floats over the player.</summary>
        void DrawPrompt()
        {
            var camera = Camera.main;
            if (camera == null) return;
            foreach (var (at, key, text) in PromptsFor(_player))
            {
                var screen = camera.WorldToScreenPoint(at);
                if (screen.z < 0f) continue;
                DrawBubble(new Vector2(screen.x, Screen.height - screen.y), key, text);
            }
        }

        void DrawBubble(Vector2 anchor, string key, string text)
        {
            var st = UiTheme.Styles;
            var label = new GUIStyle(st.Text) { wordWrap = false, alignment = TextAnchor.MiddleLeft };
            var textWidth = label.CalcSize(new GUIContent(text)).x;
            var keyWidth = string.IsNullOrEmpty(key) ? 0f : 26f;
            var width = textWidth + keyWidth + 20f;
            var rect = new Rect(anchor.x - width * 0.5f, anchor.y - 30f, width, 28f);
            GUI.Box(rect, GUIContent.none, st.Window);
            var x = rect.x + 8f;
            if (keyWidth > 0f)
            {
                var cap = new Rect(x, rect.y + 4f, 22f, 20f);
                GUI.Box(cap, GUIContent.none, st.ButtonOn);
                GUI.Label(cap, key, new GUIStyle(st.Text) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 12 });
                x += keyWidth;
            }
            GUI.Label(new Rect(x, rect.y + 3f, textWidth + 4f, 22f), text, label);
            // A little tail pointing down at the object.
            GUI.color = UiTheme.PanelEdge;
            GUI.DrawTexture(new Rect(anchor.x - 3f, rect.yMax - 1f, 6f, 5f), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        /// <summary>World point, key, text for every prompt that applies right now. Mirrors the server: E goes to the
        /// nearest of water, a harvestable node or a station; a loot pile is picked up first.</summary>
        static List<(Vector2 At, string Key, string Text)> PromptsFor(PlayerInteraction player)
        {
            var prompts = new List<(Vector2, string, string)>();
            var world = IslandWorld.Instance;
            if (world == null) return prompts;
            Vector2 position = player.transform.position;
            var overHead = Isle.UI.Art.StickFigureView.PositionOf(player) + Vector2.up * 2.1f;

            if (player.Cast != null)
            {
                var bite = player.Cast.State == Isle.Gameplay.Fishing.CastState.Bite;
                prompts.Add((player.CastPoint + Vector2.up * 0.6f, bite ? Lang.Get("@ui.key_lmb") : null, Lang.Get(bite ? "@ui.hook_now" : "@ui.waiting_bite")));
                return prompts;
            }
            if (player.Butchering != null)
            {
                prompts.Add((player.Butchering.Position + Vector2.up * (player.Butchering.Radius * 2f + 0.4f), null, $"{Lang.Get("@ui.butchering")} {player.ButcherProgress * 100f:0}%"));
                return prompts;
            }
            if (player.Gathering != null)
            {
                prompts.Add((player.Gathering.Position + Vector2.up * (NodeHeight(player.Gathering) + 0.3f), null, $"{Lang.Get("@ui.gathering")} {player.GatherProgress * 100f:0}%"));
                return prompts;
            }

            var dungeon = Isle.Gameplay.Dungeons.DungeonDirector.Instance?.Prompt(player);
            if (dungeon != null)
            {
                var clearing = Isle.Gameplay.Dungeons.DungeonDirector.Instance.IsClearing(player);
                prompts.Add((dungeon.Value.At, clearing ? null : "E", dungeon.Value.Text));
                return prompts;
            }

            // SYS-HUNT-01: a carcass in reach is what E does first after a loot pile.
            var carcass = Isle.Gameplay.Hunting.CreatureDirector.Instance?.NearestCarcass(position, PlayerInteraction.ReachTiles);
            if (carcass != null && LootPiles.Nearest(position, PlayerInteraction.ReachTiles) == null)
            {
                prompts.Add((carcass.Position + Vector2.up * (carcass.Radius * 2f + 0.4f), "E", $"{Lang.Get("@ui.butcher")} — {Lang.Get(carcass.Def.Name)}"));
                return prompts;
            }

            var pile = LootPiles.Nearest(position, PlayerInteraction.ReachTiles);
            if (pile != null) prompts.Add((pile.Position + Vector2.up * 0.7f, "E", Lang.Get("@ui.pick_up")));

            var harvest = world.NearestNode(position, PlayerInteraction.ReachTiles, n => n.IsHarvestable);
            var nearStation = WorldObjectRegistry.NearestInteractable(position, PlayerInteraction.ReachTiles);
            var water = world.NearestWater(position, PlayerInteraction.ReachTiles);
            var harvestDistance = harvest != null ? Vector2.Distance(position, harvest.Position) : float.MaxValue;
            var stationDistance = nearStation != null ? Vector2.Distance(position, nearStation.transform.position) : float.MaxValue;
            var waterDistance = water != null ? Vector2.Distance(position, IslandWorld.TileToWorld(water.Value)) : float.MaxValue;

            if (pile == null)
            {
                if (waterDistance < harvestDistance && waterDistance < stationDistance)
                    prompts.Add((IslandWorld.TileToWorld(water.Value) + Vector2.up * 0.5f, "E", $"{Lang.Get("@ui.drink")} — {Lang.Get(world.WaterAt(water.Value).Name)}"));
                else if (harvest != null && harvestDistance <= stationDistance)
                    prompts.Add((harvest.Position + Vector2.up * (NodeHeight(harvest) + 0.3f), "E", $"{Lang.Get("@ui.harvest")} — {Lang.Get(harvest.Def.Name)}"));
                else if (nearStation != null)
                {
                    var name = Lang.Get(nearStation.Def?.Name);
                    if (nearStation.TryGetComponent<RainCatcher>(out var catcher)) name += $" ({catcher.Water:0.0}/{catcher.Capacity:0})";
                    if (nearStation.TryGetComponent<CropPlot>(out var plot) && plot.Crop != null) name = $"{Lang.Get(plot.Crop.Name)} {plot.Growth * 100f:0}%";
                    var top = (Vector2)nearStation.transform.position + Vector2.up * ((nearStation.Def?.Visual?.Size ?? 1f) * 0.95f + 0.2f);
                    prompts.Add((top, "E", name));
                    if (IsNight() && nearStation.HasTag("station/campfire")) prompts.Add((top + Vector2.up * 0.7f, "R", Lang.Get("@ui.rest_short")));
                }
            }

            if (world.NearestWater(position, 8f) != null && prompts.Count == 0) prompts.Add((overHead, "F", Lang.Get("@ui.fish_short")));
            return prompts;
        }

        /// <summary>Height of a standing node's drawing above its foot (sprites stand on their pivot).</summary>
        static float NodeHeight(ResourceNode node) => (node.Def?.Visual?.Size ?? 1f) * 0.9f;

        static bool IsNight() =>
            WorldTime.Instance != null && WorldTime.Instance.Clock.Phase == DayPhase.Night;

        /// <summary>What an item is and does: ingredients and what eating it gives (dishes as cooked; raw food through
        /// the <c>eat_raw</c> method, exactly as the server will apply it), buffs with durations, freshness, and
        /// for gear its armor / warmth / bag size.</summary>
        void DrawTooltip(ItemDef item, GridInventory container)
        {
            var lines = new List<string> { Lang.Get(item.Name) };

            if (DishFactory.IsDish(item))
            {
                var names = DishFactory.IngredientsOf(item).Select(id => DefRegistry.TryGet<ItemDef>(Isle.Core.Ids.NamespacedId.Parse(id), out var d) ? Lang.Get(d.Name) : id);
                lines.Add($"{Lang.Get("@ui.tt_ingredients")}: {string.Join(", ", names)}");
            }

            if (item.Nutrition != null)
            {
                float hunger = item.Nutrition.Hunger, thirst = item.Nutrition.Thirst, durationMult = item.BuffDurationMult;
                var buffs = (IEnumerable<Isle.Core.Ids.NamespacedId>)(item.Buffs ?? new Isle.Core.Ids.NamespacedId[0]);
                var raw = DishFactory.IsDish(item) ? null : DefRegistry.All<CookMethodDef>().FirstOrDefault(m => m.EatRaw);
                if (raw != null)
                {
                    var result = CookingResolver.Resolve(raw, new[] { item }, cookingLevel: 1, failureRoll: 1f);
                    (hunger, thirst, durationMult) = (result.Hunger, result.Thirst, result.BuffDurationMult);
                    buffs = result.Buffs.Select(b => b.Buff);
                }
                lines.Add($"{Lang.Get("@ui.tt_eating")}: {Lang.Get("@ui.hunger")} +{hunger:0.#} · {Lang.Get("@ui.thirst")} {(thirst >= 0 ? "+" : "")}{thirst:0.#}");
                var buffText = buffs.Select(id => DefRegistry.TryGet<BuffDef>(id, out var b) ? $"{Lang.Get(b.Name)} ({b.DurationMin * durationMult / 60f:0.#}{Lang.Get("@ui.tt_hours")})" : id.Value).ToList();
                if (buffText.Count > 0) lines.Add($"{Lang.Get("@ui.tt_buffs")}: {string.Join(", ", buffText)}");
            }

            if (item.Spoilage != null && item.Spoilage.BaseHours > 0f && container != null)
            {
                var hoursLeft = (1f - SpoilageTracker.Live.SpoilageOf(container, item)) * item.Spoilage.BaseHours;
                lines.Add($"{Lang.Get("@ui.tt_spoils")}: {hoursLeft:0.#}{Lang.Get("@ui.tt_hours")}");
            }
            if (item.Armor > 0f) lines.Add($"{Lang.Get("@ui.armor")}: {item.Armor:0}");
            if (item.Warmth > 0f) lines.Add($"{Lang.Get("@ui.tt_warmth")}: +{item.Warmth:0}°");
            if (item.BagGrid is { } bagGrid) lines.Add($"{Lang.Get("@ui.tt_bag")}: {bagGrid.W}×{bagGrid.H}");
            lines.Add($"{Lang.Get("@ui.tt_weight")}: {item.Weight:0.##} kg");

            const float width = 300f;
            var height = lines.Count * 18f + 10f;
            var mouse = Event.current.mousePosition;
            var rect = new Rect(Mathf.Min(mouse.x - width - 12f, Screen.width - width - 4f), Mathf.Min(mouse.y + 12f, Screen.height - height - 4f), width, height);
            if (rect.x < 4f) rect.x = mouse.x + 16f;
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            for (var i = 0; i < lines.Count; i++)
                GUI.Label(new Rect(rect.x + 8f, rect.y + 5f + i * 18f, width - 16f, 20f), lines[i], i == 0 ? _title : _label);
        }

        /// <summary>The name of the item whose grid this is, for the bag section's heading.</summary>
        string BagName(GridInventory container)
        {
            foreach (var slot in EquipSlots.All)
                if (_inventory.Slots.BagFor(slot) == container) return _inventory.Slots.Get(slot).Name;
            return "@ui.bag";
        }

        static int UsedCells(GridInventory container) => container.Placements.Sum(p => p.Item.Grid.W * p.Item.Grid.H);

        /// <summary>The one thing a bag item's button does, decided by what its def says it is.</summary>
        static string VerbFor(ItemDef item)
        {
            if (item.Plants.IsValid) return Lang.Get("@ui.plant");
            if (item.Places.IsValid) return Lang.Get("@ui.place");
            if (!string.IsNullOrEmpty(item.EquipSlot)) return Lang.Get(item.Weapon.IsValid ? "@ui.wield" : "@ui.wear");
            if (item.Nutrition != null) return Lang.Get("@ui.eat");
            return null;
        }

        /// <summary>What right-clicking an item in the inventory does: place it, wear/wield it, or eat it.</summary>
        public void Use(ItemDef item)
        {
            if (item.Places.IsValid) _placing = item.Id.Value;
            else if (!string.IsNullOrEmpty(item.EquipSlot)) _player.RequestEquipItem(item.Id.Value);
            else _player.RequestUse(item.Id.Value);
        }

        static string SkillName(Isle.Core.Ids.NamespacedId id) =>
            DefRegistry.TryGet<SkillDef>(id, out var skill) ? Lang.Get(skill.Name) : id.Value;

        /// <summary>T-063: skills split into production and combat tabs, each with its level, progress, and its focus
        /// shown as a bonus ("Focus 88% — earning 1.86× XP"; SYS-SKILL-01 forbids penalty wording).</summary>
        void DrawSkills()
        {
            const float width = 380f;
            var skills = _player.Skills.Skills.Where(sk => sk.Pool == _skillsTab).ToList();
            var x = (Screen.width - width) * 0.5f;
            var y = Screen.height * 0.18f;
            Panel(new Rect(x - 8f, y - 8f, width + 16f, 60f + skills.Count * 46f));
            GUI.Label(new Rect(x, y, width, RowHeight), Lang.Get("@ui.skills"), _title);

            var tabX = x + width - 180f;
            foreach (var pool in new[] { "production", "combat" })
            {
                if (GUI.Toggle(new Rect(tabX, y, 88f, RowHeight - 2f), _skillsTab == pool, Lang.Get("@ui.pool." + pool), GUI.skin.button)) _skillsTab = pool;
                tabX += 92f;
            }
            y += RowHeight + 8f;

            foreach (var skill in skills)
            {
                var level = _player.Skills.Level(skill.Id);
                var progress = _player.Skills.ProgressToNext(skill.Id);
                GUI.Label(new Rect(x, y, 200f, RowHeight), $"{Lang.Get(skill.Name)}  {Lang.Get("@ui.level")} {level}", _label);
                var bar = new Rect(x + 170f, y + 6f, width - 170f, 10f);
                GUI.color = Color.black;
                GUI.DrawTexture(bar, Texture2D.whiteTexture);
                GUI.color = new Color(0.45f, 0.75f, 1f);
                GUI.DrawTexture(new Rect(bar.x + 1f, bar.y + 1f, (bar.width - 2f) * progress, bar.height - 2f), Texture2D.whiteTexture);
                GUI.color = Color.white;

                var focus = _player.FocusOf(skill.Id);
                var multiplier = _player.FocusMultiplier(skill.Id);
                var line = string.Format(Lang.Get("@ui.focus_line"), Mathf.RoundToInt(focus * 100f), multiplier.ToString("0.00"));
                GUI.Label(new Rect(x + 12f, y + 20f, width, RowHeight), line, _helpStyle);
                y += 46f;
            }
        }

        /// <summary>Active buffs under the gauges, with minutes left — the only place a player sees what a dish did.</summary>
        void DrawBuffs()
        {
            var y = Margin + RowHeight * 5f + 2f;
            foreach (var buff in _vitals.ActiveBuffs())
            {
                GUI.Label(new Rect(Margin, y, 320f, RowHeight), $"• {Lang.Get(buff.Name)}", _label);
                y += RowHeight - 6f;
            }
        }

        void DrawBox()
        {
            var contents = _openBox.Contents.Placements;
            var bag = _inventory.Containers().SelectMany(c => c.Placements).ToList();
            var rows = 2 + Mathf.Max(contents.Count, bag.Count);
            var width = PanelWidth * 2f + 24f;
            var rect = Panel(new Rect((Screen.width - width) * 0.5f, Screen.height * 0.25f, width, RowHeight * rows + 12f));
            GUI.Label(new Rect(rect.x + 8f, rect.y + 4f, width, RowHeight), Lang.Get(_openBox.GetComponent<Isle.World.Objects.WorldObjectInstance>()?.Def?.Name ?? "@world_object.crate"), _title);

            var y = rect.y + 4f + RowHeight;
            GUI.Label(new Rect(rect.x + 8f, y, PanelWidth, RowHeight), Lang.Get("@ui.bag"), _label);
            GUI.Label(new Rect(rect.x + PanelWidth + 16f, y, PanelWidth, RowHeight), Lang.Get("@ui.in_box"), _label);
            y += RowHeight;

            var rowY = y;
            foreach (var placed in bag)
            {
                if (GUI.Button(new Rect(rect.x + 8f, rowY, PanelWidth, RowHeight - 4f), $"{Lang.Get(placed.Item.Name)} ×{placed.Count}  →"))
                    _player.RequestStore(placed.Item.Id.Value);
                rowY += RowHeight;
            }
            rowY = y;
            foreach (var placed in contents.ToList())
            {
                if (GUI.Button(new Rect(rect.x + PanelWidth + 16f, rowY, PanelWidth, RowHeight - 4f), $"←  {Lang.Get(placed.Item.Name)} ×{placed.Count}"))
                    _player.RequestTake(placed.Item.Id.Value);
                rowY += RowHeight;
            }
        }

        /// <summary>Follows the mouse: green where the server will accept the placement, red where it won't.</summary>
        void DrawPlacementGhost()
        {
            var camera = Camera.main;
            var at = MouseWorld();
            if (camera == null || at == null) return;

            var screen = camera.WorldToScreenPoint(at.Value);
            var pixelsPerTile = Screen.height / (camera.orthographicSize * 2f);
            var ok = _player.CanPlaceAt(at.Value);
            GUI.color = ok ? new Color(0.3f, 1f, 0.3f, 0.5f) : new Color(1f, 0.3f, 0.3f, 0.5f);
            GUI.DrawTexture(new Rect(screen.x - pixelsPerTile * 0.45f, Screen.height - screen.y - pixelsPerTile * 0.45f, pixelsPerTile * 0.9f, pixelsPerTile * 0.9f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(Screen.width * 0.5f - 200f, Screen.height - 110f, 400f, RowHeight), Lang.Get("@ui.placing_hint"), _label);
        }

        /// <summary>Fills over the full-charge time (SYS-COMBAT-01: 0.8 s) and turns green when a shot is full power.</summary>
        void DrawChargeBar()
        {
            var fraction = Mathf.Clamp01((Time.time - _player.DrawStartedAt) / Isle.Gameplay.Combat.RangedCalculator.FullChargeSeconds);
            var rect = new Rect(Screen.width * 0.5f - 60f, Screen.height * 0.5f + 40f, 120f, 8f);
            GUI.color = Color.black;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = fraction >= 1f ? Color.green : Color.yellow;
            GUI.DrawTexture(new Rect(rect.x + 1f, rect.y + 1f, (rect.width - 2f) * fraction, rect.height - 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        /// <summary>SYS-FISH-01 tension bar: the safe band in green, the needle, the fish's stamina, and the two
        /// failure meters.</summary>
        void DrawFight()
        {
            var fight = _player.Fight;
            const float width = 320f;
            var x = (Screen.width - width) * 0.5f;
            var y = Screen.height * 0.62f;
            Panel(new Rect(x - 10f, y - 30f, width + 20f, 118f));
            GUI.Label(new Rect(x, y - 26f, width, RowHeight), $"{Lang.Get(_player.FightFish?.Name)}  {_player.FightWeightKg:0.0} kg — {Lang.Get("@ui.reel_hint")}", _label);

            var bar = new Rect(x, y, width, 18f);
            GUI.color = new Color(0.15f, 0.15f, 0.15f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = new Color(0.3f, 0.8f, 0.3f, 0.8f);
            GUI.DrawTexture(new Rect(x + width * Mathf.Clamp01(fight.BandLow), y, width * (Mathf.Clamp01(fight.BandHigh) - Mathf.Clamp01(fight.BandLow)), 18f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(x + width * fight.Tension - 2f, y - 4f, 4f, 26f), Texture2D.whiteTexture);

            Meter(new Rect(x, y + 30f, width, 8f), fight.FishStamina / Mathf.Max(1f, fight.MaxFishStamina), new Color(0.4f, 0.7f, 1f), "@ui.fish_stamina");
            Meter(new Rect(x, y + 50f, width, 8f), fight.LineBreak, new Color(1f, 0.4f, 0.3f), "@ui.line_meter");
            Meter(new Rect(x, y + 70f, width, 8f), fight.HookSlip, new Color(1f, 0.8f, 0.3f), "@ui.slip_meter");
        }

        void Meter(Rect rect, float fraction, Color colour, string labelKey)
        {
            GUI.color = Color.black;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = colour;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fraction), rect.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + rect.width + 6f, rect.y - 7f, 120f, 20f), Lang.Get(labelKey), _label);
        }

        static Vector2? MouseWorld()
        {
            var camera = Camera.main;
            var mouse = Mouse.current;
            if (camera == null || mouse == null) return null;
            var p = mouse.position.ReadValue();
            return camera.ScreenToWorldPoint(new Vector3(p.x, p.y, -camera.transform.position.z));
        }

        void DrawHelp()
        {
            // Sized to its text: the box grows with wrapped lines instead of clipping them, and narrows on small screens.
            const float maxWidth = 900f;
            var width = Mathf.Min(maxWidth, Screen.width - 2f * Margin);
            var content = new GUIContent(Lang.Get("@ui.help").Replace("\\n", "\n"));
            var height = _helpStyle.CalcHeight(content, width - 16f);
            var rect = new Rect((Screen.width - width) * 0.5f, Margin, width, height + 8f);
            GUI.Box(rect, GUIContent.none);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 4f, width - 16f, height), content, _helpStyle);
        }

        void DrawDeath()
        {
            var text = Lang.Get("@ui.died");
            if (_death != null && _death.IsDead)
                text += "\n" + string.Format(Lang.Get("@ui.respawn_in"), Mathf.Max(0, Mathf.CeilToInt(_death.RespawnAt - Time.time)));
            GUI.Label(new Rect(Screen.width * 0.5f - 200f, Screen.height * 0.4f, 400f, 60f), text, _title);
        }

        void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
            _title = new GUIStyle(_label) { fontSize = 16, fontStyle = FontStyle.Bold };
            _helpStyle = new GUIStyle(_label) { fontSize = 12, wordWrap = true };
        }
    }
}
