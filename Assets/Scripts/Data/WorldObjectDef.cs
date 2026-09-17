using Isle.Core.Ids;

namespace Isle.Data
{
    /// <summary>SCHEMA.md §World objects. The def a placed <c>WorldObjectInstance</c> resolves
    /// against — just id/name/tags today, since nothing beyond tag-based proximity queries
    /// (SYS-SURV-01's fire bonus) reads one yet.</summary>
    public sealed class WorldObjectDef : IDefinition
    {
        public NamespacedId Id { get; init; }

        /// <summary>Language key, e.g. <c>"@world_object.campfire"</c>. Never player-facing text.</summary>
        public string Name { get; init; }

        public string[] Tags { get; init; }
    }
}
