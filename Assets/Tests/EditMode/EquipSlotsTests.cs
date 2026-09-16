using Isle.Data;
using Isle.Gameplay.Inventory;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-INV-01 §Containers (T-044). No verification table exists for equip slots — cases
    /// below are derived from the prose rules directly, same approach T-040 took for placement.</summary>
    public sealed class EquipSlotsTests
    {
        [Test]
        public void TryEquip_MatchingSlot_Succeeds()
        {
            var slots = new EquipSlots();
            var sword = new ItemDef { EquipSlot = "main_hand", Grid = new GridSize { W = 1, H = 4 } };

            Assert.IsTrue(slots.TryEquip("main_hand", sword));
            Assert.AreEqual(sword, slots.Get("main_hand"));
        }

        [Test]
        public void TryEquip_MismatchedSlot_Fails()
        {
            var slots = new EquipSlots();
            var sword = new ItemDef { EquipSlot = "main_hand" };

            Assert.IsFalse(slots.TryEquip("off_hand", sword));
            Assert.IsNull(slots.Get("off_hand"));
        }

        [Test]
        public void TryEquip_SlotAlreadyOccupied_Fails()
        {
            var slots = new EquipSlots();
            var sword = new ItemDef { EquipSlot = "main_hand" };
            var axe = new ItemDef { EquipSlot = "main_hand" };
            slots.TryEquip("main_hand", sword);

            Assert.IsFalse(slots.TryEquip("main_hand", axe));
            Assert.AreEqual(sword, slots.Get("main_hand"));
        }

        [Test]
        public void TryEquip_BagItem_OpensGridInventory()
        {
            var slots = new EquipSlots();
            var backpack = new ItemDef { EquipSlot = "back", BagGrid = new GridSize { W = 6, H = 7 } };

            Assert.IsTrue(slots.TryEquip("back", backpack));
            Assert.IsNotNull(slots.BagFor("back"));
            Assert.AreEqual(6, slots.BagFor("back").Width);
            Assert.AreEqual(7, slots.BagFor("back").Height);
        }

        [Test]
        public void TryEquip_NonBagItem_NoGridInventory()
        {
            var slots = new EquipSlots();
            var sword = new ItemDef { EquipSlot = "main_hand" };
            slots.TryEquip("main_hand", sword);

            Assert.IsNull(slots.BagFor("main_hand"));
        }

        [Test]
        public void Unequip_EmptyBag_Succeeds()
        {
            var slots = new EquipSlots();
            var backpack = new ItemDef { EquipSlot = "back", BagGrid = new GridSize { W = 6, H = 7 } };
            slots.TryEquip("back", backpack);

            Assert.IsTrue(slots.Unequip("back"));
            Assert.IsNull(slots.Get("back"));
            Assert.IsNull(slots.BagFor("back"));
        }

        [Test]
        public void Unequip_BagHoldingItems_Fails()
        {
            var slots = new EquipSlots();
            var backpack = new ItemDef { EquipSlot = "back", BagGrid = new GridSize { W = 6, H = 7 } };
            slots.TryEquip("back", backpack);
            slots.BagFor("back").TryPlace(new ItemDef { Grid = new GridSize { W = 1, H = 1 } }, new Isle.Core.Vec2Int(0, 0));

            Assert.IsFalse(slots.Unequip("back"));
            Assert.AreEqual(backpack, slots.Get("back"));
        }

        [Test]
        public void Unequip_NothingEquipped_Fails()
        {
            var slots = new EquipSlots();

            Assert.IsFalse(slots.Unequip("head"));
        }
    }
}
