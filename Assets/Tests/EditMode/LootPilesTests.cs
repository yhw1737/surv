using Isle.Core.Ids;
using Isle.Data;
using Isle.Gameplay.Inventory;
using NUnit.Framework;
using UnityEngine;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-SURV-01 §Death: the dropped inventory can be recovered. Picking up moves what fits and
    /// leaves the rest on the ground.</summary>
    public sealed class LootPilesTests
    {
        static ItemDef Item(string id) => new ItemDef { Id = NamespacedId.Parse(id), Grid = new GridSize { W = 1, H = 1 }, Stack = 10 };

        [TearDown]
        public void TearDown() => LootPiles.Clear();

        [Test]
        public void PickUp_BagFitsOnlyOne_LeavesRestInPile()
        {
            var pile = LootPiles.Drop(Vector2.zero, new[] { (Item("isle:wood"), 2), (Item("isle:stone"), 1) });
            var bag = new GridInventory(1, 1);

            var taken = LootPiles.PickUp(pile, bag);

            Assert.AreEqual(1, taken.Count);
            Assert.AreEqual(1, pile.Items.Count);
            Assert.AreEqual(1, LootPiles.All.Count);
        }

        [Test]
        public void PickUp_EverythingFits_RemovesPile()
        {
            var pile = LootPiles.Drop(Vector2.zero, new[] { (Item("isle:wood"), 2) });
            LootPiles.PickUp(pile, new GridInventory(3, 3));
            Assert.AreEqual(0, LootPiles.All.Count);
        }

        [Test]
        public void PickUp_WithSlots_EquipsBagFirstThenFillsIt()
        {
            var pack = new ItemDef { Id = NamespacedId.Parse("isle:straw_backpack"), Grid = new GridSize { W = 2, H = 2 }, EquipSlot = "back", BagGrid = new GridSize { W = 5, H = 5 } };
            var stones = Item("isle:stone");
            var pile = LootPiles.Drop(Vector2.zero, new[] { (stones, 10), (pack, 1) });
            var bag = new GridInventory(1, 1);
            bag.TryPlace(Item("isle:wood"), new Isle.Core.Vec2Int(0, 0));
            var slots = new EquipSlots();

            LootPiles.PickUp(pile, slots, () => new[] { bag, slots.BagFor("back") });

            Assert.AreSame(pack, slots.Get("back"));
            Assert.AreEqual(1, slots.BagFor("back").Placements.Count);
            Assert.AreEqual(0, LootPiles.All.Count);
        }

        [Test]
        public void Drop_NoItems_CreatesNothing()
        {
            Assert.IsNull(LootPiles.Drop(Vector2.zero, new (ItemDef, int)[0]));
            Assert.AreEqual(0, LootPiles.All.Count);
        }

        [Test]
        public void Nearest_OutOfReach_ReturnsNull()
        {
            LootPiles.Drop(new Vector2(5f, 0f), new[] { (Item("isle:wood"), 1) });
            Assert.IsNull(LootPiles.Nearest(Vector2.zero, reachTiles: 2f));
        }
    }
}
