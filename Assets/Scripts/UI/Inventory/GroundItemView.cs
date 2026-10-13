using Isle.Gameplay.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Isle.UI.Inventory
{
    /// <summary>
    /// T-151 playtest: one item of the loot pile at the player's feet, listed in the inventory's ground panel. Right-click
    /// opens a menu (pick up, or into a chosen bag); dragging it onto a bag grid puts it in that cell. Every take is a
    /// request to the server (<see cref="InventoryNetwork.RequestTakeFromGround"/>).
    /// </summary>
    public sealed class GroundItemView : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public int Index { get; private set; }
        public LootEntry Entry { get; private set; }
        InventoryScreen _screen;
        RectTransform _ghost;
        RectTransform _canvasRect;

        public void Bind(InventoryScreen screen, int index, LootEntry entry)
        {
            _screen = screen;
            Index = index;
            Entry = entry;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right) _screen.OpenGroundMenu(Index, Entry);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            GridView.BeginDragging();
            _canvasRect = (RectTransform)canvas.transform;
            var go = new GameObject("GroundDrag", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            _ghost = (RectTransform)go.transform;
            _ghost.SetParent(_canvasRect, false);
            _ghost.SetAsLastSibling();
            _ghost.anchorMin = _ghost.anchorMax = new Vector2(0.5f, 0.5f);
            _ghost.pivot = new Vector2(0f, 1f);
            _ghost.sizeDelta = new Vector2(Entry.Item.Grid.W * GridView.CellSizePx, Entry.Item.Grid.H * GridView.CellSizePx);
            GridView.DecorateIcon(go, _ghost, Entry.Item, Entry.Count, false);
            var group = go.GetComponent<CanvasGroup>();
            group.alpha = 0.7f;
            group.blocksRaycasts = false;
            Follow(eventData);
        }

        public void OnDrag(PointerEventData eventData) => Follow(eventData);

        void OnDisable()
        {
            if (_ghost == null) return;
            GridView.EndDragging();
            Destroy(_ghost.gameObject);
            _ghost = null;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_ghost == null) return;
            GridView.EndDragging();
            GridView target = null;
            foreach (var hovered in eventData.hovered)
                if (hovered != null && hovered.GetComponentInParent<GridView>() is { } view && view.Network != null) { target = view; break; }
            if (target != null)
            {
                var topLeft = RectTransformUtility.WorldToScreenPoint(null, _ghost.position);
                var cell = target.DropCell(topLeft, null, Entry.Item, false, null);
                _screen.TakeGround(Index, Entry, target.ContainerIndex, cell);
            }
            Destroy(_ghost.gameObject);
            _ghost = null;
        }

        /// <summary>The ghost hangs so the pointer sits in the middle of its first cell.</summary>
        void Follow(PointerEventData eventData)
        {
            if (_ghost == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, eventData.position, null, out var local);
            var half = GridView.CellSizePx * 0.5f;
            _ghost.anchoredPosition = local + new Vector2(-half, half);
        }
    }
}
