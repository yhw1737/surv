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

        /// <summary>SYS-COMBAT-02: damage multiplier per type it takes (e.g. a shell: slash 0.5, blunt 1.5). Missing = 1.</summary>
        public System.Collections.Generic.Dictionary<string, float> Resist { get; init; }

        /// <summary>How the creature is drawn (SYS-CHAR-02 §Creatures). Presentation only — gameplay never reads it.</summary>
        public CreatureLook Look { get; init; }

        /// <summary><c>water</c>: lives only in flooded dungeon water — moves only there and hides when it drains
        /// (moray eel). Null = land.</summary>
        public string Habitat { get; init; }

        /// <summary>How many spawn together on one dungeon spawn mark (a swarm). 0/1 = alone.</summary>
        public int GroupSize { get; init; }

        /// <summary>Boss behaviour (SYS-DUNG-01); null for ordinary creatures.</summary>
        public BossSpec Boss { get; init; }
    }

    /// <summary>A creature's cartoon look: a body plan the figure painter knows (<c>quadruped reptile snake crab bird
    /// frog turtle</c>), colours, and for quadrupeds its proportions (tiles at the mean weight) and features.</summary>
    public sealed class CreatureLook
    {
        public string Body { get; init; }
        public string Color { get; init; }
        public string Accent { get; init; }
        public string Belly { get; init; }
        public string Ears { get; init; }
        public string Tail { get; init; }
        public string Gait { get; init; }
        public bool Tusks { get; init; }
        public bool Antlers { get; init; }
        public float BodyLength { get; init; } = 0.8f;
        public float BodyHeight { get; init; } = 0.36f;
        public float LegLength { get; init; } = 0.3f;
        public float HeadSize { get; init; } = 0.2f;
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

        /// <summary>SYS-COMBAT-02 type of its strike (<c>blunt slash pierce heat toxic</c>). Null: blunt.</summary>
        public string DamageType { get; init; }

        /// <summary>After the wind-up the creature dashes this many tiles at the player before the strike lands, so
        /// stepping back doesn't make it whiff — a sidestep or a roll still does. 0: strikes where it stands. [invented]</summary>
        public float LungeTiles { get; init; }

        /// <summary>Speed of that dash, tiles per second. [invented]</summary>
        public float LungeSpeed { get; init; }
    }

    /// <summary>Where and when this creature appears.</summary>
    public sealed class SpawnSpec
    {
        public NamespacedId[] Biomes { get; init; }

        /// <summary>Time-of-day bands, e.g. <c>["day","dusk"]</c>. Unnamespaced in SCHEMA.</summary>
        public string[] Time { get; init; }

        public float Density { get; init; }

        /// <summary>Optional: gather into patches (groves, rock fields, meadows) instead of spreading evenly.</summary>
        public ClusterSpec Cluster { get; init; }

        /// <summary>Density multiplier on land biomes not in <see cref="Biomes"/> — "found elsewhere, but rarely". 0: never.</summary>
        public float Elsewhere { get; init; }
    }

    /// <summary>SYS-WORLD-03 §Clustering: a smooth noise field of <see cref="ScaleTiles"/>-wide blobs marks patches
    /// covering about <see cref="Coverage"/> of the land; density is multiplied by <see cref="Inside"/> in them and by
    /// <see cref="Outside"/> between them.</summary>
    public sealed class ClusterSpec
    {
        public float ScaleTiles { get; init; } = 32f;
        public float Coverage { get; init; } = 0.3f;
        public float Inside { get; init; } = 3f;
        public float Outside { get; init; } = 0.15f;
    }

    /// <summary>Carcass handling (SYS-HUNT-01 §Carry).</summary>
    public sealed class CarrySpec
    {
        /// <summary>False means the carcass is a world object and can never enter a grid.</summary>
        public bool InventoryAllowed { get; init; }

        public float DragSpeedPenalty { get; init; }
        public float CoopCarryPenalty { get; init; }
    }

    /// <summary>SYS-DUNG-01 boss: a sweep that hits everyone around it, an optional shell phase, and guaranteed drops.</summary>
    public sealed class BossSpec
    {
        /// <summary>Its strike hits every player within this many tiles instead of one target. 0 = a normal strike.</summary>
        public float SweepRadiusTiles { get; init; }

        public BossShellSpec Shell { get; init; }

        public BossSummonSpec Summon { get; init; }

        public BossOverheatSpec Overheat { get; init; }

        /// <summary>Dropped where it dies, on top of its butcher yields.</summary>
        public IngredientRef[] Drops { get; init; }
    }

    /// <summary>After every <see cref="Charges"/> charges (lunges) it overheats: it stops for <see cref="Seconds"/> and
    /// takes <see cref="DamageMult"/> damage meanwhile — the window to hit it hard (Clockwork Ruin's Sentinel).</summary>
    public sealed class BossOverheatSpec
    {
        public int Charges { get; init; } = 3;
        public float Seconds { get; init; }
        public float DamageMult { get; init; } = 1f;
    }

    /// <summary>Below <see cref="BelowHealth"/> it calls <see cref="Count"/> of <see cref="Creature"/> every
    /// <see cref="EverySeconds"/>, never more than <see cref="MaxAlive"/> at once — and not while it's burning.</summary>
    public sealed class BossSummonSpec
    {
        public NamespacedId Creature { get; init; }
        public int Count { get; init; }
        public float EverySeconds { get; init; }
        public float BelowHealth { get; init; }
        public int MaxAlive { get; init; } = 6;
    }

    /// <summary>Below <see cref="BelowHealth"/> it hides every <see cref="EverySeconds"/> for <see cref="Seconds"/>,
    /// taking <see cref="DamageMult"/> damage; a stun breaks it.</summary>
    public sealed class BossShellSpec
    {
        public float BelowHealth { get; init; }
        public float Seconds { get; init; }
        public float EverySeconds { get; init; }
        public float DamageMult { get; init; } = 1f;
    }
}
