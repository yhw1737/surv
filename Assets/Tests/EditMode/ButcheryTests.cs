using Isle.Gameplay.Hunting;
using System.Linq;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-HUNT-01 §Yield verification (±0.15 kg), §Cuts survival, §Carrying butcher time, damage factors.</summary>
    public sealed class ButcheryTests
    {
        const float Boar = 62f, BoarEdible = 0.45f;

        [Test]
        public void Case1_Lv5_BareHands_Dmg095_Is10kg()
        {
            Assert.AreEqual(0.327f, ButcheryCalculator.Quality(5, 0.4f, 0.95f), 0.005f);
            Assert.AreEqual(10.0f, ButcheryCalculator.EdibleKg(Boar, BoarEdible, 1.1f, 5, 0.4f, 0.95f), 0.15f);
        }

        [Test]
        public void Case2_Lv30_Iron_Dmg095_Is21_5kg() =>
            Assert.AreEqual(21.5f, ButcheryCalculator.EdibleKg(Boar, BoarEdible, 1.1f, 30, 1.0f, 0.95f), 0.15f);

        [Test]
        public void Case3_Lv50_Master_Dmg1_Cond13_Is40_1kg()
        {
            Assert.AreEqual(1.105f, ButcheryCalculator.Quality(50, 1.15f, 1.0f), 0.005f);
            Assert.AreEqual(40.1f, ButcheryCalculator.EdibleKg(Boar, BoarEdible, 1.3f, 50, 1.15f, 1.0f), 0.15f);
        }

        [Test]
        public void Case4_Lv30_Iron_Blunt060_Is16_9kg() =>
            Assert.AreEqual(16.9f, ButcheryCalculator.EdibleKg(Boar, BoarEdible, 1.1f, 30, 1.0f, 0.60f), 0.15f);

        [Test]
        public void Rabbit_Lv20_Stone_Dmg09_Is0_79kg() =>
            Assert.AreEqual(0.79f, ButcheryCalculator.EdibleKg(3f, 0.55f, 1.0f, 20, 0.7f, 0.9f), 0.05f);

        [Test]
        public void Quality_IsClamped()
        {
            Assert.AreEqual(ButcheryCalculator.QualityCeil, ButcheryCalculator.Quality(50, 2f, 1f), 1e-5f, "capped at 1.15");
            Assert.AreEqual(ButcheryCalculator.ButcherBase, ButcheryCalculator.Quality(0, 0f, 0f), 1e-5f, "no skill: the 0.30 base");
        }

        [Test]
        public void BluntDestroysHide_Lv30_Dmg070_Survives053() =>
            Assert.AreEqual(0.532f, ButcheryCalculator.SurvivalRate(0.70f, 30), 0.005f);

        [Test]
        public void DamageFactor_ByKillMethod_AndOverkill()
        {
            Assert.AreEqual(0.95f, ButcheryCalculator.DamageFactor(KillMethod.Precise, overkill: false), 1e-5f);
            Assert.AreEqual(0.85f, ButcheryCalculator.DamageFactor(KillMethod.Melee, overkill: false), 1e-5f);
            Assert.AreEqual(0.70f, ButcheryCalculator.DamageFactor(KillMethod.Blunt, overkill: false), 1e-5f);
            Assert.AreEqual(0.70f * 0.85f, ButcheryCalculator.DamageFactor(KillMethod.Blunt, overkill: true), 1e-5f);
            Assert.IsTrue(ButcheryCalculator.IsOverkill(blowDamage: 80f, healthBefore: 20f, maxHealth: 100f), "60 excess > 50% of 100");
            Assert.IsFalse(ButcheryCalculator.IsOverkill(blowDamage: 60f, healthBefore: 20f, maxHealth: 100f), "40 excess");
        }

        [Test]
        public void ButcherSeconds_62kgBoar_Lv30_Is17_9s()
        {
            Assert.AreEqual(17.9f, ButcheryCalculator.ButcherSeconds(62f, 30), 0.05f);
            Assert.AreEqual(17.9f / 3f, ButcheryCalculator.ButcherSeconds(62f, 30, butchers: 5), 0.05f, "split among at most 3");
        }

        [Test]
        public void Pieces_TwoAndAHalfKgEach_AtLeastTheMinimum()
        {
            Assert.AreEqual(8, ButcheryCalculator.Pieces(21.5f, 2.5f, min: 1));
            Assert.AreEqual(1, ButcheryCalculator.Pieces(0.79f, 2.5f, min: 1), "a rabbit still gives one piece");
            Assert.AreEqual(0, ButcheryCalculator.Pieces(0.3f, 1f, min: 0), "a ruined hide gives nothing");
        }
    
        // --- Content: real creatures --------------------------------------------------------------------------------

        static Isle.Data.CreatureDef Creature(string id) =>
            Isle.Modding.Defs.DefRegistry.Get<Isle.Data.CreatureDef>(Isle.Core.Ids.NamespacedId.Parse("isle:" + id));

        static int CountOf(System.Collections.Generic.List<(Isle.Core.Ids.NamespacedId Item, int Count)> cuts, string item) =>
            cuts.Where(c => c.Item.Value == "isle:" + item).Sum(c => c.Count);

        [Test]
        public void Cuts_Boar_FollowTheFormula_AndTheTwoAndAHalfKgPiece()
        {
            Load();
            try
            {
                var boar = Creature("boar");
                var cuts = CreatureDirector.Cuts(boar.Butcher, 62f, 1.0f, 30, 1.0f, 0.95f);
                var edible = ButcheryCalculator.EdibleKg(62f, boar.Butcher.EdibleRatio, 1.0f, 30, 1.0f, 0.95f);
                Assert.AreEqual((int)(edible * 0.55f / 2.5f), CountOf(cuts, "raw_meat"));
                Assert.Greater(CountOf(cuts, "boar_hide"), 0);
            }
            finally { Isle.Modding.Defs.DefRegistry.Clear(); }
        }

        [Test]
        public void Cuts_SkillAndKnife_MakeTheGap_AndBluntRuinsHide()
        {
            Load();
            try
            {
                var deer = Creature("deer").Butcher;
                var novice = CreatureDirector.Cuts(deer, 50f, 1f, 0, ButcheryCalculator.BareHandsToolFactor, 0.85f);
                var master = CreatureDirector.Cuts(deer, 50f, 1f, 50, 1.08f, 0.95f);
                Assert.Greater(CountOf(master, "raw_meat"), 2 * CountOf(novice, "raw_meat"), "a skilled butcher gets far more");
                var precise = CreatureDirector.Cuts(deer, 50f, 1f, 30, 1f, 0.95f);
                var blunt = CreatureDirector.Cuts(deer, 50f, 1f, 30, 1f, 0.70f * 0.85f);
                Assert.Greater(CountOf(precise, "hide"), CountOf(blunt, "hide"), "blunt overkill ruins hide");
                Assert.AreEqual(1, CountOf(CreatureDirector.Cuts(Creature("rabbit").Butcher, 3f, 1f, 0, 0.4f, 0.7f), "raw_meat"), "a rabbit always gives a piece");
            }
            finally { Isle.Modding.Defs.DefRegistry.Clear(); }
        }

        [Test]
        public void KnifeMaterials_CarryTheSpecToolFactors()
        {
            Load();
            try
            {
                foreach (var (material, factor) in new[] { ("stone", 0.70f), ("copper", 0.85f), ("iron", 1.00f), ("steel", 1.08f) })
                {
                    var knife = Isle.Modding.Defs.DefRegistry.Get<Isle.Data.ItemDef>(Isle.Core.Ids.NamespacedId.Parse($"isle:knife__{material}"));
                    var mat = Isle.Modding.Defs.DefRegistry.Get<Isle.Data.MaterialDef>(knife.Material);
                    Assert.AreEqual(factor, mat.ButcherFactor, 1e-5f, material);
                }
            }
            finally { Isle.Modding.Defs.DefRegistry.Clear(); }
        }

        static void Load()
        {
            Isle.Modding.Defs.DefRegistry.Clear();
            Assert.IsEmpty(Isle.Modding.Defs.DefinitionBootstrap.Load(System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, "definitions")));
        }
    }
}
