using System.Collections.Generic;
using System.Linq;
using Isle.Core.Ids;
using Isle.Data;
using Isle.Gameplay.Combat;
using Isle.Modding.Defs;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>T-117 SYS-MAGIC-01 (mana, foci) and T-107 SYS-BREW-01 (brews, consume effects).</summary>
    public sealed class MagicBrewTests
    {
        [TestCase(0, 100f, 0.5f)]   // SYS-MAGIC-01 verification 1, 2
        [TestCase(50, 150f, 1.5f)]
        public void Mana_MagicLevel_CapacityAndRegen(int level, float max, float regen)
        {
            var growth = new[] { (1.0f, level) };
            Assert.AreEqual(max, ManaCalculator.ManaMax(growth), 1e-4f);
            Assert.AreEqual(regen, ManaCalculator.ManaRegen(new[] { (0.02f, level) }), 1e-4f);
        }

        [Test]
        public void Mana_NoGrowingSkills_IsTheBase()
        {
            Assert.AreEqual(ManaCalculator.ManaBase, ManaCalculator.ManaMax(new List<(float, int)>()));
            Assert.AreEqual(ManaCalculator.ManaRegenBase, ManaCalculator.ManaRegen(new List<(float, int)>()));
        }

        [Test]
        public void OverTime_SpreadsEvenly_AndNeverPastTheEnd()
        {
            Assert.AreEqual(2f, ManaCalculator.OverTimeStep(60f, 30f, 30f, 1f), 1e-5f); // mending draught: 2 HP/s
            Assert.AreEqual(1f, ManaCalculator.OverTimeStep(60f, 30f, 0.5f, 1f), 1e-5f, "the last half second gives half");
            Assert.AreEqual(0f, ManaCalculator.OverTimeStep(60f, 30f, 0f, 1f));
            var total = 0f;
            for (var t = 30f; t > 0f; t -= 0.1f) total += ManaCalculator.OverTimeStep(60f, 30f, t, 0.1f);
            Assert.AreEqual(60f, total, 0.05f, "the whole amount arrives");
        }

        [Test]
        public void Validator_ConsumeEffects_CatchesBadOnes()
        {
            var errors = new List<string>();
            SchemaValidator.Validate(new ItemDef
            {
                Id = NamespacedId.Parse("isle:bad_brew"),
                Consume = new[]
                {
                    new ConsumeEffect { Type = "heal_over_time", Amount = 10f },        // no seconds
                    new ConsumeEffect { Type = "fly", Amount = 1f },                    // unknown
                    new ConsumeEffect { Type = "mana", Amount = 0f },                   // nothing
                },
            }, errors);
            Assert.AreEqual(3, errors.Count, string.Join("\n", errors));
        }

        [Test]
        public void Validator_FocusWithoutProjectile_IsRejected()
        {
            var errors = new List<string>();
            SchemaValidator.Validate(new WeaponDef { Id = NamespacedId.Parse("isle:bad_wand"), ManaCost = 5f }, errors);
            Assert.AreEqual(1, errors.Count);
        }

        [Test]
        public void Content_MagicSkill_FociAndBrews()
        {
            DefRegistry.Clear();
            try
            {
                Assert.IsEmpty(DefinitionBootstrap.Load(System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, "definitions")));
                var magic = DefRegistry.Get<SkillDef>(NamespacedId.Parse("isle:magic"));
                Assert.AreEqual(1.0f, magic.ManaPerLevel, 1e-6f);
                Assert.AreEqual(0.02f, magic.ManaRegenPerLevel, 1e-6f);

                var foci = DefRegistry.All<WeaponDef>().Where(w => w.ManaCost > 0f).ToList();
                Assert.AreEqual(3, foci.Count, "three foci");
                foreach (var focus in foci)
                {
                    Assert.AreEqual(magic.Id, focus.CombatSkill, $"{focus.Id}: not a magic weapon");
                    var item = DefRegistry.All<ItemDef>().Single(i => i.Weapon == focus.Id);
                    var recipe = DefRegistry.All<CraftRecipeDef>().Single(r => r.Output.Item == item.Id);
                    Assert.AreEqual("isle:enchanting_table", recipe.Station.Value, $"{focus.Id}: not made at the enchanting table");
                }

                var brews = DefRegistry.All<CraftRecipeDef>().Where(r => r.Station.Value == "isle:brewing_stand").ToList();
                Assert.AreEqual(7, brews.Count, "seven brews");
                foreach (var brew in brews)
                {
                    Assert.AreEqual("isle:enchanting", brew.Skills.Single(s => s.Primary).Skill.Value, $"{brew.Id}: brewing is Enchanting");
                    var output = DefRegistry.Get<ItemDef>(brew.Output.Item);
                    Assert.IsNotNull(output.Consume, $"{output.Id}: a brew needs a consume block");
                    Assert.IsTrue(output.Tags.Contains("brew") && output.Tags.Any(t => t is "drink" or "medicine" or "drug"), $"{output.Id}: kind tag");
                }
                foreach (var kind in new[] { "drink", "medicine", "drug" })
                    Assert.IsTrue(brews.Any(b => DefRegistry.Get<ItemDef>(b.Output.Item).Tags.Contains(kind)), $"no {kind}");
            }
            finally { DefRegistry.Clear(); }
        }
    }
}
