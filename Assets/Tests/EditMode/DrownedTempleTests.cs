using System.Linq;
using Isle.Core.Ids;
using Isle.Data;
using Isle.Gameplay.Cooking;
using Isle.Modding.Defs;
using Isle.World.Generation;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>T-203 Drowned Temple: the antidote comes from boiling a bitter herb (by its tag), and poison fog takes
    /// its own share of rooms, independent of the tides'.</summary>
    public sealed class DrownedTempleTests
    {
        [SetUp]
        public void SetUp()
        {
            DefRegistry.Clear();
            Assert.IsEmpty(DefinitionBootstrap.Load(System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, "definitions")));
        }

        [TearDown]
        public void TearDown() => DefRegistry.Clear();

        [Test]
        public void BoilingABitterHerb_GrantsTheAntidote()
        {
            var boil = DefRegistry.Get<CookMethodDef>(NamespacedId.Parse("isle:boil"));
            var herb = DefRegistry.Get<ItemDef>(NamespacedId.Parse("isle:bitter_herb"));
            var result = CookingResolver.Resolve(boil, new[] { herb }, cookingLevel: 10, failureRoll: 1f);
            Assert.IsFalse(result.Failed);
            Assert.IsTrue(result.Buffs.Any(b => b.Buff.Value == "isle:antidote"), "no antidote from a boiled bitter herb");
        }

        [Test]
        public void Antidote_IsFullToxicResistance_ForFiveRealMinutes()
        {
            var antidote = DefRegistry.Get<BuffDef>(NamespacedId.Parse("isle:antidote"));
            Assert.AreEqual(5 * 60 * Isle.World.Time.WorldClock.MinutesPerRealSecond, antidote.DurationMin, 1e-6);
            var resist = antidote.Effects.Single(e => e.Type == "damage_resist");
            Assert.AreEqual("toxic", resist.DamageType);
            Assert.AreEqual(1f, resist.Value);
        }

        [Test]
        public void Miasma_PicksAThirdOfTheEligibleRooms_NeverEntranceRestOrBoss()
        {
            var temple = DefRegistry.Get<DungeonDef>(NamespacedId.Parse("isle:drowned_temple"));
            for (var seed = 1; seed <= 50; seed++)
            {
                var floor = DungeonGenerator.Generate(seed, temple.Floors - 1, temple.Floors, temple.Danger);
                var picked = Tides.PickRooms(floor, temple.Miasma.Share, seed, 7349);
                var eligible = floor.Rooms.Count(r => r.Kind is not (RoomKind.Entrance or RoomKind.Rest or RoomKind.Boss));
                Assert.AreEqual((int)System.Math.Floor(eligible * temple.Miasma.Share), picked.Count, $"seed {seed}");
                Assert.IsFalse(picked.Any(i => floor.Rooms[i].Kind is RoomKind.Entrance or RoomKind.Rest or RoomKind.Boss), $"seed {seed}");
            }
        }
    }
}
