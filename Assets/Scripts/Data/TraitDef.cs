using Isle.Core.Ids;

namespace Isle.Data
{
    /// <summary>
    /// SYS-START-01: one trait a survivor can carry. Good traits cost points (<see cref="Cost"/> &gt; 0), bad ones give
    /// points (&lt; 0); a rolled survivor's costs sum to about zero. A background trait (a past job) raises skills.
    /// Effects use the same effect types as buffs, so every hook a dish can reach, a trait can too.
    /// </summary>
    public sealed class TraitDef : IDefinition
    {
        public NamespacedId Id { get; init; }
        public string Name { get; init; }
        public string Description { get; init; }

        /// <summary>Points: positive for a good trait (it costs), negative for a bad one (it pays).</summary>
        public int Cost { get; init; }

        /// <summary>At most one trait per group (strong / weak, fast / slow learner…).</summary>
        public string Group { get; init; }

        /// <summary>A background (past job): at most one per survivor; raises <see cref="Skills"/>.</summary>
        public bool Background { get; init; }

        public SkillBonus[] Skills { get; init; }

        /// <summary>Traits that can't come with this one (a soldier isn't frail). Checked both ways.</summary>
        public NamespacedId[] Excludes { get; init; }

        /// <summary>Permanent effects, same types as <see cref="BuffDef.Effects"/>.</summary>
        public BuffEffect[] Effects { get; init; }
    }

    public sealed class SkillBonus
    {
        public NamespacedId Skill { get; init; }
        public int Levels { get; init; }
    }

    /// <summary>
    /// SYS-START-01: how a game begins — where, in what state, wearing and carrying what, and the pool survivors are
    /// rolled from. The shipwreck: washed up wet on a beach among the wreckage.
    /// </summary>
    public sealed class ScenarioDef : IDefinition
    {
        public NamespacedId Id { get; init; }
        public string Name { get; init; }

        /// <summary>Biome of the start tile (the coast: a beach next to the open sea).</summary>
        public NamespacedId StartBiome { get; init; }

        /// <summary>Start soaked (SYS-SURV-01 wet penalty).</summary>
        public bool StartWet { get; init; }

        /// <summary>What a passenger might be wearing; one is rolled. Its <see cref="StartOutfit.Cost"/> counts against
        /// the trait points (better clothes, worse traits).</summary>
        public StartOutfit[] Outfits { get; init; }

        public ItemRoll Belongings { get; init; }
        public DebrisSpec Debris { get; init; }

        /// <summary>Survivor names (proper names, not translated).</summary>
        public string[] Names { get; init; }

        /// <summary>Rolled skills are 0 … this.</summary>
        public int SkillMax { get; init; } = 3;

        /// <summary>Chance a survivor has a background.</summary>
        public float BackgroundChance { get; init; }

        /// <summary>How many traits a passenger carries, background included.</summary>
        public int TraitsMin { get; init; } = 3;
        public int TraitsMax { get; init; } = 6;
    }

    /// <summary>A starting set of clothes and what it costs in trait points (negative = it pays).</summary>
    public sealed class StartOutfit
    {
        public string Name { get; init; }
        public NamespacedId[] Items { get; init; }
        public int Cost { get; init; }
        public float Weight { get; init; } = 1f;
    }

    /// <summary>A number of picks from a weighted pool.</summary>
    public sealed class ItemRoll
    {
        public int Min { get; init; }
        public int Max { get; init; }
        public WeightedItem[] Pool { get; init; }
    }

    public sealed class WeightedItem
    {
        public NamespacedId Item { get; init; }
        public int Count { get; init; } = 1;
        public float Weight { get; init; } = 1f;
    }

    /// <summary>Wreckage piles scattered along the beach around the start.</summary>
    public sealed class DebrisSpec
    {
        public int PilesMin { get; init; }
        public int PilesMax { get; init; }
        public float RadiusTiles { get; init; }
        public DebrisContent[] Contents { get; init; }
    }

    public sealed class DebrisContent
    {
        public NamespacedId Item { get; init; }
        public int Min { get; init; }
        public int Max { get; init; }
    }
}
