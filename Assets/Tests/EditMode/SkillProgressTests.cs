using Isle.Core.Ids;
using Isle.Data;
using Isle.Gameplay.Skills;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>Earned XP turns into levels along SYS-SKILL-01's curve (cumulative 80 → Lv 2, 787 → Lv 4,
    /// 783,538 → Lv 50), and an xp block's amount is base + per_unit × units (docs/content/xp_table.md).</summary>
    public sealed class SkillProgressTests
    {
        static readonly NamespacedId Cooking = NamespacedId.Parse("isle:cooking");
        static readonly NamespacedId Melee = NamespacedId.Parse("isle:melee");

        static SkillProgress Fresh() => new(new[]
        {
            new SkillDef { Id = Cooking, Pool = "production" },
            new SkillDef { Id = Melee, Pool = "combat" },
        });

        [Test]
        public void New_EverySkillAtLevel1()
        {
            Assert.AreEqual(1, Fresh().Level(Cooking));
        }

        [Test]
        public void AddXp_80_ReachesLevel2()
        {
            var skills = Fresh();
            Assert.AreEqual(2, skills.AddXp(Cooking, 80));
        }

        [Test]
        public void AddXp_JustBelowThreshold_StaysAtLevel()
        {
            var skills = Fresh();
            Assert.AreEqual(3, skills.AddXp(Cooking, 786));
            Assert.AreEqual(4, skills.AddXp(Cooking, 1));
        }

        [Test]
        public void AddXp_Huge_CapsAt50()
        {
            var skills = Fresh();
            Assert.AreEqual(50, skills.AddXp(Cooking, 10_000_000));
            Assert.AreEqual(1f, skills.ProgressToNext(Cooking), 1e-4f);
        }

        [Test]
        public void ProgressToNext_HalfwayThroughLevel1()
        {
            var skills = Fresh();
            skills.AddXp(Cooking, 40);
            Assert.AreEqual(0.5f, skills.ProgressToNext(Cooking), 1e-4f);
        }

        [Test]
        public void AddXp_UnknownSkill_Ignored()
        {
            var skills = Fresh();
            Assert.AreEqual(1, skills.AddXp(NamespacedId.Parse("isle:magic"), 999));
            Assert.AreEqual(0d, skills.TotalXp(NamespacedId.Parse("isle:magic")), 1e-6);
        }

        [Test]
        public void Restore_SetsTotalAndLevel()
        {
            var skills = Fresh();
            skills.Restore(Melee, 10_699);
            Assert.AreEqual(10, skills.Level(Melee));
        }

        [Test]
        public void Amount_BasePlusPerUnit()
        {
            var award = new XpAward { Skill = Cooking, Base = 40f, PerUnit = 15f };
            Assert.AreEqual(70f, SkillProgress.Amount(award, units: 2f), 1e-4f);
        }

        [Test]
        public void Amount_NoAward_IsZero()
        {
            Assert.AreEqual(0f, SkillProgress.Amount(null, 5f), 1e-4f);
        }
    }
}
