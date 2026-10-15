using System.Collections.Generic;
using Isle.Core;
using Isle.Data;
using Isle.Gameplay.Inventory;
using Isle.UI.Prototype;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Isle.UI.Inventory
{
    /// <summary>T-305 SYS-INV-02: what a list shows — one of your carried containers, the ground at your feet, the crate in
    /// reach, or a campfire's fuel.</summary>
    public enum ListSource { Container, Ground, Crate, Campfire }

    /// <summary>One row's worth: an item, how many, its wear, and the key the server names it by (a ground entry's index
    /// for the ground).</summary>
    public readonly struct ListEntry
    {
        public ListEntry(ItemDef item, int count, ItemWear wear, Vec2Int key, int index)
        {
            Item = item;
            Count = count;
            Wear = wear;
            Key = key;
            Index = index;
        }

        public ItemDef Item { get; }
        public int Count { get; }
        public ItemWear Wear { get; }
        public Vec2Int Key { get; }
        public int Index { get; }
    }

    /// <summary>
    /// T-305 SYS-INV-02: one container's contents as rows (icon, name, ×count, kg). Rows drag to another list, a tab, an
    /// equipment slot or off the panels; Shift+drag moves one; double-click or Ctrl+click sends a stack across; right-click
    /// opens the item's menu. Every move is <see cref="InventoryScreen.Transfer"/>.
    /// </summary>
    public sealed class ItemListView : MonoBehaviour
    {
        public const float RowPx = 46f;

        public ListSource Source { get; private set; }
        public int ContainerIndex { get; private set; }
        public InventoryScreen Screen { get; private set; }

        RectTransform _content;
        readonly List<GameObject> _rows = new();
        readonly List<(ItemDef Item, RectTransform Fill, Image Bar, Text Label)> _freshness = new();
        GridInventory _container;

        public void Bind(InventoryScreen screen, ListSource source, int containerIndex, RectTransform content)
        {
            Screen = screen;
            Source = source;
            ContainerIndex = containerIndex;
            _content = content;
        }

        public int RowCount => _rows.Count;

        /// <param name="container">The container the rows are in, for freshness (SYS-SPOIL-01); null for the ground.</param>
        public void Refresh(IReadOnlyList<ListEntry> entries, GridInventory container = null)
        {
            foreach (var row in _rows) Destroy(row);
            _rows.Clear();
            _freshness.Clear();
            _container = container;
            var width = ((RectTransform)transform).rect.width;
            if (width <= 0f) width = ((RectTransform)transform).sizeDelta.x;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var go = new GameObject($"Row_{entry.Item.Id}", typeof(RectTransform), typeof(Image), typeof(ItemRow), typeof(ItemTooltip));
                var rect = (RectTransform)go.transform;
                rect.SetParent(_content, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(0f, -i * RowPx);
                rect.sizeDelta = new Vector2(width - 8f, RowPx - 4f);
                var image = go.GetComponent<Image>();
                image.sprite = UiTheme.Sprite(UiTheme.Inset, UiTheme.InsetEdge, 6);
                image.type = Image.Type.Sliced;

                var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                var iconRect = (RectTransform)icon.transform;
                iconRect.SetParent(rect, false);
                iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0f, 0.5f);
                iconRect.anchoredPosition = new Vector2(6f, 0f);
                iconRect.sizeDelta = new Vector2(RowPx - 10f, RowPx - 10f);
                var art = icon.GetComponent<Image>();
                art.sprite = Isle.UI.Art.ItemIcons.Sprite(entry.Item);
                art.preserveAspect = true;
                art.raycastTarget = false;

                Label(rect, ItemText.Name(entry.Item) + (entry.Count > 1 ? $"  ×{entry.Count}" : ""), RowPx + 2f, 15, TextAnchor.MiddleLeft, UiTheme.Text, width - RowPx - 120f);
                var kg = (entry.Wear?.Carcass?.WeightKg ?? entry.Item.Weight) * entry.Count;
                Label(rect, $"{kg:0.##} kg", width - 112f, 13, TextAnchor.MiddleRight, UiTheme.Muted, 96f);
                // Gear shows its wear as a thin bar along the bottom (SYS-CRAFT-02).
                if (entry.Wear != null && entry.Wear.Carcass == null && entry.Wear.NeedsRepair) WearLine(rect, entry.Wear.Fraction);
                // T-307: food that spoils shows how fresh it is — a bar that shrinks right to left, green to red.
                if (entry.Item.Spoilage != null && container != null) FreshnessBar(rect, entry.Item, width);

                go.GetComponent<ItemRow>().Bind(this, entry);
                go.GetComponent<ItemTooltip>().Bind(new Placement(entry.Item, entry.Key, false, entry.Count, entry.Wear), null);
                _rows.Add(go);
            }
            _content.sizeDelta = new Vector2(_content.sizeDelta.x, entries.Count * RowPx + 8f);
            RefreshFreshness();
        }

        /// <summary>T-307: freshness of the food rows, updated in place (spoilage moves with the clock, the rows don't).</summary>
        public void RefreshFreshness()
        {
            if (_container == null) return;
            foreach (var (item, fill, bar, label) in _freshness)
            {
                if (fill == null) continue;
                var fresh = Mathf.Clamp01(1f - SpoilageTracker.Live.SpoilageOf(_container, item));
                fill.anchorMax = new Vector2(fresh, 1f);
                bar.color = FreshColour(fresh);
                label.text = $"{fresh * 100f:0}%";
                label.color = bar.color;
            }
        }

        /// <summary>Green while fresh, through yellow, to red as it rots.</summary>
        public static Color FreshColour(float fresh) =>
            fresh > 0.5f ? Color.Lerp(new Color(0.95f, 0.8f, 0.3f), new Color(0.45f, 0.82f, 0.35f), (fresh - 0.5f) * 2f)
            : Color.Lerp(new Color(0.88f, 0.25f, 0.2f), new Color(0.95f, 0.8f, 0.3f), fresh * 2f);

        void FreshnessBar(RectTransform row, ItemDef item, float width)
        {
            // A dark track under the name, the coloured fill anchored at its left edge, the percentage beside it.
            var track = new GameObject("Freshness", typeof(RectTransform), typeof(Image));
            var trackRect = (RectTransform)track.transform;
            trackRect.SetParent(row, false);
            trackRect.anchorMin = trackRect.anchorMax = trackRect.pivot = new Vector2(0f, 0f);
            trackRect.anchoredPosition = new Vector2(RowPx + 2f, 5f);
            trackRect.sizeDelta = new Vector2(Mathf.Max(60f, width - RowPx - 200f), 5f);
            var trackImage = track.GetComponent<Image>();
            trackImage.color = new Color(0f, 0f, 0f, 0.45f);
            trackImage.raycastTarget = false;

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var fillRect = (RectTransform)fill.transform;
            fillRect.SetParent(trackRect, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
            var bar = fill.GetComponent<Image>();
            bar.raycastTarget = false;

            var label = new GameObject("Percent", typeof(RectTransform), typeof(Text));
            var labelRect = (RectTransform)label.transform;
            labelRect.SetParent(row, false);
            labelRect.anchorMin = labelRect.anchorMax = labelRect.pivot = new Vector2(0f, 0f);
            labelRect.anchoredPosition = new Vector2(RowPx + 8f + trackRect.sizeDelta.x, 0f);
            labelRect.sizeDelta = new Vector2(44f, 15f);
            var text = label.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 11;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            _freshness.Add((item, fillRect, bar, text));
        }

        static void Label(RectTransform parent, string text, float x, int size, TextAnchor anchor, Color colour, float width)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(width, 0f);
            var label = go.GetComponent<Text>();
            label.text = text;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = size;
            label.alignment = anchor;
            label.color = colour;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        static void WearLine(RectTransform parent, float fraction)
        {
            var go = new GameObject("Wear", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(Mathf.Clamp01(fraction), 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.offsetMin = new Vector2(4f, 2f);
            rect.offsetMax = new Vector2(-4f, 5f);
            var image = go.GetComponent<Image>();
            image.color = fraction > 0.5f ? UiTheme.Good : fraction > 0.2f ? UiTheme.Accent : UiTheme.Bad;
            image.raycastTarget = false;
        }
    }

    /// <summary>A tab naming a list — click to show it, drop a row on it to move the row there.</summary>
    public sealed class ListTab : MonoBehaviour
    {
        public ListSource Source;
        public int ContainerIndex;
    }

    /// <summary>One row: drag, click, double-click, right-click.</summary>
    public sealed class ItemRow : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public ItemListView View { get; private set; }
        public ListEntry Entry { get; private set; }

        RectTransform _ghost;
        RectTransform _canvasRect;
        int _dragCount;

        public void Bind(ItemListView view, ListEntry entry)
        {
            View = view;
            Entry = entry;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                View.Screen.OpenEntryMenu(View, Entry);
                return;
            }
            var ctrl = Keyboard.current != null && (Keyboard.current.ctrlKey.isPressed || Keyboard.current.leftCommandKey.isPressed);
            if (eventData.clickCount >= 2 || ctrl) View.Screen.QuickMove(View, Entry);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            canvas = canvas.rootCanvas;
            _canvasRect = (RectTransform)canvas.transform;
            _dragCount = Keyboard.current != null && Keyboard.current.shiftKey.isPressed ? 1 : Entry.Count;
            GridView.BeginDragging();
            View.Screen.BeginRowDrag();
            var go = new GameObject("RowDrag", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            _ghost = (RectTransform)go.transform;
            _ghost.SetParent(_canvasRect, false);
            _ghost.SetAsLastSibling();
            _ghost.anchorMin = _ghost.anchorMax = new Vector2(0.5f, 0.5f);
            _ghost.pivot = new Vector2(0.5f, 0.5f);
            _ghost.sizeDelta = new Vector2(56f, 56f);
            GridView.DecorateIcon(go, _ghost, Entry.Item, _dragCount, false);
            var group = go.GetComponent<CanvasGroup>();
            group.alpha = 0.8f;
            group.blocksRaycasts = false;
            Follow(eventData);
        }

        public void OnDrag(PointerEventData eventData) => Follow(eventData);

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_ghost == null) return;
            Destroy(_ghost.gameObject);
            _ghost = null;
            GridView.EndDragging();
            View.Screen.EndRowDrag(View, Entry, _dragCount, eventData);
        }

        void OnDisable()
        {
            if (_ghost == null) return;
            Destroy(_ghost.gameObject);
            _ghost = null;
            GridView.EndDragging();
            View.Screen?.EndRowDrag(null, Entry, 0, null);
        }

        void Follow(PointerEventData eventData)
        {
            if (_ghost == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, eventData.position, null, out var local);
            _ghost.anchoredPosition = local;
        }
    }
}
