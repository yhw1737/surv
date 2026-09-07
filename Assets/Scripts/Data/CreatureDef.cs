using Isle.Core.Ids;

namespace Isle.Data
{
    /// <summary>A huntable creature (SCHEMA §Creatures).</summary>
    public sealed class CreatureDef : IDefinition
    {
        public NamespacedId Id { get; init; }

        /// <summary>Language key, e.g. <c>"@creature.mountain_boar"</c>.</summary>
        public string Name { get; init; }
        public string[] Tags { get; init; }

        /// <summary>Rolled once per individual — this is why two boars butcher differently.</summary>
        public WeightDistribution WeightDist { get; init; }

        public float HealthPerKg { get; init; }

        /// <summary>Behaviour profile, e.g. <c>isle:territorial_charger</c> (T-115).</summary>
        public NamespacedId Ai { get; init; }

        public SpawnSpec Spawn { get; init; }
        public ButcherSpec Butcher { get; init; }
        public CarrySpec Carry { get; init; }
    }

    /// <summary>Where and when this creature appears.</summary>
    public sealed class SpawnSpec
    {
        public NamespacedId[] Biomes { get; init; }

        /// <summary>Time-of-day bands, e.g. <c>["day","dusk"]</c>. Unnamespaced in SCHEMA.</summary>
        public string[] Time { get; init; }

        public float Density { get; init; }
    }

    /// <summary>Carcass handling (SYS-HUNT-01 §Carry).</summary>
    public sealed class CarrySpec
    {
        /// <summary>False means the carcass is a world object and can never enter a grid.</summary>
        public bool InventoryAllowed { get; init; }

        public float DragSpeedPenalty { get; init; }
        public float CoopCarryPenalty { get; init; }
    }
}
