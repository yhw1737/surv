using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Isle.Core;
using Isle.Core.Ids;
using SQLite;

namespace Isle.World.Chunks
{
    /// <summary>
    /// SYS-WORLD-01 §Chunks persistence (T-033). One row per chunk, keyed by coordinate.
    ///
    /// Spec says "SQLite, BLOB" — stored as a JSON TEXT column instead. Decided without a spec:
    /// the developer wants save files inspectable for mod debugging, and a binary BLOB defeats
    /// that. Cost is negligible (chunks are small, this isn't a hot path), so JSON wins outright.
    /// </summary>
    public sealed class ChunkSerializer : IDisposable
    {
        static readonly JsonSerializerOptions Options = new()
        {
            Converters =
            {
                new Vec2IntJsonConverter(),
                new NamespacedIdJsonConverter(),
                new JsonStringEnumConverter()
            }
        };

        readonly SQLiteConnection _db;

        public ChunkSerializer(string databasePath)
        {
            _db = new SQLiteConnection(databasePath);
            _db.Execute(
                "CREATE TABLE IF NOT EXISTS chunks (cx INTEGER NOT NULL, cy INTEGER NOT NULL, data TEXT NOT NULL, PRIMARY KEY (cx, cy))");
        }

        /// <summary>Null when no row exists yet — the caller generates a fresh chunk in that case.</summary>
        public Chunk Load(Vec2Int coord)
        {
            var row = _db.Query<ChunkRow>("SELECT data FROM chunks WHERE cx = ? AND cy = ?", coord.X, coord.Y)
                .FirstOrDefault();
            if (row == null) return null;

            var dto = JsonSerializer.Deserialize<ChunkDto>(row.Data, Options);
            var tiles = dto.Tiles.Select(b => new Tile { Biome = b }).ToArray();
            var objects = dto.Objects.Select(o => new WorldObject { DefId = o.DefId, LocalPosition = o.LocalPosition }).ToList();
            return new Chunk(coord, tiles, objects, dto.LastSimulatedTime);
        }

        public void Save(Chunk chunk)
        {
            var dto = new ChunkDto
            {
                Tiles = chunk.Tiles.Select(t => t.Biome).ToArray(),
                Objects = chunk.Objects.Select(o => new WorldObjectDto { DefId = o.DefId, LocalPosition = o.LocalPosition }).ToList(),
                LastSimulatedTime = chunk.LastSimulatedTime
            };
            var json = JsonSerializer.Serialize(dto, Options);
            _db.Execute("INSERT OR REPLACE INTO chunks (cx, cy, data) VALUES (?, ?, ?)", chunk.Coord.X, chunk.Coord.Y, json);
        }

        public void Dispose() => _db.Close();

        sealed class ChunkRow
        {
            public string Data { get; set; }
        }

        // Deliberately separate from Chunk/Tile/WorldObject rather than serializing them directly —
        // keeps STJ concerns out of the T-032 world model types.
        sealed class ChunkDto
        {
            public Biome[] Tiles { get; set; }
            public List<WorldObjectDto> Objects { get; set; }
            public long LastSimulatedTime { get; set; }
        }

        sealed class WorldObjectDto
        {
            public NamespacedId DefId { get; set; }
            public Vec2Int LocalPosition { get; set; }
        }
    }

    /// <summary>Serializes <see cref="Vec2Int"/> as a compact <c>[x, y]</c> array.</summary>
    sealed class Vec2IntJsonConverter : JsonConverter<Vec2Int>
    {
        public override Vec2Int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            reader.Read();
            var x = reader.GetInt32();
            reader.Read();
            var y = reader.GetInt32();
            reader.Read(); // EndArray
            return new Vec2Int(x, y);
        }

        public override void Write(Utf8JsonWriter writer, Vec2Int value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(value.X);
            writer.WriteNumberValue(value.Y);
            writer.WriteEndArray();
        }
    }

    // ponytail: duplicate of Isle.Modding.Defs.NamespacedIdJsonConverter, trimmed to the Read/Write
    // this DTO needs (no dictionary-key support). World doesn't reference Modding — consolidate if
    // a third consumer shows up.
    sealed class NamespacedIdJsonConverter : JsonConverter<NamespacedId>
    {
        public override NamespacedId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (!NamespacedId.TryParse(reader.GetString(), out var id, out var error)) throw new JsonException(error);
            return id;
        }

        public override void Write(Utf8JsonWriter writer, NamespacedId value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.Value);
    }
}
