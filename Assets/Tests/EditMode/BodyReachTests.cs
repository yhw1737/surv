using Isle.Gameplay.Combat;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>Reach is measured to a creature's body edge, not its centre, so a big boar is as hittable as a
    /// rabbit when you're standing against it. Body size grows with the cube root of weight (volume ∝ mass).</summary>
    public sealed class BodyReachTests
    {
        [Test]
        public void SurfaceDistance_SubtractsRadius()
        {
            Assert.AreEqual(0.5f, BodyReach.SurfaceDistance(centreDistance: 1.2f, radius: 0.7f), 1e-4f);
        }

        [Test]
        public void SurfaceDistance_Overlapping_IsZero()
        {
            Assert.AreEqual(0f, BodyReach.SurfaceDistance(0.3f, 0.7f), 1e-4f);
        }

        [Test]
        public void InReach_BigCreatureCentreBeyondReach_StillHits()
        {
            // Fists reach 1.2; a boar's centre 1.6 away with a 0.5 body is 1.1 to its edge.
            Assert.IsTrue(BodyReach.InReach(centreDistance: 1.6f, radius: 0.5f, reach: 1.2f));
            Assert.IsFalse(BodyReach.InReach(centreDistance: 1.8f, radius: 0.5f, reach: 1.2f));
        }

        [Test]
        public void RadiusForWeight_AtMean_IsBaseRadius()
        {
            Assert.AreEqual(0.45f, BodyReach.RadiusForWeight(0.45f, weightKg: 62f, meanKg: 62f), 1e-4f);
        }

        [Test]
        public void RadiusForWeight_EightTimesMean_DoublesRadius()
        {
            Assert.AreEqual(0.9f, BodyReach.RadiusForWeight(0.45f, weightKg: 496f, meanKg: 62f), 1e-3f);
        }
    }
}
