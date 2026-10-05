using System.Collections.Generic;
using Isle.Core.Ids;
using Isle.Data;
using Isle.Gameplay.Fishing;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-FISH-01 §Species selection, verification 1–3, plus roulette selection.
    /// Band mismatch factors (depth, temperature) aren't specced — the 0.15 used here is an invention
    /// and is tested as such.</summary>
    public sealed class FishSelectorTests
    {
        const float Tolerance = 0.0001f;

        static FishDef Fish(string id, string[] time = null, int minSkill = 0, string[] terrain = null, string[] rigs = null) => new FishDef
        {
            Id = NamespacedId.Parse(id),
            Habitat = new HabitatSpec
            {
                Depth = new[] { 0f, 20f },
                WaterTemp = new[] { 0f, 30f },
                Terrain = terrain ?? new[] { "saltwater" },
                Time = time ?? new[] { "any" },
                Weather = new[] { "any" },
            },
            RigAllowed = rigs ?? new[] { "handline" },
            MinSkill = minSkill,
        };

        static FishingConditions Conditions(string time = "day", int level = 0, string terrain = "saltwater") =>
            new FishingConditions(depth: 10f, waterTemp: 15f, terrain: terrain, time: time, fishingLevel: level);

        [Test]
        public void Weight_NoBait_AllCandidatesGetSameAffinity()
        {
            var a = Fish("isle:mackerel");
            var b = Fish("isle:sardine");
            Assert.AreEqual(FishSelector.Weight(a, Conditions(), "handline"), FishSelector.Weight(b, Conditions(), "handline"), Tolerance);
        }

        [Test]
        public void Weight_NightOnlySpeciesInDaylight_IsQuartered()
        {
            var nightFish = Fish("isle:lanternfish", time: new[] { "night" });
            var anyFish = Fish("isle:mackerel");
            var ratio = FishSelector.Weight(nightFish, Conditions("day"), "handline") / FishSelector.Weight(anyFish, Conditions("day"), "handline");
            Assert.AreEqual(0.25f, ratio, Tolerance);
        }

        [Test]
        public void Weight_BelowMinSkill_IsFivePercent()
        {
            var expert = Fish("isle:snapper", minSkill: 28);
            var anyFish = Fish("isle:mackerel");
            var ratio = FishSelector.Weight(expert, Conditions(level: 10), "handline") / FishSelector.Weight(anyFish, Conditions(level: 10), "handline");
            Assert.AreEqual(0.05f, ratio, Tolerance);
        }

        [Test]
        public void Weight_WrongTerrain_IsFifteenPercent()
        {
            var freshwater = Fish("isle:trout", terrain: new[] { "freshwater" });
            var anyFish = Fish("isle:mackerel");
            var ratio = FishSelector.Weight(freshwater, Conditions(terrain: "saltwater"), "handline") / FishSelector.Weight(anyFish, Conditions(), "handline");
            Assert.AreEqual(0.15f, ratio, Tolerance);
        }

        [Test]
        public void Weight_RigNotAllowed_IsZero()
        {
            var rodOnly = Fish("isle:pike", rigs: new[] { "rod" });
            Assert.AreEqual(0f, FishSelector.Weight(rodOnly, Conditions(), "handline"), Tolerance);
        }

        [Test]
        public void Select_RollAtZero_PicksFirstCandidate()
        {
            var candidates = new List<FishDef> { Fish("isle:mackerel"), Fish("isle:sardine") };
            Assert.AreEqual("isle:mackerel", FishSelector.Select(candidates, Conditions(), "handline", roll01: 0f).Id.Value);
        }

        [Test]
        public void Select_RollNearOne_PicksLastCandidate()
        {
            var candidates = new List<FishDef> { Fish("isle:mackerel"), Fish("isle:sardine") };
            Assert.AreEqual("isle:sardine", FishSelector.Select(candidates, Conditions(), "handline", roll01: 0.999f).Id.Value);
        }

        [Test]
        public void Select_NoCandidateAllowsRig_ReturnsNull()
        {
            var rodOnly = Fish("isle:pike", rigs: new[] { "rod" });
            Assert.IsNull(FishSelector.Select(new List<FishDef> { rodOnly }, Conditions(), "handline", roll01: 0.5f));
        }
    }
}
