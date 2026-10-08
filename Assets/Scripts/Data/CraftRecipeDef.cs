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

        /// <summary>The material slot, for recipes that make a template (SYS-CRAFT-02). Null otherwise.</summary>
        public StuffIngredient Stuff { get; init; }
        public RecipeOutput Output { get; init; }
        public float TimeSec { get; init; }

        /// <summary>XP this grants (docs/content/xp_table.md). Null grants none.</summary>
        public XpAward Xp { get; init; }

        /// <summary>Minigame to run, e.g. <c>isle:forging</c>. Invalid means craft instantly.</summary>
        public NamespacedId Minigame { get; init; }
    }

    /// <summary>SYS-CRAFT-02: a recipe whose output is a template asks the crafter to pick a material from these
    /// categories; <see cref="Count"/> units of it are used (the template's <c>stuff.amount</c> when 0).</summary>
    public sealed class StuffIngredient
    {
        public string[] Categories { get; init; }
        public int Count { get; init; }
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
