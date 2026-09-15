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
    }
}
