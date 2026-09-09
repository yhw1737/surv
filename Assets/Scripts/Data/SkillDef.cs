using Isle.Core.Ids;

namespace Isle.Data
{
    /// <summary>
    /// A skill definition (SCHEMA §Skills). Modder-addable (T-018) — SYS-SKILL-01's focus formula
    /// holds for any skill count, as long as every skill picks one of the two pools below.
    /// <para>
    /// <see cref="Pool"/> is a plain string, not an enum: exactly two values are legal
    /// (<c>"production"</c>, <c>"combat"</c>), fixed by developer decision for beta — a mod may
    /// not declare a new pool (PROJECT_STATE.md §Decided without a spec, 2026-09-09). A string
    /// keeps the field JSON-trivial; <c>SchemaValidator</c> (T-012) is where the closed set gets
    /// enforced, not the type system.
    /// </para>
    /// </summary>
    public sealed class SkillDef : IDefinition
    {
        public NamespacedId Id { get; init; }

        /// <summary>Language key, e.g. <c>"@skill.cooking"</c>. Never player-facing text.</summary>
        public string Name { get; init; }

        public string Pool { get; init; }
    }
}
