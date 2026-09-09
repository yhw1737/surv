using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Isle.Core.Ids;

namespace Isle.Modding.Defs
{
    /// <summary>
    /// System.Text.Json support for <see cref="NamespacedId"/>. Needs both the value overloads and
    /// the property-name overloads — <c>FishDef.BaitAffinity</c> is keyed by one.
    /// <para>
    /// <c>HandleNull</c> is on: a handful of fields (<c>ArtifactDef.CombatSkill</c>) are always
    /// <c>null</c> in JSON and must deserialize to <c>default(NamespacedId)</c>, not throw.
    /// </para>
    /// </summary>
    public sealed class NamespacedIdJsonConverter : JsonConverter<NamespacedId>
    {
        public override bool HandleNull => true;

        public override NamespacedId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType == JsonTokenType.Null ? default : ParseOrThrow(reader.GetString());

        public override void Write(Utf8JsonWriter writer, NamespacedId value, JsonSerializerOptions options)
        {
            if (!value.IsValid) writer.WriteNullValue();
            else writer.WriteStringValue(value.Value);
        }

        public override NamespacedId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            ParseOrThrow(reader.GetString());

        public override void WriteAsPropertyName(Utf8JsonWriter writer, NamespacedId value, JsonSerializerOptions options) =>
            writer.WritePropertyName(value.Value);

        static NamespacedId ParseOrThrow(string text)
        {
            if (!NamespacedId.TryParse(text, out var id, out var error)) throw new JsonException(error);
            return id;
        }
    }
}
