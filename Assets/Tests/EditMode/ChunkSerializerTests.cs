using Isle.Core;
using Isle.Core.Ids;
using Isle.World.Chunks;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-WORLD-01 §Chunks persistence (T-033).</summary>
    public sealed class ChunkSerializerTests
    {
        [Test]
        public void SaveThenLoad_RoundTrips_AllFields()
        {
            using var serializer = new ChunkSerializer(":memory:");
            var coord = new Vec2Int(3, -2);
            var tiles = new Tile[Chunk.Size * Chunk.Size];
            tiles[5].Biome = Biome.Marsh;
            var objects = new System.Collections.Generic.List<WorldObject>
            {
                new() { DefId = NamespacedId.Parse("isle:tree"), LocalPosition = new Vec2Int(4, 7) }
            };
            var chunk = new Chunk(coord, tiles, objects, lastSimulatedTime: 123);

            serializer.Save(chunk);
            var loaded = serializer.Load(coord);

            Assert.AreEqual(coord, loaded.Coord);
            Assert.AreEqual(123, loaded.LastSimulatedTime);
            Assert.AreEqual(Biome.Marsh, loaded.Tiles[5].Biome);
            Assert.AreEqual(Biome.Coast, loaded.Tiles[0].Biome);
            Assert.AreEqual(1, loaded.Objects.Count);
            Assert.AreEqual("isle:tree", loaded.Objects[0].DefId.Value);
            Assert.AreEqual(new Vec2Int(4, 7), loaded.Objects[0].LocalPosition);
        }

        [Test]
        public void Load_NoSavedRow_ReturnsNull()
        {
            using var serializer = new ChunkSerializer(":memory:");

            var loaded = serializer.Load(new Vec2Int(0, 0));

            Assert.IsNull(loaded);
        }
    }
}
