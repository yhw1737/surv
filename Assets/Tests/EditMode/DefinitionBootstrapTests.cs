using System.IO;
using Isle.Core.Ids;
using Isle.Data;
using Isle.Modding.Defs;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-CORE-01 §Load pipeline steps 4/5/7/9/10 and §Hot reload, wired together for
    /// Isle's own base content — no mods.</summary>
    public sealed class DefinitionBootstrapTests
    {
        string _root;

        [SetUp]
        public void SetUp() => _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "isle-bootstrap-tests-" + Path.GetRandomFileName())).FullName;

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(_root, recursive: true);
            DefRegistry.Clear();
        }

        void WriteItem(string fileName, string json)
        {
            var dir = Directory.CreateDirectory(Path.Combine(_root, "items"));
            File.WriteAllText(Path.Combine(dir.FullName, fileName), json);
        }

        [Test]
        public void Load_ValidContent_PopulatesRegistry()
        {
            WriteItem("raw_meat.json", "{ \"id\": \"isle:raw_meat\", \"name\": \"@item.raw_meat\" }");

            var errors = DefinitionBootstrap.Load(_root);

            Assert.IsEmpty(errors);
            Assert.AreEqual("isle:raw_meat", DefRegistry.Get<ItemDef>(NamespacedId.Parse("isle:raw_meat")).Id.Value);
        }

        [Test]
        public void Load_UnresolvedReference_ReportsError()
        {
            var recipeDir = Directory.CreateDirectory(Path.Combine(_root, "recipes"));
            File.WriteAllText(Path.Combine(recipeDir.FullName, "bad.json"),
                "{ \"id\": \"isle:bad_recipe\", \"name\": \"@recipe.bad\", " +
                "\"ingredients\": [{\"item\": \"isle:no_such_item\", \"count\": 1}] }");

            var errors = DefinitionBootstrap.Load(_root);

            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("isle:no_such_item", errors[0].Message);
        }

        [Test]
        public void Reload_ChangedFile_UpdatesSameReference()
        {
            WriteItem("raw_meat.json", "{ \"id\": \"isle:raw_meat\", \"name\": \"@item.raw_meat\" }");
            DefinitionBootstrap.Load(_root);
            var before = DefRegistry.Get<ItemDef>(NamespacedId.Parse("isle:raw_meat"));

            WriteItem("raw_meat.json", "{ \"id\": \"isle:raw_meat\", \"name\": \"@item.changed\" }");
            var errors = DefinitionBootstrap.Reload(_root);
            var after = DefRegistry.Get<ItemDef>(NamespacedId.Parse("isle:raw_meat"));

            Assert.IsEmpty(errors);
            Assert.AreSame(before, after);
            Assert.AreEqual("@item.changed", after.Name);
        }
    }
}
