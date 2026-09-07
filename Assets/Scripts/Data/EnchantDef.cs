using Isle.Core.Ids;

namespace Isle.Data
{
    /// <summary>An enchant (SCHEMA §Enchants). Overload enchanting is post-EA — see BACKLOG.</summary>
    public sealed class EnchantDef : IDefinition
    {
        public NamespacedId Id { get; init; }

        /// <summary>Language key, e.g. <c>"@enchant.tidebound"</c>.</summary>
        public string Name { get; init; }

        /// <summary>Tags this may be applied to — how a modder's enchant reaches our weapons.</summary>
        public string[] ApplicableTags { get; init; }

        public int MaxStack { get; init; }
        public EnchantEffect[] Effects { get; init; }
        public IngredientRef[] Catalyst { get; init; }
        /// <summary>Enchanting is the enchanter's skill, not the smith's — SYS-CRAFT-01 §Enchanting.</summary>
        public int EnchantingSkillRequired { get; init; }
    }

    /// <summary>
    /// One effect. The shape is heterogeneous by <see cref="Type"/> — a <c>damage_mult</c> effect
    /// carries <see cref="Value"/>, a <c>conditional</c> one carries <see cref="When"/> and
    /// <see cref="DamageMult"/> instead. ponytail: one nullable-field class rather than a
    /// polymorphic hierarchy; T-012 validates which fields a given type requires.
    /// </summary>
    public sealed class EnchantEffect
    {
        public string Type { get; init; }
        public float? Value { get; init; }
        public string When { get; init; }
        public float? DamageMult { get; init; }
    }
}
