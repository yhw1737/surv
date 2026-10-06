using Isle.Data;
using Isle.World.Generation;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-WORLD-03 §Clustering: patches cover about the configured share of the land; inside them density is
    /// multiplied by <c>inside</c>, between them by <c>outside</c>.</summary>
    public sealed class ResourceClusterTests
    {
        static readonly ClusterSpec Spec = new() { ScaleTiles = 30f, Coverage = 0.3f, Inside = 3f, Outside = 0.1f };

        [Test]
        public void Cluster_CoversRoughlyTheConfiguredShare()
        {
            int inside = 0, total = 0;
            for (var x = 0; x < 600; x += 3)
            for (var y = 0; y < 600; y += 3)
            {
                total++;
                if (ResourcePlacer.ClusterMult(Spec, x, y, 1234) > 1.5f) inside++;
            }
            Assert.AreEqual(0.3f, inside / (float)total, 0.12f);
        }

        [Test]
        public void Cluster_MultiplierStaysBetweenOutsideAndInside()
        {
            for (var x = 0; x < 200; x += 7)
            for (var y = 0; y < 200; y += 7)
            {
                var m = ResourcePlacer.ClusterMult(Spec, x, y, 99);
                Assert.GreaterOrEqual(m, Spec.Outside - 1e-4f);
                Assert.LessOrEqual(m, Spec.Inside + 1e-4f);
            }
        }

        [Test]
        public void Cluster_IsDeterministicPerSeed()
        {
            Assert.AreEqual(ResourcePlacer.ClusterMult(Spec, 40, 70, 5), ResourcePlacer.ClusterMult(Spec, 40, 70, 5));
        }

        [Test]
        public void Cluster_NeighbouringTilesAgree()
        {
            // Patches are blobs, not noise: adjacent tiles almost always fall on the same side.
            int same = 0, total = 0;
            for (var x = 0; x < 300; x += 2)
            {
                total++;
                if (ResourcePlacer.ClusterMult(Spec, x, 50, 7) > 1.5f == ResourcePlacer.ClusterMult(Spec, x + 1, 50, 7) > 1.5f) same++;
            }
            Assert.Greater(same / (float)total, 0.9f);
        }
    }
}
