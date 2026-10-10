using System;
using System.Collections.Generic;
using System.Linq;
using Isle.Core.Ids;
using Isle.Data;
using Isle.Modding.Defs;

namespace Isle.Gameplay.Inventory
{
    /// <summary>
    /// Item spoilage (SYS-WORLD-01 §Deferred simulation: <c>freshness -= elapsed × rate × tempFactor</c>).
    /// ponytail: one spoilage value per item type per container, not per stack — placements are identical-def
    /// stacks merged by reference and carry no state. New items join the old ones' clock. Per-stack freshness
    /// needs Placement to carry state; do that when cooking quality needs it too.
    /// <c>temp_factor</c> isn't applied: SCHEMA calls it "multiplier per degree" with no reference temperature.
    /// </summary>
    public sealed class SpoilageTracker
    {
        /// <summary>The live game's tracker, resolving results through the def registry.</summary>
        public static SpoilageTracker Live { get; } = new(id => DefRegistry.TryGet<ItemDef>(id, out var def) ? def : null);

        readonly Func<NamespacedId, ItemDef> _resolve;
        readonly Dictionary<(GridInventory, ItemDef), float> _spoilage = new();

        /// <param name="resolve">Looks up a spoilage result item; the live caller passes the def registry.</param>
        public SpoilageTracker(Func<NamespacedId, ItemDef> resolve) => _resolve = resolve;

        /// <summary>0 = fresh, 1 = spoiled. <paramref name="baseHours"/> ≤ 0 never spoils.</summary>
        public static float Advance(float spoilage, float inGameMinutes, float baseHours) =>
            baseHours <= 0f ? spoilage : Math.Min(1f, spoilage + inGameMinutes / (baseHours * 60f));

        public float SpoilageOf(GridInventory bag, ItemDef item) => _spoilage.GetValueOrDefault((bag, item));

        /// <summary>Every item type ageing in <paramref name="bag"/>, for saving.</summary>
        public IEnumerable<(ItemDef Item, float Spoilage)> EntriesFor(GridInventory bag)
        {
            foreach (var ((owner, item), value) in _spoilage)
                if (owner == bag) yield return (item, value);
        }

        /// <summary>Puts a saved freshness back (a reload mustn't make food fresh again).</summary>
        public void Restore(GridInventory bag, ItemDef item, float spoilage) => _spoilage[(bag, item)] = spoilage;

        public void Tick(GridInventory bag, float inGameMinutes)
        {
            // Forget types no longer in the bag, so a fresh batch later starts at 0.
            foreach (var key in _spoilage.Keys.Where(k => k.Item1 == bag && !bag.Placements.Any(p => ReferenceEquals(p.Item, k.Item2))).ToList())
                _spoilage.Remove(key);

            foreach (var placed in bag.Placements.ToList())
            {
                var spec = placed.Item.Spoilage;
                if (spec == null || spec.BaseHours <= 0f) continue;

                var key = (bag, placed.Item);
                var value = Advance(_spoilage.GetValueOrDefault(key), inGameMinutes, spec.BaseHours);
                _spoilage[key] = value;
                if (value < 1f) continue;

                var result = spec.Result.IsValid ? _resolve(spec.Result) : null;
                bag.Remove(placed);
                if (result != null) bag.TryPlace(result, placed.Position, placed.Rotated, placed.Count);
                _spoilage.Remove(key);
            }
        }
    }
}
