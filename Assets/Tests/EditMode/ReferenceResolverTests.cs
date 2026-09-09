using System.IO;
using Isle.Core.Ids;
using Isle.Data;
using Isle.Modding.Defs;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>
    /// SYS-CORE-01 §Load pipeline step 7, scoped to T-013: item references only (see
    /// <see cref="ReferenceResolver"/>'s doc comment for why). Cases 5/8 (multi-mod) are T-130.
    /// </summary>
    public sealed class ReferenceResolverTests
    {
        string _dir;

        [SetUp]
        public void SetUp() => _dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "isle-resolver-tests-" + Path.GetRandomFileName())).FullName;

        [TearDown]
        public void TearDown() => Directory.Delete(_dir, recursive: true);

        void WriteJson(string fileName, string json) => File.WriteAllText(Path.Combine(_dir, fileName), json);

        // --- Verification case 4: unresolved reference + suggestion ----------------------------

        [Test]
        public void ResolveItemRefs_UnknownIngredient_ReportsErrorWithSuggestion()
        {
            WriteJson("harpoon.json",
                "{ \"id\": \"isle:harpoon\", \"name\": \"@recipe.harpoon\",\n" +
                "  \"ingredients\": [ { \"item\": \"isle:steel_ingott\", \"count\": 1 } ] }");
            var recipes = DefinitionLoader.LoadAll<CraftRecipeDef>(_dir);

            var validItems = new LoadResult<ItemDef>();
            validItems.Definitions.Add(new ItemDef { Id = NamespacedId.Parse("isle:steel_ingot"), Name = "@item.steel_ingot" });
            validItems.SourcePaths.Add("<fixture>");

            ReferenceResolver.ResolveItemRefs(validItems, recipes: recipes);

            Assert.AreEqual(1, recipes.Errors.Count);
            var error = recipes.Errors[0];
            StringAssert.Contains("isle:steel_ingott", error.Message);
            Assert.AreEqual("isle:steel_ingot", error.Suggestion);
            Assert.AreEqual(2, error.Line); // ingredients line, second line of the file
        }

        [Test]
        public void ResolveItemRefs_KnownIngredient_NoError()
        {
            WriteJson("harpoon.json",
                "{ \"id\": \"isle:harpoon\", \"name\": \"@recipe.harpoon\", " +
                "\"ingredients\": [ { \"item\": \"isle:steel_ingot\", \"count\": 1 } ] }");
            var recipes = DefinitionLoader.LoadAll<CraftRecipeDef>(_dir);

            var items = new LoadResult<ItemDef>();
            items.Definitions.Add(new ItemDef { Id = NamespacedId.Parse("isle:steel_ingot"), Name = "@item.steel_ingot" });
            items.SourcePaths.Add("<fixture>");

            ReferenceResolver.ResolveItemRefs(items, recipes: recipes);

            Assert.IsEmpty(recipes.Errors);
        }

        [Test]
        public void ResolveItemRefs_TagOnlyIngredient_NotChecked()
        {
            WriteJson("recipe.json",
                "{ \"id\": \"isle:test\", \"name\": \"@recipe.test\", " +
                "\"ingredients\": [ { \"tag\": \"isle:fuel\", \"count\": 1 } ] }");
            var recipes = DefinitionLoader.LoadAll<CraftRecipeDef>(_dir);
            var items = new LoadResult<ItemDef>();

            ReferenceResolver.ResolveItemRefs(items, recipes: recipes);

            Assert.IsEmpty(recipes.Errors);
        }

        [Test]
        public void ResolveItemRefs_NoCloseCandidate_ErrorWithoutSuggestion()
        {
            WriteJson("recipe.json",
                "{ \"id\": \"isle:test\", \"name\": \"@recipe.test\", " +
                "\"ingredients\": [ { \"item\": \"isle:completely_unrelated_id\", \"count\": 1 } ] }");
            var recipes = DefinitionLoader.LoadAll<CraftRecipeDef>(_dir);

            var items = new LoadResult<ItemDef>();
            items.Definitions.Add(new ItemDef { Id = NamespacedId.Parse("isle:steel_ingot"), Name = "@item.steel_ingot" });
            items.SourcePaths.Add("<fixture>");

            ReferenceResolver.ResolveItemRefs(items, recipes: recipes);

            Assert.AreEqual(1, recipes.Errors.Count);
            Assert.IsNull(recipes.Errors[0].Suggestion);
        }

        [Test]
        public void ResolveItemRefs_BaitAffinityUnknownKey_ReportsError()
        {
            WriteJson("fish.json",
                "{ \"id\": \"isle:lanternfish\", \"name\": \"@fish.lanternfish\", " +
                "\"bait_affinity\": { \"isle:wormy\": 1.5 } }");
            var fish = DefinitionLoader.LoadAll<FishDef>(_dir);

            var items = new LoadResult<ItemDef>();
            items.Definitions.Add(new ItemDef { Id = NamespacedId.Parse("isle:worm"), Name = "@item.worm" });
            items.SourcePaths.Add("<fixture>");

            ReferenceResolver.ResolveItemRefs(items, fish: fish);

            Assert.AreEqual(1, fish.Errors.Count);
            StringAssert.Contains("bait item", fish.Errors[0].Message);
            Assert.AreEqual("isle:worm", fish.Errors[0].Suggestion);
        }

        [Test]
        public void ResolveItemRefs_SpoilageResultUnknown_ReportsError()
        {
            WriteJson("meat.json",
                "{ \"id\": \"isle:raw_meat\", \"name\": \"@item.raw_meat\", " +
                "\"spoilage\": { \"base_hours\": 10, \"temp_factor\": 1.0, \"result\": \"isle:rotten_meet\" } }");
            var items = DefinitionLoader.LoadAll<ItemDef>(_dir);
            Assert.IsEmpty(items.Errors);

            ReferenceResolver.ResolveItemRefs(items);

            Assert.AreEqual(1, items.Errors.Count);
            StringAssert.Contains("isle:rotten_meet", items.Errors[0].Message);
        }
    }
}
