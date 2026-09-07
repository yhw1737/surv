using System;
using Isle.Core.Util;

namespace Isle.Core.Ids
{
    /// <summary>
    /// A definition identifier in <c>{namespace}:{name}</c> form, both halves <c>[a-z0-9_]+</c>
    /// (SYS-CORE-01 §NamespacedId).
    /// <para>
    /// Unnamespaced input is a load error and is <b>never</b> auto-corrected. Guessing a namespace
    /// would make two mods that both define <c>raw_meat</c> silently resolve to whichever loaded
    /// last, which is the exact class of bug the namespace exists to prevent.
    /// </para>
    /// </summary>
    public readonly struct NamespacedId : IEquatable<NamespacedId>
    {
        public const char Separator = ':';

        readonly string _value;
        readonly int _colon;
        readonly int _hash;

        NamespacedId(string value, int colon)
        {
            _value = value;
            _colon = colon;
            _hash = StringComparer.Ordinal.GetHashCode(value);
        }

        /// <summary>False for <c>default(NamespacedId)</c>, which is the only way to hold an unparsed one.</summary>
        public bool IsValid => _value != null;

        /// <summary>The full <c>namespace:name</c> text, or null when default.</summary>
        public string Value => _value;

        public string Namespace => _value?.Substring(0, _colon);

        public string Name => _value?.Substring(_colon + 1);

        /// <summary>
        /// Parses, or explains why not. The loader collects errors rather than throwing on the
        /// first — SYS-CORE-01 §Load pipeline step 5 requires reporting <em>all</em> failures.
        /// </summary>
        public static bool TryParse(string text, out NamespacedId id, out string error)
        {
            id = default;

            if (string.IsNullOrEmpty(text))
            {
                error = "Empty ID. Expected \"{namespace}:{name}\".";
                return false;
            }

            var colon = text.IndexOf(Separator);
            if (colon < 0)
            {
                error = $"Missing namespace: \"{text}\". IDs must be written \"{{namespace}}:{{name}}\".";
                return false;
            }

            if (text.IndexOf(Separator, colon + 1) >= 0)
            {
                error = $"Too many '{Separator}' in \"{text}\". Expected exactly one.";
                return false;
            }

            var ns = text.Substring(0, colon);
            var name = text.Substring(colon + 1);

            if (ns.Length == 0)
            {
                error = $"Empty namespace in \"{text}\".";
                return false;
            }

            if (name.Length == 0)
            {
                error = $"Empty name in \"{text}\".";
                return false;
            }

            var bad = IdText.FirstBadChar(ns);
            if (bad >= 0)
            {
                error = $"Invalid character '{ns[bad]}' in namespace \"{ns}\". Allowed: a-z, 0-9, underscore.";
                return false;
            }

            bad = IdText.FirstBadChar(name);
            if (bad >= 0)
            {
                error = $"Invalid character '{name[bad]}' in name \"{name}\". Allowed: a-z, 0-9, underscore.";
                return false;
            }

            id = new NamespacedId(text, colon);
            error = null;
            return true;
        }

        /// <summary>For code and tests that already know the ID is good. Prefer TryParse when loading.</summary>
        public static NamespacedId Parse(string text)
        {
            if (TryParse(text, out var id, out var error)) return id;
            throw new FormatException(error);
        }

        // ponytail: hash first, then Ordinal string compare. SYS-CORE-01 says "compare hashes, not
        // strings", which is right about the hot path but wrong as the whole rule — a 32-bit hash
        // over a few thousand mod IDs collides often enough (birthday) that hash-only equality would
        // silently alias two definitions. The mismatch case, which is nearly all of them, still costs
        // one int compare.
        public bool Equals(NamespacedId other) =>
            _hash == other._hash && string.Equals(_value, other._value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is NamespacedId other && Equals(other);

        public override int GetHashCode() => _hash;

        public static bool operator ==(NamespacedId a, NamespacedId b) => a.Equals(b);

        public static bool operator !=(NamespacedId a, NamespacedId b) => !a.Equals(b);

        public override string ToString() => _value ?? "<unset>";
    }
}
