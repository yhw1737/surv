using Isle.Core.Ids;
using Isle.Data;
using Isle.Gameplay.Crafting;
using Isle.Modding.Defs;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>T-151 playtest round 1: craft window tabs and search, craft time.</summary>
    public sealed class CraftWindowTests
    {
        [TestCase(4f, 0, 4f)]
        [TestCase(4f, 25, 3f)]
        [TestCase(4f, 50, 2f)]
        [TestCase(4f, 80, 2f)]   // capped at 50
        [TestCase(0f, 10, 0f)]   // no time = instant
        public void CraftTime_LevelHalvesAtFifty(float timeSec, int level, float expected) =>
            Assert.AreEqual(expected, CraftTimeCalculator.Seconds(timeSec, level), 1e-5f);

        [TestCase("hatchet", CraftCategories.Tool)]
        [TestCase("club", CraftCategories.Weapon)]
        [TestCase("apprentice_wand", CraftCategories.Weapon)]
        [TestCase("straw_backpack", CraftCategories.Gear)]
        [TestCase("crate_kit", CraftCategories.Build)]
        [TestCase("copper_ingot", CraftCategories.Material)]
        [TestCase("health_tonic", CraftCategories.Brew)]
        public void Category_ShippedItems_LandInTheirTab(string item, string expected)
        {
            DefRegistry.Clear();
            try
            {
                Assert.IsEmpty(DefinitionBootstrap.Load(System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, "definitions")));
                Assert.AreEqual(expected, CraftCategories.Of(DefRegistry.Get<ItemDef>(NamespacedId.Parse("isle:" + item))));
            }
            finally { DefRegistry.Clear(); }
        }

        [Test]
        public void Search_EveryWordMustMatch_IgnoringCase()
        {
            Assert.IsTrue(CraftCategories.MatchesSearch("", "Copper ingot", "isle:craft_copper_ingot"));
            Assert.IsTrue(CraftCategories.MatchesSearch("COPPER", "Copper ingot", "isle:craft_copper_ingot"));
            Assert.IsTrue(CraftCategories.MatchesSearch("구리", "구리 주괴", "isle:craft_copper_ingot"));
            Assert.IsTrue(CraftCategories.MatchesSearch("copper ingot", "Copper ingot", "x"));
            Assert.IsFalse(CraftCategories.MatchesSearch("copper sword", "Copper ingot", "isle:craft_copper_ingot"));
        }

        [Test]
        public void Tabs_StartWithAll() => Assert.AreEqual(CraftCategories.All, CraftCategories.Tabs[0]);
    }
}
