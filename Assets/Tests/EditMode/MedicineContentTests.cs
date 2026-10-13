using System.Linq;
using Isle.Core.Ids;
using Isle.Data;
using Isle.Gameplay.Crafting;
using Isle.Modding.Defs;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-MED-01 content: herbs and their poisonous look-alikes, medicine grades, the apothecary.</summary>
    public sealed class MedicineContentTests
    {
        [SetUp]
        public void Load()
        {
            DefRegistry.Clear();
            Assert.IsEmpty(DefinitionBootstrap.Load(System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, "definitions")));
        }

        [TearDown]
        public void Unload() => DefRegistry.Clear();

        static ItemDef Item(string id) => DefRegistry.Get<ItemDef>(NamespacedId.Parse("isle:" + id));

        [TestCase("healing_herb", "hemlock", "isle:toxin")]
        [TestCase("stanchweed", "thornweed", "isle:nausea")]
        public void HerbTwins_LookAlike_PoisonTendsAtZero(string herb, string poison, string harm)
        {
            Assert.AreEqual(Item(herb).IconStyle.Color, Item(poison).IconStyle.Color);
            Assert.AreEqual(Item(herb).Identify.UnknownName, Item(poison).Identify.UnknownName);
            Assert.AreEqual("isle:gathering", Item(herb).Identify.Skill.Value, "telling herbs apart is Gathering's");
            Assert.Greater(Item(herb).Medicine.Quality, 0f);
            Assert.AreEqual(0f, Item(poison).Medicine.Quality);
            Assert.AreEqual(harm, Item(poison).Medicine.HarmBuff.Value);
        }

        [Test]
        public void Medicine_GradesClimb_MadeAtTheApothecary()
        {
            var grades = new[] { "healing_herb", "herbal_poultice", "herbal_medicine", "refined_medicine" }.Select(id => Item(id).Medicine.Quality).ToArray();
            for (var i = 1; i < grades.Length; i++) Assert.Greater(grades[i], grades[i - 1], "a higher grade must tend better");
            foreach (var id in new[] { "herbal_poultice", "stanch_bandage", "herbal_medicine", "refined_medicine" })
            {
                var recipe = DefRegistry.All<CraftRecipeDef>().Single(r => r.Output.Item.Value == "isle:" + id);
                Assert.AreEqual("isle:apothecary_table", recipe.Station.Value, $"{id} isn't made at the apothecary");
                Assert.AreEqual(CraftCategories.Medical, CraftCategories.Of(Item(id)));
                Assert.AreEqual("isle:medicine", recipe.Skills.Single(s => s.Primary).Skill.Value, $"{id} isn't a Medicine recipe");
                Assert.AreEqual("isle:medicine", Item(id).Medicine.Skill.Value);
            }
            Assert.IsTrue(Item("splint").Medicine.Splint);
            Assert.IsNotNull(DefRegistry.Get<SkillDef>(NamespacedId.Parse("isle:medicine")));
        }
    }
}
