using Isle.Core.Ids;

namespace Isle.Data
{
    /// <summary>
    /// A cooking technique (SCHEMA §Cook methods). One new method applies to every existing
    /// ingredient and vice versa — that bidirectionality is the whole point (Ext gate, T-105).
    /// </summary>
    public sealed class CookMethodDef : IDefinition
    {
        public NamespacedId Id { get; init; }

        /// <summary>Language key, e.g. <c>"@cook_method.steam_bake"</c>.</summary>
        public string Name { get; init; }
        public SkillRequirement UnlockSkill { get; init; }

        /// <summary>Station this must be performed at, e.g. <c>isle:forge</c>.</summary>
        public NamespacedId Station { get; init; }

        public float DurationSec { get; init; }
        public CookInput Input { get; init; }
        public CookModifiers Modifiers { get; init; }

        /// <summary>Evaluated in declaration order and accumulated (SYS-COOK-01 step 4).</summary>
        public TagReaction[] TagReactions { get; init; }

        public CookFailure Failure { get; init; }

        /// <summary>Language key for the procedural dish name, e.g. <c>"@pattern.steam_baked"</c>.</summary>
        public string Naming { get; init; }
    }

    /// <summary>What the method accepts (SYS-COOK-01 step 1).</summary>
    public sealed class CookInput
    {
        public int MinItems { get; init; }
        public int MaxItems { get; init; }
        public float RequiresWaterMl { get; init; }
    }

    /// <summary>
    /// Multipliers applied to the base nutrition (SYS-COOK-01 steps 3–4).
    /// <para>
    /// Every field defaults to <c>1.0</c> because GLOSSARY §Units defines that as "no change".
    /// A <see cref="TagReaction"/> declares only the one or two it changes, so the absent ones
    /// must be identity rather than zero.
    /// </para>
    /// </summary>
    public sealed class CookModifiers
    {
        public float Hunger { get; init; } = 1f;
        public float Thirst { get; init; } = 1f;
        public float Preservation { get; init; } = 1f;
        public float BuffPower { get; init; } = 1f;
        public float BuffDuration { get; init; } = 1f;
        public float NutritionRetention { get; init; } = 1f;
    }

    /// <summary>
    /// Fires when <see cref="When"/> is a subset of the flattened ingredient tags. Grants a buff,
    /// adjusts modifiers, or both.
    /// </summary>
    public sealed class TagReaction
    {
        public string[] When { get; init; }

        /// <summary>Invalid when the reaction only adjusts modifiers.</summary>
        public NamespacedId GrantBuff { get; init; }

        /// <summary>Buff strength. Also the tiebreak when a dish exceeds its buff cap — excess drop by lowest power.</summary>
        public float Power { get; init; }

        /// <summary>Null when the reaction only grants a buff.</summary>
        public CookModifiers Modifiers { get; init; }
    }

    /// <summary>Burn chance (SYS-COOK-01 step 5).</summary>
    public sealed class CookFailure
    {
        public float BaseRate { get; init; }

        /// <summary>Subtracted from <see cref="BaseRate"/> per cooking level.</summary>
        public float SkillReduction { get; init; }

        public NamespacedId Result { get; init; }
    }
}
