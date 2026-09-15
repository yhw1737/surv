using System.IO;
using System.Text.Json;
using Isle.Core.Ids;

namespace Isle.World.Generation
{
    /// <summary>One landmark type: which def it places (moddable) and how many (fixed — see <see cref="LandmarkDefs"/>).</summary>
    public readonly struct LandmarkDef
    {
        public readonly NamespacedId DefId;
        public readonly int Count;

        public LandmarkDef(NamespacedId defId, int count)
        {
            DefId = defId;
            Count = count;
        }
    }

    /// <summary>
    /// SYS-WORLD-01 §Island generation: "1 shipwreck, 2 ruins, 3 freshwater springs". The counts and
    /// the three landmark kinds are a fixed design constant, same status as the <see cref="Chunks.Biome"/>
    /// enum — not moddable. What each kind actually renders/interacts as *is* moddable content
    /// (Absolute rule 1), so that half comes from a definition file, not a literal ID in C#.
    /// </summary>
    public static class LandmarkDefs
    {
        static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

        public static LandmarkDef[] Load(string jsonPath)
        {
            var json = File.ReadAllText(jsonPath);
            var dto = JsonSerializer.Deserialize<Dto>(json, Options);
            return new[]
            {
                new LandmarkDef(NamespacedId.Parse(dto.Shipwreck), count: 1),
                new LandmarkDef(NamespacedId.Parse(dto.Ruins), count: 2),
                new LandmarkDef(NamespacedId.Parse(dto.Spring), count: 3)
            };
        }

        sealed class Dto
        {
            public string Shipwreck { get; set; }
            public string Ruins { get; set; }
            public string Spring { get; set; }
        }
    }
}
