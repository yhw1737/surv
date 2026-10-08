using System;
using System.Collections.Generic;
using System.Linq;
using Isle.Core.Ids;
using Isle.Data;

namespace Isle.Gameplay.Character
{
    /// <summary>SYS-START-01: one cruise passenger — a name, what they wear, traits, and starting skill levels.</summary>
    public sealed class Survivor
    {
        public string Name;
        public StartOutfit Outfit;
        public readonly List<TraitDef> Traits = new();
        public readonly Dictionary<NamespacedId, int> Skills = new();

        public TraitDef Background => Traits.FirstOrDefault(t => t.Background);

        /// <summary>Trait costs plus the outfit's: rolled passengers come out at 0 or −1.</summary>
        public int Points => Traits.Sum(t => t.Cost) + (Outfit?.Cost ?? 0);
    }

    /// <summary>
    /// SYS-START-01 §Rolling: a random passenger. An outfit (better clothes cost points), maybe a background (its skills
    /// +N), then good and bad traits — 3 to 6 traits in all — until the points balance at 0 or −1. Never two traits from
    /// one group, never two that exclude each other. Skills 0 … SkillMax. Static pure; the caller owns the randomness.
    /// </summary>
    public static class SurvivorGenerator
    {
        public static bool Compatible(TraitDef trait, IEnumerable<TraitDef> chosen) => chosen.All(c =>
            c != trait
            && (string.IsNullOrEmpty(trait.Group) || c.Group != trait.Group)
            && !(trait.Excludes?.Contains(c.Id) ?? false)
            && !(c.Excludes?.Contains(trait.Id) ?? false));

        public static Survivor Roll(Random rng, ScenarioDef scenario, IReadOnlyList<TraitDef> traits, IReadOnlyList<NamespacedId> skills)
        {
            var survivor = new Survivor
            {
                Name = scenario.Names is { Length: > 0 } names ? names[rng.Next(names.Length)] : "Survivor",
                Outfit = Pick(scenario.Outfits, rng),
            };
            var backgrounds = traits.Where(t => t.Background).ToList();
            var good = traits.Where(t => !t.Background && t.Cost > 0).ToList();
            var bad = traits.Where(t => !t.Background && t.Cost < 0).ToList();
            var neutral = traits.Where(t => !t.Background && t.Cost == 0).ToList();

            for (var attempt = 0; attempt < 256; attempt++)
            {
                var chosen = new List<TraitDef>();
                var target = rng.Next(scenario.TraitsMin, scenario.TraitsMax + 1);
                if (backgrounds.Count > 0 && rng.NextDouble() < scenario.BackgroundChance) chosen.Add(backgrounds[rng.Next(backgrounds.Count)]);
                var debt = chosen.Sum(t => t.Cost) + (survivor.Outfit?.Cost ?? 0);

                // Alternate: pay off any debt with a bad trait, otherwise take a good (or even) trait — until the count is
                // reached and the points sit at 0 or −1.
                var guard = 0;
                while ((chosen.Count < target || debt > 0) && chosen.Count < scenario.TraitsMax && guard++ < 64)
                {
                    TraitDef next;
                    if (debt > 0)
                        next = Shuffled(bad, rng).FirstOrDefault(t => -t.Cost <= debt + 1 && Compatible(t, chosen));
                    else if (chosen.Count == target - 1 && debt == 0)
                        // Last slot with the books balanced: a small trait (0 or −1) keeps them balanced.
                        next = Shuffled(neutral.Concat(bad.Where(t => t.Cost == -1)), rng).FirstOrDefault(t => Compatible(t, chosen));
                    else if (chosen.Count == target - 1 && debt == -1)
                        // One point spare: a +1 trait spends it, or a 0 trait keeps it.
                        next = Shuffled(good.Where(t => t.Cost == 1).Concat(neutral), rng).FirstOrDefault(t => Compatible(t, chosen));
                    else
                        next = Shuffled(good.Concat(neutral), rng).FirstOrDefault(t => Compatible(t, chosen));
                    if (next == null) break;
                    chosen.Add(next);
                    debt += next.Cost;
                }
                var points = debt;
                if (chosen.Count < scenario.TraitsMin || chosen.Count > scenario.TraitsMax || points > 0 || points < -1) continue;
                survivor.Traits.AddRange(chosen);
                break;
            }

            foreach (var skill in skills) survivor.Skills[skill] = rng.Next(scenario.SkillMax + 1);
            if (survivor.Background?.Skills != null)
                foreach (var bonus in survivor.Background.Skills)
                    survivor.Skills[bonus.Skill] = (survivor.Skills.TryGetValue(bonus.Skill, out var level) ? level : 0) + bonus.Levels;
            return survivor;
        }

        static StartOutfit Pick(StartOutfit[] outfits, Random rng)
        {
            if (outfits == null || outfits.Length == 0) return null;
            var x = rng.NextDouble() * outfits.Sum(o => o.Weight);
            foreach (var outfit in outfits)
            {
                x -= outfit.Weight;
                if (x <= 0) return outfit;
            }
            return outfits[^1];
        }

        static List<T> Shuffled<T>(IEnumerable<T> items, Random rng)
        {
            var list = items.ToList();
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
            return list;
        }
    }
}
