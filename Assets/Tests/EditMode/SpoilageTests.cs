using Isle.Core;
using Isle.Core.Ids;
using Isle.Data;
using Isle.Gameplay.Inventory;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-WORLD-01 §Deferred simulation: <c>spoilage += elapsed × rate</c>; a stack whose spoilage reaches
    /// 1 turns into the def's <c>spoilage.result</c>.</summary>
    public sealed class SpoilageTests
    {
        static readonly ItemDef Rotten = new() { Id = NamespacedId.Parse("isle:rotten_food"), Grid = new GridSize { W = 1, H = 1 }, Stack = 10 };

        static ItemDef Meat(float baseHours = 24f) => new()
        {
            Id = NamespacedId.Parse("isle:raw_meat"),
            Grid = new GridSize { W = 1, H = 1 },
            Stack = 10,
            Spoilage = new SpoilageSpec { BaseHours = baseHours, Result = Rotten.Id },
        };

        [Test]
        public void Advance_HalfOfBaseHours_ReturnsHalf()
        {
            Assert.AreEqual(0.5f, SpoilageTracker.Advance(0f, inGameMinutes: 12 * 60, baseHours: 24f), 1e-4f);
        }

        [Test]
        public void Advance_ZeroBaseHours_NeverSpoils()
        {
            Assert.AreEqual(0f, SpoilageTracker.Advance(0f, inGameMinutes: 1000, baseHours: 0f), 1e-4f);
        }

        [Test]
        public void Tick_PastBaseHours_TurnsStackIntoResult()
        {
            var tracker = new SpoilageTracker(_ => Rotten);
            var bag = new GridInventory(3, 3);
            var meat = Meat();
            bag.TryPlace(meat, new Vec2Int(1, 1), count: 3);

            tracker.Tick(bag, inGameMinutes: 24 * 60);

            Assert.AreEqual(1, bag.Placements.Count);
            Assert.AreEqual("isle:rotten_food", bag.Placements[0].Item.Id.Value);
            Assert.AreEqual(3, bag.Placements[0].Count);
        }

        [Test]
        public void Tick_BeforeBaseHours_KeepsStackAndReportsFraction()
        {
            var tracker = new SpoilageTracker(_ => Rotten);
            var bag = new GridInventory(3, 3);
            var meat = Meat();
            bag.TryPlace(meat, new Vec2Int(0, 0));

            tracker.Tick(bag, inGameMinutes: 6 * 60);

            Assert.AreEqual("isle:raw_meat", bag.Placements[0].Item.Id.Value);
            Assert.AreEqual(0.25f, tracker.SpoilageOf(bag, meat), 1e-4f);
        }

        [Test]
        public void Tick_StackGoneThenBack_StartsFresh()
        {
            var tracker = new SpoilageTracker(_ => Rotten);
            var bag = new GridInventory(3, 3);
            var meat = Meat();
            bag.TryPlace(meat, new Vec2Int(0, 0));
            tracker.Tick(bag, inGameMinutes: 6 * 60);

            bag.Clear();
            tracker.Tick(bag, inGameMinutes: 1);
            bag.TryPlace(meat, new Vec2Int(0, 0));
            tracker.Tick(bag, inGameMinutes: 0);

            Assert.AreEqual(0f, tracker.SpoilageOf(bag, meat), 1e-4f);
        }

        [Test]
        public void Tick_NonPerishable_Ignored()
        {
            var tracker = new SpoilageTracker(_ => Rotten);
            var bag = new GridInventory(3, 3);
            var stone = new ItemDef { Id = NamespacedId.Parse("isle:stone"), Grid = new GridSize { W = 1, H = 1 } };
            bag.TryPlace(stone, new Vec2Int(0, 0));
            tracker.Tick(bag, inGameMinutes: 100000);
            Assert.AreEqual("isle:stone", bag.Placements[0].Item.Id.Value);
        }
    }
}
