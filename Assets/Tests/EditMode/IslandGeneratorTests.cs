using System.Collections.Generic;
using Isle.World.Chunks;
using Isle.World.Generation;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-WORLD-01 §Island generation (T-035), verification #2.</summary>
    public sealed class IslandGeneratorTests
    {
        [Test]
        public void BiomeAt_SameSeed_IsDeterministic()
        {
            var a = new IslandGenerator(seed: 7);
            var b = new IslandGenerator(seed: 7);

            for (var x = 0; x < IslandGenerator.Size; x += 17)
            for (var y = 0; y < IslandGenerator.Size; y += 17)
                Assert.AreEqual(a.BiomeAt(x, y), b.BiomeAt(x, y), $"({x},{y})");
        }

        [Test]
        public void BiomeAt_FarOutsideIsland_IsCoast()
        {
            var island = new IslandGenerator(seed: 1);

            Assert.AreEqual(Biome.Coast, island.BiomeAt(0, 0));
            Assert.AreEqual(Biome.Coast, island.BiomeAt(IslandGenerator.Size - 1, IslandGenerator.Size - 1));
        }

        [Test]
        public void BiomeAt_Interior_HasBothForestAndMarsh()
        {
            var island = new IslandGenerator(seed: 3);
            var seen = new HashSet<Biome>();

            var center = IslandGenerator.Size / 2;
            for (var x = center - 80; x < center + 80; x += 4)
            for (var y = center - 80; y < center + 80; y += 4)
                seen.Add(island.BiomeAt(x, y));

            Assert.IsTrue(seen.Contains(Biome.Forest));
            Assert.IsTrue(seen.Contains(Biome.Marsh));
        }

        [Test]
        public void BiomeAt_DifferentSeeds_ProduceDifferentIslands()
        {
            var a = new IslandGenerator(seed: 1);
            var b = new IslandGenerator(seed: 2);

            var differs = false;
            for (var x = 0; x < IslandGenerator.Size && !differs; x += 11)
            for (var y = 0; y < IslandGenerator.Size && !differs; y += 11)
                if (a.BiomeAt(x, y) != b.BiomeAt(x, y))
                    differs = true;

            Assert.IsTrue(differs, "expected at least one tile to differ between two seeds");
        }

        [Test]
        public void IsLand_CentreIsLand_FarCornerIsNot()
        {
            var island = new IslandGenerator(seed: 5);

            Assert.IsTrue(island.IsLand(IslandGenerator.Size / 2, IslandGenerator.Size / 2));
            Assert.IsFalse(island.IsLand(0, 0));
        }
    

        // SYS-WORLD-03 §Island shape (2026-10-05 revision): 1152 tiles, 1–4 joined blobs, sea border.

        [Test]
        public void Size_Is1152()
        {
            Assert.AreEqual(1152, IslandGenerator.Size);
        }

        [TestCase(1)]
        [TestCase(77)]
        [TestCase(4242)]
        public void IsLand_BorderAlwaysSea(int seed)
        {
            var island = new IslandGenerator(seed);
            for (var i = 0; i < IslandGenerator.Size; i += 7)
            {
                Assert.IsFalse(island.IsLand(i, 10));
                Assert.IsFalse(island.IsLand(10, i));
                Assert.IsFalse(island.IsLand(IslandGenerator.Size - 11, i));
            }
        }

        [TestCase(1)]
        [TestCase(77)]
        [TestCase(4242)]
        [TestCase(90210)]
        public void FloodFill_FromCentre_ReachesAllLand(int seed)
        {
            var island = new IslandGenerator(seed);
            var size = IslandGenerator.Size;
            var seen = new bool[size * size];
            var queue = new System.Collections.Generic.Queue<(int, int)>();
            queue.Enqueue((size / 2, size / 2));
            seen[size / 2 * size + size / 2] = true;
            var reached = 0;
            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                reached++;
                foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                {
                    if (nx < 0 || ny < 0 || nx >= size || ny >= size || seen[ny * size + nx] || !island.IsLand(nx, ny)) continue;
                    seen[ny * size + nx] = true;
                    queue.Enqueue((nx, ny));
                }
            }
            var land = 0;
            for (var x = 0; x < size; x++)
            for (var y = 0; y < size; y++)
                if (island.IsLand(x, y)) land++;
            Assert.AreEqual(land, reached, "some land is cut off from the spawn");
            Assert.Greater(land, size * size / 10, "island too small");
        }

        [Test]
        public void BlobCount_VariesAcrossSeeds()
        {
            var counts = new System.Collections.Generic.HashSet<int>();
            for (var seed = 1; seed <= 40; seed++) counts.Add(new IslandGenerator(seed).BlobCount);
            Assert.GreaterOrEqual(counts.Count, 3, "island shapes don't vary in landmass count");
        }

    }
}
