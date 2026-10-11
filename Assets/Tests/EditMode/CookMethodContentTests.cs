using System.Linq;
using Isle.Core.Ids;
using Isle.Data;
using Isle.Gameplay.Cooking;
using Isle.Modding.Defs;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>T-102 SYS-COOK-01: the eight Core methods as shipped — unlocks, inputs, modifiers and time verbatim from
    /// the spec, stew and ferment's buff rules on real ingredients, every station buildable.</summary>
    public sealed class CookMethodContentTests
    {
        [SetUp]
        public void Load()
        {
            DefRegistry.Clear();
            Assert.IsEmpty(DefinitionBootstrap.Load(System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, "definitions")));
        }

        [TearDown]
        public void Unload() => DefRegistry.Clear();

        static CookMethodDef Method(string id) => DefRegistry.Get<CookMethodDef>(NamespacedId.Parse("isle:" + id));
        static ItemDef Item(string id) => DefRegistry.Get<ItemDef>(NamespacedId.Parse("isle:" + id));

        [TestCase("raw", 1, 1, 1, 0.60f, 1.00f, 0.30f, 0.0f, 0.0f, 1.00f)]
        [TestCase("grill", 1, 1, 2, 1.30f, 0.70f, 0.60f, 0.8f, 1.0f, 0.85f)]
        [TestCase("boil", 4, 1, 3, 1.00f, 1.80f, 0.50f, 0.9f, 1.0f, 0.90f)]
        [TestCase("porridge", 8, 2, 3, 1.00f, 1.40f, 0.40f, 1.0f, 1.2f, 0.95f)]
        [TestCase("dry", 12, 1, 4, 0.70f, 0.30f, 4.00f, 0.5f, 1.0f, 0.70f)]
        [TestCase("stew", 20, 3, 5, 1.35f, 1.30f, 0.80f, 1.3f, 1.4f, 0.95f)]
        [TestCase("smoke", 28, 1, 4, 1.10f, 0.60f, 3.00f, 1.1f, 1.6f, 0.85f)]
        [TestCase("ferment", 40, 1, 3, 1.00f, 1.00f, 2.00f, 1.5f, 1.8f, 1.10f)]
        public void Method_AsShipped_MatchesSpecTables(string id, int level, int min, int max,
            float hunger, float thirst, float preservation, float buffPower, float buffDuration, float retention)
        {
            var m = Method(id);
            Assert.AreEqual(level, m.UnlockSkill?.Level ?? 1, "unlock level");
            Assert.AreEqual(min, m.Input.MinItems, "min items");
            Assert.AreEqual(max, m.Input.MaxItems, "max items");
            Assert.AreEqual(hunger, m.Modifiers.Hunger, 1e-4f);
            Assert.AreEqual(thirst, m.Modifiers.Thirst, 1e-4f);
            Assert.AreEqual(preservation, m.Modifiers.Preservation, 1e-4f);
            Assert.AreEqual(buffPower, m.Modifiers.BuffPower, 1e-4f);
            Assert.AreEqual(buffDuration, m.Modifiers.BuffDuration, 1e-4f);
            Assert.AreEqual(retention, m.Modifiers.NutritionRetention, 1e-4f);
        }

        [TestCase("grill", 1f)]
        [TestCase("boil", 1.5f)]
        [TestCase("stew", 2f)]
        [TestCase("dry", 3f)]
        [TestCase("smoke", 3f)]
        [TestCase("raw", 0f)]
        public void Method_TimeMult_MatchesSpec(string id, float expected) => Assert.AreEqual(expected, Method(id).TimeMult, 1e-4f);

        [Test]
        public void Methods_Count_IsEightAndEachStationBuildable()
        {
            var methods = DefRegistry.All<CookMethodDef>();
            Assert.AreEqual(8, methods.Count);
            foreach (var m in methods.Where(m => m.Station.IsValid))
            {
                Assert.IsTrue(DefRegistry.TryGet<WorldObjectDef>(m.Station, out _), $"{m.Id}: station {m.Station} isn't a world object");
                if (m.Station.Value == "isle:campfire") continue; // the campfire comes from its own spawner and kit
                var kit = DefRegistry.All<ItemDef>().FirstOrDefault(i => i.Places == m.Station);
                Assert.IsNotNull(kit, $"{m.Id}: nothing places {m.Station}");
                Assert.IsTrue(DefRegistry.All<CraftRecipeDef>().Any(r => r.Output.Item == kit.Id), $"{m.Id}: no recipe makes {kit.Id}");
            }
        }

        [Test]
        public void Stew_MeatVegetableGrain_GrantsTwoBuffs() // SYS-COOK-01 verification 3
        {
            var result = CookingResolver.Resolve(Method("stew"), new[] { Item("raw_meat"), Item("wild_onion"), Item("wild_grain") }, 20, 1f);
            Assert.AreEqual(2, result.Buffs.Count);
        }

        [Test]
        public void Stew_MeatVegetable_GrantsOneBuff() // SYS-COOK-01 verification 4
        {
            var result = CookingResolver.Resolve(Method("stew"), new[] { Item("raw_meat"), Item("wild_onion"), Item("raw_meat") }, 20, 1f);
            Assert.AreEqual(1, result.Buffs.Count);
        }

        [Test]
        public void Ferment_TwoKinds_GrantsTwoBuffsWithoutThreeGroups()
        {
            var result = CookingResolver.Resolve(Method("ferment"), new[] { Item("wild_onion"), Item("berries") }, 40, 1f);
            Assert.AreEqual(2, result.Buffs.Count);
        }

        [Test]
        public void Porridge_Grain_GrantsABuff()
        {
            var result = CookingResolver.Resolve(Method("porridge"), new[] { Item("wild_grain"), Item("berries") }, 8, 1f);
            Assert.AreEqual(1, result.Buffs.Count);
        }

        [Test]
        public void EveryFood_EveryMethod_Resolves() // SYS-COOK-01 §Extensibility gate, for what ships today
        {
            var foods = DefRegistry.All<ItemDef>().Where(i => i.Nutrition != null).ToList();
            foreach (var method in DefRegistry.All<CookMethodDef>())
                foreach (var food in foods)
                {
                    var count = method.Input?.MinItems ?? 1;
                    var result = CookingResolver.Resolve(method, Enumerable.Repeat(food, count).ToList(), 50, 1f);
                    Assert.GreaterOrEqual(result.Hunger, 0f, $"{method.Id} × {food.Id}");
                    Assert.LessOrEqual(result.Buffs.Count, method.MaxBuffs, $"{method.Id} × {food.Id}");
                }
        }

        [Test]
        public void SpoiledIngredient_EveryMethod_Poisons() // SYS-COOK-01 verification 9, across methods
        {
            foreach (var method in DefRegistry.All<CookMethodDef>().Where(m => m.Id.Value != "isle:raw"))
            {
                var count = method.Input?.MinItems ?? 1;
                var items = Enumerable.Repeat(Item("rotten_food"), 1).Concat(Enumerable.Repeat(Item("raw_meat"), count - 1)).ToList();
                var result = CookingResolver.Resolve(method, items, 1, 1f);
                Assert.IsTrue(result.Buffs.Any(b => b.Buff.Value == "isle:food_poisoning"), $"{method.Id} doesn't poison");
            }
        }
    }
}
