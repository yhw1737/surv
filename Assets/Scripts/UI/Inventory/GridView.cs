using System.Collections.Generic;
using Isle.Core;
using Isle.Core.Util;
using Isle.Gameplay.Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace Isle.UI.Inventory
{
    /// <summary>
    /// SYS-INV-01 §Required UX. Renders one <see cref="GridInventory"/> as placeholder cells + item
    /// icons, drawn at runtime via <see cref="PlaceholderVisuals"/> — no baked prefabs or art
    /// (ART_PIPELINE §Placeholders, Absolute Rule 7).
    /// <para>
    /// <see cref="Vec2Int.Y"/> increases downward in the pure model; rendered top-to-bottom here.
    /// Presentation-only — the data model has no notion of "up".
    /// </para>
    /// <para>
    /// <see cref="_cellLayer"/>/<see cref="_itemLayer"/> can be wired to hand-placed RectTransforms
    /// (e.g. a future art-pass prefab) via the Inspector; left unassigned, <see cref="Bind"/> builds
    /// them itself with a top-left pivot/anchor — see <c>InventoryDemo</c> for the runtime setup this
    /// needs (Canvas, EventSystem) and the manual test checklist for what to do with it.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class GridView : MonoBehaviour
    {
        // ART_PIPELINE §Style: one tile = 32px, same constant PlaceholderIcons uses for icons.
        // ponytail: placeholder dimension (Absolute Rule 7), not a spec value.
        public static int CellSizePx { get; set; } = 32;

        public GridInventory Inventory { get; private set; }

        /// <summary>The other grid Ctrl+click / move-all send items to (e.g. a bag ↔ the crate UI
        /// it was opened next to). Null when this view has no counterpart open.</summary>
        public GridView PairedView { get; set; }

        /// <summary>Non-null when this view is bound to a player's server-authoritative bag (T-045)
        /// — <see cref="DragHandler"/> routes mutations through it instead of touching
        /// <see cref="Inventory"/> directly. Null for local-only views (warehouse, demo crates).</summary>
        public InventoryNetwork Network { get; set; }

        /// <summary>With <see cref="Network"/>: this grid's index in <see cref="InventoryNetwork.Containers"/>.</summary>
        public int ContainerIndex { get; set; }

        /// <summary>Right-click on an item: the screen decides what "use" means (eat, wear, place…).</summary>
        public static event System.Action<GridView, Placement> ItemActivated;

        internal static void RaiseItemActivated(GridView view, Placement placement) => ItemActivated?.Invoke(view, placement);

        /// <summary>SYS-INV-01 §Required UX: auto-sort is warehouse-only, bags stay manual.</summary>
        public bool IsWarehouse { get; set; }

        [SerializeField] RectTransform _cellLayer;
        [SerializeField] RectTransform _itemLayer;
        [SerializeField] Button _autoSortButton;

        readonly List<GameObject> _spawnedIcons = new();
        readonly List<(Placement Placement, RectTransform Fill, Image FillImage, Text Label)> _freshness = new();

        public void Bind(GridInventory inventory)
        {
            Inventory = inventory;
            EnsureLayers();
            BuildCells();
            Redraw();

            if (IsWarehouse && _autoSortButton == null) _autoSortButton = BuildAutoSortButton();
            if (_autoSortButton != null) _autoSortButton.gameObject.SetActive(IsWarehouse);
        }

        /// <summary>No hand-placed layers (see class remarks) — build our own so this works on a
        /// bare GameObject, not just a hand-wired prefab.</summary>
        void EnsureLayers()
        {
            _cellLayer ??= CreateLayer("Cells");
            _itemLayer ??= CreateLayer("Items");
        }

        RectTransform CreateLayer(string layerName)
        {
            var rect = new GameObject(layerName, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        Button BuildAutoSortButton()
        {
            var go = new GameObject("AutoSortButton", typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(0f, -(Inventory.Height * CellSizePx) - 8f);
            rect.sizeDelta = new Vector2(Inventory.Width * CellSizePx, 20f);
            go.GetComponent<Image>().color = new Color(0.35f, 0.35f, 0.35f); // else Image defaults to opaque white

            // ponytail: Text needs its own child GameObject — Text sharing a GameObject with Image
            // (two Graphics on one object) is what threw the NullReferenceException here before.
            BuildButtonLabel(rect, "Auto-Sort", Color.black);

            var button = go.GetComponent<Button>();
            button.onClick.AddListener(OnAutoSortClicked);
            return button;
        }

        /// <summary>A centred, non-interactive label stretched over a button. Must live on its own
        /// child GameObject — see the comment in <see cref="BuildAutoSortButton"/>.</summary>
        internal static void BuildButtonLabel(RectTransform parent, string text, Color color)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            var label = go.GetComponent<Text>();
            label.text = text;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // ponytail: built-in font, real UI style comes at T-160
            label.fontSize = 12;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = color;
            label.raycastTarget = false;
        }

        void BuildCells()
        {
            foreach (Transform child in _cellLayer) Destroy(child.gameObject);

            var cellSprite = Isle.UI.Prototype.UiTheme.Sprite(new Color(0.15f, 0.13f, 0.11f), new Color(0.27f, 0.23f, 0.19f), 4, 1);

            for (var y = 0; y < Inventory.Height; y++)
            for (var x = 0; x < Inventory.Width; x++)
            {
                var cell = new GameObject($"Cell_{x}_{y}", typeof(RectTransform), typeof(Image));
                var rect = (RectTransform)cell.transform;
                rect.SetParent(_cellLayer, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
                rect.sizeDelta = new Vector2(CellSizePx, CellSizePx);
                rect.anchoredPosition = new Vector2(x * CellSizePx, -y * CellSizePx);
                cell.GetComponent<Image>().sprite = cellSprite;
                cell.GetComponent<Image>().type = Image.Type.Sliced;
            }
        }

        /// <summary>Rebuilds item icons from <see cref="Inventory"/>.Placements. Call after any mutation.</summary>
        public void Redraw()
        {
            foreach (var icon in _spawnedIcons) Destroy(icon);
            _spawnedIcons.Clear();
            _freshness.Clear();

            foreach (var placement in Inventory.Placements)
            {
                var size = placement.EffectiveSize();
                var icon = new GameObject($"Item_{placement.Item.Id}", typeof(RectTransform), typeof(Image),
                    typeof(CanvasGroup), typeof(DragHandler), typeof(ItemTooltip));
                var rect = (RectTransform)icon.transform;
                rect.SetParent(_itemLayer, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
                rect.sizeDelta = new Vector2(size.W * CellSizePx, size.H * CellSizePx);
                rect.anchoredPosition = new Vector2(placement.Position.X * CellSizePx, -placement.Position.Y * CellSizePx);

                DecorateIcon(icon, rect, placement.Item, placement.Count, placement.Rotated);
                icon.GetComponent<DragHandler>().Bind(this, placement);
                icon.GetComponent<ItemTooltip>().Bind(placement, Inventory);
                AddFreshness(rect, placement);

                _spawnedIcons.Add(icon);
            }
            RefreshFreshness();
        }

        /// <summary>Item tile look shared by the grid, the drag ghost and equip slots: a tinted backing, the cartoon icon
        /// (turned with the item when it's rotated), the stack count, and a short name.</summary>
        internal static void DecorateIcon(GameObject icon, RectTransform rect, Isle.Data.ItemDef item, int count, bool rotated)
        {
            var backing = icon.GetComponent<Image>();
            var tint = Color.Lerp(Isle.UI.Art.ItemIcons.MainColour(item), new Color(0.16f, 0.13f, 0.11f), 0.78f);
            backing.sprite = Isle.UI.Prototype.UiTheme.Sprite(tint, Color.Lerp(tint, Color.white, 0.18f), 6, 1);
            backing.type = Image.Type.Sliced;

            var art = new GameObject("Art", typeof(RectTransform), typeof(Image));
            var artRect = (RectTransform)art.transform;
            artRect.SetParent(rect, false);
            artRect.anchorMin = artRect.anchorMax = new Vector2(0.5f, 0.5f);
            var side = Mathf.Min(rect.sizeDelta.x, rect.sizeDelta.y) * 0.9f;
            artRect.sizeDelta = new Vector2(side, side);
            if (rotated) artRect.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var image = art.GetComponent<Image>();
            image.sprite = Isle.UI.Art.ItemIcons.Sprite(item);
            image.preserveAspect = true;
            image.raycastTarget = false;

            if (count > 1) Corner(rect, $"×{count}", TextAnchor.LowerRight, 11, Color.white);
            if (rect.sizeDelta.x >= 64f || rect.sizeDelta.y >= 64f)
                Corner(rect, Isle.UI.Prototype.Lang.Get(item.Name), TextAnchor.UpperLeft, 9, new Color(0.95f, 0.9f, 0.8f, 0.85f));
        }

        static Text Corner(RectTransform parent, string text, TextAnchor anchor, int size, Color colour)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(Outline));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(3f, 5f);
            rect.offsetMax = new Vector2(-3f, -2f);
            var label = go.GetComponent<Text>();
            label.text = text;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = size;
            label.fontStyle = FontStyle.Bold;
            label.alignment = anchor;
            label.color = colour;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            go.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            return label;
        }

        /// <summary>Food shows how fresh it is without hovering: a bar along the bottom and a percentage.</summary>
        void AddFreshness(RectTransform rect, Placement placement)
        {
            var spoilage = placement.Item.Spoilage;
            if (spoilage == null || spoilage.BaseHours <= 0f) return;

            var track = new GameObject("Freshness", typeof(RectTransform), typeof(Image));
            var trackRect = (RectTransform)track.transform;
            trackRect.SetParent(rect, false);
            trackRect.anchorMin = new Vector2(0f, 0f);
            trackRect.anchorMax = new Vector2(1f, 0f);
            trackRect.pivot = new Vector2(0f, 0f);
            trackRect.offsetMin = new Vector2(3f, 2f);
            trackRect.offsetMax = new Vector2(-3f, 6f);
            var trackImage = track.GetComponent<Image>();
            trackImage.color = new Color(0f, 0f, 0f, 0.6f);
            trackImage.raycastTarget = false;

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var fillRect = (RectTransform)fill.transform;
            fillRect.SetParent(trackRect, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
            var fillImage = fill.GetComponent<Image>();
            fillImage.raycastTarget = false;

            var label = Corner(rect, "", TextAnchor.UpperRight, 9, Color.white);
            _freshness.Add((placement, fillRect, fillImage, label));
        }

        /// <summary>Updates the freshness bars — spoilage moves on its own, so the screen calls this every so often.</summary>
        public void RefreshFreshness()
        {
            foreach (var (placement, fill, image, label) in _freshness)
            {
                if (fill == null) continue;
                var fresh = 1f - SpoilageTracker.Live.SpoilageOf(Inventory, placement.Item);
                fill.anchorMax = new Vector2(Mathf.Clamp01(fresh), 1f);
                image.color = fresh > 0.5f ? Color.Lerp(new Color(0.95f, 0.8f, 0.3f), new Color(0.55f, 0.85f, 0.4f), (fresh - 0.5f) * 2f)
                    : Color.Lerp(new Color(0.9f, 0.35f, 0.25f), new Color(0.95f, 0.8f, 0.3f), fresh * 2f);
                label.text = $"{fresh * 100f:0}%";
                label.color = image.color;
            }
        }

        /// <summary>Draws a plain, non-interactive copy of <paramref name="placement"/> showing
        /// <paramref name="count"/> instead of its real one. <see cref="DragHandler"/> uses this to
        /// preview a Shift+drag split's remainder still sitting in its original spot — the real icon
        /// being split is the one following the pointer, so without this the source cell just looks
        /// empty for the whole drag. Caller destroys the returned GameObject once the drag ends.</summary>
        public GameObject SpawnGhostIcon(Placement placement, int count)
        {
            var size = placement.EffectiveSize();
            var icon = new GameObject($"Ghost_{placement.Item.Id}", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)icon.transform;
            rect.SetParent(_itemLayer, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(size.W * CellSizePx, size.H * CellSizePx);
            rect.anchoredPosition = new Vector2(placement.Position.X * CellSizePx, -placement.Position.Y * CellSizePx);

            DecorateIcon(icon, rect, placement.Item, count, placement.Rotated);
            return icon;
        }

        /// <summary>Shows an item's name on top of its placeholder-coloured box — there's no icon
        /// art yet (Absolute Rule 7), and the hover tooltip alone isn't enough to tell items apart
        /// at a glance. Not raycast-blocking, so it doesn't steal drag/click/hover from the icon
        /// underneath.</summary>
        internal static void BuildLabel(RectTransform parent, string name, int count)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            var label = go.GetComponent<Text>();
            var shown = Isle.UI.Prototype.Lang.Get(name);
            label.text = count > 1 ? $"{shown}\n×{count}" : shown;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // ponytail: built-in font, real UI style comes at T-160
            label.fontSize = 10;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.08f, 0.07f, 0.06f);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
        }

        /// <summary>Converts a pointer's screen position to a grid cell, clamped to bounds.</summary>
        public Vec2Int ScreenToCell(Vector2 screenPosition, Camera eventCamera)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_itemLayer, screenPosition, eventCamera, out var local);
            var x = Mathf.Clamp(Mathf.RoundToInt(local.x / CellSizePx), 0, Inventory.Width - 1);
            var y = Mathf.Clamp(Mathf.RoundToInt(-local.y / CellSizePx), 0, Inventory.Height - 1);
            return new Vec2Int(x, y);
        }

        /// <summary>Wire to the "move all" button's OnClick (container UI only, SYS-INV-01 §Required UX).</summary>
        public void OnMoveAllClicked()
        {
            if (PairedView == null) return;
            Inventory.MoveAllTo(PairedView.Inventory);
            Redraw();
            PairedView.Redraw();
        }

        /// <summary>Wire to the "auto-sort" button's OnClick. Only present on warehouse views (<see cref="IsWarehouse"/>).</summary>
        public void OnAutoSortClicked()
        {
            Inventory.AutoSort();
            Redraw();
        }
    }
}
