using Isle.Core.Ids;

namespace Isle.Data
{
    /// <summary>
    /// An artifact (SCHEMA §Artifacts, SYS-ART-01).
    /// <para>
    /// <see cref="CombatSkill"/> invalid plus <see cref="GrantsCombatXp"/> false <b>is</b> the
    /// definition of an artifact (Absolute Rule 5). They scale off a production skill instead, so
    /// a cook or an angler is not helpless in a fight. Both fields exist here only so the
    /// validator can insist on them; nothing may "fix" them for convenience.
    /// </para>
    /// </summary>
    public sealed class ArtifactDef : IDefinition
    {
        public NamespacedId Id { get; init; }

        /// <summary>Language key, e.g. <c>"@artifact.tide_callers_reel"</c>.</summary>
        public string Name { get; init; }
        public string[] Tags { get; init; }

        /// <summary>The production skill this scales off, e.g. <c>isle:fishing</c>.</summary>
        public NamespacedId ScalingSkill { get; init; }

        /// <summary>Always null in JSON; always <c>default</c> here.</summary>
        public NamespacedId CombatSkill { get; init; }

        /// <summary>Always false.</summary>
        public bool GrantsCombatXp { get; init; }

        public ArtifactPower Power { get; init; }
        public ArtifactFuel Fuel { get; init; }
        public ZoneBonus[] ZoneBonus { get; init; }
        public int EnchantSlots { get; init; }
        public ArtifactAbility[] Abilities { get; init; }
        public ArtifactAcquisition Acquisition { get; init; }
    }

    /// <summary>Damage before zone bonuses. SCHEMA caps <see cref="Base"/> at 33.</summary>
    public sealed class ArtifactPower
    {
        public float Base { get; init; }

        /// <summary>Fraction of the scaling skill's level folded into power.</summary>
        public float SkillRatio { get; init; }
    }

    /// <summary>What the artifact consumes to fire (GLOSSARY: <c>ArtifactFuel</c>, never "ammo").</summary>
    public sealed class ArtifactFuel
    {
        public string[] Tags { get; init; }
        public int PerUse { get; init; }
    }

    /// <summary>A terrain multiplier — the reason an angler's artifact is strongest by water.</summary>
    public sealed class ZoneBonus
    {
        public string NearTag { get; init; }

        /// <summary>Radius in tiles.</summary>
        public float Radius { get; init; }

        public float Multiplier { get; init; }
    }

    /// <summary>
    /// One artifact ability. <see cref="Id"/> is <b>not</b> namespaced — it is local to the
    /// artifact (SCHEMA writes <c>"hook_pull"</c>), so it stays a plain string.
    /// </summary>
    public sealed class ArtifactAbility
    {
        public string Id { get; init; }
        public string Type { get; init; }
        public float Cooldown { get; init; }
        public float Range { get; init; }

        /// <summary>Execute threshold as a fraction of target HP.</summary>
        public float HpThreshold { get; init; }

        public string Condition { get; init; }
    }

    /// <summary>How the artifact is obtained.</summary>
    public sealed class ArtifactAcquisition
    {
        public int MinSkillLevel { get; init; }
        public NamespacedId Quest { get; init; }
    }
}
