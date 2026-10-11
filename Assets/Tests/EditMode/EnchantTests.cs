using System.Collections.Generic;
using System.Linq;
using Isle.Core.Ids;
using Isle.Data;
using Isle.Gameplay.Crafting;
using Isle.Modding.Defs;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>T-106 SYS-CRAFT-01 §Enchanting: success rate, slots from quality, stacking, effects, the four enchants.</summary>
    public sealed class EnchantTests
    {
        static EnchantDef Make(string id, string[] tags, int maxStack, int level, params EnchantEffect[] effects) =>
            new() { Id = NamespacedId.Parse(id), ApplicableTags = tags, MaxStack = maxStack, EnchantingSkillRequired = level, Effects = effects };

        [TestCase(0, 0.60f)]
        [TestCase(25, 0.80f)]
        [TestCase(50, 1.00f)]
        [TestCase(70, 1.00f)]
        public void SuccessRate_SpecFormula(int level, float rate) =>
            Assert.AreEqual(rate, EnchantCalculator.SuccessRate(level), 1e-5f);

        [Test]
        public void Applies_ByTagOrUnderIt()
        {
            var swift = Make("t:swift", new[] { "tool" }, 2, 0);
            Assert.IsTrue(EnchantCalculator.Applies(swift, new[] { "tool", "tool/axe" }));
            Assert.IsTrue(EnchantCalculator.Applies(swift, new[] { "tool/pickaxe" }));
            Assert.IsFalse(EnchantCalculator.Applies(swift, new[] { "toolbox" }));
            Assert.IsFalse(EnchantCalculator.Applies(swift, new[] { "weapon" }));
        }

        [Test]
        public void Check_SlotsFromQuality_MaxStack_Skill()
        {
            var keen = Make("t:keen", new[] { "weapon" }, 2, 10);
            var none = new List<NamespacedId>();
            var one = new List<NamespacedId> { keen.Id };
            var two = new List<NamespacedId> { keen.Id, keen.Id };
            var common = QualityCalculator.EnchantSlots(QualityTier.Common);   // 1
            var superior = QualityCalculator.EnchantSlots(QualityTier.Superior); // 3
            Assert.AreEqual(EnchantRefusal.None, EnchantCalculator.Check(keen, new[] { "weapon" }, common, none, 10));
            Assert.AreEqual(EnchantRefusal.NoFreeSlot, EnchantCalculator.Check(keen, new[] { "weapon" }, common, one, 10));
            Assert.AreEqual(EnchantRefusal.MaxStack, EnchantCalculator.Check(keen, new[] { "weapon" }, superior, two, 10));
            Assert.AreEqual(EnchantRefusal.SkillTooLow, EnchantCalculator.Check(keen, new[] { "weapon" }, superior, none, 9));
            Assert.AreEqual(EnchantRefusal.NotApplicable, EnchantCalculator.Check(keen, new[] { "armor" }, superior, none, 10));
            Assert.AreEqual(EnchantRefusal.NoFreeSlot, EnchantCalculator.Check(keen, new[] { "weapon" }, QualityCalculator.EnchantSlots(QualityTier.Crude), none, 50), "crude gear has no slots");
        }

        [Test]
        public void Effects_MultiplyAndCombine()
        {
            var keen = Make("t:keen", new[] { "weapon" }, 2, 0, new EnchantEffect { Type = "damage_mult", Value = 1.12f });
            var sturdy = Make("t:sturdy", new[] { "weapon" }, 2, 0, new EnchantEffect { Type = "wear_skip_chance", Value = 0.3f });
            Assert.AreEqual(1.12f * 1.12f, EnchantCalculator.Mult(new[] { keen, keen }, EnchantCalculator.DamageMult), 1e-5f);
            Assert.AreEqual(1f, EnchantCalculator.Mult(new[] { sturdy }, EnchantCalculator.DamageMult));
            Assert.AreEqual(0.51f, EnchantCalculator.SkipChance(new[] { sturdy, sturdy }), 1e-5f);
            Assert.AreEqual(0f, EnchantCalculator.SkipChance(new EnchantDef[0]));
        }

        [Test]
        public void Content_FourEnchants_CatalystsAreDungeonMaterials()
        {
            DefRegistry.Clear();
            try
            {
                Assert.IsEmpty(DefinitionBootstrap.Load(System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, "definitions")));
                var enchants = DefRegistry.All<EnchantDef>().ToList();
                Assert.AreEqual(4, enchants.Count);
                foreach (var enchant in enchants)
                {
                    Assert.IsTrue(enchant.Skill.IsValid, $"{enchant.Id}: no skill");
                    Assert.IsNotEmpty(enchant.Catalyst, $"{enchant.Id}: no catalyst");
                    foreach (var c in enchant.Catalyst)
                        Assert.IsTrue(DefRegistry.TryGet<ItemDef>(c.Item, out var item) && item.Tags.Contains("dungeon_material"), $"{enchant.Id}: catalyst {c.Item} isn't a dungeon material");
                }
            }
            finally { DefRegistry.Clear(); }
        }
    }
}
