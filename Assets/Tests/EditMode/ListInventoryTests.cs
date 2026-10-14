using Isle.Core;
using Isle.Data;
using Isle.Gameplay.Inventory;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>T-305 SYS-INV-02: containers as weight-capped lists.</summary>
    public sealed class ListInventoryTests
    {
        static ItemDef Item(float kg, int w = 1, int h = 1, int? durability = null) =>
            new() { Weight = kg, Grid = new GridSize { W = w, H = h }, Stack = 40, Durability = durability };

        [Test]
        public void Capacity_Pockets12kg_RefusesOverweight() // verification 1, 2
        {
            var pockets = new GridInventory(InventoryNetwork.PocketsCapacityKg);
            Assert.AreEqual(12f, pockets.CapacityKg);
            Assert.IsTrue(pockets.IsList);
            var rock = Item(2.5f);
            Assert.IsTrue(InventoryOps.TryGive(new[] { pockets }, rock, 4)); // 10 kg
            Assert.IsTrue(InventoryOps.TryGive(new[] { pockets }, Item(1.5f), 1)); // 11.5 kg
            Assert.IsFalse(InventoryOps.TryGive(new[] { pockets }, Item(1f), 1), "12 kg is the cap");
            Assert.IsTrue(InventoryOps.TryGive(new[] { pockets }, Item(0.5f), 1));
            Assert.AreEqual(12f, pockets.TotalWeightKg(), 1e-4f);
        }

        [Test]
        public void Shape_DoesNotMatter()
        {
            var bag = new GridInventory(10f);
            Assert.IsTrue(InventoryOps.TryGive(new[] { bag }, Item(1f, 1, 4), 1), "a long item fits a list");
            Assert.IsTrue(InventoryOps.TryGive(new[] { bag }, Item(1f, 3, 3), 1));
        }

        [Test]
        public void SameItem_MergesIntoOneEntry() // verification 3
        {
            var bag = new GridInventory(20f);
            var wood = Item(1f);
            InventoryOps.TryGive(new[] { bag }, wood, 2);
            InventoryOps.TryGive(new[] { bag }, wood, 3);
            Assert.AreEqual(1, bag.Placements.Count);
            Assert.AreEqual(5, bag.Placements[0].Count);
        }

        [Test]
        public void Gear_StaysOnePerEntry_WithUniqueKeys()
        {
            var bag = new GridInventory(20f);
            var knife = Item(0.5f, durability: 50);
            InventoryOps.TryGive(new[] { bag }, knife, 3);
            Assert.AreEqual(3, bag.Placements.Count);
            Assert.AreEqual(3, new System.Collections.Generic.HashSet<Vec2Int>(System.Linq.Enumerable.Select(bag.Placements, p => p.Position)).Count, "keys must be unique");
            Assert.IsNotNull(bag.PlacementAt(bag.Placements[1].Position));
        }

        [Test]
        public void TryPlace_TakenKey_GetsAFreshOne()
        {
            var bag = new GridInventory(20f);
            Assert.IsTrue(bag.TryPlace(Item(1f), new Vec2Int(0, 0)));
            Assert.IsTrue(bag.TryPlace(Item(1f), new Vec2Int(0, 0)), "a list doesn't care where");
            Assert.AreEqual(2, bag.Placements.Count);
            Assert.AreNotEqual(bag.Placements[0].Position, bag.Placements[1].Position);
        }

        [Test]
        public void Merge_StillChecksWeight()
        {
            var bag = new GridInventory(3f);
            var stone = Item(1f);
            InventoryOps.TryGive(new[] { bag }, stone, 3);
            Assert.IsFalse(InventoryOps.TryGive(new[] { bag }, stone, 1), "merging past the cap");
        }

        [Test]
        public void Split_OneToAnotherList() // verification 5
        {
            var a = new GridInventory(20f);
            var b = new GridInventory(20f);
            var wood = Item(1f);
            InventoryOps.TryGive(new[] { a }, wood, 5);
            Assert.IsTrue(a.TrySplit(a.Placements[0], 1, b, new Vec2Int(0, 0)));
            Assert.AreEqual(4, a.Placements[0].Count);
            Assert.AreEqual(1, b.Placements[0].Count);
        }

        [Test]
        public void MoveTo_AnotherList_Merges()
        {
            var a = new GridInventory(20f);
            var b = new GridInventory(20f);
            var wood = Item(1f);
            InventoryOps.TryGive(new[] { a }, wood, 2);
            InventoryOps.TryGive(new[] { b }, wood, 3);
            Assert.IsTrue(a.TryMoveTo(b, a.Placements[0]));
            Assert.AreEqual(0, a.Placements.Count);
            Assert.AreEqual(1, b.Placements.Count);
            Assert.AreEqual(5, b.Placements[0].Count);
        }
    }
}
