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

        /// <summary>SYS-COMBAT-01 §Hit detection: "Melee uses a forward cone (angle and radius from the weapon def)" —
        /// the full cone angle in degrees, centred on the aim. 0 or unset: no cone (anything in reach).</summary>
        public float ConeDegrees { get; init; }

        public float StaminaCost { get; init; }

        /// <summary>SYS-COMBAT-02: one attack per combo step (last = finisher). Null: a single <c>arc</c> of
        /// <see cref="ConeDegrees"/>/<see cref="Reach"/> dealing <see cref="DamageType"/>.</summary>
        public AttackSpec[] Attacks { get; init; }

        /// <summary>SYS-COMBAT-02 damage type for a weapon without <see cref="Attacks"/>, and for its projectiles.</summary>
        public string DamageType { get; init; }

        /// <summary>
        /// <c>one_hand</c> / <c>two_hand</c> / <c>tool</c>. Selects IK targets — and a <c>two_hand</c> weapon can't be used while swimming (SYS-DUNG-01). Swapping a
        /// weapon must never change gameplay through the rig (ARCHITECTURE §Presentation boundary).
        /// </summary>
        public string Grip { get; init; }

        public bool QualityEnabled { get; init; }
        public int Durability { get; init; }

        /// <summary>Animation set, e.g. <c>isle:spear_basic</c>.</summary>
        public NamespacedId Moveset { get; init; }

        /// <summary>Ranged weapons only: the item each shot uses up (SYS-COMBAT-01: "Arrows (crafted)").
        /// Invalid (default) for melee weapons.</summary>
        public NamespacedId Ammo { get; init; }

        /// <summary>Ranged weapons: any bag item with this tag can be shot; the one with the highest
        /// <see cref="ItemDef.AmmoPower"/> goes first (SYS-CRAFT-02 tiered arrows). Takes precedence over <see cref="Ammo"/>.</summary>
        public string AmmoTag { get; init; }

        /// <summary>Ranged weapons only: projectile flight speed in tiles per second.</summary>
        public float ProjectileSpeed { get; init; }
    }
}
