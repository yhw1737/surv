using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Isle.UI.Prototype
{
    /// <summary>
    /// Player-facing strings from <c>StreamingAssets/lang/*.json</c> (CLAUDE.md §Language). The files
    /// are flat <c>"key": "text"</c> objects, so a regex reads them and avoids a JSON dependency this
    /// assembly doesn't reference. A missing key falls back to the key itself, so a gap shows up on
    /// screen instead of crashing the HUD.
    /// </summary>
    public static class Lang
    {
        static readonly Regex Entry = new("\"([^\"]+)\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
        static Dictionary<string, string> _table;

        static Dictionary<string, string> Table
        {
            get
            {
                if (_table != null) return _table;
                _table = new Dictionary<string, string>();
                var code = Code;
                var path = Path.Combine(Application.streamingAssetsPath, "lang", code + ".json");
                if (!File.Exists(path)) return _table;
                foreach (Match match in Entry.Matches(File.ReadAllText(path)))
                    _table[match.Groups[1].Value] = match.Groups[2].Value;
                return _table;
            }
        }

        /// <summary>The language in use: the options' choice, or the system language when it's "auto".</summary>
        public static string Code
        {
            get
            {
                var chosen = GameOptions.Language;
                if (chosen != "auto" && File.Exists(Path.Combine(Application.streamingAssetsPath, "lang", chosen + ".json"))) return chosen;
                return Application.systemLanguage == SystemLanguage.Korean ? "ko" : "en";
            }
        }

        /// <summary>Drops the loaded table so the next lookup reads the newly chosen language.</summary>
        public static void Reload() => _table = null;

        /// <summary>Accepts a def's <c>@key</c> reference or a bare key.</summary>
        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            // "pattern|arg|arg" — a generated name such as a dish: "@pattern.grilled|@item.raw_meat" → "Grilled raw meat".
            if (key.Contains("|"))
            {
                var parts = key.Split('|');
                var args = new object[parts.Length - 1];
                // An argument "a+b" is a list: each resolved and joined, e.g. "berries, salt".
                for (var i = 1; i < parts.Length; i++)
                    args[i - 1] = string.Join(", ", System.Array.ConvertAll(parts[i].Split('+'), Get));
                try { return string.Format(Get(parts[0]), args); }
                catch (System.FormatException) { return Get(parts[0]); }
            }
            var bare = key.StartsWith("@") ? key.Substring(1) : key;
            return Table.TryGetValue(bare, out var text) ? text : bare;
        }
    }
}
