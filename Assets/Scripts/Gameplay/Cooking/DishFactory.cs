using System;
using System.Collections.Generic;
using System.Linq;
using Isle.Core.Ids;
using Isle.Data;

namespace Isle.Gameplay.Cooking
{
    /// <summary>
    /// Turns a <see cref="CookResult"/> into an item (SYS-COOK-01 step 7, naming). Dishes aren't in any definition
    /// file — "no hardcoded recipes" — so they're built at runtime, and the same method + ingredients always
    /// returns the same <see cref="ItemDef"/> instance so dishes stack. <see cref="KeyOf"/> / <see cref="Rebuild"/>
    /// let a save file bring a dish back after a restart.
    /// </summary>
    public static class DishFactory
    {
        /// <summary>SYS-COOK-01 method table: raw's preservation. Ingredient <c>spoilage.base_hours</c> is read as the
        /// raw state's lifetime, and a dish lasts preservation / 0.30 times as long. [invented] reading of a
        /// relative column.</summary>
        public const float RawPreservation = 0.30f;

        static readonly HashSet<string> DropTags = new() { "raw", "spoiled" };

        static readonly Dictionary<string, ItemDef> _byKey = new();
        static readonly Dictionary<ItemDef, string> _keyOf = new();

        public static void Clear()
        {
            _byKey.Clear();
            _keyOf.Clear();
        }

        /// <summary>Ingredient ids that went into a dish (repeats collapsed), for its tooltip.</summary>
        public static IReadOnlyList<string> IngredientsOf(ItemDef dish)
        {
            var key = KeyOf(dish);
            return key == null ? Array.Empty<string>() : key.Split('|')[1].Split(',').Distinct().ToArray();
        }

        public static bool IsDish(ItemDef item) => item != null && _keyOf.ContainsKey(item);

        /// <summary>"method|ingredient,ingredient" — what a save stores for a dish.</summary>
        public static string KeyOf(ItemDef dish) => _keyOf.GetValueOrDefault(dish);

        public static ItemDef Create(CookMethodDef method, IReadOnlyList<ItemDef> ingredients, CookResult result)
        {
            var key = method.Id.Value + "|" + string.Join(",", ingredients.Select(i => i.Id.Value).OrderBy(v => v, StringComparer.Ordinal));
            if (_byKey.TryGetValue(key, out var existing)) return existing;

            // The dish is named after its heaviest ingredient (a fillet with berries is fish, not berries).
            var main = ingredients.OrderByDescending(i => i.Weight).ThenByDescending(i => i.Nutrition?.Hunger ?? 0f).First();
            var others = ingredients.Where(i => i.Id != main.Id).Select(i => i.Name).Distinct().ToList();
            var largest = ingredients.OrderByDescending(i => i.Grid.W * i.Grid.H).First().Grid;
            var perishable = ingredients.Where(i => i.Spoilage != null && i.Spoilage.BaseHours > 0f).ToList();

            var dish = new ItemDef
            {
                Id = NamespacedId.Parse("isle:dish_" + StableHash(key)),
                // "@pattern.grilled|@item.raw_meat" alone; "@pattern.grilled_with|main|a+b" with side ingredients.
                Name = others.Count == 0
                    ? (method.Naming ?? method.Name) + "|" + main.Name
                    : (method.Naming ?? method.Name) + "_with|" + main.Name + "|" + string.Join("+", others),
                Tags = new[] { "food", "cooked", "dish" }
                    .Concat(ingredients.SelectMany(i => i.Tags ?? Array.Empty<string>()).Where(t => !DropTags.Contains(t)))
                    .Distinct().ToArray(),
                Grid = method.ResultGrid ?? largest,
                Weight = ingredients.Sum(i => i.Weight),
                Stack = 10,
                Nutrition = new NutritionSpec { Hunger = result.Hunger, Thirst = result.Thirst },
                Spoilage = perishable.Count == 0 ? null : new SpoilageSpec
                {
                    BaseHours = perishable.Average(i => i.Spoilage.BaseHours) * result.Preservation / RawPreservation,
                    Result = perishable[0].Spoilage.Result,
                },
                Buffs = result.Buffs.Select(b => b.Buff).ToArray(),
                BuffDurationMult = result.BuffDurationMult,
                DishSignature = method.Id.Value + "|" + MainGroup(main),
            };
            _byKey[key] = dish;
            _keyOf[dish] = key;
            return dish;
        }

        /// <summary>Brings a dish back from its key. Resolved at no skill and no failure — a care tag isn't restored.</summary>
        public static ItemDef Rebuild(string key, Func<NamespacedId, CookMethodDef> methods, Func<NamespacedId, ItemDef> items)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (_byKey.TryGetValue(key, out var existing)) return existing;
            var parts = key.Split('|');
            if (parts.Length != 2 || !NamespacedId.TryParse(parts[0], out var methodId, out _)) return null;
            var method = methods(methodId);
            if (method == null) return null;

            var ingredients = new List<ItemDef>();
            foreach (var text in parts[1].Split(','))
            {
                if (!NamespacedId.TryParse(text, out var id, out _) || items(id) is not { } item) return null;
                ingredients.Add(item);
            }
            return Create(method, ingredients, CookingResolver.Resolve(method, ingredients, cookingLevel: 1, failureRoll: 1f));
        }

        /// <summary>SYS-COOK-01 dishSignature for eating <paramref name="item"/> by <paramref name="method"/> — used for
        /// raw eating, where no dish item exists to carry it.</summary>
        public static string SignatureFor(CookMethodDef method, ItemDef item) => method.Id.Value + "|" + MainGroup(item);

        static string MainGroup(ItemDef main) =>
            main.Tags?.FirstOrDefault(t => t != "food" && !DropTags.Contains(t)) ?? main.Id.Value;

        /// <summary>FNV-1a, lower-case hex — a stable id across runs (string.GetHashCode isn't).</summary>
        static string StableHash(string text)
        {
            unchecked
            {
                var hash = 2166136261u;
                foreach (var c in text) hash = (hash ^ c) * 16777619u;
                return hash.ToString("x8");
            }
        }
    }
}
