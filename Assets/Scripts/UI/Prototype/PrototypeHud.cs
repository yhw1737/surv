using System.Collections.Generic;
using System.Linq;
using Isle.Data;
using Isle.Gameplay.Building;
using Isle.Gameplay.Character;
using Isle.Gameplay.Cooking;
using Isle.Gameplay.Crafting;
using Isle.Gameplay.Feedback;
using Isle.Gameplay.Inventory;
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
    public sealed class PrototypeHud : MonoBehaviour
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

        bool _bagOpen = true;
        ItemDef _hover;
        GridInventory _hoverContainer;
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

        void OnEnable() => GameFeed.StorageOpened += OnStorageOpened;
        void OnDisable() => GameFeed.StorageOpened -= OnStorageOpened;
        void OnStorageOpened(object box) => _openBox = box as StorageBox;

        void Update()
        {
            if (_player == null || !_player.IsOwner) FindLocalPlayer();

            var kb = Keyboard.current;
            if (kb != null && kb.cKey.wasPressedThisFrame) _craftOpen = !_craftOpen;
            if (kb != null && kb.kKey.wasPressedThisFrame) _cookOpen = !_cookOpen;
            if (kb != null && kb.tabKey.wasPressedThisFrame) _bagOpen = !_bagOpen;
            if (kb != null && kb.hKey.wasPressedThisFrame) _helpOpen = !_helpOpen;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) _placing = null;

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
            foreach (var candidate in FindObjectsByType<PlayerInteraction>(FindObjectsSortMode.None))
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

            DrawGauges();
            DrawClock();
            DrawPrompt();
            _hover = null;
            if (_bagOpen) DrawBag();
            if (_craftOpen) DrawCraftWindow();
            if (_cookOpen) DrawCookWindow();
            DrawBuffs();
            if (_openBox != null) DrawBox();
            if (_placing != null) DrawPlacementGhost();
            if (_player.DrawStartedAt >= 0f) DrawChargeBar();
            if (_player.Fight != null) DrawFight();
            if (_helpOpen) DrawHelp();
            if (_death != null ? _death.IsDead : _vitals.Health <= 0f) DrawDeath();
            if (_hover != null) DrawTooltip(_hover, _hoverContainer);

            if (Event.current.type == EventType.Repaint) PointerGate.Captured = _placing != null || OverPanel();
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
            GUI.Box(rect, GUIContent.none);
            return rect;
        }

        void DrawGauges()
        {
            var y = Margin;
            DrawGauge(ref y, Lang.Get("@ui.health"), _vitals.Health, Color.red);
            DrawGauge(ref y, Lang.Get("@ui.hunger"), _vitals.Hunger, new Color(0.95f, 0.6f, 0.2f));
            DrawGauge(ref y, Lang.Get("@ui.thirst"), _vitals.Thirst, new Color(0.3f, 0.6f, 1f));
            DrawGauge(ref y, Lang.Get("@ui.stamina"), _vitals.Stamina, new Color(0.95f, 0.85f, 0.3f));

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

        void DrawPrompt()
        {
            var prompt = PromptFor(_player.transform.position);
            if (string.IsNullOrEmpty(prompt)) return;
            GUI.Label(new Rect(Screen.width * 0.5f - 300f, Screen.height - 80f, 600f, RowHeight), prompt, _title);
        }

        static string PromptFor(Vector3 position)
        {
            var world = IslandWorld.Instance;
            if (world == null) return string.Empty;

            var lines = new List<string>();
            if (LootPiles.Nearest(position, PlayerInteraction.ReachTiles) != null) lines.Add($"[E] {Lang.Get("@ui.pick_up")}");
            var drink = world.NearestNode(position, PlayerInteraction.ReachTiles, n => n.IsDrinkable);
            if (drink != null) lines.Add($"[E] {Lang.Get("@ui.drink")} — {Lang.Get(drink.Def.Name)}");

            var harvest = world.NearestNode(position, PlayerInteraction.ReachTiles, n => n.IsHarvestable);
            var nearStation = WorldObjectRegistry.NearestInteractable(position, PlayerInteraction.ReachTiles);
            if (harvest != null && nearStation != null &&
                Vector2.Distance(position, nearStation.transform.position) < Vector2.Distance(position, harvest.Position))
                harvest = null;
            if (harvest != null) lines.Add($"[E] {Lang.Get("@ui.harvest")} — {Lang.Get(harvest.Def.Name)}");

            var spot = world.NearestNode(position, PlayerInteraction.ReachTiles, n => n.Def.Fishing != null);
            if (spot != null) lines.Add(Lang.Get("@ui.fish_hint"));

            var station = WorldObjectRegistry.NearestInteractable(position, PlayerInteraction.ReachTiles);
            if (station != null)
            {
                var name = Lang.Get(station.Def?.Name);
                if (station.TryGetComponent<RainCatcher>(out var catcher)) name += $" ({catcher.Water:0.0}/{catcher.Capacity:0})";
                if (station.TryGetComponent<CropPlot>(out var plot) && plot.Crop != null) name = $"{Lang.Get(plot.Crop.Name)} {plot.Growth * 100f:0}%";
                lines.Add($"[E] {name}");
                if (IsNight() && station.HasTag("station/campfire")) lines.Add(Lang.Get("@ui.rest_hint"));
            }

            return string.Join("   ", lines);
        }

        static bool IsNight() =>
            WorldTime.Instance != null && WorldTime.Instance.Clock.Phase == DayPhase.Night;

        void DrawBag()
        {
            var x = Screen.width - PanelWidth - Margin;
            var y = Margin + 64f;
            var equipped = EquipSlots.All.Where(slot => _inventory.Slots.Get(slot) != null).ToList();
            var containers = _inventory.Containers();
            var rows = 1 + Mathf.Max(1, equipped.Count) + containers.Count + containers.Sum(c => c.Placements.Count);
            Panel(new Rect(x - 6f, y - 4f, PanelWidth + 12f, RowHeight * rows + 12f));

            var weight = containers.Sum(c => c.TotalWeightKg()) + equipped.Sum(slot => _inventory.Slots.Get(slot).Weight);
            GUI.Label(new Rect(x, y, PanelWidth, RowHeight), $"{Lang.Get("@ui.equipment")}   {weight:0.0} / {WeightCalculator.FreeWeightKg:0} kg", _title);
            y += RowHeight;

            if (equipped.Count == 0)
            {
                GUI.Label(new Rect(x, y, PanelWidth, RowHeight), $"{Lang.Get("@ui.hand")}: {Lang.Get("@weapon.bare_hands")}", _label);
                y += RowHeight;
            }
            foreach (var slot in equipped)
            {
                var item = _inventory.Slots.Get(slot);
                GUI.Label(new Rect(x, y, PanelWidth - ButtonWidth - 6f, RowHeight), $"{Lang.Get("@ui.slot." + slot)}: {Lang.Get(item.Name)}", _label);
                if (GUI.Button(new Rect(x + PanelWidth - ButtonWidth, y, ButtonWidth, RowHeight - 4f), Lang.Get("@ui.unwield")))
                    _player.RequestUnequipSlot(slot);
                y += RowHeight;
            }

            for (var i = 0; i < containers.Count; i++)
            {
                var container = containers[i];
                var title = i == 0 ? Lang.Get("@ui.bag") : Lang.Get(BagName(container));
                GUI.Label(new Rect(x, y, PanelWidth, RowHeight), $"{title}  ({UsedCells(container)}/{container.Width * container.Height})", _title);
                y += RowHeight;
                foreach (var placed in container.Placements)
                {
                    var item = placed.Item;
                    var text = $"{Lang.Get(item.Name)} ×{placed.Count}";
                    if (item.Spoilage != null && item.Spoilage.BaseHours > 0f)
                        text += $"  ({Lang.Get("@ui.fresh")} {(1f - SpoilageTracker.Live.SpoilageOf(container, item)) * 100f:0}%)";
                    var row = new Rect(x, y, PanelWidth - ButtonWidth - 6f, RowHeight);
                    GUI.Label(row, text, _label);
                    if (row.Contains(Event.current.mousePosition))
                    {
                        _hover = item;
                        _hoverContainer = container;
                    }
                    var verb = VerbFor(item);
                    if (verb != null && GUI.Button(new Rect(x + PanelWidth - ButtonWidth, y, ButtonWidth, RowHeight - 4f), verb))
                        Use(item);
                    y += RowHeight;
                }
            }
        }

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

        void Use(ItemDef item)
        {
            if (item.Places.IsValid) _placing = item.Id.Value;
            else if (!string.IsNullOrEmpty(item.EquipSlot)) _player.RequestEquipItem(item.Id.Value);
            else _player.RequestUse(item.Id.Value);
        }

        void DrawCraftWindow()
        {
            var recipes = DefRegistry.All<CraftRecipeDef>();
            var x = Margin;
            var y = Margin + RowHeight * 5 + 24f;
            Panel(new Rect(x - 4f, y - 4f, PanelWidth + 8f, RowHeight * (1 + recipes.Count) + 8f));
            GUI.Label(new Rect(x, y, PanelWidth, RowHeight), Lang.Get("@ui.craft"), _title);
            y += RowHeight;

            var stock = CraftingCalculator.StockOf(_inventory.Containers());
            var stationPosition = _player.transform.position;
            foreach (var recipe in recipes)
            {
                var stationOk = !recipe.Station.IsValid || WorldObjectRegistry.IsActiveNear(stationPosition, recipe.Station, PlayerInteraction.ReachTiles);
                var affordable = CraftingCalculator.HasIngredients(recipe.Ingredients, stock);

                GUI.enabled = stationOk && affordable;
                if (GUI.Button(new Rect(x, y, PanelWidth, RowHeight - 4f), Lang.Get(recipe.Name)))
                    _player.RequestCraft(recipe.Id.Value);
                GUI.enabled = true;
                y += RowHeight;
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

        /// <summary>SYS-COOK-01 cooking: pick a method, put food in, cook. Methods above the cooking level or away
        /// from their station show why they're unavailable; the server checks the same rules.</summary>
        void DrawCookWindow()
        {
            const float width = 300f;
            var x = Margin + PanelWidth + 20f;
            var y = Margin + RowHeight * 5 + 24f;
            var methods = DefRegistry.All<CookMethodDef>().Where(m => !m.EatRaw).ToList();
            var foods = _inventory.Containers().SelectMany(c => c.Placements).Where(p => p.Item.Nutrition != null).ToList();
            var rows = 3 + methods.Count + foods.Count + _cookPicks.Count;
            Panel(new Rect(x - 4f, y - 4f, width + 8f, RowHeight * rows + 16f));
            GUI.Label(new Rect(x, y, width, RowHeight), Lang.Get("@ui.cook"), _title);
            y += RowHeight;

            var position = _player.transform.position;
            foreach (var method in methods)
            {
                var level = method.UnlockSkill?.Level ?? 1;
                var locked = level > 1;
                var stationOk = !method.Station.IsValid || WorldObjectRegistry.IsActiveNear(position, method.Station, PlayerInteraction.ReachTiles);
                var label = Lang.Get(method.Name);
                if (locked) label += $"  ({Lang.Get("@ui.needs_level")} {level})";
                else if (!stationOk) label += $"  ({Lang.Get("@ui.need_station")})";
                GUI.enabled = !locked && stationOk;
                var selected = _cookMethod == method.Id.Value;
                if (GUI.Toggle(new Rect(x, y, width, RowHeight - 4f), selected, label, GUI.skin.button) && !selected) _cookMethod = method.Id.Value;
                GUI.enabled = true;
                y += RowHeight;
            }

            GUI.Label(new Rect(x, y, width, RowHeight), Lang.Get("@ui.ingredients"), _label);
            y += RowHeight;
            foreach (var placed in foods)
            {
                var available = placed.Count - _cookPicks.Count(id => id == placed.Item.Id.Value);
                GUI.enabled = available > 0;
                if (GUI.Button(new Rect(x, y, width, RowHeight - 4f), $"+ {Lang.Get(placed.Item.Name)} ×{available}"))
                    _cookPicks.Add(placed.Item.Id.Value);
                GUI.enabled = true;
                y += RowHeight;
            }
            for (var i = 0; i < _cookPicks.Count; i++)
            {
                var item = foods.FirstOrDefault(p => p.Item.Id.Value == _cookPicks[i]).Item;
                if (GUI.Button(new Rect(x, y, width, RowHeight - 4f), $"− {(item != null ? Lang.Get(item.Name) : _cookPicks[i])}"))
                {
                    _cookPicks.RemoveAt(i);
                    break;
                }
                y += RowHeight;
            }

            var chosen = methods.FirstOrDefault(m => m.Id.Value == _cookMethod);
            GUI.enabled = chosen != null && CookingResolver.CountAllowed(chosen, _cookPicks.Count);
            if (GUI.Button(new Rect(x, y, width, RowHeight), Lang.Get("@ui.cook_now")))
            {
                _player.RequestCook(_cookMethod, _cookPicks);
                _cookPicks.Clear();
            }
            GUI.enabled = true;
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
            const float width = 760f;
            var rect = new Rect((Screen.width - width) * 0.5f, Margin, width, 46f);
            GUI.Box(rect, GUIContent.none);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 3f, width - 16f, 42f), Lang.Get("@ui.help").Replace("\\n", "\n"), _helpStyle);
        }

        void DrawDeath()
        {
            GUI.Label(new Rect(Screen.width * 0.5f - 160f, Screen.height * 0.4f, 320f, 60f), Lang.Get("@ui.died"), _title);
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
