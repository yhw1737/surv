using Isle.Gameplay.World;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>T-028 SYS-DIFF-01: the tier multipliers, ids, and what Unlimited switches off.</summary>
    public sealed class DifficultyTests
    {
        [TearDown]
        public void Reset() => Difficulty.Current = DifficultyLevel.Normal;

        [TestCase(DifficultyLevel.Easy, 0.50f)]
        [TestCase(DifficultyLevel.Normal, 1.00f)]
        [TestCase(DifficultyLevel.Hard, 1.25f)]
        public void Mult_Tier_MatchesSpec(DifficultyLevel tier, float expected) =>
            Assert.AreEqual(expected, Difficulty.Mult(tier), 1e-6f);

        [Test]
        public void HardStrike_Of20_Is25BeforeArmor() // SYS-DIFF-01 verification 3
        {
            Difficulty.Current = DifficultyLevel.Hard;
            Assert.AreEqual(25f, Difficulty.CreatureDamage(20f), 1e-4f);
        }

        [Test]
        public void EasyNeeds_HalveTheDrain() // verification 2
        {
            Difficulty.Current = DifficultyLevel.Easy;
            Assert.AreEqual(0.75f, Difficulty.NeedsDrain(1.5f), 1e-6f);
        }

        [Test]
        public void Unlimited_NoDrainNoDamage()
        {
            Difficulty.Current = DifficultyLevel.Unlimited;
            Assert.IsTrue(Difficulty.IsUnlimited);
            Assert.AreEqual(0f, Difficulty.NeedsDrain(3f));
            Assert.AreEqual(0f, Difficulty.CreatureDamage(50f));
        }

        [TestCase("easy", DifficultyLevel.Easy)]
        [TestCase("hard", DifficultyLevel.Hard)]
        [TestCase("unlimited", DifficultyLevel.Unlimited)]
        [TestCase("", DifficultyLevel.Normal)]       // saves from before version 10
        [TestCase(null, DifficultyLevel.Normal)]
        [TestCase("nonsense", DifficultyLevel.Normal)]
        public void Parse_SavedId_RoundTrips(string id, DifficultyLevel expected)
        {
            Assert.AreEqual(expected, Difficulty.Parse(id));
            if (!string.IsNullOrEmpty(id) && id != "nonsense") Assert.AreEqual(id, Difficulty.IdOf(expected));
        }
    }
}
