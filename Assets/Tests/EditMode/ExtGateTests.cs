using System.IO;
using System.Linq;
using Isle.Core.Ids;
using Isle.Data;
using Isle.Gameplay.Cooking;
using Isle.Modding.Defs;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>
    /// T-105 🚩 Ext gate (SYS-COOK-01 §Extensibility gate): one new ingredient, added as a single JSON file next to the
    /// shipped definitions — no C# — is accepted by the loader and handled by all eight cook methods: it resolves, its
    /// tags fire the methods' reactions, it makes a named dish that rebuilds from its save key, and it mixes with
    /// shipped ingredients. The content is a temp copy of StreamingAssets, so the project itself is untouched.
    /// </summary>
    public sealed class ExtGateTests
    {
        const string KelpId = "isle:ext_gate_sea_kelp";

        // A modder's file: existing tags (vegetable, high_water) plus one nothing knows about (seaweed).
        const string KelpJson = @"{
  ""id"": ""isle:ext_gate_sea_kelp"",
  ""name"": ""@item.ext_gate_sea_kelp"",
  ""tags"": [""food"", ""vegetable"", ""high_water"", ""seaweed"", ""raw""],
  ""grid"": { ""w"": 1, ""h"": 1 },
  ""weight"": 0.2,
  ""stack"": 20,
  ""nutrition"": { ""hunger"": 4, ""thirst"": 6, ""sanitation_risk"": 0.0 },
  ""spoilage"": { ""base_hours"": 36, ""temp_factor"": 0, ""result"": ""isle:rotten_food"" },
  ""icon_style"": { ""shape"": ""berries"", ""color"": ""#3C6E3A"", ""accent"": ""#7FB070"" }
}";

        string _root;

        [SetUp]
        public void Load()
        {
            _root = Path.Combine(Path.GetTempPath(), "isle_ext_gate_" + System.Guid.NewGuid().ToString("N"));
            Copy(Path.Combine(UnityEngine.Application.streamingAssetsPath, "definitions"), _root);
            File.WriteAllText(Path.Combine(_root, "items", "ext_gate_sea_kelp.json"), KelpJson);
            DefRegistry.Clear();
            DishFactory.Clear();
            Assert.IsEmpty(DefinitionBootstrap.Load(_root), "the loader rejected the new ingredient");
        }

        [TearDown]
        public void Unload()
        {
            DefRegistry.Clear();
            DishFactory.Clear();
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        static void Copy(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (var file in Directory.GetFiles(from, "*.json")) File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
            foreach (var dir in Directory.GetDirectories(from)) Copy(dir, Path.Combine(to, Path.GetFileName(dir)));
        }

        static ItemDef Kelp => DefRegistry.Get<ItemDef>(NamespacedId.Parse(KelpId));
        static ItemDef Item(string id) => DefRegistry.Get<ItemDef>(NamespacedId.Parse("isle:" + id));
        static CookMethodDef Method(string id) => DefRegistry.Get<CookMethodDef>(NamespacedId.Parse("isle:" + id));

        [Test]
        public void NewIngredient_AllEightMethods_MakeANamedDishThatRebuilds()
        {
            var methods = DefRegistry.All<CookMethodDef>();
            Assert.AreEqual(8, methods.Count, "the gate is about the eight Core methods");
            foreach (var method in methods)
            {
                var count = method.Input?.MinItems ?? 1;
                var ingredients = Enumerable.Repeat(Kelp, count).ToList();
                Assert.IsTrue(CookingResolver.CountAllowed(method, count), $"{method.Id}: count");
                var result = CookingResolver.Resolve(method, ingredients, 20, 1f);
                Assert.Greater(result.Hunger, 0f, $"{method.Id}: no hunger from the new ingredient");
                Assert.IsFalse(result.Failed, $"{method.Id}: failed at a roll of 1");

                var dish = DishFactory.Create(method, ingredients, result);
                Assert.IsTrue(dish.Name.Contains(Kelp.Name), $"{method.Id}: dish isn't named after the new ingredient ({dish.Name})");
                Assert.IsTrue(dish.Tags.Contains("vegetable") && dish.Tags.Contains("seaweed"), $"{method.Id}: the dish lost the ingredient's tags");
                Assert.GreaterOrEqual(CookTimeCalculator.Seconds(count, method.TimeMult, 20), 0f, $"{method.Id}: cook time");

                var key = DishFactory.KeyOf(dish);
                DishFactory.Clear();
                var rebuilt = DishFactory.Rebuild(key, id => DefRegistry.TryGet<CookMethodDef>(id, out var m) ? m : null,
                    id => DefRegistry.TryGet<ItemDef>(id, out var i) ? i : null);
                Assert.IsNotNull(rebuilt, $"{method.Id}: a dish of the new ingredient doesn't come back from a save");
            }
        }

        [TestCase("stew", "isle:endurance")]
        [TestCase("ferment", "isle:iron_gut")]
        public void NewIngredient_ExistingTag_FiresTheMethodsReaction(string method, string buff)
        {
            var m = Method(method);
            var result = CookingResolver.Resolve(m, Enumerable.Repeat(Kelp, m.Input.MinItems).ToList(), 40, 1f);
            Assert.IsTrue(result.Buffs.Any(b => b.Buff.Value == buff), $"{method}: the vegetable tag didn't fire {buff}");
        }

        [Test]
        public void NewIngredient_InAThreeGroupStew_CountsAsAGroup()
        {
            var result = CookingResolver.Resolve(Method("stew"), new[] { Kelp, Item("raw_meat"), Item("wild_grain") }, 20, 1f);
            Assert.AreEqual(2, result.Buffs.Count, "kelp + meat + grain is three groups → two buffs");
        }

        [Test]
        public void NewIngredient_Boiled_GetsTheMethodsThirstModifier()
        {
            var result = CookingResolver.Resolve(Method("boil"), new[] { Kelp }, 1, 1f);
            Assert.AreEqual(Kelp.Nutrition.Thirst * 1.8f, result.Thirst, 1e-3f);
        }
    }
}
