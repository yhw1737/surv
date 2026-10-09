using Isle.Gameplay.Hunting;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>The spoilage percentage the UI shows: the way from fresh to gone.</summary>
    public sealed class CarcassSpoilShareTests
    {
        [TestCase(0f, 0f)]
        [TestCase(0.75f, 0.5f)]
        [TestCase(1.5f, 1f)]
        [TestCase(3f, 1f)]
        public void SpoilShare_IsShareOfTheWayToGone(float spoilage, float share) =>
            Assert.That(CarcassCalculator.SpoilShare(spoilage), Is.EqualTo(share).Within(1e-5f));
    }
}
