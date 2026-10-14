using System.Collections.Generic;
using System.Linq;
using Isle.Gameplay.Character;
using Isle.Gameplay.Inventory;
using Isle.UI.Prototype;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Isle.UI.Inventory
{
    /// <summary>
    /// The full-screen inventory (Tab), in the extraction-shooter / Palworld layout: equipment and character stats on the
    /// left, every bag as a SYS-INV-01 grid in the middle, the ground on the right. Drag to move, R/Q/E rotates while
    /// dragging, Shift+drag splits, Ctrl+click moves to the next bag, right-click opens a menu of what the item can do
    /// (eat, wear, wield, place, plant, move to another bag, drop) or takes off, and letting go outside the panels drops it. Every change is a request to
    /// <see cref="InventoryNetwork"/> (server authority); this screen only draws.
    /// </summary>
    public sealed class InventoryScreen : MonoBehaviour
    {
        // Layout in reference pixels (1920×1080, scaled to the screen) — presentation only.
        // T-151 playtest: bigger bag cells (items were hard to see), smaller equipment frames.
        const int CellPx = 64;
        const int EquipSlotPx = 84;
        const float Pad = 18f;
        const float SlotGap = 14f;
        const float LabelPx = 18f;
        const float TitlePx = 30f;
        const float PanelTop = 92f;
        const float PanelGap = 22f;
        const float CharacterWidth = 420f;
        const float GroundWidth = 300f;
        const float FreshnessRefreshSeconds = 0.5f;

        public static InventoryScreen Instance { get; private set; }

        /// <summary>True while the screen is open — it covers everything, so the HUD treats the pointer as captured.</summary>
        public static bool PointerOverUi { get; private set; }

        public bool Open { get; private set; }

        Canvas _canvas;
        RectTransform _root;
        Text _weight, _stats, _ground;
        InventoryNetwork _network;
        PlayerInteraction _player;
        Vitals _vitals;
        readonly List<GridView> _grids = new();
        readonly List<EquipSlotView> _slots = new();
        int _builtContainers = -1;
        string _bagKeys;
        long _signature;
        float _nextFreshnessAt;
        int _builtWidth, _builtHeight;

        // Paper-doll positions (column, row) for each equip slot.
        static readonly Dictionary<string, Vector2> DollLayout = new()
        {
            ["head"] = new(1, 0), ["shirt"] = new(2, 0), ["back"] = new(0, 1), ["chest"] = new(1, 1), ["belt"] = new(2, 1),
            ["main_hand"] = new(0, 2), ["legs"] = new(1, 2), ["off_hand"] = new(2, 2), ["feet"] = new(1, 3),
        };

        void Awake()
        {
            Instance = this;
            ItemTooltip.UseExternal = true;
            GridView.CellSizePx = CellPx;
            EquipSlotView.SlotSizePx = EquipSlotPx;
            GridView.ItemActivated += OnItemActivated;
            Isle.Gameplay.Feedback.GameFeed.CampfireOpened += OnCampfireOpened;
            EquipSlotView.SlotActivated += OnSlotActivated;
        }

        void OnDestroy()
        {
            GridView.ItemActivated -= OnItemActivated;
            Isle.Gameplay.Feedback.GameFeed.CampfireOpened -= OnCampfireOpened;
            EquipSlotView.SlotActivated -= OnSlotActivated;
            if (Instance == this) Instance = null;
            if (_canvas != null) Destroy(_canvas.gameObject);
        }

        /// <summary>Esc closes the inventory first (the pause menu checks this).</summary>
        public void Close()
        {
            Open = false;
            Campfire = null;
        }

        /// <summary>Opens the screen without the Tab key (tests and tools).</summary>
        public void Show() => Open = true;

        /// <summary>The toolbar's equipment button.</summary>
        public void Toggle()
        {
            Open = !Open;
            if (!Open) Campfire = null;
        }

        // ------------------------------------------------------------------ campfire (T-151 playtest)

        /// <summary>The fire this screen is showing beside the bags, or null for the plain inventory.</summary>
        public Isle.Gameplay.World.CampfireInteraction Campfire { get; private set; }

        Text _fireStatus;
        Text _fireButtonLabel;

        void OnCampfireOpened(object fire) => OpenCampfire(fire as Isle.Gameplay.World.CampfireInteraction);

        /// <summary>E at a campfire: the inventory opens with the fire's fuel grid on the left.</summary>
        public void OpenCampfire(Isle.Gameplay.World.CampfireInteraction fire)
        {
            if (fire == null) return;
            Campfire = fire;
            Open = true;
            _builtContainers = -1; // rebuild with the fire panel
        }

        void BuildCampfire(RectTransform panel)
        {
            var width = panel.sizeDelta.x;
            Heading(panel, Lang.Get("@world_object.campfire"), Pad, -Pad, width);
            _fireStatus = NewText("FireStatus", panel, "", 15, FontStyle.Normal, UiTheme.Text, TextAnchor.UpperLeft);
            _fireStatus.supportRichText = true;
            Place(_fireStatus, Pad, -Pad - TitlePx - 4f, width - 2 * Pad, 48f);

            var grid = Campfire.Grid;
            var gridWidth = grid.Width * CellPx;
            var gridHeight = grid.Height * CellPx;
            var left = (width - gridWidth) * 0.5f;
            var top = -Pad - TitlePx - 60f;
            var frame = NewRect("FireFrame", panel);
            Place(frame, left - 6f, top + 6f, gridWidth + 12f, gridHeight + 12f);
            var frameImage = frame.gameObject.AddComponent<Image>();
            frameImage.sprite = UiTheme.Sprite(UiTheme.Inset, UiTheme.Accent, 8, 2);
            frameImage.type = Image.Type.Sliced;
            var go = new GameObject("Grid_Fire", typeof(RectTransform), typeof(Image), typeof(GridView));
            var rect = (RectTransform)go.transform;
            rect.SetParent(panel, false);
            Place(rect, left, top, gridWidth, gridHeight);
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            var view = go.GetComponent<GridView>();
            view.Campfire = Campfire;
            view.ContainerIndex = -1;
            view.Bind(grid);
            _fireGrid = view;

            var hint = NewText("FireHint", panel, Lang.Get("@ui.fire_hint"), 13, FontStyle.Normal, UiTheme.Muted, TextAnchor.UpperLeft);
            Place(hint, Pad, top - gridHeight - 18f, width - 2 * Pad, 40f);

            var buttonTop = top - gridHeight - 64f;
            _fireButtonLabel = Button(panel, "FireToggle", Pad, buttonTop, width - 2 * Pad, () =>
            {
                if (PlayerInteraction.Local is not { } player || Campfire == null) return;
                if (Campfire.Lit) player.RequestExtinguish();
                else player.RequestLightFire();
            });
            Button(panel, "FireCook", Pad, buttonTop - 48f, width - 2 * Pad, () =>
            {
                Close();
                if (PrototypeHud.Instance != null && !PrototypeHud.Instance.IsToolOpen("cook")) PrototypeHud.Instance.ToggleTool("cook");
            }).text = Lang.Get("@ui.fire_cook");
        }

        GridView _fireGrid;

        Text Button(RectTransform panel, string name, float x, float y, float width, System.Action onClick)
        {
            var rect = NewRect(name, panel);
            Place(rect, x, y, width, 40f);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = UiTheme.Sprite(UiTheme.Inset, UiTheme.Accent, 8, 2);
            image.type = Image.Type.Sliced;
            rect.gameObject.AddComponent<Button>().onClick.AddListener(() => onClick());
            var label = NewText("Label", rect, "", 16, FontStyle.Bold, UiTheme.Text, TextAnchor.MiddleCenter);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            return label;
        }

        void RefreshCampfire()
        {
            if (Campfire == null || _fireStatus == null) return;
            var minutes = Mathf.CeilToInt(Campfire.TotalFuelMinutes);
            var state = Campfire.Lit ? UiTheme.Colour(Lang.Get("@ui.fire_lit"), UiTheme.Accent) : UiTheme.Colour(Lang.Get("@ui.fire_out"), UiTheme.Muted);
            _fireStatus.text = $"{state}\n{string.Format(Lang.Get("@ui.fire_fuel"), minutes / 60, minutes % 60)}";
            _fireButtonLabel.text = Lang.Get(Campfire.Lit ? "@ui.extinguish" : "@ui.fire_light");
        }

        /// <summary>Test hook: presses the campfire panel's light / put-out button.</summary>
        public void PressFireToggle() => _fireButtonLabel?.GetComponentInParent<Button>()?.onClick.Invoke();

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.tabKey.wasPressedThisFrame) Open = !Open;

            if (_network == null || !_network.IsOwner) Bind();
            if (_network == null) return;
            if (_canvas.gameObject.activeSelf != Open) _canvas.gameObject.SetActive(Open);
            PointerOverUi = Open;
            if (!Open)
            {
                CloseMenu();
                return;
            }

            // Walked away from the fire: back to the plain inventory.
            if (Campfire != null && (Campfire == null || _player == null
                || Vector2.Distance(Campfire.transform.position, _player.transform.position) > PlayerInteraction.ReachTiles + 0.5f))
            {
                Campfire = null;
                _builtContainers = -1;
            }
            var containers = _network.Containers();
            if (containers.Count != _builtContainers || BagKeys() != _bagKeys || Screen.width != _builtWidth || Screen.height != _builtHeight)
                Rebuild(containers);
            else
            {
                var signature = Signature(containers);
                if (signature != _signature)
                {
                    _signature = signature;
                    foreach (var grid in _grids) grid.Redraw();
                    foreach (var slot in _slots) slot.Redraw();
                    if (_fireGrid != null) _fireGrid.Redraw();
                }
            }
            if (Time.unscaledTime >= _nextFreshnessAt)
            {
                _nextFreshnessAt = Time.unscaledTime + FreshnessRefreshSeconds;
                foreach (var grid in _grids) grid.RefreshFreshness();
            }
            RefreshCampfire();
            _weight.text = WeightText(containers);
            _stats.text = StatsText();
            _ground.text = GroundText();
        }

        void Bind()
        {
            _network = null;
            foreach (var player in PlayerInteraction.All)
            {
                if (!player.IsOwner || !player.TryGetComponent(out InventoryNetwork network)) continue;
                _network = network;
                _player = player;
                player.TryGetComponent(out _vitals);
                break;
            }
            if (_network == null) return;
            EnsureCanvas();
            _builtContainers = -1;
        }

        void EnsureCanvas()
        {
            if (_canvas != null) return;
            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var go = new GameObject("InventoryCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas = go.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 10;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _root = (RectTransform)go.transform;
            go.SetActive(false);
        }

        void Rebuild(List<GridInventory> containers)
        {
            _menu = null;
            foreach (Transform child in _root) Destroy(child.gameObject);
            _grids.Clear();
            _slots.Clear();
            _builtContainers = containers.Count;
            _bagKeys = BagKeys();
            _builtWidth = Screen.width;
            _builtHeight = Screen.height;

            // Backdrop: dims the world, and letting an item go on it drops the item.
            var backdrop = NewRect("Backdrop", _root);
            backdrop.anchorMin = Vector2.zero;
            backdrop.anchorMax = Vector2.one;
            backdrop.offsetMin = backdrop.offsetMax = Vector2.zero;
            backdrop.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.025f, 0.02f, 0.78f);
            backdrop.gameObject.AddComponent<DropZone>();

            _fireGrid = null;
            _fireStatus = null;
            var bagsWidth = Mathf.Max(420f, containers.Max(c => c.Width) * CellPx + 2 * Pad);
            var total = CharacterWidth + bagsWidth + GroundWidth + 2 * PanelGap;
            var x = -total * 0.5f;

            // Top bar.
            var title = NewText("Title", _root, Lang.Get("@ui.inventory_title"), 30, FontStyle.Bold, UiTheme.Text, TextAnchor.MiddleLeft);
            PlaceTop(title, x, -28f, 500f, 40f);
            var hint = NewText("Hint", _root, Lang.Get("@ui.inventory_hint"), 14, FontStyle.Normal, UiTheme.Muted, TextAnchor.MiddleRight);
            PlaceTop(hint, x + total - 1000f, -28f, 1000f, 40f);

            // At a campfire the fire takes the ground panel's place, on the left: fuel in, bags and equipment to the right.
            if (Campfire != null)
            {
                var fire = Panel("Campfire", x, GroundWidth);
                BuildCampfire(fire);
                x += GroundWidth + PanelGap;
            }

            var character = Panel("Character", x, CharacterWidth);
            BuildCharacter(character);
            x += CharacterWidth + PanelGap;

            var bags = Panel("Bags", x, bagsWidth);
            BuildBags(bags, containers, bagsWidth);
            x += bagsWidth + PanelGap;

            if (Campfire == null)
            {
                var ground = Panel("Ground", x, GroundWidth);
                BuildGround(ground);
            }

            for (var i = 0; i < _grids.Count; i++) _grids[i].PairedView = _grids.Count > 1 ? _grids[(i + 1) % _grids.Count] : null;
            foreach (var slot in _slots) slot.PairedView = _grids[0];
            _signature = Signature(containers);
        }

        RectTransform Panel(string name, float x, float width)
        {
            var panel = NewRect(name, _root);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 1f);
            panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = new Vector2(x, -PanelTop);
            panel.sizeDelta = new Vector2(width, 1080f - PanelTop - 60f);
            var image = panel.gameObject.AddComponent<Image>();
            image.sprite = UiTheme.Sprite(UiTheme.Panel, UiTheme.PanelEdge, 12);
            image.type = Image.Type.Sliced;
            return panel;
        }

        void BuildCharacter(RectTransform panel)
        {
            var width = panel.sizeDelta.x;
            Heading(panel, Lang.Get("@ui.equipment"), Pad, -Pad, width);
            _weight = NewText("Weight", panel, "", 14, FontStyle.Normal, UiTheme.Muted, TextAnchor.MiddleRight);
            Place(_weight, Pad, -Pad, width - 2 * Pad, TitlePx);

            var slotPx = (float)EquipSlotPx;
            var dollWidth = 3 * slotPx + 2 * SlotGap;
            var dollX = (width - dollWidth) * 0.5f;
            var top = -Pad - TitlePx - 12f;
            foreach (var slotName in EquipSlots.All)
            {
                var cell = DollLayout.TryGetValue(slotName, out var at) ? at : Vector2.zero;
                var sx = dollX + cell.x * (slotPx + SlotGap);
                var sy = top - cell.y * (slotPx + LabelPx + 6f);

                var go = new GameObject($"Slot_{slotName}", typeof(RectTransform), typeof(Image), typeof(EquipSlotView));
                var rect = (RectTransform)go.transform;
                rect.SetParent(panel, false);
                Place(rect, sx, sy, slotPx, slotPx);
                var view = go.GetComponent<EquipSlotView>();
                view.Network = _network;
                view.Bind(_network.Slots, slotName);
                var image = go.GetComponent<Image>();
                image.sprite = UiTheme.Sprite(UiTheme.Inset, UiTheme.InsetEdge, 10);
                image.type = Image.Type.Sliced;
                _slots.Add(view);

                var label = NewText("Label", panel, Lang.Get("@ui.slot." + slotName), 13, FontStyle.Normal, UiTheme.Muted, TextAnchor.UpperCenter);
                Place(label, sx - 10f, sy - slotPx - 2f, slotPx + 20f, LabelPx);
            }

            var statsTop = top - 4 * (slotPx + LabelPx + 6f) - 6f;
            Heading(panel, Lang.Get("@ui.inventory_status"), Pad, statsTop, width);
            _stats = NewText("Stats", panel, "", 15, FontStyle.Normal, UiTheme.Text, TextAnchor.UpperLeft);
            _stats.supportRichText = true;
            _stats.lineSpacing = 1.25f;
            Place(_stats, Pad + 4f, statsTop - TitlePx - 4f, width - 2 * Pad, 200f);
        }

        void BuildBags(RectTransform panel, List<GridInventory> containers, float width)
        {
            var y = -Pad;
            for (var i = 0; i < containers.Count; i++)
            {
                var container = containers[i];
                var used = container.Placements.Sum(p => p.Item.Grid.W * p.Item.Grid.H);
                Heading(panel, $"{Lang.Get(i == 0 ? "@ui.bag" : BagName(container))}   <size=13><color=#B3A48C>{container.Width}×{container.Height}</color></size>", Pad, y, width);
                y -= TitlePx + 2f;

                var gridWidth = container.Width * CellPx;
                var gridHeight = container.Height * CellPx;
                var left = (width - gridWidth) * 0.5f;
                var frame = NewRect("Frame", panel);
                Place(frame, left - 6f, y + 6f, gridWidth + 12f, gridHeight + 12f);
                var frameImage = frame.gameObject.AddComponent<Image>();
                frameImage.sprite = UiTheme.Sprite(UiTheme.Inset, UiTheme.InsetEdge, 8);
                frameImage.type = Image.Type.Sliced;

                var go = new GameObject($"Grid_{i}", typeof(RectTransform), typeof(Image), typeof(GridView));
                var rect = (RectTransform)go.transform;
                rect.SetParent(panel, false);
                Place(rect, left, y, gridWidth, gridHeight);
                go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
                var view = go.GetComponent<GridView>();
                view.Network = _network;
                view.ContainerIndex = i;
                view.Bind(container);
                _grids.Add(view);
                y -= gridHeight + 24f;
            }
        }

        void BuildGround(RectTransform panel)
        {
            var width = panel.sizeDelta.x;
            Heading(panel, Lang.Get("@ui.inventory_ground"), Pad, -Pad, width);

            // T-151 playtest: the whole ground panel takes drops (as does everything outside the panels), and lists the pile
            // at your feet — right-click or drag an item from it into a bag.
            panel.gameObject.AddComponent<DropZone>();
            _ground = NewText("Nearby", panel, "", 14, FontStyle.Normal, UiTheme.Text, TextAnchor.UpperLeft);
            _ground.supportRichText = true;
            Place(_ground, Pad, -Pad - TitlePx - 8f, width - 2 * Pad, 24f);
            _groundList = NewRect("GroundList", panel);
            Place(_groundList, Pad, -Pad - TitlePx - 40f, width - 2 * Pad, 760f);
            _groundKey = null;
        }

        // ------------------------------------------------------------------ live text

        string WeightText(List<GridInventory> containers)
        {
            var weight = containers.Sum(c => c.TotalWeightKg()) + EquipSlots.All.Select(s => _network.Slots.Get(s)).Where(i => i != null).Sum(i => i.Weight);
            // Past the free weight you slow down; past the max you can't roll and stamina stops recovering.
            var colour = weight > WeightCalculator.MaxWeightKg ? UiTheme.Bad : weight > WeightCalculator.FreeWeightKg ? UiTheme.Accent : UiTheme.Muted;
            return UiTheme.Colour(string.Format(Lang.Get("@ui.weight_line"), weight.ToString("0.0"), WeightCalculator.FreeWeightKg.ToString("0"), WeightCalculator.MaxWeightKg.ToString("0")), colour);
        }

        string StatsText()
        {
            if (_vitals == null) return "";
            string Bar(string key, float value, Color colour) =>
                $"{Lang.Get(key)}  {UiTheme.Colour(new string('■', Mathf.Clamp(Mathf.RoundToInt(value / 10f), 0, 10)), colour)}{UiTheme.Colour(new string('■', 10 - Mathf.Clamp(Mathf.RoundToInt(value / 10f), 0, 10)), new Color(0.3f, 0.26f, 0.22f))}  {value:0}";
            return string.Join("\n",
                Bar("@ui.health", _vitals.Health, new Color(0.88f, 0.35f, 0.3f)),
                Bar("@ui.hunger", _vitals.Hunger, new Color(0.9f, 0.65f, 0.3f)),
                Bar("@ui.thirst", _vitals.Thirst, new Color(0.4f, 0.65f, 0.95f)),
                Bar("@ui.stamina", _vitals.Stamina, new Color(0.6f, 0.85f, 0.4f)),
                $"{Lang.Get("@ui.temperature")}  {Lang.Get("@temp_state." + VitalsCalculator.ThermalState(_vitals.Temperature))} ({Lang.Get("@ui.ambient")} {_vitals.AmbientTemp:0}°)   {Lang.Get("@ui.armor")} {_vitals.WornArmor():0}   {Lang.Get("@ui.tt_warmth")} +{_vitals.ClothingBonus:0}°");
        }

        string GroundText()
        {
            var pile = LootPiles.Nearest(_player.transform.position, PlayerInteraction.ReachTiles);
            RefreshGroundList(pile);
            return pile == null
                ? UiTheme.Colour(Lang.Get("@ui.inventory_ground_empty"), UiTheme.Muted)
                : UiTheme.Colour(Lang.Get("@ui.inventory_ground_pile"), UiTheme.Accent);
        }

        // ------------------------------------------------------------------ ground list (T-151 playtest)

        const float GroundRowPx = 56f;
        RectTransform _groundList;
        string _groundKey;

        /// <summary>Rows in the ground list right now (tests).</summary>
        public int GroundRowCount => _groundList == null ? 0 : _groundList.GetComponentsInChildren<GroundItemView>().Length;

        void RefreshGroundList(LootPile pile)
        {
            if (_groundList == null || GridView.Dragging) return; // rows rebuilt mid-drag would drop the drag
            var key = pile == null ? "" : $"{pile.GetHashCode()}:{string.Join(",", pile.Items.Select(i => i.Item.Id.Value + "×" + i.Count))}";
            if (key == _groundKey) return;
            _groundKey = key;
            foreach (Transform child in _groundList) Destroy(child.gameObject);
            if (pile == null) return;
            var width = _groundList.sizeDelta.x;
            for (var i = 0; i < pile.Items.Count; i++)
            {
                var entry = pile.Items[i];
                var row = new GameObject($"Ground_{i}", typeof(RectTransform), typeof(Image), typeof(GroundItemView));
                var rect = (RectTransform)row.transform;
                rect.SetParent(_groundList, false);
                Place(rect, 0f, -i * GroundRowPx, width, GroundRowPx - 4f);
                var image = row.GetComponent<Image>();
                image.sprite = UiTheme.Sprite(UiTheme.Inset, UiTheme.InsetEdge, 8);
                image.type = Image.Type.Sliced;
                row.GetComponent<GroundItemView>().Bind(this, i, entry);

                var icon = NewRect("Icon", rect);
                Place(icon, 4f, -4f, GroundRowPx - 12f, GroundRowPx - 12f);
                var art = icon.gameObject.AddComponent<Image>();
                art.sprite = Isle.UI.Art.ItemIcons.Sprite(entry.Item);
                art.preserveAspect = true;
                art.raycastTarget = false;
                var label = NewText("Name", rect, $"{ItemText.Name(entry.Item)}{(entry.Count > 1 ? $"  ×{entry.Count}" : "")}", 15, FontStyle.Normal, UiTheme.Text, TextAnchor.MiddleLeft);
                Place(label, GroundRowPx, 0f, width - GroundRowPx - 6f, GroundRowPx - 4f);
            }
        }

        /// <summary>Right-click on a ground item: pick it up (wherever it fits), or into a chosen bag.</summary>
        public void OpenGroundMenu(int index, LootEntry entry)
        {
            CloseMenu();
            var options = new List<(string Label, System.Action Act)>
            {
                (Lang.Get("@ui.ctx_pick_up"), () => TakeGround(index, entry, -1, default)),
            };
            foreach (var grid in _grids)
            {
                var target = grid;
                var spot = target.Inventory.FindPlacementSpot(entry.Item, false);
                if (spot == null) continue;
                options.Add((string.Format(Lang.Get("@ui.ctx_move_to"), Lang.Get(target.ContainerIndex == 0 ? "@ui.bag" : BagName(target.Inventory))),
                    () => TakeGround(index, entry, target.ContainerIndex, spot.Value.position, spot.Value.rotated)));
            }
            OpenMenu(options);
        }

        /// <summary>Asks the server for one ground item — into a container at a cell, or anywhere (container &lt; 0).</summary>
        public void TakeGround(int index, LootEntry entry, int container, Isle.Core.Vec2Int cell, bool rotated = false)
        {
            _network.RequestTakeFromGround(index, entry.Item.Id.Value, container, cell, rotated, _ =>
            {
                _groundKey = null;
                foreach (var grid in _grids) grid.Redraw();
            });
        }

        /// <summary>Test hook: opens the ground menu for row <paramref name="index"/> and returns its labels.</summary>
        public List<string> OpenGroundMenuFor(int index)
        {
            var row = _groundList.GetComponentsInChildren<GroundItemView>().First(r => r.Index == index);
            OpenGroundMenu(row.Index, row.Entry);
            return _menu == null ? new List<string>() : _menu.GetComponentsInChildren<Text>().Select(t => t.text).ToList();
        }

        // ------------------------------------------------------------------ helpers

        string BagKeys() => string.Join(",", EquipSlots.All.Select(s => _network.Slots.BagFor(s) != null ? _network.Slots.Get(s).Id.Value : "-"));

        long Signature(List<GridInventory> containers)
        {
            unchecked
            {
                long h = 17;
                foreach (var c in containers)
                {
                    h = h * 31 + c.Placements.Count;
                    foreach (var p in c.Placements)
                        h = h * 31 + (p.Item.Id.Value?.GetHashCode() ?? 0) + p.Position.X * 7 + p.Position.Y * 131 + p.Count * 1009 + (p.Rotated ? 1 : 0);
                }
                foreach (var slot in EquipSlots.All) h = h * 31 + (_network.Slots.Get(slot)?.Id.Value?.GetHashCode() ?? 0);
                if (Campfire != null)
                    foreach (var p in Campfire.Grid.Placements) h = h * 31 + (p.Item.Id.Value?.GetHashCode() ?? 0) + p.Position.X * 7 + p.Position.Y * 131 + p.Count * 1009;
                return h;
            }
        }

        string BagName(GridInventory container)
        {
            foreach (var slot in EquipSlots.All)
                if (_network.Slots.BagFor(slot) == container) return _network.Slots.Get(slot).Name;
            return "@ui.bag";
        }

        // ------------------------------------------------------------------ right-click menu (T-151 playtest)

        GameObject _menu;

        /// <summary>Right-click on a bag item: a small menu of what it can do — eat/drink, wear, wield, place or plant; move
        /// to each other bag; drop. Clicking anywhere else closes it.</summary>
        void OnItemActivated(GridView view, Placement placement)
        {
            if (PrototypeHud.Instance == null) return;
            if (view.Campfire != null)
            {
                CloseMenu();
                OpenMenu(new List<(string Label, System.Action Act)>
                {
                    (Lang.Get("@ui.ctx_take_out"), () => PlayerInteraction.Local?.RequestTakeFuel(placement.Position, -1, default)),
                });
                return;
            }
            if (view.Network != _network) return;
            CloseMenu();
            var options = new List<(string Label, System.Action Act)>();
            var item = placement.Item;
            if (Campfire != null && item.FuelMinutes > 0f)
                options.Add((Lang.Get("@ui.ctx_put_on_fire"), () => PlayerInteraction.Local?.RequestPutFuel(view.ContainerIndex, placement.Position, placement.Count)));
            var use = UseLabel(item);
            if (use != null)
                options.Add((Lang.Get(use), () =>
                {
                    PrototypeHud.Instance.Use(item);
                    if (item.Places.IsValid) Open = false; // placing happens in the world
                }));
            foreach (var other in _grids)
            {
                if (other == view) continue;
                var target = other;
                options.Add((string.Format(Lang.Get("@ui.ctx_move_to"), Lang.Get(target.ContainerIndex == 0 ? "@ui.bag" : BagName(target.Inventory))), () =>
                    _network.RequestQuickMove(view.ContainerIndex, placement.Position, target.ContainerIndex, _ =>
                    {
                        view.Redraw();
                        target.Redraw();
                    })));
            }
            options.Add((Lang.Get("@ui.ctx_drop"), () => _network.RequestDrop(view.ContainerIndex, placement.Position, placement.Count, _ => view.Redraw())));
            OpenMenu(options);
        }

        /// <summary>The verb for using an item, or null when it has none (materials).</summary>
        public static string UseLabel(Isle.Data.ItemDef item)
        {
            if (item.Medicine != null) return "@ui.ctx_tend";
            if (item.Plants.IsValid) return "@ui.ctx_plant";
            if (item.Places.IsValid) return "@ui.ctx_place";
            if (!string.IsNullOrEmpty(item.EquipSlot))
                return item.EquipSlot is "main_hand" or "off_hand" ? "@ui.ctx_wield" : "@ui.ctx_wear";
            if (item.Consume != null || (item.Tags != null && System.Array.IndexOf(item.Tags, "drink") >= 0)) return "@ui.ctx_drink";
            if (item.Nutrition != null) return "@ui.ctx_eat";
            return null;
        }

        void OpenMenu(List<(string Label, System.Action Act)> options)
        {
            const float width = 200f, row = 34f;
            _menu = NewRect("ItemMenu", _root).gameObject;
            var blockerRect = (RectTransform)_menu.transform;
            blockerRect.anchorMin = Vector2.zero;
            blockerRect.anchorMax = Vector2.one;
            blockerRect.offsetMin = blockerRect.offsetMax = Vector2.zero;
            _menu.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f); // catches the click that closes the menu
            _menu.AddComponent<Button>().onClick.AddListener(CloseMenu);

            var panel = NewRect("Panel", blockerRect);
            var height = options.Count * row + 12f;
            var mouse = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(blockerRect, mouse, null, out var local);
            var half = blockerRect.rect.size * 0.5f;
            local.x = Mathf.Min(local.x + 4f, half.x - width - 4f);
            local.y = Mathf.Max(local.y - 4f, -half.y + height + 4f);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = local;
            panel.sizeDelta = new Vector2(width, height);
            var image = panel.gameObject.AddComponent<Image>();
            image.sprite = UiTheme.Sprite(UiTheme.Panel, UiTheme.Accent, 8, 2);
            image.type = Image.Type.Sliced;

            for (var i = 0; i < options.Count; i++)
            {
                var (label, act) = options[i];
                var button = NewRect($"Option_{i}", panel);
                Place(button, 6f, -6f - i * row, width - 12f, row - 2f);
                var buttonImage = button.gameObject.AddComponent<Image>();
                buttonImage.sprite = UiTheme.Sprite(UiTheme.Inset, UiTheme.InsetEdge, 6);
                buttonImage.type = Image.Type.Sliced;
                button.gameObject.AddComponent<Button>().onClick.AddListener(() =>
                {
                    CloseMenu();
                    act();
                });
                var text = NewText("Label", button, label, 15, FontStyle.Normal, UiTheme.Text, TextAnchor.MiddleLeft);
                text.rectTransform.anchorMin = Vector2.zero;
                text.rectTransform.anchorMax = Vector2.one;
                text.rectTransform.offsetMin = new Vector2(12f, 0f);
                text.rectTransform.offsetMax = Vector2.zero;
            }
        }

        void CloseMenu()
        {
            if (_menu != null) Destroy(_menu);
            _menu = null;
        }

        /// <summary>Test hook: the labels the menu would show for <paramref name="placement"/> in container
        /// <paramref name="container"/>, after opening it.</summary>
        public List<string> OpenMenuFor(int container, Placement placement)
        {
            OnItemActivated(_grids[container], placement);
            return _menu == null ? new List<string>() : _menu.GetComponentsInChildren<Text>().Select(t => t.text).ToList();
        }

        /// <summary>Test hook: presses the menu option whose label is <paramref name="label"/>.</summary>
        public bool PressMenu(string label)
        {
            if (_menu == null) return false;
            var text = _menu.GetComponentsInChildren<Text>().FirstOrDefault(t => t.text == label);
            if (text == null) return false;
            text.GetComponentInParent<Button>().onClick.Invoke();
            return true;
        }

        void OnSlotActivated(EquipSlotView view)
        {
            if (view.Network != _network || _player == null) return;
            _player.RequestUnequipSlot(view.SlotName);
        }

        static void Heading(RectTransform panel, string text, float x, float y, float width)
        {
            var label = NewText("Heading", panel, text, 19, FontStyle.Bold, UiTheme.Accent, TextAnchor.MiddleLeft);
            label.supportRichText = true;
            Place(label, x, y, width - 2 * x, TitlePx);
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        static Text NewText(string name, Transform parent, string text, int size, FontStyle style, Color colour, TextAnchor anchor)
        {
            var rect = NewRect(name, parent);
            var label = rect.gameObject.AddComponent<Text>();
            label.text = text;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = size;
            label.fontStyle = style;
            label.color = colour;
            label.alignment = anchor;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        static void Place(Component c, float x, float y, float width, float height)
        {
            var rect = (RectTransform)c.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        static void PlaceTop(Component c, float x, float y, float width, float height)
        {
            var rect = (RectTransform)c.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y - height * 0.5f + 20f);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
