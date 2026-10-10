using Isle.Core.Ids;

namespace Isle.Data
{
    /// <summary>SYS-DUNG-01: a dungeon the island generates — where its entrance goes, how deep and dangerous it is,
    /// what lives in it, and the soft gate its vault hides behind.</summary>
    public sealed class DungeonDef : IDefinition
    {
        public NamespacedId Id { get; init; }
        public string Name { get; init; }

        /// <summary>★ count (1–4): sets the soft-gate skill level (10/20/30) and the locks per floor (1/1/2).</summary>
        public int Danger { get; init; } = 1;

        public int Floors { get; init; } = 1;

        public DungeonEntrance Entrance { get; init; }

        /// <summary>Floor and wall colours of its tiles (presentation).</summary>
        public string FloorColor { get; init; }
        public string WallColor { get; init; }

        /// <summary>Creatures placed on the rooms' spawn marks, picked in turn.</summary>
        public NamespacedId[] Creatures { get; init; }

        /// <summary>The obstacle in front of each floor's vault.</summary>
        public DungeonGate Gate { get; init; }

        /// <summary>The creature waiting in the last floor's boss room; it never returns once killed.</summary>
        public NamespacedId Boss { get; init; }

        /// <summary>Clockwork Ruin's gear doors (SYS-DUNG-01, T-203); null = none.</summary>
        public DungeonClockwork Clockwork { get; init; }

        /// <summary>Drowned Temple's poison-fog rooms (SYS-DUNG-01, T-203); null = none.</summary>
        public DungeonMiasma Miasma { get; init; }

        /// <summary>Tidal Grotto's signature mechanic (SYS-DUNG-01, T-202); null = no tides.</summary>
        public DungeonTides Tides { get; init; }

        /// <summary>SYS-CRAFT-02: ore veins (world object ids with a <c>gather</c> block) placed on each floor, in turn.</summary>
        public NamespacedId[] Veins { get; init; }
    }

    /// <summary>SYS-DUNG-01 §Tidal Grotto: the in-game clock floods part of each floor twice a day.</summary>
    public sealed class DungeonTides
    {
        /// <summary>Hours from one high tide to the next (2 a day = 12).</summary>
        public float CycleHours { get; init; } = 12f;

        /// <summary>Hour of day of a high-tide peak; the water is up for the half cycle around it.</summary>
        public float HighAtHour { get; init; } = 6f;

        /// <summary>Share of the rooms that can flood (entrance, rest and boss rooms never do).</summary>
        public float FloodedShare { get; init; } = 0.5f;

        /// <summary>Movement multiplier while swimming.</summary>
        public float SwimSpeed { get; init; } = 0.6f;

        /// <summary>The low-tide cache, one per floor, in a room that floods.</summary>
        public DungeonCache Cache { get; init; }
    }

    public sealed class DungeonCache
    {
        public NamespacedId Item { get; init; }
        public int Min { get; init; } = 1;
        public int Max { get; init; } = 1;
    }

    public sealed class DungeonEntrance
    {
        /// <summary>Biome the entrance is placed in (<c>isle:coast</c> etc.); null = any land.</summary>
        public NamespacedId Biome { get; init; }
        public string Shape { get; init; }
        public string Color { get; init; }
        public float Size { get; init; } = 2f;
    }

    /// <summary>SYS-DUNG-01 §Soft gates: a specialist with <see cref="Skill"/> clears it fast, anyone slowly and loudly.</summary>
    public sealed class DungeonGate
    {
        public string Name { get; init; }
        public NamespacedId Skill { get; init; }
        public string Shape { get; init; }
        public string Color { get; init; }

        /// <summary>Seconds after it's cleared that it closes again (Rootwood Hollow's regrowing roots). 0 = stays open.</summary>
        public float RegrowSeconds { get; init; }

        /// <summary>Holding a tool with this tag, E burns the gate away for good — and wakes something. Null = can't.</summary>
        public DungeonGateBurn Burn { get; init; }
    }

    /// <summary>SYS-DUNG-01 Clockwork Ruin: extra doorways make loops, then a share of the plain doors split into two
    /// gear groups that swap open and shut every <see cref="PeriodSeconds"/>; doors about to shut blink for
    /// <see cref="WarnSeconds"/>.</summary>
    public sealed class DungeonClockwork
    {
        public float PeriodSeconds { get; init; } = 30f;
        public float Share { get; init; }
        public float LoopChance { get; init; }
        public float WarnSeconds { get; init; } = 3f;
        public string Color { get; init; }
    }

    /// <summary>SYS-DUNG-01 Drowned Temple: a share of each floor's rooms (never the entrance, rest or boss room) is
    /// filled with poison fog that hurts every second; an antidote (a toxic-resist buff) shrugs it off.</summary>
    public sealed class DungeonMiasma
    {
        public float Share { get; init; }
        public float DamagePerSecond { get; init; }
        public string DamageType { get; init; } = "toxic";
        public string Color { get; init; }
    }

    /// <summary>SYS-DUNG-01 Rootwood Hollow: fire clears a root wall at once but enrages the Hollow.</summary>
    public sealed class DungeonGateBurn
    {
        public string ToolTag { get; init; }
        public NamespacedId Spawn { get; init; }
        public int Count { get; init; }
    }

    /// <summary>SYS-DUNG-01: a room layout — see <c>Isle.World.Generation.RoomTemplate</c> for the row legend.</summary>
    public sealed class RoomTemplateDef : IDefinition
    {
        public NamespacedId Id { get; init; }
        public string Name { get; init; }
        public string[] Tags { get; init; }

        /// <summary>Dungeons it may appear in; null = all.</summary>
        public NamespacedId[] Dungeons { get; init; }

        public string[] Rows { get; init; }
    }
}
