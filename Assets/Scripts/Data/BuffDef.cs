using Isle.Core.Ids;

namespace Isle.Data
{
    /// <summary>
    /// A buff definition (SCHEMA §Buffs, SYS-BUFF-01). Modder-addable — any system that wants to
    /// grant a status effect points a <c>grant_buff</c> reference at one of these (already wired
    /// from <see cref="TagReaction.GrantBuff"/>) instead of hardcoding a new case per consumer.
    /// </summary>
    public sealed class BuffDef : IDefinition
    {
        public NamespacedId Id { get; init; }

        /// <summary>Language key, e.g. <c>"@buff.steady_hand"</c>.</summary>
        public string Name { get; init; }

        /// <summary>In-game minutes (GLOSSARY §Units). 0 = until cleared some other way, not time.</summary>
        public long DurationMin { get; init; }

        public BuffEffect[] Effects { get; init; }
    }

    /// <summary>
    /// One effect. Heterogeneous by <see cref="Type"/>, same pattern as <c>EnchantEffect</c>
    /// (T-012) — <c>SchemaValidator</c> checks which fields a given type requires, not the type
    /// system.
    /// </summary>
    public sealed class BuffEffect
    {
        public string Type { get; init; }
        public float? Value { get; init; }
    }
}
