using Isle.Gameplay.Cooking;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-COOK-01 §Cook time.</summary>
    public sealed class CookTimeTests
    {
        [TestCase(1, 1f, 0, 6f)]      // grill one meat, novice
        [TestCase(3, 1.5f, 0, 15f)]   // boil three things
        [TestCase(2, 3f, 50, 12f)]    // dry two things, master: half
        [TestCase(2, 1f, 25, 6f)]     // halfway to master: ×0.75
        [TestCase(4, 0f, 10, 0f)]     // eating raw is instant
        public void Seconds_ByIngredientsMethodAndLevel(int ingredients, float mult, int level, float expected) =>
            Assert.That(CookTimeCalculator.Seconds(ingredients, mult, level), Is.EqualTo(expected).Within(1e-4f));

        [Test]
        public void Seconds_LevelPast50_IsClamped() =>
            Assert.That(CookTimeCalculator.Seconds(1, 1f, 80), Is.EqualTo(3f).Within(1e-4f));
    }
}
