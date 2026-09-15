using System.IO;
using Isle.World.Generation;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-WORLD-01 §Island generation (T-036): counts are fixed, def ids come from a file.</summary>
    public sealed class LandmarkDefsTests
    {
        [Test]
        public void Load_ParsesCountsAndDefIdsFromFile()
        {
            var path = Path.Combine(Path.GetTempPath(), $"landmarks_{System.Guid.NewGuid():N}.json");
            File.WriteAllText(path, "{ \"shipwreck\": \"isle:shipwreck\", \"ruins\": \"isle:ruins\", \"spring\": \"isle:freshwater_spring\" }");

            try
            {
                var defs = LandmarkDefs.Load(path);

                Assert.AreEqual(3, defs.Length);
                Assert.AreEqual("isle:shipwreck", defs[0].DefId.Value);
                Assert.AreEqual(1, defs[0].Count);
                Assert.AreEqual("isle:ruins", defs[1].DefId.Value);
                Assert.AreEqual(2, defs[1].Count);
                Assert.AreEqual("isle:freshwater_spring", defs[2].DefId.Value);
                Assert.AreEqual(3, defs[2].Count);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
