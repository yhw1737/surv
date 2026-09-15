using System.Collections.Generic;
using Isle.Core;
using Isle.World.Chunks;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-WORLD-01 §Chunks, verification #3.</summary>
    public sealed class ChunkManagerTests
    {
        static ChunkManager NewManager(List<Chunk> saved, out Dictionary<Vec2Int, int> loadCounts)
        {
            var counts = new Dictionary<Vec2Int, int>();
            loadCounts = counts;
            return new ChunkManager(
                coord =>
                {
                    counts.TryGetValue(coord, out var count);
                    counts[coord] = count + 1;
                    return new Chunk(coord);
                },
                saved.Add);
        }

        [Test]
        public void UpdateActiveChunks_FirstCall_LoadsThreeByThreeAroundPlayer()
        {
            var manager = NewManager(new List<Chunk>(), out _);

            manager.UpdateActiveChunks(new[] { new Vec2Int(0, 0) }, worldTime: 0);

            Assert.AreEqual(9, manager.LoadedChunks.Count);
            for (var dx = -1; dx <= 1; dx++)
            for (var dy = -1; dy <= 1; dy++)
                Assert.IsTrue(manager.LoadedChunks.ContainsKey(new Vec2Int(dx, dy)));
        }

        [Test]
        public void UpdateActiveChunks_PlayerMovesAway_UnloadsAndSavesOutOfRangeChunks()
        {
            var saved = new List<Chunk>();
            var manager = NewManager(saved, out _);
            manager.UpdateActiveChunks(new[] { new Vec2Int(0, 0) }, worldTime: 0);

            manager.UpdateActiveChunks(new[] { new Vec2Int(5, 0) }, worldTime: 100);

            Assert.IsFalse(manager.LoadedChunks.ContainsKey(new Vec2Int(-1, -1)));
            Assert.AreEqual(9, saved.Count);
            Assert.IsTrue(saved.TrueForAll(c => c.LastSimulatedTime == 100));
        }

        [Test]
        public void UpdateActiveChunks_ChunkStaysInRange_NotReloadedOrUnloaded()
        {
            var manager = NewManager(new List<Chunk>(), out var loadCounts);
            manager.UpdateActiveChunks(new[] { new Vec2Int(0, 0) }, worldTime: 0);
            var original = manager.LoadedChunks[new Vec2Int(0, 0)];

            manager.UpdateActiveChunks(new[] { new Vec2Int(1, 0) }, worldTime: 50);

            Assert.AreSame(original, manager.LoadedChunks[new Vec2Int(0, 0)]);
            Assert.AreEqual(1, loadCounts[new Vec2Int(0, 0)]);
        }

        [Test]
        public void UpdateActiveChunks_TwoDistantPlayers_LoadsUnionOfBothNeighborhoods()
        {
            var manager = NewManager(new List<Chunk>(), out _);

            manager.UpdateActiveChunks(new[] { new Vec2Int(0, 0), new Vec2Int(10, 10) }, worldTime: 0);

            Assert.AreEqual(18, manager.LoadedChunks.Count);
            Assert.IsTrue(manager.LoadedChunks.ContainsKey(new Vec2Int(10, 10)));
        }

        [Test]
        public void UpdateActiveChunks_NoPlayers_UnloadsAndSavesEverything()
        {
            var saved = new List<Chunk>();
            var manager = NewManager(saved, out _);
            manager.UpdateActiveChunks(new[] { new Vec2Int(0, 0) }, worldTime: 0);

            manager.UpdateActiveChunks(System.Array.Empty<Vec2Int>(), worldTime: 42);

            Assert.AreEqual(0, manager.LoadedChunks.Count);
            Assert.AreEqual(9, saved.Count);
        }
    }
}
