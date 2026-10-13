using System.Linq;
using Isle.Core.Ids;
using Isle.Data;
using Isle.Gameplay.Cooking;
using Isle.Gameplay.Gathering;
using Isle.Modding.Defs;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>T-151 / SYS-FORAGE-01: berries and mushrooms, look-alike poisons, identification by Gathering level.</summary>
    public sealed class ForageTests
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
        static WorldObjectDef Node(string id) => DefRegistry.Get<WorldObjectDef>(NamespacedId.Parse("isle:" + id));

        [TestCase("sunberry", 9, true)]   // verification 1, 2: Lv 10 to tell
        [TestCase("sunberry", 10, false)]
        [TestCase("death_cap", 24, true)]
        [TestCase("death_cap", 25, false)]
        [TestCase("berries", 0, false)]   // the basic berry needs no skill
        public void Identification_ByGatheringLevel(string id, int level, bool hidden) =>
            Assert.AreEqual(!hidden, Identification.Known(Item(id), _ => level));

        [TestCase("sunberry", "duskberry", "sunberry_bush", "duskberry_bush")]
        [TestCase("frostberry", "ghostberry", "frostberry_bush", "ghostberry_bush")]
        [TestCase("brown_cap", "fools_cap", "brown_cap_patch", "fools_cap_patch")]
        [TestCase("honey_cap", "death_cap", "honey_cap_patch", "death_cap_patch")]
        public void Twins_LookTheSame_OneIsPoison(string safe, string poison, string safeNode, string poisonNode)
        {
            Assert.AreEqual(Item(safe).IconStyle.Color, Item(poison).IconStyle.Color, "icons differ");
            Assert.AreEqual(Item(safe).IconStyle.Shape, Item(poison).IconStyle.Shape);
            Assert.AreEqual(Item(safe).Identify.UnknownName, Item(poison).Identify.UnknownName, "unknown names differ");
            Assert.AreEqual(Item(safe).Identify.Level, Item(poison).Identify.Level);
            Assert.AreEqual(Node(safeNode).Name, Node(poisonNode).Name, "node names give it away");
            Assert.AreEqual(Node(safeNode).Visual.Color, Node(poisonNode).Visual.Color, "nodes look different");
            Assert.IsFalse(Item(safe).Tags.Any(t => t.StartsWith("toxin")), $"{safe} is poisonous");
            Assert.IsTrue(Item(poison).Tags.Any(t => t.StartsWith("toxin/")), $"{poison} isn't poisonous");
        }

        [Test]
        public void EveryMethod_ReactsToBothPoisons()
        {
            foreach (var method in DefRegistry.All<CookMethodDef>())
            {
                Assert.IsTrue(method.TagReactions.Any(r => r.When.SequenceEqual(new[] { "toxin/deadly" }) && r.GrantBuff.Value == "isle:toxin"), $"{method.Id}: no toxin reaction");
                Assert.IsTrue(method.TagReactions.Any(r => r.When.SequenceEqual(new[] { "toxin/nausea" }) && r.GrantBuff.Value == "isle:nausea"), $"{method.Id}: no nausea reaction");
            }
        }

        [Test]
        public void DeathCap_EatenRaw_Poisons() // verification 3
        {
            var raw = DefRegistry.All<CookMethodDef>().Single(m => m.EatRaw);
            var result = CookingResolver.Resolve(raw, new[] { Item("death_cap") }, 1, 1f);
            Assert.IsTrue(result.Buffs.Any(b => b.Buff.Value == "isle:toxin"));
        }

        [Test]
        public void FoolsCap_InAStew_StillNauseates() // verification 4
        {
            var stew = DefRegistry.Get<CookMethodDef>(NamespacedId.Parse("isle:stew"));
            var result = CookingResolver.Resolve(stew, new[] { Item("fools_cap"), Item("raw_meat"), Item("wild_grain") }, 30, 1f);
            Assert.IsTrue(result.Buffs.Any(b => b.Buff.Value == "isle:nausea"), "cooking made a fool's cap safe");
        }

        [TestCase("toxin")]
        [TestCase("nausea")]
        [TestCase("food_poisoning")]
        public void Poisons_HaveFixedDuration(string buff) =>
            Assert.IsTrue(DefRegistry.Get<BuffDef>(NamespacedId.Parse("isle:" + buff)).FixedDuration);

        [Test]
        public void BasicBerries_PoorButSafe_BushesRarerAndRicher()
        {
            var berries = Item("berries");
            Assert.AreEqual(3f, berries.Nutrition.Hunger);
            Assert.AreEqual(1f, berries.Nutrition.Thirst);
            Assert.IsNull(berries.Identify);
            var bush = Node("berry_bush");
            Assert.AreEqual(0.003f, bush.Spawn.Density, 1e-6f);
            Assert.AreEqual(8, bush.Gather.Count);
        }
    }
}
