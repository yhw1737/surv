using Isle.Core.Ids;
using Isle.Data;
using Isle.Gameplay.Skills;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-SKILL-01 — per-player skill levels, built from whatever <see cref="SkillDef"/>s are loaded.</summary>
    public sealed class SkillSetTests
    {
        static SkillDef Cooking => new() { Id = NamespacedId.Parse("isle:cooking"), Name = "@skill.cooking", Pool = "production" };

        [Test]
        public void LevelOf_KnownSkill_StartsAtOne()
        {
            var set = new SkillSet(new[] { Cooking });
            Assert.AreEqual(1, set.LevelOf(Cooking.Id));
        }

        [Test]
        public void LevelOf_UnknownSkill_ReturnsOneRatherThanThrowing()
        {
            var set = new SkillSet(new[] { Cooking });
            Assert.AreEqual(1, set.LevelOf(NamespacedId.Parse("coolmod:brewing")));
        }

        [Test]
        public void SetLevel_ClampsToXpCurveRange()
        {
            var set = new SkillSet(new[] { Cooking });

            set.SetLevel(Cooking.Id, 0);
            Assert.AreEqual(1, set.LevelOf(Cooking.Id));

            set.SetLevel(Cooking.Id, 999);
            Assert.AreEqual(XpCurve.MaxLevel, set.LevelOf(Cooking.Id));
        }
    }
}
