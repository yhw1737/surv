using Isle.Core.Ids;

namespace Isle.Data
{
    /// <summary>
    /// SYS-CRAFT-02 material ("stuff"): what a crafted thing is made of. Gear is defined once as a template (a T-shirt,
    /// a sword) and made from any material whose <see cref="Categories"/> it accepts; the material sets the result's
    /// protection, warmth, durability, weight and power. Every combination is generated at load
    /// (<c>StuffVariants</c>), so a new material works with every template without new item files.
    /// </summary>
    public sealed class MaterialDef : IDefinition
    {
        public NamespacedId Id { get; init; }

        /// <summary>The adjective in a variant's name ("iron" → "iron sword").</summary>
        public string Name { get; init; }

        /// <summary>The item consumed when crafting with it.</summary>
        public NamespacedId Item { get; init; }

        /// <summary><c>fabric</c>, <c>leather</c>, <c>fur</c>, <c>wood</c>, <c>stone</c>, <c>metal</c>.</summary>
        public string[] Categories { get; init; }

        /// <summary>Tool tier (veins check it) and the old tier ladder: 1 stone/wood/hide, 2 copper, 3 iron, 4 steel.</summary>
        public int Tier { get; init; } = 1;

        /// <summary>Crafting level needed to work it (the recipe's own requirement applies too).</summary>
        public int CraftLevel { get; init; } = 1;

        /// <summary>Station required to work it, overriding the recipe's (metal → anvil). Invalid = the recipe's.</summary>
        public NamespacedId Station { get; init; }

        /// <summary>Protection multipliers by damage group: sharp (slash, pierce), blunt, heat.</summary>
        public float ArmorSharp { get; init; }
        public float ArmorBlunt { get; init; }
        public float ArmorHeat { get; init; }

        /// <summary>Warmth multiplier (cold insulation).</summary>
        public float Warmth { get; init; }

        /// <summary>Durability multiplier on the template's base.</summary>
        public float Durability { get; init; } = 1f;

        /// <summary>kg each unit adds to the result.</summary>
        public float Mass { get; init; }

        /// <summary>Weapon power multipliers for sharp (slash, pierce) and blunt hits; also arrow power.</summary>
        public float PowerSharp { get; init; }
        public float PowerBlunt { get; init; }

        /// <summary>Tools: harvest time is divided by this.</summary>
        public float ToolSpeed { get; init; } = 1f;

        /// <summary>SYS-HUNT-01 toolFactor for a knife made of it (stone 0.70, iron 1.00). 0 = not a knife material.</summary>
        public float ButcherFactor { get; init; }

        /// <summary>Tint for the result's look (worn colour, blade, icon).</summary>
        public string Color { get; init; }
    }

    /// <summary>On a template item: which material categories it can be made of, and how many units it takes.</summary>
    public sealed class StuffSpec
    {
        public string[] Categories { get; init; }
        public int Amount { get; init; } = 1;
    }
}
