using System.Collections.Generic;
using System.Linq;
using Isle.Core;
using Isle.Data;

namespace Isle.Gameplay.Inventory
{
    /// <summary>
    /// One placed item (SYS-INV-01 §Placement). <see cref="Rotated"/> swaps <c>W</c> and <c>H</c>
    /// of <see cref="Item"/>'s <see cref="GridSize"/> — there is no separate orientation field, same
    /// convention as <see cref="GridSize"/> itself.
    /// </summary>
    public readonly struct Placement
    {
        public ItemDef Item { get; }
        public Vec2Int Position { get; }
        public bool Rotated { get; }

        /// <summary>Stack size (SYS-INV-01 §Required UX, split stack). Added for T-042/T-043 —
        /// T-040 didn't need it since it never split or moved partial stacks.</summary>
        public int Count { get; }

        public Placement(ItemDef item, Vec2Int position, bool rotated, int count = 1)
        {
            Item = item;
            Position = position;
            Rotated = rotated;
            Count = count;
        }

        public GridSize EffectiveSize() => Rotated
            ? new GridSize { W = Item.Grid.H, H = Item.Grid.W }
            : Item.Grid;
    }

    /// <summary>
    /// Tetris-style grid placement (SYS-INV-01 §Containers, §Placement) — pure structure, no
    /// network concerns (T-045). One instance per container; a bag adds a separate instance rather
    /// than growing this one's bounds.
    /// </summary>
    public sealed class GridInventory
    {
        public int Width { get; }
        public int Height { get; }

        readonly List<Placement> _placements = new();

        public IReadOnlyList<Placement> Placements => _placements;

        public GridInventory(int width, int height)
        {
            Width = width;
            Height = height;
        }

        /// <summary>Places <paramref name="item"/> at <paramref name="position"/> if every cell it covers is
        /// in bounds and empty — or, if the very same item (by reference) is already sitting at that exact
        /// position and rotation, merges into its count instead. Not in SYS-INV-01's text, but
        /// <see cref="TrySplit"/> needs some way back together — dragging a split-off stack back onto its
        /// sibling should recombine it, the same way most Tetris-style loot inventories do.</summary>
        public bool TryPlace(ItemDef item, Vec2Int position, bool rotated = false, int count = 1)
        {
            var mergeIndex = _placements.FindIndex(p =>
                ReferenceEquals(p.Item, item) && p.Position == position && p.Rotated == rotated);
            if (mergeIndex >= 0)
            {
                _placements[mergeIndex] = new Placement(item, position, rotated, _placements[mergeIndex].Count + count);
                return true;
            }

            var placement = new Placement(item, position, rotated, count);
            if (!Fits(placement)) return false;

            _placements.Add(placement);
            return true;
        }

        public bool Remove(Placement placement) => _placements.Remove(placement);

        /// <summary>The placement anchored exactly at <paramref name="position"/>, or null. Added for
        /// T-045: a network request identifies "which item" by where it already sits in this
        /// container, rather than by definition id — there's nothing to resolve from a wire id when
        /// the item is already here.</summary>
        public Placement? PlacementAt(Vec2Int position)
        {
            var index = _placements.FindIndex(p => p.Position == position);
            return index >= 0 ? _placements[index] : null;
        }

        /// <summary>First position (row-major scan) that fits <paramref name="size"/>, or null if none does.
        /// Backs bulk move, move-all and auto-sort — none of them care which free spot is used, just that one is found.</summary>
        public Vec2Int? FindFreePosition(GridSize size, bool rotated = false)
        {
            var effective = rotated ? new GridSize { W = size.H, H = size.W } : size;

            for (var y = 0; y <= Height - effective.H; y++)
            for (var x = 0; x <= Width - effective.W; x++)
            {
                var candidate = new Vec2Int(x, y);
                if (FitsAt(candidate, effective, exclude: null)) return candidate;
            }

            return null;
        }

        /// <summary>Where <paramref name="item"/> would land if placed now: the position of a matching
        /// item (by reference) already here, to merge into — or the first free spot otherwise. Shared
        /// by anything that just needs to put an item "somewhere sensible" (<see cref="TryMoveTo"/>,
        /// <c>EquipDragHandler</c>'s Ctrl+click unequip), so all of them merge onto an existing stack
        /// the same way a manual drag-to-that-exact-cell already does via <see cref="TryPlace"/>.</summary>
        public (Vec2Int position, bool rotated)? FindPlacementSpot(ItemDef item, bool rotated)
        {
            var existingIndex = _placements.FindIndex(p => ReferenceEquals(p.Item, item));
            if (existingIndex >= 0)
                return (_placements[existingIndex].Position, _placements[existingIndex].Rotated);

            var free = FindFreePosition(item.Grid, rotated);
            return free == null ? null : (free.Value, rotated);
        }

