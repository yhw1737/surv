using Isle.Core.Ids;
using Isle.Data;
using Isle.Gameplay.Crafting;
using Isle.Modding.Defs;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>T-092 SYS-CRAFT-01 §Adjacent assist: the effective level a nearby ally lends, and the spec's worked example.</summary>
    public sealed class AdjacentAssistTests
    {
        [TestCase(20, 40, 32f)]
        [TestCase(20, 50, 38f)]
        [TestCase(30, 50, 42f)]
        [TestCase(20, 10, 20f)] // a weaker ally never lowers you
        [TestCase(20, 0, 20f)]  // nobody near
        public void EffectiveLevel_SpecFormula_Matches(int own, int nearby, float expected) =>
            Assert.AreEqual(expected, AdjacentAssist.EffectiveLevel(own, nearby), 1e-4f);

        [Test]
        public void Meets_Crafting20OnLv40Recipe_StillShortEvenWithLv50Ally()
        {
            Assert.IsFalse(AdjacentAssist.Meets(20, 40, 40, allowed: true), "a Lv 40 neighbour gives 32");
            Assert.IsFalse(AdjacentAssist.Meets(20, 50, 40, allowed: true), "a Lv 50 neighbour gives 38");
        }

        [Test]
        public void Meets_Crafting30WithLv50Ally_ReachesLv40()
        {
            Assert.IsTrue(AdjacentAssist.Meets(30, 50, 40, allowed: true), "30 + 20×0.6 = 42");
            Assert.IsFalse(AdjacentAssist.Meets(30, 0, 40, allowed: true), "alone it's 30");
        }

        [Test]
        public void Meets_RecipeDisallowsAssist_UsesOwnLevel() =>
            Assert.IsFalse(AdjacentAssist.Meets(30, 50, 40, allowed: false));

        [Test]
        public void Meets_ExactThreshold_Passes() =>
            Assert.IsTrue(AdjacentAssist.Meets(20, 40, 32, allowed: true));

        [Test]
        public void Recipe_WithoutTheField_AllowsAssist()
        {
            DefRegistry.Clear();
            try
            {
                Assert.IsEmpty(DefinitionBootstrap.Load(System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, "definitions")));
                Assert.IsTrue(DefRegistry.Get<CraftRecipeDef>(NamespacedId.Parse("isle:craft_knife")).AllowAdjacentAssist,
                    "SYS-CRAFT-01: assist is on unless a recipe sets allow_adjacent_assist: false");
            }
            finally { DefRegistry.Clear(); }
        }
    }
}
