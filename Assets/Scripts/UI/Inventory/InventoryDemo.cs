using Isle.Core;
using Isle.Data;
using Isle.Gameplay.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Isle.UI.Inventory
{
    /// <summary>
    /// Manual-test harness for T-042/T-043/T-044 (grid inventory drag/rotate/split/bulk-move/
    /// auto-sort, equip slots + bags). Builds a Canvas + EventSystem + sample containers entirely
    /// at runtime — there is no baked scene or prefab for any of this, matching the project's
    /// "generate at runtime, nothing baked" convention (<see cref="Isle.Core.Util.PlaceholderVisuals"/>).
    /// <para>
    /// The <see cref="ItemDef"/>s below are throwaway fixtures built directly in C#, the same way
    /// <c>GridInventoryTests</c> already does — not shipped content. The project has zero real item
    /// definitions yet (nothing under <c>StreamingAssets/definitions/items</c>), so there is nothing
    /// real to load instead; Absolute Rule 1 is about game content, not test fixtures.
    /// </para>
    /// <para>
    /// Drop this component on any empty GameObject and press Play — see the manual test checklist
    /// in PROJECT_STATE.md for what to do with it.
    /// </para>
    /// </summary>
    public sealed class InventoryDemo : MonoBehaviour
    {
        // ponytail: fixed layout constants for a one-scene test harness, not a real UI layout system.
        const float Margin = 16f;
        const float TitleHeight = 18f;

        void Start()
        {
            EnsureEventSystem();
            var canvas = EnsureCanvas();

            var bag = new GridInventory(6, 4);
            var warehouse = new GridInventory(8, 6);
            var slots = new EquipSlots();

            var sword = Item("@item.debug_sword", 1, 3, 2.5f, new[] { "weapon" }, equipSlot: "main_hand");
            var potion = Item("@item.debug_potion", 1, 1, 0.2f, new[] { "consumable" });
            var helmet = Item("@item.debug_helmet", 2, 2, 1.5f, new[] { "armor" }, equipSlot: "head");
            var backpack = Item("@item.debug_backpack", 2, 2, 1f, new[] { "storage" },
                equipSlot: "back", bagGrid: new GridSize { W = 4, H = 4 });

            bag.TryPlace(sword, new Vec2Int(0, 0));
            bag.TryPlace(potion, new Vec2Int(1, 0), count: 6);
            bag.TryPlace(helmet, new Vec2Int(2, 0));
            bag.TryPlace(backpack, new Vec2Int(4, 0));

            // Scattered, not packed — so Auto-Sort visibly does something.
            warehouse.TryPlace(Item("@item.debug_crate_a", 2, 2, 3f), new Vec2Int(0, 0));
            warehouse.TryPlace(Item("@item.debug_crate_b", 1, 2, 1f), new Vec2Int(5, 3));
            warehouse.TryPlace(Item("@item.debug_crate_c", 3, 1, 2f), new Vec2Int(2, 5));

            var x = Margin;
            var bagView = BuildGridView(canvas, "Player Bag", bag, false, ref x);
            var warehouseView = BuildGridView(canvas, "Warehouse (Auto-Sort)", warehouse, true, ref x);
            bagView.PairedView = warehouseView;
            warehouseView.PairedView = bagView;

            BuildMoveAllButton(canvas, "Bag -> Warehouse", bagView, ref x, -Margin - TitleHeight);
            BuildMoveAllButton(canvas, "Warehouse -> Bag", warehouseView, ref x, -Margin - TitleHeight - 32f);

            BuildEquipRow(canvas, slots, bagView, warehouseView);
        }

        void BuildEquipRow(Canvas canvas, EquipSlots slots, GridView bagView, GridView warehouseView)
        {
            const float y = -320f;

            var x = Margin;
            GridView bagGridView = null;
            GameObject bagMoveAllButton = null;

            foreach (var slotName in EquipSlots.All)
            {
                var slotView = BuildEquipSlotView(canvas, slotName, slots, slotName, ref x, y);
                // Ctrl+click quick-unequip (EquipDragHandler) sends the item to the player's own bag.
                slotView.PairedView = bagView;

                // T-044: equipping/unequipping the backpack opens/closes its bag grid.
                if (slotName != "back") continue;
                slotView.Changed.AddListener(() =>
                {
                    if (bagGridView != null)
                    {
                        Destroy(bagGridView.gameObject);
                        bagGridView = null;
                    }

                    if (bagMoveAllButton != null)
                    {
                        Destroy(bagMoveAllButton);
                        bagMoveAllButton = null;
                    }

                    var openBag = slots.BagFor("back");
                    if (openBag == null) return;

                    var bagX = Margin;
                    var bagY = y - 100f;
                    bagGridView = BuildGridView(canvas, "Backpack (opened)", openBag, false, ref bagX, bagY);

                    // Round 6: items inside an opened backpack can bulk-move too — into the
                    // warehouse for now (Ctrl+click and this button both route through PairedView).
                    bagGridView.PairedView = warehouseView;
                    var buttonX = Margin;
                    var buttonY = bagY - TitleHeight - openBag.Height * GridView.CellSizePx - Margin;
                    bagMoveAllButton = BuildMoveAllButton(canvas, "Backpack -> Warehouse", bagGridView, ref buttonX, buttonY);
                });
            }
        }

        static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        static Canvas EnsureCanvas()
        {
            var existing = FindFirstObjectByType<Canvas>();
            if (existing != null) return existing;

            var go = new GameObject("InventoryDemoCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            return canvas;
        }

        static GridView BuildGridView(Canvas canvas, string title, GridInventory inventory, bool isWarehouse, ref float x, float y = -Margin)
        {
            var width = inventory.Width * GridView.CellSizePx;
            BuildTitle(canvas, title, x, y, width);

            var go = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(GridView));
            var rect = (RectTransform)go.transform;
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, y - TitleHeight);
            rect.sizeDelta = new Vector2(width, inventory.Height * GridView.CellSizePx);
            go.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.05f, 0.5f);

            var view = go.GetComponent<GridView>();
            view.IsWarehouse = isWarehouse;
            view.Bind(inventory);

            x += width + Margin;
            return view;
        }

        static EquipSlotView BuildEquipSlotView(Canvas canvas, string title, EquipSlots slots, string slotName, ref float x, float y)
        {
            const float frameSizePx = GridView.CellSizePx * 2; // matches EquipSlotView.FrameSizePx
            BuildTitle(canvas, title, x, y, frameSizePx);

            var go = new GameObject($"Slot_{slotName}", typeof(RectTransform), typeof(Image), typeof(EquipSlotView));
            var rect = (RectTransform)go.transform;
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, y - TitleHeight);

            var view = go.GetComponent<EquipSlotView>();
            view.Bind(slots, slotName);

            x += frameSizePx + Margin;
            return view;
        }

        /// <summary>A plain, non-interactive name label placed above a panel (grid or slot) so the
        /// developer can tell what they're looking at without guessing from a coloured box.</summary>
        static void BuildTitle(Canvas canvas, string text, float x, float y, float width)
        {
            var go = new GameObject($"Title_{text}", typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, TitleHeight);

            var label = go.GetComponent<Text>();
            label.text = text;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // ponytail: built-in font, real UI style comes at T-160
            label.fontSize = 12;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = Color.white;
            label.raycastTarget = false;
        }

        static GameObject BuildMoveAllButton(Canvas canvas, string label, GridView source, ref float x, float y)
        {
            var go = new GameObject($"MoveAll_{label}", typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(140f, 24f);
            go.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f);

            // ponytail: Text needs its own child GameObject — see GridView.BuildAutoSortButton.
            GridView.BuildButtonLabel(rect, label, Color.white);

            go.GetComponent<Button>().onClick.AddListener(source.OnMoveAllClicked);
            return go;
        }

        static ItemDef Item(string nameKey, int w, int h, float weight, string[] tags = null,
            string equipSlot = null, GridSize? bagGrid = null) => new()
        {
            Name = nameKey,
            Grid = new GridSize { W = w, H = h },
            Weight = weight,
            Tags = tags ?? System.Array.Empty<string>(),
            EquipSlot = equipSlot,
            BagGrid = bagGrid,
        };
    }
}
