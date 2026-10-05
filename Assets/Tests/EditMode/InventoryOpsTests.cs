using System.Collections.Generic;
using Isle.Core;
using Isle.Core.Ids;
using Isle.Data;
using Isle.Gameplay.Inventory;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>Giving and taking across the base carry and equipped bags (SYS-INV-01 §Containers: bags add a
    /// separate grid). Stacks merge first, then free space, then rotated free space.</summary>
    public sealed class InventoryOpsTests
    {
        static ItemDef Item(string id, int w = 1, int h = 1) => new() { Id = NamespacedId.Parse(id), Grid = new GridSize { W = w, H = h }, Stack = 10 };

        [Test]
        public void TryGive_ExistingStackInSecondContainer_MergesThere()
        {
            var wood = Item("isle:wood");
            var bag = new GridInventory(1, 1);
            var pack = new GridInventory(3, 3);
            bag.TryPlace(Item("isle:stone"), new Vec2Int(0, 0));
            pack.TryPlace(wood, new Vec2Int(2, 2), count: 2);

            Assert.IsTrue(InventoryOps.TryGive(new[] { bag, pack }, wood, 3));
            Assert.AreEqual(5, InventoryOps.Count(new[] { bag, pack }, wood.Id));
            Assert.AreEqual(1, pack.Placements.Count);
        }

        [Test]
        public void TryGive_OnlyFitsRotated_PlacesRotated()
        {
            var spear = Item("isle:stone_spear", 1, 4);
            var bag = new GridInventory(6, 3);
            Assert.IsTrue(InventoryOps.TryGive(new[] { bag }, spear, 1));
            Assert.IsTrue(bag.Placements[0].Rotated);
        }

        [Test]
        public void TryGive_NoRoomAnywhere_ReturnsFalse()
        {
            var bag = new GridInventory(1, 1);
            bag.TryPlace(Item("isle:stone"), new Vec2Int(0, 0));
            Assert.IsFalse(InventoryOps.TryGive(new[] { bag }, Item("isle:wood"), 1));
        }

        [Test]
        public void TryTake_SpansContainers_RemovesExactCount()
        {
            var wood = Item("isle:wood");
            var bag = new GridInventory(2, 2);
            var pack = new GridInventory(2, 2);
            bag.TryPlace(wood, new Vec2Int(0, 0), count: 2);
            pack.TryPlace(wood, new Vec2Int(0, 0), count: 3);
            var containers = new List<GridInventory> { bag, pack };

            Assert.IsTrue(InventoryOps.TryTake(containers, wood.Id, 4));
            Assert.AreEqual(1, InventoryOps.Count(containers, wood.Id));
        }

        [Test]
        public void TryTake_NotEnough_TakesNothing()
        {
            var wood = Item("isle:wood");
            var bag = new GridInventory(2, 2);
            bag.TryPlace(wood, new Vec2Int(0, 0), count: 2);
            Assert.IsFalse(InventoryOps.TryTake(new[] { bag }, wood.Id, 3));
            Assert.AreEqual(2, InventoryOps.Count(new[] { bag }, wood.Id));
        }

        [Test]
        public void TryTakeOne_ReturnsTheItem()
        {
            var meat = Item("isle:raw_meat");
            var bag = new GridInventory(2, 2);
            bag.TryPlace(meat, new Vec2Int(0, 0), count: 2);
            Assert.AreSame(meat, InventoryOps.TakeOne(new[] { bag }, meat.Id));
            Assert.AreEqual(1, InventoryOps.Count(new[] { bag }, meat.Id));
        }
    }
}
