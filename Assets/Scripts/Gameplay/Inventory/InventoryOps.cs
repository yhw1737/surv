using System;
using System.Collections.Generic;
using System.Linq;
using Isle.Core.Ids;
using Isle.Data;

namespace Isle.Gameplay.Inventory
{
    /// <summary>
    /// Give, count and take across several containers at once — the base carry plus any equipped bags
    /// (SYS-INV-01 §Containers). Every gameplay path that adds or removes items for a player goes through here, so
    /// "is there room" and "do I have it" mean the same thing everywhere. Pure, no Unity refs.
    /// </summary>
    public static class InventoryOps
    {
        /// <summary>Merges onto an existing stack first, then the first free spot as-is, then rotated.</summary>
        public static bool TryGive(IReadOnlyList<GridInventory> containers, ItemDef item, int count, ItemWear wear = null)
        {
            if (!Placement.Stacks(item))
            {
                // Durable items go in one by one, each its own placement (only the first carries the given wear).
                if (count > 1 && !HasRoomFor(containers, item, count)) return false;
                for (var i = 0; i < count; i++)
                    if (!PlaceFree(containers, item, 1, i == 0 ? wear : null)) return false;
                return true;
            }
            foreach (var container in containers)
            {
                var existing = container.Placements.FirstOrDefault(p => ReferenceEquals(p.Item, item));
                if (existing.Item != null && container.TryPlace(item, existing.Position, existing.Rotated, count)) return true;
            }
            return PlaceFree(containers, item, count, wear);
        }

        static bool PlaceFree(IReadOnlyList<GridInventory> containers, ItemDef item, int count, ItemWear wear)
        {
            foreach (var rotated in new[] { false, true })
            foreach (var container in containers)
            {
                var spot = container.FindFreePosition(item.Grid, rotated);
                if (spot != null && container.TryPlace(item, spot.Value, rotated, count, wear)) return true;
            }
            return false;
        }

        /// <summary>Room for <paramref name="count"/> separate units, checked on scratch copies so nothing is half-given.</summary>
        static bool HasRoomFor(IReadOnlyList<GridInventory> containers, ItemDef item, int count)
        {
            var scratch = containers.Select(c =>
            {
                var copy = new GridInventory(c.Width, c.Height);
                foreach (var p in c.Placements) copy.TryPlace(p.Item, p.Position, p.Rotated, p.Count, p.Wear);
                return copy;
            }).ToList();
            for (var i = 0; i < count; i++)
                if (!PlaceFree(scratch, item, 1, null)) return false;
            return true;
        }

        public static int Count(IEnumerable<GridInventory> containers, NamespacedId item) =>
            containers.Sum(c => c.Placements.Where(p => p.Item.Id == item).Sum(p => p.Count));

        /// <summary>All-or-nothing: removes <paramref name="count"/> units only if that many are held.</summary>
        public static bool TryTake(IReadOnlyList<GridInventory> containers, NamespacedId item, int count)
        {
            if (Count(containers, item) < count) return false;
            var remaining = count;
            foreach (var container in containers)
            foreach (var placed in container.Placements.Where(p => p.Item.Id == item).ToList())
            {
                if (remaining <= 0) return true;
                var take = Math.Min(remaining, placed.Count);
                SetCount(container, placed, placed.Count - take);
                remaining -= take;
            }
            return true;
        }

        /// <summary>Removes one unit and returns its def, or null when none is held.</summary>
        public static ItemDef TakeOne(IReadOnlyList<GridInventory> containers, NamespacedId item)
        {
            foreach (var container in containers)
            {
                var placed = container.Placements.FirstOrDefault(p => p.Item.Id == item);
                if (placed.Item == null) continue;
                SetCount(container, placed, placed.Count - 1);
                return placed.Item;
            }
            return null;
        }

        /// <summary>SYS-CRAFT-02: one use off a tool sitting in a bag (a knife used for butchering). Returns true when this
        /// broke it.</summary>
        public static bool WearPlacement(GridInventory container, Placement placement)
        {
            var wear = placement.Wear ?? ItemWear.Fresh(placement.Item);
            if (wear == null) return false;
            if (placement.Wear == null)
            {
                // A brand-new tool gets its wear record now; swap the placement for one that carries it.
                container.Remove(placement);
                container.TryPlace(placement.Item, placement.Position, placement.Rotated, placement.Count, wear);
            }
            var was = wear.Broken;
            wear.Use();
            return !was && wear.Broken;
        }

        /// <summary>Swaps a stack for the same stack at a new count; zero removes it. Removing first frees its
        /// cells, so the re-place at the same spot always fits.</summary>
        public static void SetCount(GridInventory container, Placement placement, int newCount)
        {
            container.Remove(placement);
            if (newCount > 0) container.TryPlace(placement.Item, placement.Position, placement.Rotated, newCount, placement.Wear);
        }
    }
}
