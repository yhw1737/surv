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

        /// <summary>Units dropped when this creature dies. Prototype shortcut for SYS-HUNT-01's
        /// kg-to-units conversion (not specced yet); 0 or unset means 1.</summary>
        /// <summary>SYS-HUNT-01: kg of this cut per item (meat 2.5). With it, the count comes from the butchered weight;
        /// without it, <see cref="Count"/> is a fixed amount.</summary>
        public float UnitKg { get; init; }

        /// <summary>At least this many, however poor the butchering (meat 1: even a rabbit gives a piece).</summary>
        public int Min { get; init; }

        public int Count { get; init; }

        /// <summary>Degrades by weapon type and skill — the rule that makes guns cost you the hide.</summary>
        public bool DamageSensitive { get; init; }

        /// <summary>
        /// Skill whose level sets this output's quality. SCHEMA wrote this unnamespaced
        /// (<c>"hunting"</c>) alone among skill references; corrected to <c>isle:hunting</c> on
        /// 2026-09-07 so one rule covers every ID.
        /// </summary>
        public NamespacedId QualityFrom { get; init; }
    }

    /// <summary>Placeholder look (ART_PIPELINE §Placeholders): a shape key from the placeholder library, a "#RRGGBB"
    /// colour, and a size in tiles. Presentation only — gameplay never reads it (Absolute Rule 7).</summary>
    public sealed class VisualSpec
    {
        public string Shape { get; init; }
        public string Color { get; init; }
        public float Size { get; init; } = 1f;
        /// <summary>Lies on the ground (a crop plot, a rug) instead of standing up toward the camera. Things that stand
        /// — people, trees, stations — are the default.</summary>
        public bool Flat { get; init; }
        /// <summary>Per-instance variety for placed nodes, 0..1: each one is up to this much larger or smaller and
        /// lighter or darker, and may be mirrored. 0 draws them all alike.</summary>
        public float Jitter { get; init; }
        /// <summary>Shape key drawn while a harvestable node is used up (a tree's stump), until it grows back. Null
        /// fades the node to a faint ghost instead.</summary>
        public string Depleted { get; init; }
    }

    /// <summary>XP an action grants (docs/content/xp_table.md): <c>base + per_unit × units</c> to <c>skill</c>, before
    /// the focus multiplier. What "units" means is the action's (items harvested, ingredients, fish kg).</summary>
    public sealed class XpAward
    {
        public NamespacedId Skill { get; init; }
        public float Base { get; init; }
        public float PerUnit { get; init; }
    }

    /// <summary>SYS-CHAR-02 §Equipment: how an equipped item is drawn on the stick figure — what, not where. Style is
    /// one of <c>cap hood shirt cloak pants boots backpack pouch</c>; unknown styles fall back to a generic shape.
    /// Presentation only (Absolute Rule 7).</summary>
    public sealed class WearSpec
    {
        public string Style { get; init; }
        public string Color { get; init; }
    }

    /// <summary>SYS-CHAR-02 §Equipment: how a held item is drawn in the figure's hand. Style is one of
    /// <c>spear hatchet pickaxe rod torch bow sword</c>. <see cref="Length"/> in tiles; <see cref="Tip"/> colours the head
    /// (blade, stone, flame). Presentation only.</summary>
    public sealed class HoldSpec
    {
        public string Style { get; init; }
        public float Length { get; init; } = 1f;
        public string Color { get; init; }
        public string Tip { get; init; }
    }

    /// <summary>How an item's inventory icon is drawn (presentation only, Absolute Rule 7): a named drawing from the
    /// icon painter with a main and an accent colour. Items with a <c>hold</c> or <c>wear</c> block don't need one —
    /// their icon is drawn from that. On a cook method it is the dish's vessel (bowl, plate, skewer…), coloured by
    /// the ingredients.</summary>
    public sealed class IconSpec
    {
        public string Shape { get; init; }
        public string Color { get; init; }
        public string Accent { get; init; }
    }

    /// <summary>SYS-COMBAT-02: one attack — what it touches (<see cref="Shape"/> and its parameters, tiles/degrees) and
    /// what kind of damage it deals. A weapon lists one per combo step; the last is the finisher.</summary>
    public sealed class AttackSpec
    {
        /// <summary><c>arc thrust smash sweep line</c> (projectile/aura belong to ranged weapons and artifacts).</summary>
        public string Shape { get; init; } = "arc";

        public float Degrees { get; init; }
        public float Radius { get; init; }
        public float Length { get; init; }
        public float Width { get; init; }
        public float Offset { get; init; }

        /// <summary><c>blunt slash pierce heat toxic</c>.</summary>
        public string Type { get; init; } = "blunt";

        public float PowerMult { get; init; } = 1f;
    }
}
