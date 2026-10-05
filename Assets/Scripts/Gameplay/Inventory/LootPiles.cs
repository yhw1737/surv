using System.Collections.Generic;
using System.Linq;
using Isle.Core.Util;
using Isle.Data;
using UnityEngine;

namespace Isle.Gameplay.Inventory
{
    /// <summary>A heap of items on the ground: a dead player's inventory, or loot that didn't fit.</summary>
    public sealed class LootPile
    {
        public Vector2 Position { get; init; }
        public List<(ItemDef Item, int Count)> Items { get; } = new();
        public GameObject View { get; set; }
    }

    /// <summary>
    /// Every loot pile on the island. SYS-SURV-01 §Death: "drop entire inventory including equipment", and an
    /// ally recovering the body returns the items immediately. The prototype lets anyone (including the dead
    /// player after respawn) pick up a pile with the interact key. Host-only plain state, like the creatures.
    /// </summary>
    public static class LootPiles
    {
        static readonly List<LootPile> _piles = new();
        static Sprite _sprite;

        public static IReadOnlyList<LootPile> All => _piles;

        public static LootPile Drop(Vector2 at, IEnumerable<(ItemDef Item, int Count)> items)
        {
            var pile = new LootPile { Position = at };
            pile.Items.AddRange(items);
            if (pile.Items.Count == 0) return null;

            _sprite ??= ShapeLibrary.Sprite("sack", new Color(0.62f, 0.45f, 0.26f));
            var view = new GameObject("LootPile");
            view.transform.position = at;
            var renderer = view.AddComponent<SpriteRenderer>();
            renderer.sprite = _sprite;
            renderer.sortingOrder = 4;
            view.transform.localScale = Vector3.one * 0.7f;
            pile.View = view;

            _piles.Add(pile);
            return pile;
        }

        public static LootPile Nearest(Vector2 from, float reachTiles)
        {
            LootPile best = null;
            var bestDistance = reachTiles;
            foreach (var pile in _piles)
            {
                var distance = Vector2.Distance(from, pile.Position);
                if (distance > bestDistance) continue;
                best = pile;
                bestDistance = distance;
            }
            return best;
        }

        /// <summary>Moves as much of the pile as fits into <paramref name="bag"/>; the pile disappears once empty.
        /// Returns what was taken, for feedback.</summary>
        public static List<(ItemDef Item, int Count)> PickUp(LootPile pile, GridInventory bag) => PickUp(pile, new[] { bag });

        /// <summary>Recovering a body: bags and gear go back into their empty slots first (bags before anything else, so
        /// their grids are there for the rest), then everything else into the containers.</summary>
        public static List<(ItemDef Item, int Count)> PickUp(LootPile pile, EquipSlots slots, System.Func<IReadOnlyList<GridInventory>> containers)
        {
            var taken = new List<(ItemDef Item, int Count)>();
            foreach (var entry in pile.Items.OrderByDescending(e => e.Item.BagGrid != null).ToList())
            {
                var (item, count) = entry;
                if (count != 1 || string.IsNullOrEmpty(item.EquipSlot) || slots.Get(item.EquipSlot) != null) continue;
                if (!slots.TryEquip(item.EquipSlot, item)) continue;
                pile.Items.Remove(entry);
                taken.Add(entry);
            }
            taken.AddRange(PickUp(pile, containers()));
            return taken;
        }

        public static List<(ItemDef Item, int Count)> PickUp(LootPile pile, IReadOnlyList<GridInventory> containers)
        {
            var taken = new List<(ItemDef, int)>();
            for (var i = pile.Items.Count - 1; i >= 0; i--)
            {
                var (item, count) = pile.Items[i];
                if (!InventoryOps.TryGive(containers, item, count)) continue;
                taken.Add((item, count));
                pile.Items.RemoveAt(i);
            }

            if (pile.Items.Count == 0) Remove(pile);
            return taken;
        }

        /// <summary>Clears every pile — for scene reloads and tests.</summary>
        public static void Clear()
        {
            foreach (var pile in _piles) DestroyView(pile);
            _piles.Clear();
        }

        static void Remove(LootPile pile)
        {
            _piles.Remove(pile);
            DestroyView(pile);
        }

        /// <summary>EditMode tests run outside Play mode, where <c>Object.Destroy</c> isn't allowed.</summary>
        static void DestroyView(LootPile pile)
        {
            if (pile.View == null) return;
            if (Application.isPlaying) Object.Destroy(pile.View);
            else Object.DestroyImmediate(pile.View);
        }
    }
}
