using Isle.Core.Ids;

namespace Isle.Data
{
    /// <summary>A crafting recipe (SCHEMA §Craft recipes, SYS-CRAFT-01).</summary>
    public sealed class CraftRecipeDef : IDefinition
    {
        public NamespacedId Id { get; init; }

        /// <summary>Language key, e.g. <c>"@recipe.harpoon_steel"</c>.</summary>
        public string Name { get; init; }
        public NamespacedId Station { get; init; }

        /// <summary>One entry per gating skill; the <c>primary</c> one drives quality.</summary>
        public SkillRequirement[] Skills { get; init; }

        /// <summary>Enables the nearby-ally rule (SYS-CRAFT-01, T-092).</summary>
        public bool AllowAdjacentAssist { get; init; }

        public IngredientRef[] Ingredients { get; init; }
        public RecipeOutput Output { get; init; }
        public float TimeSec { get; init; }

        /// <summary>Minigame to run, e.g. <c>isle:forging</c>. Invalid means craft instantly.</summary>
        public NamespacedId Minigame { get; init; }
    }

    /// <summary>What the recipe produces.</summary>
    public sealed class RecipeOutput
    {
        public NamespacedId Item { get; init; }
        public int Count { get; init; }

        /// <summary>Enables quality tiers and enchant slots on the result.</summary>
        public bool InheritQuality { get; init; }
    }
}
