using System.Collections.Generic;
using Isle.Core.Ids;
using Isle.Data;
using Isle.Gameplay.Skills;
using NUnit.Framework;
using static Isle.Gameplay.Skills.FocusCalculator;

namespace Isle.Tests.EditMode
{
    /// <summary>
    /// SYS-SKILL-01 §Focus, re-simulated for T-018's 8-skill, 5/3-pool roster
    /// (Production: gathering, fishing, cooking, crafting, enchanting; Combat: melee, ranged,
    /// magic). Values recomputed from the spec formula against this table — see PROJECT_STATE.md
    /// §Decided without a spec, 2026-09-09, for why the old 7-skill/5-2-pool cases don't carry over
    /// unchanged (cases 4 and 6-9, which have more than one non-trivial level, shift because Combat
    /// gained a third member).
    /// </summary>
    public sealed class FocusCalculatorTests
    {
        const string Production = "production";
        const string Combat = "combat";
        const float Tolerance = 0.005f;

        static SkillLevel[] Others(params (int Level, string Pool)[] skills)
        {
            var result = new SkillLevel[skills.Length];
            for (var i = 0; i < skills.Length; i++) result[i] = new SkillLevel(skills[i].Level, skills[i].Pool);
            return result;
        }

        [Test]
        public void Focus_Case1_AllOtherNovice_ReturnsMax()
        {
            var target = new SkillLevel(45, Production);
            var others = Others((10, Production), (10, Production), (10, Production), (10, Production), (10, Combat), (10, Combat), (10, Combat));

            Assert.AreEqual(1.000f, Focus(target, others, 4), Tolerance);
            Assert.AreEqual(2.00f, Multiplier(Focus(target, others, 4)), 0.01f);
        }

        [Test]
        public void Focus_Case2_CrossPoolAdept_CostsAlmostNothing()
        {
            var target = new SkillLevel(45, Production);
            var others = Others((10, Production), (10, Production), (10, Production), (10, Combat), (35, Combat), (10, Combat), (10, Production));

            Assert.AreEqual(0.880f, Focus(target, others, 4), Tolerance);
            Assert.AreEqual(1.86f, Multiplier(Focus(target, others, 4)), 0.01f);
        }

        [Test]
        public void Focus_Case3_SamePoolMaster_CostsMuchMore()
        {
            var target = new SkillLevel(45, Production);
            var others = Others((10, Production), (10, Production), (40, Production), (10, Production), (10, Combat), (10, Combat), (10, Combat));

            Assert.AreEqual(0.529f, Focus(target, others, 4), Tolerance);
            Assert.AreEqual(1.41f, Multiplier(Focus(target, others, 4)), 0.01f);
        }

        [Test]
        public void Focus_Case4_AllSkillsAtAdept_ReflectsFivethreeSplit()
        {
            var target = new SkillLevel(35, Production);
            var others = Others((35, Production), (35, Production), (35, Production), (35, Production), (35, Combat), (35, Combat), (35, Combat));

            Assert.AreEqual(0.284f, Focus(target, others, 4), Tolerance);
            Assert.AreEqual(1.03f, Multiplier(Focus(target, others, 4)), 0.01f);
        }

        [Test]
        public void Focus_Case5_AllNovice_ReturnsMax()
        {
            var target = new SkillLevel(10, Production);
            var others = Others((10, Production), (10, Production), (10, Production), (10, Production), (10, Combat), (10, Combat), (10, Combat));

            Assert.AreEqual(1.000f, Focus(target, others, 4), Tolerance);
            Assert.AreEqual(2.00f, Multiplier(Focus(target, others, 4)), 0.01f);
        }

        [TestCase(1, 0.637f, 1.55f)]
        [TestCase(2, 0.506f, 1.37f)]
        [TestCase(3, 0.435f, 1.27f)]
        [TestCase(4, 0.381f, 1.19f)]
        public void Focus_Cases6To9_ScaleWithActivePlayerCount(int activePlayers, float expectedFocus, float expectedMultiplier)
        {
            var target = new SkillLevel(45, Production);
            var others = Others((30, Production), (30, Production), (30, Production), (30, Production), (25, Combat), (25, Combat), (25, Combat));

            Assert.AreEqual(expectedFocus, Focus(target, others, activePlayers), Tolerance);
            Assert.AreEqual(expectedMultiplier, Multiplier(Focus(target, others, activePlayers)), 0.01f);
        }

        [Test]
        public void InterferenceWeight_BelowNoviceCap_IsZero()
        {
            Assert.AreEqual(0f, InterferenceWeight(15));
        }

        [Test]
        public void InterferenceWeight_AtAdeptCap_IsHalf()
        {
            Assert.AreEqual(0.5f, InterferenceWeight(35));
        }

        [Test]
        public void InterferenceWeight_AboveAdeptCap_IsFull()
        {
            Assert.AreEqual(1.0f, InterferenceWeight(36));
        }

        [Test]
        public void Focus_FromSkillSetAndDefs_MatchesRawLevelOverload()
        {
            var cooking = new SkillDef { Id = NamespacedId.Parse("isle:cooking"), Name = "@skill.cooking", Pool = Production };
            var crafting = new SkillDef { Id = NamespacedId.Parse("isle:crafting"), Name = "@skill.crafting", Pool = Production };
            var melee = new SkillDef { Id = NamespacedId.Parse("isle:melee"), Name = "@skill.melee", Pool = Combat };
            var allSkills = new List<SkillDef> { cooking, crafting, melee };

            var skillSet = new SkillSet(allSkills);
            skillSet.SetLevel(cooking.Id, 45);
            skillSet.SetLevel(crafting.Id, 40);
            skillSet.SetLevel(melee.Id, 10);

            var expected = Focus(new SkillLevel(45, Production), Others((40, Production), (10, Combat)), 4);
            var actual = Focus(skillSet, allSkills, cooking.Id, 4);

            Assert.AreEqual(expected, actual, Tolerance);
        }

        [Test]
        public void Focus_ScalesToAnySkillCount_NotJustEight()
        {
            // A mod adding a ninth skill, well below the novice cap, must not move anyone's focus.
            var target = new SkillLevel(45, Production);
            var withoutModSkill = Others((10, Production), (10, Combat));
            var withModSkill = Others((10, Production), (10, Combat), (10, Combat));

            Assert.AreEqual(Focus(target, withoutModSkill, 4), Focus(target, withModSkill, 4), Tolerance);
        }
    }
}
