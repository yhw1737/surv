using System.Collections.Generic;
using System.Linq;
using Isle.Core;
using Isle.Data;
using Isle.Gameplay.Building;
using Isle.Gameplay.Character;
using Isle.Gameplay.Inventory;
using Isle.UI.Prototype;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Campfire = Isle.Gameplay.World.CampfireInteraction;

namespace Isle.UI.Inventory
{
    /// <summary>
    /// T-305 SYS-INV-02: the inventory as lists (Tab). Left, you: equipment slots and a tab per carried container (pockets,
    /// each worn bag) with the selected one's list. Right, around you: the ground at your feet, the crate in reach, a
    /// campfire in reach (with its light / put-out controls). Drag rows between lists, onto tabs or equipment slots;
    /// Shift+drag moves one; double-click / Ctrl+click sends a stack across; right-click opens the item's menu; letting go
    /// off the panels drops it. Every move is a request to the server; this screen only draws.
    /// </summary>
    public sealed class InventoryScreen : MonoBehaviour
    {
        // Layout in reference pixels (1920×1080, scaled to the screen) — presentation only.
        const float Pad = 18f;
        const float TitlePx = 30f;
        const float PanelTop = 92f;
        const float PanelGap = 24f;
        const float LeftWidth = 820f;
        const float RightWidth = 620f;
        const float TabWidth = 170f;
        const float TabPx = 58f;
        const int EquipSlotPx = 72;
        const float FireControlsPx = 150f;
        const float RefreshSeconds = 0.5f;

        /// <summary>The key a server request takes for "a fresh entry" in a list container.</summary>
        static readonly Vec2Int AnyKey = new(-1, 0);

        public static InventoryScreen Instance { get; private set; }

        /// <summary>True while the screen is open — it covers everything, so the HUD treats the pointer as captured.</summary>
        public static bool PointerOverUi { get; private set; }

        public bool Open { get; private set; }

        Canvas _canvas;
        RectTransform _root;
        Text _weight;
        InventoryNetwork _network;
        PlayerInteraction _player;
        readonly List<EquipSlotView> _slots = new();
        readonly List<(ListTab Tab, Text Sub)> _containerTabs = new();
        ItemListView _leftList, _rightList;
        Text _fireStatus, _fireButtonLabel;
        string _layoutKey;
        string _contentKey;
        float _nextRefreshAt;
        int _leftSelected;
        ListSource _rightSelected = ListSource.Ground;
        bool _rowDragging;

        void Awake()
        {
            Instance = this;
            ItemTooltip.UseExternal = true;
            GridView.CellSizePx = 64;
            EquipSlotView.SlotSizePx = EquipSlotPx;
            EquipSlotView.SlotActivated += OnSlotActivated;
            Isle.Gameplay.Feedback.GameFeed.CampfireOpened += OnCampfireOpened;
        }

        void OnDestroy()
        {
            EquipSlotView.SlotActivated -= OnSlotActivated;
            Isle.Gameplay.Feedback.GameFeed.CampfireOpened -= OnCampfireOpened;
            if (Instance == this) Instance = null;
            if (_canvas != null) Destroy(_canvas.gameObject);
        }

        // ------------------------------------------------------------------ open / close

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

        /// <summary>The campfire opened with E (its tab is selected on the right), or null.</summary>
        public Campfire Campfire { get; private set; }

        void OnCampfireOpened(object fire) => OpenCampfire(fire as Campfire);

        /// <summary>E at a campfire: the inventory opens with the fire's fuel on the right.</summary>
        public void OpenCampfire(Campfire fire)
        {
            if (fire == null) return;
            Campfire = fire;
            Open = true;
            _rightSelected = ListSource.Campfire;
            _layoutKey = null;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.tabKey.wasPressedThisFrame) Toggle();

            if (_network == null || !_network.IsOwner) Bind();
            if (_network == null) return;
            if (_canvas.gameObject.activeSelf != Open) _canvas.gameObject.SetActive(Open);
            PointerOverUi = Open;
            if (!Open)
            {
                CloseMenu();
                return;
            }

            // Walked away from the fire: it leaves the right side.
            if (Campfire != null && Vector2.Distance(Campfire.transform.position, _player.transform.position) > PlayerInteraction.ReachTiles + 0.5f)
                Campfire = null;
            var containers = _network.Containers();
            if (_leftSelected >= containers.Count) _leftSelected = 0;
            if (!RightTabs().Contains(_rightSelected)) _rightSelected = ListSource.Ground;

