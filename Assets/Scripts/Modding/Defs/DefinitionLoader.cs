using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Isle.Data;

namespace Isle.Modding.Defs
{
    /// <summary>One file's worth of trouble — parse/shape/semantic/reference failures all report the same way.</summary>
    public readonly struct LoadError
    {
        public string FilePath { get; }
        public string Message { get; }

        /// <summary>1-based, or 0 when no meaningful location exists (SYS-CORE-01 verification case 7).</summary>
        public int Line { get; }

        /// <summary>Null unless a Levenshtein ≤2 candidate was found (verification case 4).</summary>
        public string Suggestion { get; }

        public LoadError(string filePath, string message, int line = 0, string suggestion = null)
        {
            FilePath = filePath;
            Message = message;
            Line = line;
            Suggestion = suggestion;
        }

        public override string ToString()
        {
            var location = Line > 0 ? $"{FilePath}:{Line}" : FilePath;
            var text = $"{location}\n  {Message}";
            if (Suggestion != null) text += $"\n  Did you mean \"{Suggestion}\"?";
            return text;
        }
    }

    /// <summary>
    /// Everything <see cref="DefinitionLoader.LoadAll{T}"/> found in one directory. A file with an
    /// error contributes no definition; a file with only warnings still does — §Load pipeline step 5
    /// wants every failure reported, not the first, so loading never stops partway through the batch.
    /// </summary>
    public sealed class LoadResult<T>
    {
        public List<T> Definitions { get; } = new();

        /// <summary>Parallel to <see cref="Definitions"/> — same index, source file. T-013 needs this
        /// to attribute a reference-resolution failure found after the fact.</summary>
        public List<string> SourcePaths { get; } = new();

        public List<LoadError> Errors { get; } = new();
        public List<LoadError> Warnings { get; } = new();
    }

    /// <summary>
    /// SYS-CORE-01 §Load pipeline steps 4–5 for a single content root: read every <c>*.json</c> in a
    /// directory, deserialize to <typeparamref name="T"/>, validate. Multi-mod orchestration (scan,
    /// dependency order, patches — steps 1–3, 6), reference resolution (step 7, <see cref="ReferenceResolver"/>)
    /// and the registry (steps 9–10) are T-130/T-014, not this.
    /// </summary>
    public static class DefinitionLoader
    {
        static readonly JsonSerializerOptions Options = BuildOptions();

        static JsonSerializerOptions BuildOptions()
        {
            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
            options.Converters.Add(new NamespacedIdJsonConverter());
            return options;
        }

        public static LoadResult<T> LoadAll<T>(string directoryPath) where T : class, IDefinition
        {
            var result = new LoadResult<T>();
            if (!Directory.Exists(directoryPath)) return result;

            var knownFields = KnownFields(typeof(T));
            foreach (var path in Directory.EnumerateFiles(directoryPath, "*.json", SearchOption.AllDirectories)
                         .OrderBy(p => p, StringComparer.Ordinal))
                LoadFile(path, knownFields, result);

            return result;
        }

        static void LoadFile<T>(string path, HashSet<string> knownFields, LoadResult<T> result) where T : class, IDefinition
        {
            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (IOException ex)
            {
                result.Errors.Add(new LoadError(path, ex.Message));
                return;
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(json);
            }
            catch (JsonException ex)
            {
                result.Errors.Add(new LoadError(path, ex.Message, JsonLine.Of(ex)));
                return;
            }

            using (doc)
            {
                // Shape check on the raw JSON, before it ever becomes a T (verification cases 6/7,
                // scoped down: only id/name are known-required across every SCHEMA.md example, and
                // only root-level keys are checked — nested typos aren't caught here).
                var root = doc.RootElement;
                var missingRequired = false;
                if (!root.TryGetProperty("id", out _))
                {
                    result.Errors.Add(new LoadError(path, "Missing required field: \"id\".", line: 1));
                    missingRequired = true;
                }
                if (!root.TryGetProperty("name", out _))
                {
                    result.Errors.Add(new LoadError(path, "Missing required field: \"name\".", line: 1));
                    missingRequired = true;
                }
                foreach (var member in root.EnumerateObject())
                    if (!knownFields.Contains(member.Name))
                        result.Warnings.Add(new LoadError(path, $"Unknown field: \"{member.Name}\" — ignored."));

                if (missingRequired) return;

                T def;
                try
                {
                    def = JsonSerializer.Deserialize<T>(root.GetRawText(), Options);
                }
                catch (JsonException ex)
                {
                    result.Errors.Add(new LoadError(path, ex.Message, JsonLine.Of(ex)));
                    return;
                }

                if (def == null)
                {
                    result.Errors.Add(new LoadError(path, "Definition parsed to null."));
                    return;
                }

                var semanticErrors = new List<string>();
                SchemaValidator.Validate(def, semanticErrors);
                if (semanticErrors.Count > 0)
                {
                    foreach (var message in semanticErrors)
                        result.Errors.Add(new LoadError(path, message));
                    return;
                }

                result.Definitions.Add(def);
                result.SourcePaths.Add(path);
            }
        }

        static HashSet<string> KnownFields(Type type)
        {
            var fields = new HashSet<string>();
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                fields.Add(JsonNamingPolicy.SnakeCaseLower.ConvertName(prop.Name));
            return fields;
        }
    }

    /// <summary>
    /// Line numbers by counting newlines up to a raw-text offset. ponytail: string search over a
    /// real JSON span tracker — good enough to point a modder at roughly the right line; upgrade to
    /// a <c>Utf8JsonReader</c> walk if a duplicate literal ever misattributes one.
    /// </summary>
    static class JsonLine
    {
        public static int Of(JsonException ex) => ex.LineNumber.HasValue ? (int)ex.LineNumber.Value + 1 : 0;

        public static int OfValue(string json, string value)
        {
            var idx = json.IndexOf($"\"{value}\"", StringComparison.Ordinal);
            if (idx < 0) return 0;

            var line = 1;
            for (var i = 0; i < idx; i++)
                if (json[i] == '\n') line++;
            return line;
        }
    }
}
