using System.Collections.Generic;
using System.Linq;
using Isle.Core;
using Isle.World.Chunks;
using Isle.World.Generation;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-WORLD-03 §Water bodies, verification 9–11: deterministic per seed, ponds inland, rivers reach
    /// the sea, depth grows with distance from land.</summary>
    public sealed class WaterGeneratorTests
    {
        const int Seed = 4242;
        static readonly WaterBodyPlan Pond = new(index: 0, kind: "pond", count: 6, minSize: 3, maxSize: 7);
        static readonly WaterBodyPlan River = new(index: 1, kind: "river", count: 2, minSize: 2, maxSize: 3);

        static Dictionary<Vec2Int, int> Generate() =>
            WaterGenerator.Generate(new IslandGenerator(Seed), Seed, new[] { Pond, River });

        [Test]
        public void Generate_SameSeedTwice_IdenticalTiles()
        {
            CollectionAssert.AreEquivalent(Generate(), Generate());
        }

        [Test]
        public void Generate_PondTiles_OnIslandAndNotCoast()
        {
            var island = new IslandGenerator(Seed);
            var ponds = Generate().Where(kv => kv.Value == Pond.Index).Select(kv => kv.Key).ToList();
            Assert.Greater(ponds.Count, 30, "ponds too small or missing");
            foreach (var tile in ponds)
            {
                Assert.IsTrue(island.IsLand(tile.X, tile.Y));
                Assert.AreNotEqual(Biome.Coast, island.BiomeAt(tile.X, tile.Y));
            }
        }

        [Test]
        public void Generate_River_ReachesTheCoastRing()
        {
            var island = new IslandGenerator(Seed);
            var river = Generate().Where(kv => kv.Value == River.Index).Select(kv => kv.Key).ToList();
            Assert.Greater(river.Count, 20);
            Assert.IsTrue(river.Any(t => island.BiomeAt(t.X, t.Y) == Biome.Coast), "no river reaches the coast");
        }

        [Test]
        public void DistanceToLand_ShoreIsOne_InteriorDeeper()
        {
            var water = new HashSet<Vec2Int>();
            for (var x = 0; x < 5; x++)
            for (var y = 0; y < 5; y++)
                water.Add(new Vec2Int(x + 10, y + 10));
            var depth = WaterGenerator.DistanceToLand(water);
            Assert.AreEqual(1, depth[new Vec2Int(10, 12)]);
            Assert.AreEqual(3, depth[new Vec2Int(12, 12)]);
        }
    }
}
