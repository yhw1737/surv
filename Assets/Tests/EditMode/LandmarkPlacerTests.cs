using System;
using System.Collections.Generic;
using System.Linq;
using Isle.Core;
using Isle.Core.Ids;
using Isle.World.Chunks;
using Isle.World.Generation;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-WORLD-01 §Island generation (T-036).</summary>
    public sealed class LandmarkPlacerTests
    {
        static readonly LandmarkDef[] Landmarks =
        {
            new(NamespacedId.Parse("isle:shipwreck"), count: 1),
            new(NamespacedId.Parse("isle:ruins"), count: 2),
            new(NamespacedId.Parse("isle:freshwater_spring"), count: 3)
        };

        static List<Vec2Int> WorldPositions(Dictionary<Vec2Int, List<WorldObject>> byChunk) =>
            byChunk.SelectMany(kv => kv.Value.Select(o =>
                new Vec2Int(kv.Key.X * Chunk.Size + o.LocalPosition.X, kv.Key.Y * Chunk.Size + o.LocalPosition.Y))).ToList();

        [Test]
        public void Place_TotalCount_MatchesSpec()
        {
            var island = new IslandGenerator(seed: 42);

            var byChunk = LandmarkPlacer.Place(island, Landmarks, seed: 42);

            Assert.AreEqual(6, byChunk.Values.Sum(l => l.Count));
        }

        [Test]
        public void Place_EveryLandmark_IsOnLand()
        {
            var island = new IslandGenerator(seed: 42);

            var byChunk = LandmarkPlacer.Place(island, Landmarks, seed: 42);

            foreach (var pos in WorldPositions(byChunk))
                Assert.IsTrue(island.IsLand(pos.X, pos.Y), $"{pos} is not on land");
        }

        [Test]
        public void Place_SameSeed_IsDeterministic()
        {
            var island = new IslandGenerator(seed: 42);

            var a = WorldPositions(LandmarkPlacer.Place(island, Landmarks, seed: 42));
            var b = WorldPositions(LandmarkPlacer.Place(island, Landmarks, seed: 42));

            CollectionAssert.AreEquivalent(a, b);
        }

        [Test]
        public void Place_MaximizesSeparation_NoTwoLandmarksAreClustered()
        {
            var island = new IslandGenerator(seed: 42);
            var positions = WorldPositions(LandmarkPlacer.Place(island, Landmarks, seed: 42));

            var minPairwise = double.MaxValue;
            for (var i = 0; i < positions.Count; i++)
            for (var j = i + 1; j < positions.Count; j++)
            {
                var dx = positions[i].X - positions[j].X;
                var dy = positions[i].Y - positions[j].Y;
                minPairwise = Math.Min(minPairwise, Math.Sqrt(dx * dx + dy * dy));
            }

            // Spec target is ~256 tiles (2 minutes walking) but that's not reachable for all 6
            // pairs at once on a 384-tile island — see LandmarkPlacer's doc comment. This floor
            // just catches a broken/degenerate placement (everything piled in one spot).
            Assert.GreaterOrEqual(minPairwise, 80);
        }
    }
}
