using Isle.Core.Ids;

namespace Isle.Data
{
    /// <summary>A weapon (SCHEMA §Weapons, SYS-COMBAT-01). Artifacts are a separate type — see <see cref="ArtifactDef"/>.</summary>
    public sealed class WeaponDef : IDefinition
    {
        public NamespacedId Id { get; init; }

        /// <summary>Language key, e.g. <c>"@weapon.steel_harpoon"</c>.</summary>
        public string Name { get; init; }
        public string[] Tags { get; init; }
        public GridSize Grid { get; init; }
        public float Weight { get; init; }

        /// <summary>The skill that scales damage and earns XP.</summary>
        public NamespacedId CombatSkill { get; init; }

        public float BasePower { get; init; }
        public float AttackSpeed { get; init; }

        /// <summary>Reach in tiles.</summary>
        public float Reach { get; init; }

        public float StaminaCost { get; init; }

        /// <summary>
        /// <c>one_hand</c> / <c>two_hand</c> / <c>tool</c>. Selects IK targets only — swapping a
        /// weapon must never change gameplay through the rig (ARCHITECTURE §Presentation boundary).
        /// </summary>
        public string Grip { get; init; }

        public bool QualityEnabled { get; init; }
        public int Durability { get; init; }

        /// <summary>Animation set, e.g. <c>isle:spear_basic</c>.</summary>
        public NamespacedId Moveset { get; init; }
    }
}
