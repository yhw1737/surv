using Isle.Data;
using Isle.Core.Ids;
using Isle.World.Chunks;
using Isle.World.Generation;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-WORLD-03 §Placement, verification 1–4.</summary>
    public sealed class ResourcePlacerTests
    {
        const int Seed = 4242;

        static SpawnSpec Spawn(params string[] biomes) => new SpawnSpec
        {
            Biomes = System.Array.ConvertAll(biomes, NamespacedId.Parse),
            Density = 0.2f,
        };

        [Test]
        public void Roll_SameSeedTwice_ReturnsIdenticalTiles()
        {
            var island = new IslandGenerator(Seed);
            var first = ResourcePlacer.Roll(island, Spawn("isle:forest"), Seed, salt: 1);
            var second = ResourcePlacer.Roll(island, Spawn("isle:forest"), Seed, salt: 1);
            CollectionAssert.AreEqual(first, second);
        }

        [Test]
        public void Roll_ForestOnlyDensity_PlacesWithinFifteenPercentOfDensity()
        {
            var island = new IslandGenerator(Seed);
            var forestLand = 0;
            for (var x = 0; x < IslandGenerator.Size; x++)
            for (var y = 0; y < IslandGenerator.Size; y++)
                if (island.IsLand(x, y) && island.BiomeAt(x, y) == Biome.Forest) forestLand++;

            var placed = ResourcePlacer.Roll(island, Spawn("isle:forest"), Seed, salt: 1).Count;
            var fraction = placed / (float)forestLand;

            Assert.Greater(forestLand, 2000);
            Assert.AreEqual(0.2f, fraction, 0.2f * 0.15f);
        }

        [Test]
        public void Roll_ForestOnly_NeverPlacesOnMarshOrCoast()
        {
            var island = new IslandGenerator(Seed);
            foreach (var tile in ResourcePlacer.Roll(island, Spawn("isle:forest"), Seed, salt: 1))
                Assert.AreEqual(Biome.Forest, island.BiomeAt(tile.X, tile.Y));
        }

        [Test]
        public void Roll_AnyBiome_NeverPlacesOffIsland()
        {
            var island = new IslandGenerator(Seed);
            foreach (var tile in ResourcePlacer.Roll(island, Spawn("isle:forest", "isle:marsh", "isle:coast"), Seed, salt: 1))
                Assert.IsTrue(island.IsLand(tile.X, tile.Y));
        }
    }
}
