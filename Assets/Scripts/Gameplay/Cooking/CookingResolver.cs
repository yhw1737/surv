using System.Collections.Generic;
using System.Linq;
using Isle.Core.Ids;
using Isle.Data;

namespace Isle.Gameplay.Cooking
{
    /// <summary>What a cook produced, before it becomes an item.</summary>
    public sealed class CookResult
    {
        public float Hunger { get; set; }
        public float Thirst { get; set; }
        public float Preservation { get; set; }
        public float BuffDurationMult { get; set; } = 1f;
        public bool CareTag { get; set; }
        public bool Failed { get; set; }
        public List<(NamespacedId Buff, float Power)> Buffs { get; } = new();
    }

    /// <summary>
    /// SYS-COOK-01 §Resolution algorithm, steps 1–6. Static pure. Ingredients are whatever is in the pot; the method
    /// def supplies every number. Not built yet: weightScale (no per-individual item weight — always 1), the quality
    /// step (no quality formula in the spec — every dish is Common), and the naming step (DishFactory).
    /// </summary>
    public static class CookingResolver
    {
        /// <summary>SYS-COOK-01 step 6.</summary>
        public const int CareTagThreshold = 40;
        public const float CareDurationBonus = 1.5f;

        /// <summary>SCHEMA §Tags "Ingredient type" row — the groups stew counts for its two-buff rule.
        /// ponytail: a fixed vocabulary from SCHEMA; becomes a tag-group def when tags get their own files.</summary>
        static readonly HashSet<string> IngredientGroups = new()
        {
            "meat", "fish", "vegetable", "grain", "fruit", "fat", "spice", "ferment_agent", "high_water", "starch",
        };

        public static bool CountAllowed(CookMethodDef method, int count) =>
            count >= (method.Input?.MinItems ?? 1) && count <= (method.Input?.MaxItems ?? int.MaxValue);

        /// <param name="failureRoll">Uniform [0, 1); the cook fails when it's below the method's failure rate.</param>
        public static CookResult Resolve(CookMethodDef method, IReadOnlyList<ItemDef> ingredients, int cookingLevel, float failureRoll)
        {
            var mods = method.Modifiers ?? new CookModifiers();
            var result = new CookResult
            {
                Hunger = ingredients.Sum(i => i.Nutrition?.Hunger ?? 0f) * mods.Hunger,
                Thirst = ingredients.Sum(i => i.Nutrition?.Thirst ?? 0f) * mods.Thirst,
                Preservation = mods.Preservation,
                BuffDurationMult = mods.BuffDuration,
            };

            var allTags = Flatten(ingredients.SelectMany(i => i.Tags ?? System.Array.Empty<string>()));
            foreach (var reaction in method.TagReactions ?? System.Array.Empty<TagReaction>())
            {
                if (reaction.When == null || !reaction.When.All(allTags.Contains)) continue;
                if (reaction.GrantBuff.IsValid) AddBuff(result, reaction.GrantBuff, reaction.Power * mods.BuffPower);
                if (reaction.Modifiers != null)
                {
                    result.Hunger *= reaction.Modifiers.Hunger;
                    result.Thirst *= reaction.Modifiers.Thirst;
                    result.Preservation *= reaction.Modifiers.Preservation;
                }
            }

            var groups = allTags.Count(IngredientGroups.Contains);
            var maxBuffs = method.MaxBuffs > 1 && groups >= method.MaxBuffsMinGroups ? method.MaxBuffs : 1;
            var kept = result.Buffs.OrderByDescending(b => b.Power).Take(maxBuffs).ToList();
            result.Buffs.Clear();
            result.Buffs.AddRange(kept);

            if (cookingLevel >= CareTagThreshold)
            {
                result.CareTag = true;
                result.BuffDurationMult *= CareDurationBonus;
            }

            if (method.Failure != null)
                result.Failed = failureRoll < method.Failure.BaseRate - cookingLevel * method.Failure.SkillReduction;
            return result;
        }

        /// <summary>The same buff from two reactions counts once, at its strongest.</summary>
        static void AddBuff(CookResult result, NamespacedId buff, float power)
        {
            var index = result.Buffs.FindIndex(b => b.Buff == buff);
            if (index < 0) result.Buffs.Add((buff, power));
            else if (power > result.Buffs[index].Power) result.Buffs[index] = (buff, power);
        }

        /// <summary>SYS-CORE-01 tag hierarchy: <c>fish/saltwater</c> also counts as <c>fish</c>.</summary>
        public static HashSet<string> Flatten(IEnumerable<string> tags)
        {
            var flat = new HashSet<string>();
            foreach (var tag in tags)
            {
                var path = tag;
                while (true)
                {
                    flat.Add(path);
                    var slash = path.LastIndexOf('/');
                    if (slash < 0) break;
                    path = path.Substring(0, slash);
                }
            }
            return flat;
        }
    }
}
