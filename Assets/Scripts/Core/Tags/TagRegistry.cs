using System;
using System.Collections.Generic;
using Isle.Core.Util;

namespace Isle.Core.Tags
{
    /// <summary>
    /// Interns hierarchical tag paths and precomputes each one's ancestor chain
    /// (SYS-CORE-01 §Tags).
    /// <para>
    /// Tags are slash-separated and at most <see cref="MaxDepth"/> segments deep;
    /// <c>fish/saltwater</c> implies <c>fish</c>. The implication is resolved once, at load, into
    /// a <see cref="HashSet{T}"/> of interned IDs — cooking asks <c>HasTag</c> on every ingredient
    /// combination, so nothing may parse a string on that path.
    /// </para>
    /// <para>
    /// Not a singleton. ARCHITECTURE.md caps those at three and this is not one of them; the
    /// registry is owned by whatever loads definitions, which also keeps tests isolated.
    /// </para>
    /// </summary>
    public sealed class TagRegistry
    {
        public const int MaxDepth = 3;
        public const char Separator = '/';

        readonly Dictionary<string, int> _ids = new Dictionary<string, int>(StringComparer.Ordinal);
        readonly List<string> _paths = new List<string>();
        readonly List<int[]> _chains = new List<int[]>();

        /// <summary>How many distinct paths are interned, ancestors included.</summary>
        public int Count => _paths.Count;

        /// <summary>
        /// Interns <paramref name="path"/> and every ancestor, returning the ID of the path itself.
        /// Idempotent.
        /// </summary>
        public bool TryIntern(string path, out int id, out string error)
        {
            if (path != null && _ids.TryGetValue(path, out id))
            {
                error = null;
                return true;
            }

            id = -1;
            if (!TryValidate(path, out error)) return false;

            // Ancestors first, so a parent's ID is always lower than its children's and its chain
            // is already built by the time we copy it.
            var parentId = -1;
            var lastSlash = path.LastIndexOf(Separator);
            if (lastSlash >= 0 && !TryIntern(path.Substring(0, lastSlash), out parentId, out error))
                return false;

            id = _paths.Count;
            _paths.Add(path);
            _ids.Add(path, id);

            if (parentId < 0)
            {
                _chains.Add(new[] { id });
            }
            else
            {
                var parentChain = _chains[parentId];
                var chain = new int[parentChain.Length + 1];
                chain[0] = id;
                Array.Copy(parentChain, 0, chain, 1, parentChain.Length);
                _chains.Add(chain);
            }

            return true;
        }

        /// <summary>Throwing form. Prefer <see cref="TryIntern"/> when loading — the loader reports all failures at once.</summary>
        public int Intern(string path)
        {
            if (TryIntern(path, out var id, out var error)) return id;
            throw new FormatException(error);
        }

        public bool TryGetId(string path, out int id)
        {
            id = -1;
            return path != null && _ids.TryGetValue(path, out id);
        }

        public string PathOf(int id) => _paths[id];

        /// <summary>The tag itself followed by its ancestors, nearest first.</summary>
        public IReadOnlyList<int> SelfAndAncestors(int id) => _chains[id];

        /// <summary>
        /// The set a definition stores: every listed tag plus every implied ancestor.
        /// Tags not seen before are interned here — an item may carry a tag no tag file declared
        /// (SCHEMA.md's own item examples do, with <c>oily</c> and <c>protein</c>).
        /// </summary>
        public HashSet<int> Flatten(IEnumerable<string> paths)
        {
            var set = new HashSet<int>();
            if (paths == null) return set;

            foreach (var path in paths)
            {
                var id = Intern(path);
                var chain = _chains[id];
                for (var i = 0; i < chain.Length; i++) set.Add(chain[i]);
            }

            return set;
        }

        /// <summary>
        /// Convenience over a flattened set. Hot paths should resolve the ID once with
        /// <see cref="TryGetId"/> and call <c>Contains</c> directly.
        /// </summary>
        public bool HasTag(HashSet<int> flattened, string path) =>
            TryGetId(path, out var id) && flattened.Contains(id);

        /// <summary>
        /// <c>root ("/" segment){0,2}</c> where every segment is <c>[a-z0-9_]+</c> and the root may
        /// carry a <c>namespace:</c> prefix, as SCHEMA.md §Tags writes them
        /// (<c>coolmod:deep_sea</c>, <c>isle:fish/saltwater</c>).
        /// </summary>
        public static bool TryValidate(string path, out string error)
        {
            if (string.IsNullOrEmpty(path))
            {
                error = "Empty tag.";
                return false;
            }

            var segments = path.Split(Separator);
            if (segments.Length > MaxDepth)
            {
                error = $"Tag \"{path}\" is {segments.Length} levels deep; the maximum is {MaxDepth}.";
                return false;
            }

            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];

                if (segment.Length == 0)
                {
                    error = $"Empty segment in tag \"{path}\".";
                    return false;
                }

                // Only the root may be namespaced, and only once.
                if (i == 0)
                {
                    var colon = segment.IndexOf(':');
                    if (colon >= 0)
                    {
                        if (segment.IndexOf(':', colon + 1) >= 0)
                        {
                            error = $"Too many ':' in tag root \"{segment}\".";
                            return false;
                        }

                        if (!IdText.IsSegment(segment.Substring(0, colon)) ||
                            !IdText.IsSegment(segment.Substring(colon + 1)))
                        {
                            error = $"Invalid namespaced tag root \"{segment}\" in \"{path}\". Expected \"{{namespace}}:{{name}}\", a-z, 0-9, underscore.";
                            return false;
                        }

                        continue;
                    }
                }

                var bad = IdText.FirstBadChar(segment);
                if (bad >= 0)
                {
                    error = $"Invalid character '{segment[bad]}' in tag segment \"{segment}\" of \"{path}\". Allowed: a-z, 0-9, underscore.";
                    return false;
                }
            }

            error = null;
            return true;
        }
    }
}
