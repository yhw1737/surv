using Isle.Core.Ids;

namespace Isle.Data
{
    /// <summary>
    /// A loaded JSON definition. <c>DefRegistry</c> (T-014) keys every type on <see cref="Id"/>.
    /// <para>
    /// Definitions are immutable once loaded (<c>ARCHITECTURE.md</c> §Never). Every property in
    /// this assembly is <c>init</c>-only for that reason: <c>ItemStack</c> holds its
    /// <c>ItemDef</c> by reference, so a single runtime write would silently retune every stack
    /// in the world.
    /// </para>
    /// </summary>
    public interface IDefinition
    {
        NamespacedId Id { get; }

        /// <summary>
        /// Language key, e.g. <c>"@creature.mountain_boar"</c> — never player-facing text
        /// (GLOSSARY §Display names). Every definition carries one; without it nothing can label
        /// a boar in the UI.
        /// </summary>
        string Name { get; }
    }
}
