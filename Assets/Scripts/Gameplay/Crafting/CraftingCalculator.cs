using System.Collections.Generic;
using Isle.Core.Ids;
using Isle.Data;
using Isle.Gameplay.Inventory;

namespace Isle.Gameplay.Crafting
{
    /// <summary>One inventory line as seen by the recipe check: an item id, its tags, and how many are
    /// held. Built from <c>GridInventory.Placements</c> by the caller, so this stays free of inventory types.</summary>
    public readonly struct Stock
    {
        public Stock(NamespacedId item, string[] tags, int count)
        {
            Item = item;
            Tags = tags ?? System.Array.Empty<string>();
            Count = count;
        }

        public NamespacedId Item { get; }
        public string[] Tags { get; }
        public int Count { get; }
    }

    /// <summary>SCHEMA §Craft recipes, SYS-CRAFT-01 ingredient rule. Static pure.</summary>
    public static class CraftingCalculator
    {
        /// <summary>One <see cref="Stock"/> line per bag placement, for <see cref="HasIngredients"/>.</summary>
        public static Stock[] StockOf(IEnumerable<GridInventory> containers) =>
            System.Linq.Enumerable.ToArray(System.Linq.Enumerable.SelectMany(containers, StockOf));

        public static Stock[] StockOf(GridInventory bag)
        {
            var lines = new Stock[bag.Placements.Count];
            for (var i = 0; i < lines.Length; i++)
            {
                var placed = bag.Placements[i];
                lines[i] = new Stock(placed.Item.Id, placed.Item.Tags, placed.Count);
            }
            return lines;
        }

        /// <summary>True when every requirement can be paid from <paramref name="stock"/>. Each requirement
        /// claims units greedily, so one unit never satisfies two requirements (SYS-CRAFT-01 "consumed").</summary>
        public static bool HasIngredients(IngredientRef[] needs, IReadOnlyList<Stock> stock)
        {
            if (needs == null) return true;

            var remaining = new int[stock.Count];
            for (var i = 0; i < stock.Count; i++) remaining[i] = stock[i].Count;

            foreach (var need in needs)
            {
                var stillNeeded = need.Count;
                for (var i = 0; i < stock.Count && stillNeeded > 0; i++)
                {
                    if (!Matches(need, stock[i])) continue;
                    var take = System.Math.Min(stillNeeded, remaining[i]);
                    remaining[i] -= take;
                    stillNeeded -= take;
                }
                if (stillNeeded > 0) return false;
            }
            return true;
        }

        /// <summary>Whether one stock line can pay one requirement. Shared by the check above and the
        /// server-side consumption, so the two can never disagree about what counts.</summary>
        public static bool Matches(IngredientRef need, Stock line)
        {
            if (need.Item.IsValid) return line.Item == need.Item;
            if (need.Tag != null)
                foreach (var tag in line.Tags)
                    if (tag == need.Tag) return true;
            return false;
        }
    }
}
