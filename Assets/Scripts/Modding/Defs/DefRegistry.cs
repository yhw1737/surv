using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Isle.Core.Ids;
using Isle.Core.Tags;
using Isle.Data;

namespace Isle.Modding.Defs
{
    /// <summary>
    /// SYS-CORE-01 §Load pipeline steps 9–10: the read-only lookup every system queries at
    /// runtime. One of ARCHITECTURE.md's three allowed singletons — static state, no instance.
    /// <para>
    /// Spec says "Singleton in Isle.Core", but <see cref="Get{T}"/> needs <see cref="IDefinition"/>
    /// (Isle.Data), which Core may not reference (ARCHITECTURE.md's asmdef table) — same
    /// one-way-dependency reasoning already recorded for <see cref="DefinitionLoader"/> and
    /// <see cref="ReferenceResolver"/>'s placement, so this lives in Isle.Modding too.
    /// </para>
    /// <para>
    /// Whether a failed <see cref="ReferenceResolver"/> check should block <see cref="Freeze"/> is
    /// not decided (no spec says either way) — today <see cref="Register{T}"/> takes whatever it's
    /// given, so the caller is responsible for only registering a clean <see cref="LoadResult{T}"/>.
    /// </para>
    /// </summary>
    public static class DefRegistry
    {
        static readonly Dictionary<Type, object> _byId = new();
        static readonly Dictionary<Type, object> _byTagId = new();
        static readonly Dictionary<Type, PropertyInfo> _tagsProperty = new();
        static TagRegistry _tags = new();
        static bool _frozen;

        /// <summary>
        /// Indexes every definition in <paramref name="result"/> by id, and by tag (self and
        /// implied ancestors, via <see cref="TagRegistry.Flatten"/>) for any type that has a
        /// <c>string[] Tags</c> property — <c>CraftRecipeDef</c>, <c>CookMethodDef</c> and
        /// <c>EnchantDef</c> don't, and are simply never tag-indexed.
        /// </summary>
        public static void Register<T>(LoadResult<T> result) where T : class, IDefinition
        {
            if (_frozen) throw new InvalidOperationException("DefRegistry is read-only after Freeze().");

            var byId = ByIdFor<T>();
            var byTag = ByTagFor<T>();

            foreach (var def in result.Definitions)
            {
                byId[def.Id] = def;

                var literalTags = TagsOf(def);
                if (literalTags == null) continue;
                foreach (var tagId in _tags.Flatten(literalTags))
                {
                    if (!byTag.TryGetValue(tagId, out var list))
                        byTag[tagId] = list = new List<T>();
                    list.Add(def);
                }
            }
        }

        /// <summary>
        /// SYS-CORE-01 §Hot reload: re-parses one type's definitions and applies them in place.
        /// The one sanctioned exception to "read-only after <see cref="Freeze"/>" — this <b>is</b>
        /// the mechanism that keeps the registry current, not a caller mutating it.
        /// <para>
        /// A def already held by reference (e.g. an <c>ItemStack.Def</c>) must see the update
        /// without re-fetching, so existing ids are updated by copying the incoming values onto
        /// the <b>existing instance</b> rather than replacing it — <see cref="IDefinition"/>
        /// properties are init-only (T-011), but that's a compiler restriction on ordinary
        /// assignment, not on reflection. New ids are inserted; ids missing from this reload are
        /// removed. The tag index has no incremental-update story, so it's rebuilt from scratch.
        /// </para>
        /// </summary>
        public static void Reload<T>(LoadResult<T> result) where T : class, IDefinition
        {
            var byId = ByIdFor<T>();

            var incomingIds = new HashSet<NamespacedId>();
            foreach (var def in result.Definitions)
            {
                incomingIds.Add(def.Id);
                if (byId.TryGetValue(def.Id, out var existing))
                    CopyProperties(def, existing);
                else
                    byId[def.Id] = def;
            }

            foreach (var staleId in byId.Keys.Where(id => !incomingIds.Contains(id)).ToList())
                byId.Remove(staleId);

            RebuildTagIndex(byId.Values);
        }

        static void CopyProperties<T>(T source, T target) where T : class, IDefinition
        {
            foreach (var prop in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                if (prop.CanRead && prop.CanWrite)
                    prop.SetValue(target, prop.GetValue(source));
        }

        static void RebuildTagIndex<T>(IEnumerable<T> defs) where T : class, IDefinition
        {
            var byTag = ByTagFor<T>();
            byTag.Clear();
            foreach (var def in defs)
            {
                var literalTags = TagsOf(def);
                if (literalTags == null) continue;
                foreach (var tagId in _tags.Flatten(literalTags))
                {
                    if (!byTag.TryGetValue(tagId, out var list))
                        byTag[tagId] = list = new List<T>();
                    list.Add(def);
                }
            }
        }

        public static void Freeze() => _frozen = true;

        /// <summary>Test isolation only — production code never calls this after startup.</summary>
        public static void Clear()
        {
            _byId.Clear();
            _byTagId.Clear();
            _tags = new TagRegistry();
            _frozen = false;
        }

        public static T Get<T>(NamespacedId id) where T : class, IDefinition
        {
            if (TryGet<T>(id, out var def)) return def;
            throw new KeyNotFoundException($"No {typeof(T).Name} registered for id \"{id.Value}\".");
        }

        public static bool TryGet<T>(NamespacedId id, out T def) where T : class, IDefinition =>
            ByIdFor<T>().TryGetValue(id, out def);

        /// <summary>O(1): resolves <paramref name="tag"/> to its interned id, then one dictionary lookup.</summary>
        public static IReadOnlyList<T> AllWithTag<T>(string tag) where T : class, IDefinition
        {
            if (!_tags.TryGetId(tag, out var tagId)) return Array.Empty<T>();
            return ByTagFor<T>().TryGetValue(tagId, out var list) ? list : Array.Empty<T>();
        }

        public static IReadOnlyList<T> All<T>() where T : class, IDefinition => ByIdFor<T>().Values.ToList();

        static Dictionary<NamespacedId, T> ByIdFor<T>() where T : class, IDefinition
        {
            if (!_byId.TryGetValue(typeof(T), out var dict))
                _byId[typeof(T)] = dict = new Dictionary<NamespacedId, T>();
            return (Dictionary<NamespacedId, T>)dict;
        }

        static Dictionary<int, List<T>> ByTagFor<T>() where T : class, IDefinition
        {
            if (!_byTagId.TryGetValue(typeof(T), out var dict))
                _byTagId[typeof(T)] = dict = new Dictionary<int, List<T>>();
            return (Dictionary<int, List<T>>)dict;
        }

        // ponytail: reflection over a "Tags" property rather than adding it to IDefinition — most
        // def types don't have one (CraftRecipeDef, CookMethodDef, EnchantDef), and IDefinition is
        // T-011's contract, not this task's to widen. Cached per type, so it's one lookup per T ever.
        static string[] TagsOf<T>(T def) where T : class, IDefinition
        {
            if (!_tagsProperty.TryGetValue(typeof(T), out var prop))
                _tagsProperty[typeof(T)] = prop = typeof(T).GetProperty("Tags", BindingFlags.Public | BindingFlags.Instance);
            return prop?.GetValue(def) as string[];
        }
    }
}
