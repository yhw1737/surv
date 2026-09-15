using Isle.Core;
using Isle.World.Chunks;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-WORLD-01 §Chunks.</summary>
    public sealed class ChunkTests
    {
        [Test]
        public void Constructor_Defaults_CreatesFullTileArrayAndEmptyObjects()
        {
            var chunk = new Chunk(new Vec2Int(0, 0));

            Assert.AreEqual(Chunk.Size * Chunk.Size, chunk.Tiles.Length);
            Assert.AreEqual(0, chunk.Objects.Count);
            Assert.AreEqual(0, chunk.LastSimulatedTime);
        }

        [TestCase(0, 0, 0, 0)]
        [TestCase(31, 31, 0, 0)]
        [TestCase(32, 0, 1, 0)]
        [TestCase(35, 70, 1, 2)]
        [TestCase(-1, -1, -1, -1)]
        [TestCase(-32, 0, -1, 0)]
        [TestCase(-33, 0, -2, 0)]
        public void CoordFromTilePosition_MatchesChunkSize(int tileX, int tileY, int chunkX, int chunkY)
        {
            var coord = Chunk.CoordFromTilePosition(new Vec2Int(tileX, tileY));

            Assert.AreEqual(new Vec2Int(chunkX, chunkY), coord);
        }
    }
}