            var layout = LayoutKey(containers);
            if (layout != _layoutKey) Rebuild(containers, layout);
            if (_rowDragging) return; // rows rebuilt mid-drag would drop the drag
            // Rows rebuild only when something changed (a rebuild would eat a double-click and blink the tooltip).
            var content = ContentKey(containers);
            if (content != _contentKey)
            {
                _contentKey = content;
                RefreshLists(containers);
            }
            if (Time.unscaledTime >= _nextRefreshAt)
            {
                _nextRefreshAt = Time.unscaledTime + RefreshSeconds;
                _weight.text = WeightText(containers);
                RefreshFire();
            }
        }

        void Bind()
        {
            _network = null;
            foreach (var player in PlayerInteraction.All)
            {
                if (!player.IsOwner || !player.TryGetComponent(out InventoryNetwork network)) continue;
                _network = network;
                _player = player;
                break;
            }
            if (_network == null) return;
            EnsureCanvas();
            _layoutKey = null;
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

        // ------------------------------------------------------------------ what's around

        StorageBox NearbyCrate() => _player != null ? StructureFactory.NearestStorage(_player.transform.position, PlayerInteraction.ReachTiles) : null;

        Campfire NearbyFire() =>
            Campfire != null ? Campfire
            : _player != null && _player.NearestCampfire() is { } fire && fire.TryGetComponent<Campfire>(out var burner) ? burner : null;

        LootPile NearbyPile() => _player != null ? LootPiles.Nearest(_player.transform.position, PlayerInteraction.ReachTiles) : null;

        IEnumerable<ListSource> RightTabs()
        {
            yield return ListSource.Ground;
            if (NearbyCrate() != null) yield return ListSource.Crate;
            if (NearbyFire() != null) yield return ListSource.Campfire;
        }

        string LayoutKey(List<GridInventory> containers) =>
            $"{containers.Count}|{BagKeys()}|{_leftSelected}|{_rightSelected}|{string.Join(",", RightTabs())}|{UnityEngine.Screen.width}x{UnityEngine.Screen.height}";

        string ContentKey(List<GridInventory> containers)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var c in containers) sb.Append(Signature(c.Placements)).Append(';');
            foreach (var slot in EquipSlots.All) sb.Append(_network.Slots.Get(slot)?.Id.Value).Append(',');
            if (NearbyPile() is { } pile)
                foreach (var e in pile.Items) sb.Append(e.Item.Id.Value).Append('×').Append(e.Count).Append(',');
            sb.Append(';');
            if (NearbyCrate() is { } crate) sb.Append(Signature(crate.Contents.Placements));
            sb.Append(';');
            if (NearbyFire() is { } fire) sb.Append(Signature(fire.Grid.Placements));
            return sb.ToString();
        }

        static long Signature(IReadOnlyList<Placement> placements)
        {
            unchecked
            {
                long h = 17;
                foreach (var p in placements)
                    h = h * 31 + (p.Item.Id.Value?.GetHashCode() ?? 0) + p.Position.X * 7 + p.Position.Y * 131 + p.Count * 1009 + (p.Wear?.Current ?? 0);
                return h;
            }
        }

        string BagKeys() => string.Join(",", EquipSlots.All.Select(s => _network.Slots.BagFor(s) != null ? _network.Slots.Get(s).Id.Value : "-"));

        // ------------------------------------------------------------------ building

        void Rebuild(List<GridInventory> containers, string layout)
        {
            _menu = null;
            foreach (Transform child in _root) Destroy(child.gameObject);
            _slots.Clear();
            _containerTabs.Clear();
            _fireStatus = _fireButtonLabel = null;
            _layoutKey = layout;
            _contentKey = null;
            _nextRefreshAt = 0f;

            // Backdrop: dims the world, and letting a row go on it drops the item.
            var backdrop = NewRect("Backdrop", _root);
            backdrop.anchorMin = Vector2.zero;
            backdrop.anchorMax = Vector2.one;
            backdrop.offsetMin = backdrop.offsetMax = Vector2.zero;
            backdrop.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.025f, 0.02f, 0.78f);
            backdrop.gameObject.AddComponent<DropZone>();

            var total = LeftWidth + RightWidth + PanelGap;
            var x = -total * 0.5f;
            var title = NewText("Title", _root, Lang.Get("@ui.inventory_title"), 30, FontStyle.Bold, UiTheme.Text, TextAnchor.MiddleLeft);
            PlaceTop(title, x, -28f, 400f, 40f);
            var hint = NewText("Hint", _root, Lang.Get("@ui.inventory_hint"), 14, FontStyle.Normal, UiTheme.Muted, TextAnchor.MiddleRight);
            PlaceTop(hint, x + total - 1040f, -28f, 1040f, 40f);

            BuildLeft(Panel("Left", x, LeftWidth), containers);
            BuildRight(Panel("Right", x + LeftWidth + PanelGap, RightWidth));
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

        void BuildLeft(RectTransform panel, List<GridInventory> containers)
        {
            var width = panel.sizeDelta.x;
            Heading(panel, Lang.Get("@ui.inventory_you"), Pad, -Pad, width);
            _weight = NewText("Weight", panel, "", 14, FontStyle.Normal, UiTheme.Muted, TextAnchor.MiddleRight);
            _weight.supportRichText = true;
            Place(_weight, Pad, -Pad, width - 2 * Pad, TitlePx);

            // Equipment: one row of slots, their names under them.
            var slotY = -Pad - TitlePx - 8f;
            var count = EquipSlots.All.Length;
            var gap = (width - 2 * Pad - count * EquipSlotPx) / Mathf.Max(1, count - 1);
            for (var i = 0; i < count; i++)
            {
                var slotName = EquipSlots.All[i];
                var sx = Pad + i * (EquipSlotPx + gap);
                var go = new GameObject($"Slot_{slotName}", typeof(RectTransform), typeof(Image), typeof(EquipSlotView));
                var rect = (RectTransform)go.transform;
                rect.SetParent(panel, false);
                Place(rect, sx, slotY, EquipSlotPx, EquipSlotPx);
                var view = go.GetComponent<EquipSlotView>();
                view.Network = _network;
                view.Bind(_network.Slots, slotName);
                var image = go.GetComponent<Image>();
                image.sprite = UiTheme.Sprite(UiTheme.Inset, UiTheme.InsetEdge, 10);
                image.type = Image.Type.Sliced;
                _slots.Add(view);
                var label = NewText("Label", panel, Lang.Get("@ui.slot." + slotName), 12, FontStyle.Normal, UiTheme.Muted, TextAnchor.UpperCenter);
                Place(label, sx - 8f, slotY - EquipSlotPx - 2f, EquipSlotPx + 16f, 18f);
            }

            // Carried containers: a tab each down the side, the selected one's list beside them.
            var top = slotY - EquipSlotPx - 30f;
            for (var i = 0; i < containers.Count; i++)
            {
                var index = i;
                var sub = Tab(panel, ListSource.Container, i, Pad, top - i * (TabPx + 6f), TabWidth, ContainerName(containers, i), CapacityText(containers[i]),
                    i == _leftSelected, () => SelectLeft(index));
                _containerTabs.Add((sub.GetComponentInParent<ListTab>(), sub));
            }
            var listX = Pad + TabWidth + 10f;
            _leftList = List(panel, ListSource.Container, _leftSelected, listX, top, width - listX - Pad, panel.sizeDelta.y + top - Pad);
        }

        void BuildRight(RectTransform panel)
        {
            var width = panel.sizeDelta.x;
            Heading(panel, Lang.Get("@ui.inventory_around"), Pad, -Pad, width);
            var tabs = RightTabs().ToList();
            var tabWidth = (width - 2 * Pad - 8f * (tabs.Count - 1)) / tabs.Count;
            var tabY = -Pad - TitlePx - 6f;
            for (var i = 0; i < tabs.Count; i++)
            {
                var source = tabs[i];
                Tab(panel, source, 0, Pad + i * (tabWidth + 8f), tabY, tabWidth, Lang.Get("@ui.list_" + source.ToString().ToLowerInvariant()), null,
                    source == _rightSelected, () => SelectRight(source));
            }
            var listTop = tabY - TabPx - 10f;
            var listHeight = panel.sizeDelta.y + listTop - Pad;
            if (_rightSelected == ListSource.Campfire)
            {
                listHeight -= FireControlsPx + 10f;
                BuildFireControls(panel, listTop - listHeight - 10f, width);
            }
            _rightList = List(panel, _rightSelected, 0, Pad, listTop, width - 2 * Pad, listHeight);
        }

        ItemListView List(RectTransform panel, ListSource source, int index, float x, float y, float width, float height)
        {
            var frame = NewRect($"List_{source}_{index}", panel);
            Place(frame, x, y, width, height);
            var image = frame.gameObject.AddComponent<Image>();
            image.sprite = UiTheme.Sprite(UiTheme.Inset, UiTheme.InsetEdge, 8);
            image.type = Image.Type.Sliced;
            frame.gameObject.AddComponent<RectMask2D>();
            var content = NewRect("Content", frame);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0f, 1f);
            content.anchoredPosition = new Vector2(4f, -4f);
            content.sizeDelta = new Vector2(width - 8f, 0f);
            var scroll = frame.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            var view = frame.gameObject.AddComponent<ItemListView>();
            view.Bind(this, source, index, content);
            return view;
        }

        /// <summary>A tab button; returns its second line (or its name when it has none).</summary>
        Text Tab(RectTransform panel, ListSource source, int index, float x, float y, float width, string name, string sub, bool selected, System.Action onClick)
        {
            var rect = NewRect($"Tab_{source}_{index}", panel);
            Place(rect, x, y, width, TabPx);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = selected ? UiTheme.Sprite(Color.Lerp(UiTheme.Inset, UiTheme.Accent, 0.35f), UiTheme.Accent, 8, 2) : UiTheme.Sprite(UiTheme.Inset, UiTheme.InsetEdge, 8);
            image.type = Image.Type.Sliced;
            rect.gameObject.AddComponent<Button>().onClick.AddListener(() => onClick());
            var tab = rect.gameObject.AddComponent<ListTab>();
            tab.Source = source;
            tab.ContainerIndex = index;
            var label = NewText("Name", rect, name, 15, FontStyle.Bold, UiTheme.Text, sub == null ? TextAnchor.MiddleCenter : TextAnchor.UpperCenter);
            Fill(label.rectTransform, 4f, sub == null ? 0f : 8f);
            if (sub == null) return label;
            var detail = NewText("Sub", rect, sub, 12, FontStyle.Normal, UiTheme.Muted, TextAnchor.LowerCenter);
            Fill(detail.rectTransform, 4f, 7f);
            return detail;
        }

        void BuildFireControls(RectTransform panel, float y, float width)
        {
            var box = NewRect("FireControls", panel);
            var inner = width - 2 * Pad;
            Place(box, Pad, y, inner, FireControlsPx);
            _fireStatus = NewText("FireStatus", box, "", 15, FontStyle.Normal, UiTheme.Text, TextAnchor.UpperLeft);
            _fireStatus.supportRichText = true;
            Place(_fireStatus, 0f, 0f, inner, 44f);
            var half = (inner - 10f) * 0.5f;
            _fireButtonLabel = Button(box, "FireToggle", 0f, -50f, half, () =>
            {
                if (_player == null || NearbyFire() is not { } fire) return;
                if (fire.Lit) _player.RequestExtinguish();
                else _player.RequestLightFire();
            });
            Button(box, "FireCook", half + 10f, -50f, half, () =>
            {
                Close();
                if (PrototypeHud.Instance != null && !PrototypeHud.Instance.IsToolOpen("cook")) PrototypeHud.Instance.ToggleTool("cook");
            }).text = Lang.Get("@ui.fire_cook");
            var hint = NewText("FireHint", box, Lang.Get("@ui.fire_hint"), 13, FontStyle.Normal, UiTheme.Muted, TextAnchor.UpperLeft);
            Place(hint, 0f, -100f, inner, 44f);
        }

        // ------------------------------------------------------------------ content

        void RefreshLists(List<GridInventory> containers)
        {
            foreach (var slot in _slots) slot.Redraw();
            if (_leftList != null && _leftSelected < containers.Count) _leftList.Refresh(Entries(containers[_leftSelected]));
            if (_rightList != null) _rightList.Refresh(RightEntries());
            foreach (var (tab, sub) in _containerTabs)
                if (tab != null && sub != null && tab.ContainerIndex < containers.Count) sub.text = CapacityText(containers[tab.ContainerIndex]);
            _weight.text = WeightText(containers);
            RefreshFire();
        }

        static List<ListEntry> Entries(GridInventory container) =>
            container.Placements.Select(p => new ListEntry(p.Item, p.Count, p.Wear, p.Position, -1)).ToList();

        List<ListEntry> RightEntries()
        {
            switch (_rightSelected)
            {
                case ListSource.Ground:
                    return NearbyPile() is { } pile ? pile.Items.Select((e, i) => new ListEntry(e.Item, e.Count, e.Wear, default, i)).ToList() : new List<ListEntry>();
                case ListSource.Crate:
                    return NearbyCrate() is { } crate ? Entries(crate.Contents) : new List<ListEntry>();
                case ListSource.Campfire:
                    return NearbyFire() is { } fire ? Entries(fire.Grid) : new List<ListEntry>();
                default:
                    return new List<ListEntry>();
            }
        }

        void RefreshFire()
        {
            if (_fireStatus == null || NearbyFire() is not { } fire) return;
            var minutes = Mathf.CeilToInt(fire.TotalFuelMinutes);
            var state = fire.Lit ? UiTheme.Colour(Lang.Get("@ui.fire_lit"), UiTheme.Accent) : UiTheme.Colour(Lang.Get("@ui.fire_out"), UiTheme.Muted);
            _fireStatus.text = $"{state}   {CapacityText(fire.Grid)}\n{string.Format(Lang.Get("@ui.fire_fuel"), minutes / 60, minutes % 60)}";
            _fireButtonLabel.text = Lang.Get(fire.Lit ? "@ui.extinguish" : "@ui.fire_light");
        }

        static string CapacityText(GridInventory c) =>
            c.IsList ? $"{c.TotalWeightKg():0.#} / {c.CapacityKg:0.#} kg" : $"{c.TotalWeightKg():0.#} kg";

        string ContainerName(List<GridInventory> containers, int index) => index == 0 ? Lang.Get("@ui.pockets") : Lang.Get(BagName(containers[index]));

        string WeightText(List<GridInventory> containers)
        {
            var weight = containers.Sum(c => c.TotalWeightKg()) + EquipSlots.All.Select(s => _network.Slots.Get(s)).Where(i => i != null).Sum(i => i.Weight);
            // Past the free weight you slow down; past the max you can't roll and stamina stops recovering.
            var colour = weight > WeightCalculator.MaxWeightKg ? UiTheme.Bad : weight > WeightCalculator.FreeWeightKg ? UiTheme.Accent : UiTheme.Muted;
            return UiTheme.Colour(string.Format(Lang.Get("@ui.weight_line"), weight.ToString("0.0"), WeightCalculator.FreeWeightKg.ToString("0"), WeightCalculator.MaxWeightKg.ToString("0")), colour);
        }

        string BagName(GridInventory container)
        {
            foreach (var slot in EquipSlots.All)
                if (_network.Slots.BagFor(slot) == container) return _network.Slots.Get(slot).Name;
            return "@ui.bag";
        }

        // ------------------------------------------------------------------ moving

        internal void BeginRowDrag() => _rowDragging = true;

        /// <summary>A row let go: onto an equipment slot, a tab, a list, or off the panels (drop).</summary>
        internal void EndRowDrag(ItemListView from, ListEntry entry, int count, PointerEventData eventData)
        {
            _rowDragging = false;
            _contentKey = null;
            if (from == null || eventData == null) return;
            foreach (var hovered in eventData.hovered)
            {
                if (hovered == null) continue;
                if (hovered.GetComponentInParent<EquipSlotView>() is { } slot)
                {
                    if (from.Source == ListSource.Container) _network.RequestEquipFrom(from.ContainerIndex, entry.Key, slot.SlotName, Done);
                    return;
                }
                if (hovered.GetComponentInParent<ListTab>() is { } tab)
                {
                    Transfer(from, entry, count, tab.Source, tab.ContainerIndex);
                    return;
                }
                if (hovered.GetComponentInParent<ItemListView>() is { } list)
                {
                    if (list != from) Transfer(from, entry, count, list.Source, list.ContainerIndex);
                    return;
                }
            }
            foreach (var hovered in eventData.hovered)
                if (hovered != null && hovered.GetComponent<DropZone>() != null)
                {
                    Transfer(from, entry, count, ListSource.Ground, 0);
                    return;
                }
        }

        /// <summary>Double-click / Ctrl+click: the whole stack to the other side's selected list.</summary>
        public void QuickMove(ItemListView from, ListEntry entry)
        {
            if (from.Source == ListSource.Container) Transfer(from, entry, entry.Count, _rightSelected, 0);
            else Transfer(from, entry, entry.Count, ListSource.Container, _leftSelected);
        }

        /// <summary>T-305: one move between two lists, as the matching server request. A pair with no request (ground to
        /// crate) does nothing.</summary>
        public void Transfer(ItemListView from, ListEntry entry, int count, ListSource to, int toIndex)
        {
            if (_player == null || _network == null) return;
            count = Mathf.Clamp(count, 1, Mathf.Max(1, entry.Count));
            switch (from.Source, to)
            {
                case (ListSource.Container, ListSource.Container):
                    if (from.ContainerIndex != toIndex) _network.RequestTransfer(from.ContainerIndex, entry.Key, toIndex, count, Done);
                    break;
                case (ListSource.Container, ListSource.Ground):
                    _network.RequestDrop(from.ContainerIndex, entry.Key, count, Done);
                    break;
                case (ListSource.Container, ListSource.Crate):
                    _player.RequestCrateStore(from.ContainerIndex, entry.Key, count);
                    break;
                case (ListSource.Container, ListSource.Campfire):
                    _player.RequestPutFuel(from.ContainerIndex, entry.Key, count);
                    break;
                case (ListSource.Ground, ListSource.Container):
                    _network.RequestTakeFromGround(entry.Index, entry.Item.Id.Value, toIndex, AnyKey, false, Done);
                    break;
                case (ListSource.Crate, ListSource.Container):
                    _player.RequestCrateTake(entry.Key, toIndex, count);
                    break;
                case (ListSource.Campfire, ListSource.Container):
                    _player.RequestTakeFuel(entry.Key, toIndex, AnyKey);
                    break;
            }
            _contentKey = null;
        }

        void Done(bool _) => _contentKey = null;

        void OnSlotActivated(EquipSlotView view)
        {
            if (view.Network != _network || _player == null) return;
            _player.RequestUnequipSlot(view.SlotName);
        }

        // ------------------------------------------------------------------ right-click menu

        GameObject _menu;

        /// <summary>Right-click on a row: use it; move it to each other container, the crate or the fire; drop it — or, on
        /// the right side, pick it up / take it out.</summary>
        public void OpenEntryMenu(ItemListView view, ListEntry entry)
        {
            CloseMenu();
            var options = new List<(string Label, System.Action Act)>();
            var item = entry.Item;
            if (view.Source == ListSource.Container)
            {
                if (UseLabel(item) is { } use && PrototypeHud.Instance != null)
                    options.Add((Lang.Get(use), () =>
                    {
                        PrototypeHud.Instance.Use(item);
                        if (item.Places.IsValid) Close(); // placing happens in the world
                    }));
                var containers = _network.Containers();
                for (var i = 0; i < containers.Count; i++)
                {
                    if (i == view.ContainerIndex) continue;
                    var target = i;
                    options.Add((string.Format(Lang.Get("@ui.ctx_move_to"), ContainerName(containers, i)), () => Transfer(view, entry, entry.Count, ListSource.Container, target)));
                }
                if (NearbyCrate() != null)
                    options.Add((string.Format(Lang.Get("@ui.ctx_move_to"), Lang.Get("@ui.list_crate")), () => Transfer(view, entry, entry.Count, ListSource.Crate, 0)));
                if (NearbyFire() != null && item.FuelMinutes > 0f)
                    options.Add((Lang.Get("@ui.ctx_put_on_fire"), () => Transfer(view, entry, entry.Count, ListSource.Campfire, 0)));
                options.Add((Lang.Get("@ui.ctx_drop"), () => Transfer(view, entry, entry.Count, ListSource.Ground, 0)));
            }
            else
            {
                options.Add((Lang.Get(view.Source == ListSource.Ground ? "@ui.ctx_pick_up" : "@ui.ctx_take_out"),
                    () => Transfer(view, entry, entry.Count, ListSource.Container, _leftSelected)));
                // A kit can be set up straight from the ground (the heaviest outweigh most bags).
                if (view.Source == ListSource.Ground && item.Places.IsValid && !item.Plants.IsValid && PrototypeHud.Instance != null)
                    options.Insert(0, (Lang.Get("@ui.ctx_place"), () =>
                    {
                        PrototypeHud.Instance.Use(item);
                        Close();
                    }));
            }
            OpenMenu(options);
        }

        /// <summary>The verb for using an item, or null when it has none (materials).</summary>
        public static string UseLabel(ItemDef item)
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
            const float width = 220f, row = 34f;
            _menu = NewRect("ItemMenu", _root).gameObject;
            var blockerRect = (RectTransform)_menu.transform;
            blockerRect.anchorMin = Vector2.zero;
            blockerRect.anchorMax = Vector2.one;
            blockerRect.offsetMin = blockerRect.offsetMax = Vector2.zero;
            _menu.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f); // catches the click that closes the menu
            _menu.AddComponent<Button>().onClick.AddListener(CloseMenu);

            var panel = NewRect("Panel", blockerRect);
            var height = options.Count * row + 12f;
            var mouse = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(UnityEngine.Screen.width * 0.5f, UnityEngine.Screen.height * 0.5f);
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
                Fill(text.rectTransform, 12f, 0f);
            }
        }

        void CloseMenu()
        {
            if (_menu != null) Destroy(_menu);
            _menu = null;
        }

        // ------------------------------------------------------------------ selection and test hooks

        /// <summary>Shows carried container <paramref name="container"/> on the left.</summary>
        public void SelectLeft(int container)
        {
            _leftSelected = container;
            _layoutKey = null;
        }

        /// <summary>Shows a list on the right, if it's around.</summary>
        public void SelectRight(ListSource source)
        {
            _rightSelected = source;
            _layoutKey = null;
        }

        public ItemListView LeftList => _leftList;
        public ItemListView RightList => _rightList;
        public ListSource RightSelected => _rightSelected;
        public int LeftSelected => _leftSelected;

        /// <summary>Rows on the right while the ground is shown (tests).</summary>
        public int GroundRowCount => _rightSelected == ListSource.Ground && _rightList != null ? _rightList.RowCount : 0;

        /// <summary>Opens the menu for the left list's row holding <paramref name="item"/> and returns its labels.</summary>
        public List<string> OpenMenuFor(ItemDef item)
        {
            var row = _leftList == null ? null : _leftList.GetComponentsInChildren<ItemRow>().FirstOrDefault(r => r.Entry.Item == item);
            if (row == null) return new List<string>();
            OpenEntryMenu(_leftList, row.Entry);
            return MenuLabels();
        }

        /// <summary>Opens the menu for the right list's row <paramref name="index"/> and returns its labels.</summary>
        public List<string> OpenRightMenuFor(int index)
        {
            var rows = _rightList == null ? new ItemRow[0] : _rightList.GetComponentsInChildren<ItemRow>();
            if (index >= rows.Length) return new List<string>();
            OpenEntryMenu(_rightList, rows[index].Entry);
            return MenuLabels();
        }

        List<string> MenuLabels() => _menu == null ? new List<string>() : _menu.GetComponentsInChildren<Text>().Select(t => t.text).ToList();

        /// <summary>Presses the menu option whose label is <paramref name="label"/>.</summary>
        public bool PressMenu(string label)
        {
            if (_menu == null) return false;
            var text = _menu.GetComponentsInChildren<Text>().FirstOrDefault(t => t.text == label);
            if (text == null) return false;
            text.GetComponentInParent<Button>().onClick.Invoke();
            return true;
        }

        /// <summary>Presses the campfire's light / put-out button.</summary>
        public void PressFireToggle() => _fireButtonLabel?.GetComponentInParent<Button>()?.onClick.Invoke();

        // ------------------------------------------------------------------ helpers

        static void Heading(RectTransform panel, string text, float x, float y, float width)
        {
            var label = NewText("Heading", panel, text, 19, FontStyle.Bold, UiTheme.Accent, TextAnchor.MiddleLeft);
            label.supportRichText = true;
            Place(label, x, y, width - 2 * x, TitlePx);
        }

        Text Button(RectTransform panel, string name, float x, float y, float width, System.Action onClick)
        {
            var rect = NewRect(name, panel);
            Place(rect, x, y, width, 40f);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = UiTheme.Sprite(UiTheme.Inset, UiTheme.Accent, 8, 2);
            image.type = Image.Type.Sliced;
            rect.gameObject.AddComponent<Button>().onClick.AddListener(() => onClick());
            var label = NewText("Label", rect, "", 16, FontStyle.Bold, UiTheme.Text, TextAnchor.MiddleCenter);
            Fill(label.rectTransform, 0f, 0f);
            return label;
        }

        static void Fill(RectTransform rect, float padX, float padY)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padX, padY);
            rect.offsetMax = new Vector2(-padX, -padY);
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
