using Isle.Core.Ids;

namespace Isle.Data
{
    /// <summary>
    /// Grid footprint in inventory cells (SCHEMA §Items, SYS-INV-01). Rotation is inferred from
    /// <c>W != H</c>, so there is no separate orientation field.
    /// </summary>
    public struct GridSize
    {
        public int W { get; init; }
        public int H { get; init; }
    }

    /// <summary>
    /// Per-individual weight roll for creatures and fish (SCHEMA §Creatures).
    /// <para>
    /// <see cref="Type"/> names the sampler. Only <c>lognormal</c> is documented, so it stays a
    /// string — a one-member enum would have to invent the rest of the list.
    /// </para>
    /// </summary>
    public sealed class WeightDistribution
    {
        public string Type { get; init; }
        public float Mean { get; init; }
        public float Sigma { get; init; }
        public float Min { get; init; }
        public float Max { get; init; }
    }

    /// <summary>
    /// A skill gate. Shared by <c>cook_methods.unlock_skill</c> and <c>recipes.skills[]</c>.
    /// <see cref="Primary"/> only means anything in a recipe; <c>unlock_skill</c> omits it.
    /// </summary>
    public sealed class SkillRequirement
    {
        public NamespacedId Skill { get; init; }
        public int Level { get; init; }
        public bool Primary { get; init; }
    }

    /// <summary>
    /// A material slot: exactly one of <see cref="Item"/> or <see cref="Tag"/> is set. The tag form
    /// is what lets a new wood satisfy an old recipe (Absolute Rule 4). Shared by recipe
    /// ingredients and enchant catalysts.
    /// <para>"Exactly one" is a load-time check — <c>SchemaValidator</c> (T-012) owns it.</para>
    /// </summary>
    public sealed class IngredientRef
    {
        public NamespacedId Item { get; init; }
        public string Tag { get; init; }
        public int Count { get; init; }
    }

    /// <summary>
    /// Butchery inputs (SCHEMA §Creatures, §Fish). The formula lives in SYS-HUNT-01; a modder
    /// fills these in without knowing the math.
    /// </summary>
    public sealed class ButcherSpec
    {
        public float EdibleRatio { get; init; }

        /// <summary>
        /// <c>[min, max]</c>. ponytail: a bare pair, exactly as the JSON writes it, rather than a
        /// <c>Range</c> type — length is a validator concern (T-012), not a shape concern. Fish
        /// omit this.
        /// </summary>
        public float[] ConditionRange { get; init; }

        public ButcherYield[] Yields { get; init; }
    }

    /// <summary>One output of butchering, as a share of the edible mass.</summary>
    public sealed class ButcherYield
    {
        public NamespacedId Item { get; init; }
        public float Share { get; init; }

        /// <summary>Degrades by weapon type and skill — the rule that makes guns cost you the hide.</summary>
        public bool DamageSensitive { get; init; }

        /// <summary>
        /// Skill whose level sets this output's quality. SCHEMA wrote this unnamespaced
        /// (<c>"hunting"</c>) alone among skill references; corrected to <c>isle:hunting</c> on
        /// 2026-09-07 so one rule covers every ID.
        /// </summary>
        public NamespacedId QualityFrom { get; init; }
    }
}
