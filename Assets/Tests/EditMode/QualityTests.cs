using Isle.Gameplay.Crafting;
using Isle.Gameplay.Inventory;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-CRAFT-01 §Quality: the score, its tier, and what a tier does to an item.</summary>
    public sealed class QualityTests
    {
        // §Verification (±0.005)
        [TestCase(5, 0.2f, 0.0f, 0.3f, 0.305f, QualityTier.Common)]
        [TestCase(30, 0.5f, 0.5f, 0.5f, 0.645f, QualityTier.Fine)]
        [TestCase(50, 1.0f, 1.0f, 1.0f, 1.000f, QualityTier.Master)]
        [TestCase(50, 0.5f, 0.5f, 0.5f, 0.825f, QualityTier.Superior)]
        [TestCase(1, 0.0f, 0.0f, 0.0f, 0.209f, QualityTier.Crude)]
        public void Score_SpecCases_MatchTable(int level, float material, float station, float minigame, float score, QualityTier tier)
        {
            var s = QualityCalculator.Score(level, material, station, minigame);
            Assert.That(s, Is.EqualTo(score).Within(0.005f));
            Assert.That(QualityCalculator.TierFor(s), Is.EqualTo(tier));
        }

        [TestCase(0.2999f, QualityTier.Crude)]
        [TestCase(0.30f, QualityTier.Common)]
        [TestCase(0.55f, QualityTier.Fine)]
        [TestCase(0.75f, QualityTier.Superior)]
        [TestCase(0.90f, QualityTier.Master)]
        public void TierFor_Boundaries_AreInclusiveBelow(float score, QualityTier tier) =>
            Assert.That(QualityCalculator.TierFor(score), Is.EqualTo(tier));

        [Test]
        public void Score_Clamped()
        {
            Assert.That(QualityCalculator.Score(80, 1f, 1f, 1f), Is.EqualTo(1f));
            Assert.That(QualityCalculator.Score(-5, 0f, 0f, 0f), Is.GreaterThanOrEqualTo(0f));
        }

        [TestCase(QualityTier.Crude, 0.70f, 0, 0.60f)]
        [TestCase(QualityTier.Common, 1.00f, 1, 1.00f)]
        [TestCase(QualityTier.Fine, 1.15f, 2, 1.30f)]
        [TestCase(QualityTier.Superior, 1.30f, 3, 1.70f)]
        [TestCase(QualityTier.Master, 1.50f, 4, 2.20f)]
        public void TierTable_MatchesSpec(QualityTier tier, float power, int slots, float durability)
        {
            Assert.That(QualityCalculator.PowerMult(tier), Is.EqualTo(power));
            Assert.That(QualityCalculator.EnchantSlots(tier), Is.EqualTo(slots));
            Assert.That(QualityCalculator.DurabilityMult(tier), Is.EqualTo(durability));
        }

        [Test]
        public void MaxDurability_ScalesBaseByTier()
        {
            Assert.That(QualityCalculator.MaxDurability(60, QualityTier.Crude), Is.EqualTo(36));
            Assert.That(QualityCalculator.MaxDurability(300, QualityTier.Fine), Is.EqualTo(390));
            Assert.That(QualityCalculator.MaxDurability(500, QualityTier.Master), Is.EqualTo(1100));
        }

        [Test]
        public void MaterialPurity_MeanOfInputs_UnratedCountsHalf()
        {
            Assert.That(QualityCalculator.MaterialPurity(new QualityTier?[] { null, null }), Is.EqualTo(0.5f));
            Assert.That(QualityCalculator.MaterialPurity(new QualityTier?[] { QualityTier.Master, QualityTier.Crude }), Is.EqualTo(0.5f));
            Assert.That(QualityCalculator.MaterialPurity(new QualityTier?[] { QualityTier.Fine, null }), Is.EqualTo(0.5f));
            Assert.That(QualityCalculator.MaterialPurity(new QualityTier?[] { QualityTier.Master }), Is.EqualTo(1f));
            Assert.That(QualityCalculator.MaterialPurity(new QualityTier?[0]), Is.EqualTo(0.5f));
        }

        [Test]
        public void NewSurvivor_RawMaterialsByHand_MakesCommon()
        {
            // Lv 0, raw inputs, no station, no minigame yet: never Crude by default.
            var s = QualityCalculator.Score(0, QualityCalculator.UnratedPurity, 0f, QualityCalculator.NoMinigameScore);
            Assert.That(QualityCalculator.TierFor(s), Is.EqualTo(QualityTier.Common));
        }

        [Test]
        public void Unrated_ActsAsCommon()
        {
            Assert.That(QualityCalculator.PowerMult(null), Is.EqualTo(1f));
            Assert.That(QualityCalculator.PowerOf(null), Is.EqualTo(1f));
            Assert.That(QualityCalculator.PowerOf(new ItemWear(10, 10) { Quality = QualityTier.Fine }), Is.EqualTo(1.15f));
        }

        [Test]
        public void Repair_KeepsQuality()
        {
            var wear = new ItemWear(0, 78) { Quality = QualityTier.Fine };
            wear.Repair();
            Assert.That(wear.Quality, Is.EqualTo(QualityTier.Fine));
            Assert.That(wear.Max, Is.EqualTo(72));
        }
    }
}
