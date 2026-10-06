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

        /// <summary>Placeholder shape and colour. Null falls back to a tag-coloured circle.</summary>
        public VisualSpec Visual { get; init; }

        /// <summary>Rolled once per individual — this is why two boars butcher differently.</summary>
        public WeightDistribution WeightDist { get; init; }

        public float HealthPerKg { get; init; }

        /// <summary>Behaviour profile, e.g. <c>isle:territorial_charger</c> (T-115).</summary>
        public NamespacedId Ai { get; init; }

        public SpawnSpec Spawn { get; init; }
        public ButcherSpec Butcher { get; init; }
        public CarrySpec Carry { get; init; }

        /// <summary>How it fights and how far it senses you (SYS-COMBAT-01 §Creature AI: "all parameters
        /// load from definitions"). Null means it never fights back and never reacts.</summary>
        public CreatureCombatSpec Combat { get; init; }
    }

    /// <summary>Numbers the creature AI reads. Values are per-def data, never C# constants.</summary>
    public sealed class CreatureCombatSpec
    {
        /// <summary>Tiles within which the AI preset reacts to the player.</summary>
        public float VisionTiles { get; init; }

        public float MoveSpeed { get; init; }

        /// <summary>Movement speed while fleeing or charging.</summary>
        public float ChaseSpeed { get; init; }

        /// <summary>Damage per bite/strike to the player's HP.</summary>
        public float Damage { get; init; }

        /// <summary>Tiles within which an engaged creature can strike.</summary>
        public float AttackRangeTiles { get; init; }

        /// <summary>Seconds between strikes.</summary>
        public float AttackIntervalSeconds { get; init; }

        /// <summary>Body radius in tiles at the def's mean weight. Reach to this creature is measured to its body edge.</summary>
        public float BodyRadiusTiles { get; init; }

        /// <summary>For the alert-then-flee preset: how long the creature alerts before running. [invented] per def.</summary>
        public float AlertSeconds { get; init; }

        /// <summary>Seconds between starting a strike and it landing — the tell a player reads to block or parry
        /// (SYS-COMBAT-01 §Melee). 0 strikes instantly. [invented] per def.</summary>
        public float WindupSeconds { get; init; }
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
