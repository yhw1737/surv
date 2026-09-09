using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Isle.Core.Ids;
using Isle.Data;

namespace Isle.Modding.Defs
{
    /// <summary>
    /// SYS-CORE-01 §Load pipeline step 7: verify every referenced id exists. Scoped to
    /// <see cref="NamespacedId"/> fields that target <see cref="ItemDef"/> — the only definition
    /// type anything else here actually references today. Skill (<c>SkillRequirement.Skill</c>,
    /// <c>ArtifactDef.ScalingSkill</c>, <c>WeaponDef.CombatSkill</c>, <c>ButcherYield.QualityFrom</c>),
    /// buff (<c>TagReaction.GrantBuff</c>), AI (<c>CreatureDef.Ai</c>), biome
    /// (<c>SpawnSpec.Biomes</c>), station, quest and moveset ids have no backing definition type
    /// yet (T-014/T-018/T-019/T-115 and friends) — checking them now would just report every one
    /// of them as unresolved, so they stay unchecked until a type exists to check them against.
    /// Multi-mod concerns (circular deps, patch conflicts — verification cases 5/8) are T-130.
    /// <para>
    /// Appends to each <see cref="LoadResult{T}.Errors"/> in place, alongside whatever
    /// <see cref="DefinitionLoader"/> already found — a definition that fails here already passed
    /// steps 4–5 on its own, so it stays in <c>Definitions</c>; whether to reject the whole batch
    /// on an unresolved reference is a mod-loader decision (T-014/T-130), not this one's to make.
    /// </para>
    /// </summary>
    public static class ReferenceResolver
    {
        public static void ResolveItemRefs(
            LoadResult<ItemDef> items,
            LoadResult<CraftRecipeDef> recipes = null,
            LoadResult<EnchantDef> enchants = null,
            LoadResult<CreatureDef> creatures = null,
            LoadResult<FishDef> fish = null,
            LoadResult<CropDef> crops = null,
            LoadResult<CookMethodDef> cookMethods = null)
        {
            var itemIds = new HashSet<NamespacedId>(items.Definitions.Select(d => d.Id));
            var jsonCache = new Dictionary<string, string>();

            if (recipes != null)
                for (var i = 0; i < recipes.Definitions.Count; i++)
                {
                    var def = recipes.Definitions[i];
                    var path = recipes.SourcePaths[i];
                    foreach (var ingredient in def.Ingredients ?? Array.Empty<IngredientRef>())
                        Check(ingredient.Item, itemIds, "item", path, jsonCache, recipes.Errors);
                    Check(def.Output?.Item ?? default, itemIds, "item", path, jsonCache, recipes.Errors);
                }

            if (enchants != null)
                for (var i = 0; i < enchants.Definitions.Count; i++)
                {
                    var def = enchants.Definitions[i];
                    var path = enchants.SourcePaths[i];
                    foreach (var catalyst in def.Catalyst ?? Array.Empty<IngredientRef>())
                        Check(catalyst.Item, itemIds, "item", path, jsonCache, enchants.Errors);
                }

            if (creatures != null)
                for (var i = 0; i < creatures.Definitions.Count; i++)
                {
                    var def = creatures.Definitions[i];
                    var path = creatures.SourcePaths[i];
                    foreach (var yield in def.Butcher?.Yields ?? Array.Empty<ButcherYield>())
                        Check(yield.Item, itemIds, "item", path, jsonCache, creatures.Errors);
                }

            if (fish != null)
                for (var i = 0; i < fish.Definitions.Count; i++)
                {
                    var def = fish.Definitions[i];
                    var path = fish.SourcePaths[i];
                    foreach (var yield in def.Butcher?.Yields ?? Array.Empty<ButcherYield>())
                        Check(yield.Item, itemIds, "item", path, jsonCache, fish.Errors);
                    var baitIds = def.BaitAffinity != null ? (IEnumerable<NamespacedId>)def.BaitAffinity.Keys : Array.Empty<NamespacedId>();
                    foreach (var baitId in baitIds)
                        Check(baitId, itemIds, "bait item", path, jsonCache, fish.Errors);
                }

            if (crops != null)
                for (var i = 0; i < crops.Definitions.Count; i++)
                {
                    var def = crops.Definitions[i];
                    var path = crops.SourcePaths[i];
                    Check(def.Output?.Item ?? default, itemIds, "item", path, jsonCache, crops.Errors);
                }

            if (cookMethods != null)
                for (var i = 0; i < cookMethods.Definitions.Count; i++)
                {
                    var def = cookMethods.Definitions[i];
                    var path = cookMethods.SourcePaths[i];
                    if (def.Failure != null)
                        Check(def.Failure.Result, itemIds, "item", path, jsonCache, cookMethods.Errors);
                }

            // Self-references (an item that spoils into another item) check against the same set.
            for (var i = 0; i < items.Definitions.Count; i++)
            {
                var def = items.Definitions[i];
                if (def.Spoilage != null)
                    Check(def.Spoilage.Result, itemIds, "item", items.SourcePaths[i], jsonCache, items.Errors);
            }
        }

        static void Check(NamespacedId id, HashSet<NamespacedId> validIds, string label, string path,
            Dictionary<string, string> jsonCache, List<LoadError> errors)
        {
            if (!id.IsValid || validIds.Contains(id)) return;

            var json = ReadCached(path, jsonCache);
            var line = JsonLine.OfValue(json, id.Value);
            var suggestion = SuggestClosest(id.Value, validIds);
            errors.Add(new LoadError(path, $"Unknown {label} ID: \"{id.Value}\".", line, suggestion));
        }

        static string ReadCached(string path, Dictionary<string, string> cache)
        {
            if (cache.TryGetValue(path, out var json)) return json;
            try { json = File.ReadAllText(path); }
            catch (IOException) { json = string.Empty; }
            cache[path] = json;
            return json;
        }

        /// <summary>SYS-CORE-01 wants candidates within Levenshtein distance 2, closest first.</summary>
        static string SuggestClosest(string badId, HashSet<NamespacedId> candidates)
        {
            string best = null;
            var bestDistance = int.MaxValue;
            foreach (var candidate in candidates.OrderBy(c => c.Value, StringComparer.Ordinal))
            {
                var distance = Levenshtein(badId, candidate.Value);
                if (distance > 2 || distance >= bestDistance) continue;
                bestDistance = distance;
                best = candidate.Value;
            }
            return best;
        }

        static int Levenshtein(string a, string b)
        {
            var dist = new int[a.Length + 1, b.Length + 1];
            for (var i = 0; i <= a.Length; i++) dist[i, 0] = i;
            for (var j = 0; j <= b.Length; j++) dist[0, j] = j;

            for (var i = 1; i <= a.Length; i++)
                for (var j = 1; j <= b.Length; j++)
                {
                    var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    dist[i, j] = Math.Min(Math.Min(dist[i - 1, j] + 1, dist[i, j - 1] + 1), dist[i - 1, j - 1] + cost);
                }

            return dist[a.Length, b.Length];
        }
    }
}
