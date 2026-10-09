using System.Linq;
using Isle.Core.Ids;
using Isle.Data;

namespace Isle.Modding.Defs
{
    /// <summary>
    /// SYS-HUNT-01 §Carrying: every butcherable creature light enough to carry (some individuals under 15 kg) gets a bag
    /// item — "isle:carcass__rabbit" — 1×2 when the species averages under 5 kg, 2×3 otherwise. Generated at load like the
    /// material variants, so a modded animal is carryable with no item file. The item's weight is the species mean; the
    /// real weight rides with each carried body.
    /// </summary>
    public static class CarcassItems
    {
        public const string Tag = "carcass";
        const float SmallMaxKg = 5f;
        const float CarryMaxKg = 15f;

        public static NamespacedId IdFor(NamespacedId creature) => NamespacedId.Parse($"{creature.Namespace}:carcass__{creature.Name}");

        public static void Expand(LoadResult<ItemDef> items, LoadResult<CreatureDef> creatures)
        {
            foreach (var creature in creatures.Definitions.Where(c => c.Butcher?.Yields is { Length: > 0 } && c.WeightDist != null && c.WeightDist.Min < CarryMaxKg))
            {
                var small = creature.WeightDist.Mean < SmallMaxKg;
                items.Definitions.Add(new ItemDef
                {
                    Id = IdFor(creature.Id),
                    Name = $"@pattern.carcass_of|{creature.Name}",
                    Tags = new[] { Tag },
                    Grid = small ? new GridSize { W = 1, H = 2 } : new GridSize { W = 2, H = 3 },
                    Weight = creature.WeightDist.Mean,
                    Stack = 1,
                    IconStyle = new IconSpec { Shape = "carcass", Color = creature.Look?.Color ?? "#8A6A48", Accent = creature.Look?.Belly ?? creature.Look?.Accent },
                });
                items.SourcePaths.Add("(generated)");
            }
        }
    }
}
