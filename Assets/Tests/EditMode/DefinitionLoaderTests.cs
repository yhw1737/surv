using System.IO;
using Isle.Data;
using Isle.Modding.Defs;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>
    /// SYS-CORE-01 §Load pipeline steps 4–5, scoped to T-012: a single directory, no mods, no
    /// references. Cases 4/5/8 (multi-mod) and case's typo-suggestion half (T-013) are out of scope.
    /// </summary>
    public sealed class DefinitionLoaderTests
    {
        string _dir;

        [SetUp]
        public void SetUp() => _dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "isle-loader-tests-" + Path.GetRandomFileName())).FullName;

        [TearDown]
        public void TearDown() => Directory.Delete(_dir, recursive: true);

        void WriteJson(string fileName, string json) => File.WriteAllText(Path.Combine(_dir, fileName), json);

        // --- Basic pipeline --------------------------------------------------------------------

        [Test]
        public void LoadAll_MissingDirectory_ReturnsEmpty()
        {
            var result = DefinitionLoader.LoadAll<ItemDef>(Path.Combine(_dir, "does_not_exist"));
            Assert.IsEmpty(result.Definitions);
            Assert.IsEmpty(result.Errors);
        }

        [Test]
        public void LoadAll_ValidItem_RoundTrips()
        {
            WriteJson("raw_meat.json",
                "{ \"id\": \"isle:raw_meat\", \"name\": \"@item.raw_meat\", \"tags\": [\"meat\"], " +
                "\"grid\": {\"w\":1,\"h\":1}, \"weight\": 1.0, \"stack\": 20 }");

            var result = DefinitionLoader.LoadAll<ItemDef>(_dir);

            Assert.IsEmpty(result.Errors);
            Assert.AreEqual(1, result.Definitions.Count);
            var def = result.Definitions[0];
            Assert.AreEqual("isle:raw_meat", def.Id.Value);
            Assert.AreEqual("@item.raw_meat", def.Name);
            Assert.AreEqual(1, def.Grid.W);
            Assert.AreEqual(20, def.Stack);
        }

        [Test]
        public void LoadAll_MalformedJson_ReportsErrorAndSkipsFile()
        {
            WriteJson("broken.json", "{ this is not json");

            var result = DefinitionLoader.LoadAll<ItemDef>(_dir);

            Assert.IsEmpty(result.Definitions);
            Assert.AreEqual(1, result.Errors.Count);
        }

        // --- Verification case 1 (SYS-CORE-01, via NamespacedIdJsonConverter) ------------------

        [Test]
        public void LoadAll_UnnamespacedId_FailsWithNamespaceMessage()
        {
            WriteJson("bad_id.json", "{ \"id\": \"raw_meat\", \"name\": \"@item.raw_meat\" }");

            var result = DefinitionLoader.LoadAll<ItemDef>(_dir);

            Assert.IsEmpty(result.Definitions);
            Assert.AreEqual(1, result.Errors.Count);
            StringAssert.Contains("Missing namespace", result.Errors[0].Message);
        }

        // --- Verification case 7: missing required field ----------------------------------------

        [Test]
        public void LoadAll_MissingName_ReportsError()
        {
            WriteJson("no_name.json", "{ \"id\": \"isle:raw_meat\" }");

            var result = DefinitionLoader.LoadAll<ItemDef>(_dir);

            Assert.IsEmpty(result.Definitions);
            StringAssert.Contains("\"name\"", result.Errors[0].Message);
        }

        // --- Verification case 6: unknown field is a warning, not an error ---------------------

        [Test]
        public void LoadAll_UnknownField_WarnsButStillLoads()
        {
            WriteJson("typo.json", "{ \"id\": \"isle:raw_meat\", \"name\": \"@item.raw_meat\", \"wieght\": 1.0 }");

            var result = DefinitionLoader.LoadAll<ItemDef>(_dir);

            Assert.AreEqual(1, result.Definitions.Count);
            Assert.IsEmpty(result.Errors);
            Assert.AreEqual(1, result.Warnings.Count);
            StringAssert.Contains("wieght", result.Warnings[0].Message);
        }

        // --- Dictionary key support (FishDef.BaitAffinity) --------------------------------------

        [Test]
        public void LoadAll_NamespacedIdDictionaryKey_RoundTrips()
        {
            WriteJson("fish.json",
                "{ \"id\": \"isle:lanternfish\", \"name\": \"@fish.lanternfish\", " +
                "\"bait_affinity\": { \"isle:worm\": 1.5 } }");

            var result = DefinitionLoader.LoadAll<FishDef>(_dir);

            Assert.IsEmpty(result.Errors);
            var affinity = result.Definitions[0].BaitAffinity;
            Assert.AreEqual(1.5f, affinity[Isle.Core.Ids.NamespacedId.Parse("isle:worm")]);
        }

        // --- SchemaValidator: IngredientRef exactly-one-of item/tag -----------------------------

        [Test]
        public void LoadAll_IngredientWithBothItemAndTag_ReportsError()
        {
            WriteJson("recipe.json",
                "{ \"id\": \"isle:test_recipe\", \"name\": \"@recipe.test\", " +
                "\"ingredients\": [ { \"item\": \"isle:wood\", \"tag\": \"isle:fuel\", \"count\": 1 } ] }");

            var result = DefinitionLoader.LoadAll<CraftRecipeDef>(_dir);

            Assert.IsEmpty(result.Definitions);
            StringAssert.Contains("exactly one", result.Errors[0].Message);
        }

        [Test]
        public void LoadAll_IngredientWithNeitherItemNorTag_ReportsError()
        {
            WriteJson("recipe.json",
                "{ \"id\": \"isle:test_recipe\", \"name\": \"@recipe.test\", " +
                "\"ingredients\": [ { \"count\": 1 } ] }");

            var result = DefinitionLoader.LoadAll<CraftRecipeDef>(_dir);

            Assert.IsEmpty(result.Definitions);
            StringAssert.Contains("exactly one", result.Errors[0].Message);
        }

        [Test]
        public void LoadAll_IngredientWithOnlyTag_Loads()
        {
            WriteJson("recipe.json",
                "{ \"id\": \"isle:test_recipe\", \"name\": \"@recipe.test\", " +
                "\"ingredients\": [ { \"tag\": \"isle:fuel\", \"count\": 1 } ] }");

            var result = DefinitionLoader.LoadAll<CraftRecipeDef>(_dir);

            Assert.IsEmpty(result.Errors);
            Assert.AreEqual(1, result.Definitions.Count);
        }

        // --- SchemaValidator: length-2 numeric bands ---------------------------------------------

        [Test]
        public void LoadAll_HabitatDepthWrongLength_ReportsError()
        {
            WriteJson("fish.json",
                "{ \"id\": \"isle:test_fish\", \"name\": \"@fish.test\", " +
                "\"habitat\": { \"depth\": [1.0, 2.0, 3.0] } }");

            var result = DefinitionLoader.LoadAll<FishDef>(_dir);

            Assert.IsEmpty(result.Definitions);
            StringAssert.Contains("habitat.depth", result.Errors[0].Message);
        }

        // --- SchemaValidator: artifacts never touch combat skills (Absolute Rule 5) ------------

        [Test]
        public void LoadAll_ArtifactWithCombatSkill_ReportsError()
        {
            WriteJson("artifact.json",
                "{ \"id\": \"isle:test_artifact\", \"name\": \"@artifact.test\", \"combat_skill\": \"isle:melee\" }");

            var result = DefinitionLoader.LoadAll<ArtifactDef>(_dir);

            Assert.IsEmpty(result.Definitions);
            StringAssert.Contains("combat_skill", result.Errors[0].Message);
        }

        // --- SchemaValidator: EnchantEffect per-type required fields ----------------------------

        [Test]
        public void LoadAll_DamageMultEffectMissingValue_ReportsError()
        {
            WriteJson("enchant.json",
                "{ \"id\": \"isle:test_enchant\", \"name\": \"@enchant.test\", " +
                "\"effects\": [ { \"type\": \"damage_mult\" } ] }");

            var result = DefinitionLoader.LoadAll<EnchantDef>(_dir);

            Assert.IsEmpty(result.Definitions);
            StringAssert.Contains("damage_mult", result.Errors[0].Message);
        }

        [Test]
        public void LoadAll_ConditionalEffectMissingWhenAndDamageMult_ReportsBothErrors()
        {
            WriteJson("enchant.json",
                "{ \"id\": \"isle:test_enchant\", \"name\": \"@enchant.test\", " +
                "\"effects\": [ { \"type\": \"conditional\" } ] }");

            var result = DefinitionLoader.LoadAll<EnchantDef>(_dir);

            Assert.IsEmpty(result.Definitions);
            Assert.AreEqual(2, result.Errors.Count);
        }

        [Test]
        public void LoadAll_UnknownEffectType_LoadsWithoutError()
        {
            WriteJson("enchant.json",
                "{ \"id\": \"isle:test_enchant\", \"name\": \"@enchant.test\", " +
                "\"effects\": [ { \"type\": \"not_yet_decided\" } ] }");

            var result = DefinitionLoader.LoadAll<EnchantDef>(_dir);

            Assert.IsEmpty(result.Errors);
            Assert.AreEqual(1, result.Definitions.Count);
        }
    }
}