        /// <summary>Moves one placement into <paramref name="destination"/> — merging into a matching
        /// item already there, or its first free spot otherwise (SYS-INV-01 §Required UX: bulk move,
        /// and the building block "move all" loops this over every placement).</summary>
        public bool TryMoveTo(GridInventory destination, Placement placement)
        {
            var spot = destination.FindPlacementSpot(placement.Item, placement.Rotated);
            if (spot == null) return false;
            if (!destination.TryPlace(placement.Item, spot.Value.position, spot.Value.rotated, placement.Count)) return false;

            _placements.Remove(placement);
            return true;
        }

        /// <summary>SYS-INV-01 §Required UX: "move all" button. Best-effort — items that don't fit stay behind.</summary>
        public int MoveAllTo(GridInventory destination)
        {
            var moved = 0;
            foreach (var placement in _placements.ToList())
                if (TryMoveTo(destination, placement)) moved++;

            return moved;
        }

        /// <summary>
        /// Splits <paramref name="splitCount"/> units off <paramref name="source"/> into
        /// <paramref name="destination"/> at <paramref name="destPosition"/> (SYS-INV-01 §Required UX:
        /// Shift+drag split). Leaves the remainder behind at <paramref name="source"/>'s own spot;
        /// use <see cref="TryMoveTo"/> instead to move a whole stack. All-or-nothing: on failure
        /// <paramref name="source"/> is untouched.
        /// </summary>
        public bool TrySplit(Placement source, int splitCount, GridInventory destination, Vec2Int destPosition, bool destRotated = false)
        {
            if (splitCount <= 0 || splitCount >= source.Count) return false;
            if (destination == this && destPosition == source.Position && destRotated == source.Rotated) return false;
            if (!_placements.Remove(source)) return false;

            var moved = new Placement(source.Item, destPosition, destRotated, splitCount);
            if (!destination.FitsAt(moved.Position, moved.EffectiveSize(), exclude: null))
            {
                _placements.Add(source);
                return false;
            }

            _placements.Add(new Placement(source.Item, source.Position, source.Rotated, source.Count - splitCount));
            destination._placements.Add(moved);
            return true;
        }

        /// <summary>
        /// Repacks by descending footprint area, first-fit (SYS-INV-01 §Required UX: warehouse-only
        /// auto-sort). The spec leaves the exact ordering an open question — see
        /// <c>PROJECT_STATE.md</c> §Decided without a spec. Computed on a scratch grid first so a
        /// packing failure (shouldn't happen; every item already fit somewhere) leaves this instance
        /// untouched rather than half-sorted.
        /// </summary>
        public bool AutoSort()
        {
            var sorted = _placements
                .OrderByDescending(p => p.EffectiveSize().W * p.EffectiveSize().H)
                .ToList();

            var scratch = new GridInventory(Width, Height);
            foreach (var p in sorted)
            {
                var pos = scratch.FindFreePosition(p.Item.Grid, p.Rotated);
                if (pos == null) return false;
                scratch.TryPlace(p.Item, pos.Value, p.Rotated, p.Count);
            }

            _placements.Clear();
            _placements.AddRange(scratch._placements);
            return true;
        }

        /// <summary>Sum of <c>Item.Weight * Count</c> across every placement — feeds <see cref="WeightCalculator"/>.</summary>
        public float TotalWeightKg() => _placements.Sum(p => p.Item.Weight * p.Count);

        bool Fits(Placement placement) => FitsAt(placement.Position, placement.EffectiveSize(), exclude: null);

        bool FitsAt(Vec2Int position, GridSize size, Placement? exclude)
        {
            if (position.X < 0 || position.Y < 0) return false;
            if (position.X + size.W > Width || position.Y + size.H > Height) return false;

            foreach (var existing in _placements)
            {
                if (exclude.HasValue && existing.Equals(exclude.Value)) continue;
                if (Overlaps(position, size, existing.Position, existing.EffectiveSize())) return false;
            }

            return true;
        }

        /// <summary>AABB overlap is sufficient — SYS-INV-01 §Placement: there are no L-shaped items.</summary>
        static bool Overlaps(Vec2Int aPos, GridSize aSize, Vec2Int bPos, GridSize bSize) =>
            aPos.X < bPos.X + bSize.W && aPos.X + aSize.W > bPos.X
            && aPos.Y < bPos.Y + bSize.H && aPos.Y + aSize.H > bPos.Y;
    }
}
