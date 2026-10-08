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
    /// dragging, Shift+drag splits, Ctrl+click moves to the next bag, right-click uses (eat, wear, wield, place, plant)
    /// or takes off, and letting go over the backdrop or the ground panel drops the item. Every change is a request to
    /// <see cref="InventoryNetwork"/> (server authority); this screen only draws.
    /// </summary>
    public sealed class InventoryScreen : MonoBehaviour
    {
        // Layout in reference pixels (1920×1080, scaled to the screen) — presentation only.
        const int CellPx = 52;
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
            GridView.ItemActivated += OnItemActivated;
            EquipSlotView.SlotActivated += OnSlotActivated;
        }

        void OnDestroy()
        {
            GridView.ItemActivated -= OnItemActivated;
            EquipSlotView.SlotActivated -= OnSlotActivated;
            if (Instance == this) Instance = null;
            if (_canvas != null) Destroy(_canvas.gameObject);
        }

        /// <summary>Esc closes the inventory first (the pause menu checks this).</summary>
        public void Close() => Open = false;

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.tabKey.wasPressedThisFrame) Open = !Open;

            if (_network == null || !_network.IsOwner) Bind();
            if (_network == null) return;
            if (_canvas.gameObject.activeSelf != Open) _canvas.gameObject.SetActive(Open);
            PointerOverUi = Open;
            if (!Open) return;

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
                }
            }
            if (Time.unscaledTime >= _nextFreshnessAt)
            {
                _nextFreshnessAt = Time.unscaledTime + FreshnessRefreshSeconds;
                foreach (var grid in _grids) grid.RefreshFreshness();
            }
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

            var bagsWidth = Mathf.Max(420f, containers.Max(c => c.Width) * CellPx + 2 * Pad);
            var total = CharacterWidth + bagsWidth + GroundWidth + 2 * PanelGap;
            var x = -total * 0.5f;

            // Top bar.
            var title = NewText("Title", _root, Lang.Get("@ui.inventory_title"), 30, FontStyle.Bold, UiTheme.Text, TextAnchor.MiddleLeft);
            PlaceTop(title, x, -28f, 500f, 40f);
            var hint = NewText("Hint", _root, Lang.Get("@ui.inventory_hint"), 14, FontStyle.Normal, UiTheme.Muted, TextAnchor.MiddleRight);
            PlaceTop(hint, x + total - 1000f, -28f, 1000f, 40f);

            var character = Panel("Character", x, CharacterWidth);
            BuildCharacter(character);
            x += CharacterWidth + PanelGap;

            var bags = Panel("Bags", x, bagsWidth);
            BuildBags(bags, containers, bagsWidth);
            x += bagsWidth + PanelGap;

            var ground = Panel("Ground", x, GroundWidth);
            BuildGround(ground);

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

            var slotPx = CellPx * 2f;
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

            var zone = NewRect("DropArea", panel);
            Place(zone, Pad, -Pad - TitlePx - 8f, width - 2 * Pad, 180f);
            var image = zone.gameObject.AddComponent<Image>();
            image.sprite = UiTheme.Sprite(new Color(0.12f, 0.1f, 0.08f, 0.9f), UiTheme.Accent, 10, 2);
            image.type = Image.Type.Sliced;
            zone.gameObject.AddComponent<DropZone>();
            var label = NewText("Label", zone, Lang.Get("@ui.inventory_drop_here"), 16, FontStyle.Bold, UiTheme.Accent, TextAnchor.MiddleCenter);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;

            _ground = NewText("Nearby", panel, "", 14, FontStyle.Normal, UiTheme.Text, TextAnchor.UpperLeft);
            _ground.supportRichText = true;
            Place(_ground, Pad, -Pad - TitlePx - 200f, width - 2 * Pad, 500f);
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
                $"{Lang.Get("@ui.temperature")}  {_vitals.Temperature:0.0}°   {Lang.Get("@ui.armor")} {_vitals.WornArmor():0}   {Lang.Get("@ui.tt_warmth")} +{_vitals.ClothingBonus:0}°");
        }

        string GroundText()
        {
            var pile = LootPiles.Nearest(_player.transform.position, PlayerInteraction.ReachTiles);
            if (pile == null) return UiTheme.Colour(Lang.Get("@ui.inventory_ground_empty"), UiTheme.Muted);
            var lines = pile.Items.Select(i => $"• {Lang.Get(i.Item.Name)} ×{i.Count}");
            return $"{UiTheme.Colour(Lang.Get("@ui.inventory_ground_pile"), UiTheme.Accent)}\n{string.Join("\n", lines)}";
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
                return h;
            }
        }

        string BagName(GridInventory container)
        {
            foreach (var slot in EquipSlots.All)
                if (_network.Slots.BagFor(slot) == container) return _network.Slots.Get(slot).Name;
            return "@ui.bag";
        }

        void OnItemActivated(GridView view, Placement placement)
        {
            if (view.Network != _network || PrototypeHud.Instance == null) return;
            PrototypeHud.Instance.Use(placement.Item);
            if (placement.Item.Places.IsValid) Open = false; // placing happens in the world
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
