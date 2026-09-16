using System.Linq;
using Isle.Core;
using Isle.Data;
using Isle.Gameplay.Inventory;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>
    /// SYS-INV-01 §Placement (T-040). No literal verification table is given for placement itself
    /// (the spec's table covers §Weight, T-041's scope) — cases below are derived directly from
    /// the placement rules: in bounds, no overlap, rotation swaps W/H, AABB overlap is sufficient.
    /// </summary>
    public sealed class GridInventoryTests
    {
        static ItemDef Item(int w, int h) => new() { Grid = new GridSize { W = w, H = h } };

        [Test]
        public void TryPlace_FitsInBounds_Succeeds()
        {
            var inventory = new GridInventory(width: 6, height: 3);

            Assert.IsTrue(inventory.TryPlace(Item(2, 2), new Vec2Int(0, 0)));
            Assert.AreEqual(1, inventory.Placements.Count);
        }

        [Test]
        public void TryPlace_OutOfBounds_Fails()
        {
            var inventory = new GridInventory(width: 6, height: 3);

            Assert.IsFalse(inventory.TryPlace(Item(2, 2), new Vec2Int(5, 2)));
            Assert.AreEqual(0, inventory.Placements.Count);
        }

        [Test]
        public void TryPlace_NegativePosition_Fails()
        {
            var inventory = new GridInventory(width: 6, height: 3);

            Assert.IsFalse(inventory.TryPlace(Item(1, 1), new Vec2Int(-1, 0)));
        }

        [Test]
        public void TryPlace_OverlappingExistingItem_Fails()
        {
            var inventory = new GridInventory(width: 6, height: 3);
            inventory.TryPlace(Item(2, 2), new Vec2Int(0, 0));

            Assert.IsFalse(inventory.TryPlace(Item(1, 1), new Vec2Int(1, 1)));
            Assert.AreEqual(1, inventory.Placements.Count);
        }

        [Test]
        public void TryPlace_AdjacentNotOverlapping_Succeeds()
        {
            var inventory = new GridInventory(width: 6, height: 3);
            inventory.TryPlace(Item(2, 2), new Vec2Int(0, 0));

            Assert.IsTrue(inventory.TryPlace(Item(2, 2), new Vec2Int(2, 0)));
            Assert.AreEqual(2, inventory.Placements.Count);
        }

        [Test]
        public void TryPlace_SameItemSamePositionAndRotation_MergesCount()
        {
            var inventory = new GridInventory(width: 6, height: 3);
            var potion = Item(1, 1);
            inventory.TryPlace(potion, new Vec2Int(0, 0), count: 3);

            Assert.IsTrue(inventory.TryPlace(potion, new Vec2Int(0, 0), count: 2));
            Assert.AreEqual(1, inventory.Placements.Count);
            Assert.AreEqual(5, inventory.Placements[0].Count);
        }

        [Test]
        public void TryPlace_DifferentItemSamePosition_StillFails()
        {
            var inventory = new GridInventory(width: 6, height: 3);
            inventory.TryPlace(Item(1, 1), new Vec2Int(0, 0), count: 3);

            // A different ItemDef reference at the same spot is an overlap, not a merge target.
            Assert.IsFalse(inventory.TryPlace(Item(1, 1), new Vec2Int(0, 0), count: 2));
            Assert.AreEqual(1, inventory.Placements.Count);
        }

        [Test]
        public void TryPlace_Rotated_SwapsWidthAndHeight()
        {
            var inventory = new GridInventory(width: 6, height: 3);

            // A 1x4 item (sword) doesn't fit upright in a 3-tall container, but does rotated.
            Assert.IsFalse(inventory.TryPlace(Item(1, 4), new Vec2Int(0, 0)));
            Assert.IsTrue(inventory.TryPlace(Item(1, 4), new Vec2Int(0, 0), rotated: true));
        }

        [Test]
        public void Remove_PlacedItem_FreesItsCells()
        {
            var inventory = new GridInventory(width: 6, height: 3);
            inventory.TryPlace(Item(2, 2), new Vec2Int(0, 0));
            var placement = inventory.Placements[0];

            Assert.IsTrue(inventory.Remove(placement));
            Assert.IsTrue(inventory.TryPlace(Item(2, 2), new Vec2Int(0, 0)));
        }

        [Test]
        public void FindFreePosition_EmptyGrid_ReturnsOrigin()
        {
            var inventory = new GridInventory(width: 6, height: 3);

            Assert.AreEqual(new Vec2Int(0, 0), inventory.FindFreePosition(new GridSize { W = 2, H = 2 }));
        }

        [Test]
        public void FindFreePosition_FirstRowFull_ScansToNextRow()
        {
            var inventory = new GridInventory(width: 2, height: 2);
            inventory.TryPlace(Item(2, 1), new Vec2Int(0, 0));

            Assert.AreEqual(new Vec2Int(0, 1), inventory.FindFreePosition(new GridSize { W = 2, H = 1 }));
        }

        [Test]
        public void FindFreePosition_NoRoom_ReturnsNull()
        {
            var inventory = new GridInventory(width: 2, height: 2);
            inventory.TryPlace(Item(2, 2), new Vec2Int(0, 0));

            Assert.IsNull(inventory.FindFreePosition(new GridSize { W = 1, H = 1 }));
        }

        [Test]
        public void TryMoveTo_FitsInDestination_MovesAndFreesSource()
        {
            var source = new GridInventory(width: 6, height: 3);
            var destination = new GridInventory(width: 2, height: 2);
            source.TryPlace(Item(2, 2), new Vec2Int(0, 0));
            var placement = source.Placements[0];

            Assert.IsTrue(source.TryMoveTo(destination, placement));
            Assert.AreEqual(0, source.Placements.Count);
            Assert.AreEqual(1, destination.Placements.Count);
        }

        [Test]
        public void TryMoveTo_NoRoomInDestination_LeavesSourceUntouched()
        {
            var source = new GridInventory(width: 6, height: 3);
            var destination = new GridInventory(width: 1, height: 1);
            source.TryPlace(Item(2, 2), new Vec2Int(0, 0));
            var placement = source.Placements[0];

            Assert.IsFalse(source.TryMoveTo(destination, placement));
            Assert.AreEqual(1, source.Placements.Count);
            Assert.AreEqual(0, destination.Placements.Count);
        }

        [Test]
        public void TryMoveTo_MatchingItemAlreadyInDestination_MergesIntoIt()
        {
            var source = new GridInventory(width: 6, height: 3);
            var destination = new GridInventory(width: 6, height: 3);
            var potion = Item(1, 1);
            destination.TryPlace(potion, new Vec2Int(0, 0), count: 2);
            source.TryPlace(potion, new Vec2Int(3, 0), count: 3);

            Assert.IsTrue(source.TryMoveTo(destination, source.Placements[0]));
            Assert.AreEqual(0, source.Placements.Count);
            Assert.AreEqual(1, destination.Placements.Count);
            Assert.AreEqual(5, destination.Placements[0].Count);
        }

        [Test]
        public void MoveAllTo_MovesEveryItemThatFits()
        {
            var source = new GridInventory(width: 6, height: 3);
            var destination = new GridInventory(width: 2, height: 4);
            source.TryPlace(Item(2, 2), new Vec2Int(0, 0));
            source.TryPlace(Item(2, 1), new Vec2Int(2, 0));

            Assert.AreEqual(2, source.MoveAllTo(destination));
            Assert.AreEqual(0, source.Placements.Count);
            Assert.AreEqual(2, destination.Placements.Count);
        }

        [Test]
        public void MoveAllTo_PartialFit_LeavesLeftoversBehind()
        {
            var source = new GridInventory(width: 6, height: 3);
            var destination = new GridInventory(width: 2, height: 2);
            source.TryPlace(Item(2, 2), new Vec2Int(0, 0));
            source.TryPlace(Item(2, 1), new Vec2Int(2, 0));

            Assert.AreEqual(1, source.MoveAllTo(destination));
            Assert.AreEqual(1, source.Placements.Count);
            Assert.AreEqual(1, destination.Placements.Count);
        }

        [Test]
        public void TrySplit_PartialCount_LeavesRemainderAndAddsNewStack()
        {
            var inventory = new GridInventory(width: 6, height: 3);
            inventory.TryPlace(Item(1, 1), new Vec2Int(0, 0), count: 5);
            var source = inventory.Placements[0];

            Assert.IsTrue(inventory.TrySplit(source, splitCount: 2, inventory, new Vec2Int(3, 0)));
            Assert.AreEqual(2, inventory.Placements.Count);
            Assert.IsTrue(inventory.Placements.Any(p => p.Position == new Vec2Int(0, 0) && p.Count == 3));
            Assert.IsTrue(inventory.Placements.Any(p => p.Position == new Vec2Int(3, 0) && p.Count == 2));
        }

        [Test]
        public void TrySplit_FullCount_Fails()
        {
            var inventory = new GridInventory(width: 6, height: 3);
            inventory.TryPlace(Item(1, 1), new Vec2Int(0, 0), count: 5);
            var source = inventory.Placements[0];

            Assert.IsFalse(inventory.TrySplit(source, splitCount: 5, inventory, new Vec2Int(3, 0)));
        }

        [Test]
        public void TrySplit_DestinationDoesNotFit_RollsBackSource()
        {
            var inventory = new GridInventory(width: 6, height: 3);
            inventory.TryPlace(Item(1, 1), new Vec2Int(0, 0), count: 5);
            inventory.TryPlace(Item(1, 1), new Vec2Int(3, 0), count: 1);
            var source = inventory.Placements[0];

            Assert.IsFalse(inventory.TrySplit(source, splitCount: 2, inventory, new Vec2Int(3, 0)));
            Assert.AreEqual(2, inventory.Placements.Count);
            Assert.IsTrue(inventory.Placements.Any(p => p.Position == new Vec2Int(0, 0) && p.Count == 5));
        }

        [Test]
        public void TrySplit_SamePositionAndRotation_Fails()
        {
            var inventory = new GridInventory(width: 6, height: 3);
            inventory.TryPlace(Item(1, 1), new Vec2Int(0, 0), count: 5);
            var source = inventory.Placements[0];

            Assert.IsFalse(inventory.TrySplit(source, splitCount: 2, inventory, new Vec2Int(0, 0)));
        }

        [Test]
        public void AutoSort_PacksLargestFirst()
        {
            var inventory = new GridInventory(width: 4, height: 4);
            inventory.TryPlace(Item(1, 1), new Vec2Int(3, 3));
            inventory.TryPlace(Item(2, 2), new Vec2Int(0, 2));

            Assert.IsTrue(inventory.AutoSort());
            Assert.AreEqual(2, inventory.Placements.Count);
            // Largest item (2x2) is packed first, landing at the origin.
            Assert.IsTrue(inventory.Placements.Any(p => p.Position == new Vec2Int(0, 0) && p.Item.Grid.W == 2));
        }

        [Test]
        public void TotalWeightKg_SumsWeightTimesCount()
        {
            var inventory = new GridInventory(width: 6, height: 3);
            var item = new ItemDef { Grid = new GridSize { W = 1, H = 1 }, Weight = 0.5f };
            inventory.TryPlace(item, new Vec2Int(0, 0), count: 4);

            Assert.AreEqual(2.0f, inventory.TotalWeightKg(), 0.001f);
        }
    }
}
