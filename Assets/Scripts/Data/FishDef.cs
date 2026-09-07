using System.Collections.Generic;
using Isle.Core.Ids;

namespace Isle.Data
{
    /// <summary>A catchable fish (SCHEMA §Fish).</summary>
    public sealed class FishDef : IDefinition
    {
        public NamespacedId Id { get; init; }

        /// <summary>Language key, e.g. <c>"@fish.lanternfish"</c>.</summary>
        public string Name { get; init; }
        public string[] Tags { get; init; }
        public WeightDistribution WeightDist { get; init; }

        /// <summary>Feeds SYS-FISH-01's probability weights.</summary>
        public HabitatSpec Habitat { get; init; }

        /// <summary>Bait item to weight multiplier. Absent bait means no affinity, not zero chance.</summary>
        public Dictionary<NamespacedId, float> BaitAffinity { get; init; }

        /// <summary>Rig kinds, e.g. <c>["rod","longline","net"]</c>. Unnamespaced in SCHEMA.</summary>
        public string[] RigAllowed { get; init; }

        public int MinSkill { get; init; }
        public FightSpec Fight { get; init; }
        public ButcherSpec Butcher { get; init; }
    }

    /// <summary>
    /// Where the fish lives. ponytail: the numeric bands stay bare <c>[min, max]</c> pairs, as the
    /// JSON writes them; T-012 validates length.
    /// </summary>
    public sealed class HabitatSpec
    {
        public float[] Depth { get; init; }
        public float[] WaterTemp { get; init; }
        public string[] Terrain { get; init; }
        public string[] Time { get; init; }
        public string[] Weather { get; init; }
    }

    /// <summary>The tension minigame's parameters (SYS-FISH-01, T-081).</summary>
    public sealed class FightSpec
    {
        public string Pattern { get; init; }

        /// <summary>Smaller is harder; skill widens it.</summary>
        public float TensionWindow { get; init; }

        public float Stamina { get; init; }

        /// <summary>Above this weight a solo angler is penalized.</summary>
        public float CoopThresholdKg { get; init; }
    }
}
