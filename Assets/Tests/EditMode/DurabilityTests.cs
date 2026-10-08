using System.Collections.Generic;
using System.Linq;
using Isle.Core;
using Isle.Core.Ids;
using Isle.Data;
using Isle.Gameplay.Crafting;
using Isle.Gameplay.Inventory;
using Isle.Modding.Defs;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-CRAFT-02 verification: wear per use, broken-not-destroyed, repair at ×0.92 for half the inputs,
    /// tool tiers on veins, tool speed, and wear that follows the item through equip, moves and drops.</summary>
    public sealed class DurabilityTests
    {
        static ItemDef Tool(int durability = 60, int tier = 1, string tag = "tool/axe") => new()
        {
            Id = NamespacedId.Parse("test:hatchet_" + tier),
            Grid = new GridSize { W = 1, H = 2 },
            Stack = 1,
            EquipSlot = "main_hand",
            Durability = durability,
            Tier = tier,
            ToolPower = tier switch { 2 => 1.25f, 3 => 1.5f, 4 => 1.8f, _ => 1f },
            Tags = new[] { "tool", tag },
        };

        // --- 1. Wear and breaking ---------------------------------------------------------------------------------

        [Test]
        public void Wear_SixtyUses_BreaksAStoneTool()
        {
            var wear = ItemWear.Fresh(Tool());
            for (var i = 0; i < 59; i++) wear.Use();
            Assert.IsFalse(wear.Broken);
            wear.Use();
            Assert.IsTrue(wear.Broken);
            Assert.AreEqual(0, wear.Current);
            wear.Use();
            Assert.AreEqual(0, wear.Current, "never below zero");
        }

        [Test]
        public void Wear_ItemWithoutDurability_HasNone() =>
            Assert.IsNull(ItemWear.Fresh(new ItemDef { Id = NamespacedId.Parse("test:stone") }));

        [Test]
        public void BrokenTool_IsNotWorkingInItsSlot_ButStaysEquipped()
        {
            var slots = new EquipSlots();
            var tool = Tool();
            Assert.IsTrue(slots.TryEquip("main_hand", tool));
            Assert.AreSame(tool, slots.Working("main_hand"));
            for (var i = 0; i < 60; i++) slots.Wear("main_hand", 1);
            Assert.AreSame(tool, slots.Get("main_hand"), "broken, not destroyed");
            Assert.IsNull(slots.Working("main_hand"), "a broken tool counts as no tool");
        }

        // --- 2–3. Repair ------------------------------------------------------------------------------------------

        [Test]
        public void Repair_MaxDecaysBy092_Rounded()
        {
            Assert.AreEqual(55, RepairCalculator.NewMax(60));
            Assert.AreEqual(51, RepairCalculator.NewMax(55));
            Assert.AreEqual(47, RepairCalculator.NewMax(51));
        }

        [Test]
        public void Repair_RestoresToTheNewMaximum()
        {
            var wear = ItemWear.Fresh(Tool());
            for (var i = 0; i < 60; i++) wear.Use();
            wear.Repair();
            Assert.AreEqual(55, wear.Max);
            Assert.AreEqual(55, wear.Current);
            Assert.IsFalse(wear.Broken);
        }

        [Test]
        public void Repair_CostsHalfTheInputs_RoundedUp()
        {
            var inputs = new[]
            {
                new IngredientRef { Item = NamespacedId.Parse("isle:copper_ingot"), Count = 3 },
                new IngredientRef { Item = NamespacedId.Parse("isle:wood"), Count = 2 },
                new IngredientRef { Item = NamespacedId.Parse("isle:hide"), Count = 1 },
            };
            var cost = RepairCalculator.Cost(inputs);
            Assert.AreEqual(new[] { 2, 1, 1 }, cost.Select(c => c.Count).ToArray());
            Assert.AreEqual(inputs.Select(i => i.Item), cost.Select(c => c.Item));
        }

        [Test]
        public void Repair_NotNeededAtFullDurability()
        {
            var wear = ItemWear.Fresh(Tool());
            Assert.IsFalse(wear.NeedsRepair);
            wear.Use();
            Assert.IsTrue(wear.NeedsRepair);
        }

        // --- 4–5. Tool tiers and speed -----------------------------------------------------------------------------

        [Test]
        public void Vein_NeedsAPickaxeOfItsTier()
        {
            Assert.IsFalse(ToolTiers.CanWork(toolTier: 1, requiredTier: 2), "iron vein, stone pickaxe");
            Assert.IsTrue(ToolTiers.CanWork(toolTier: 2, requiredTier: 2));
            Assert.IsTrue(ToolTiers.CanWork(toolTier: 4, requiredTier: 2));
            Assert.IsTrue(ToolTiers.CanWork(toolTier: 0, requiredTier: 0), "no requirement");
        }

        [Test]
        public void ToolSpeed_DividesHarvestTimeByItsPower()
        {
            Assert.AreEqual(8f, ToolTiers.HarvestSeconds(10f, toolPower: 1.25f), 1e-4f);
            Assert.AreEqual(10f, ToolTiers.HarvestSeconds(10f, toolPower: 0f), 1e-4f, "no tool: unchanged");
        }

        // --- 8. Wear follows the item ----------------------------------------------------------------------------

        [Test]
        public void Wear_SurvivesUnequipAndReequip()
        {
            var slots = new EquipSlots();
            var bag = new GridInventory(4, 4);
            var tool = Tool();
            slots.TryEquip("main_hand", tool);
            slots.Wear("main_hand", 7);
            var wear = slots.WearOf("main_hand");
            Assert.IsTrue(slots.Unequip("main_hand", out var carried));
            Assert.AreSame(wear, carried);
            Assert.IsTrue(bag.TryPlace(tool, new Vec2Int(0, 0), false, 1, carried));
            var placed = bag.PlacementAt(new Vec2Int(0, 0)).Value;
            Assert.AreEqual(53, placed.Wear.Current);
            bag.Remove(placed);
            Assert.IsTrue(slots.TryEquip("main_hand", tool, placed.Wear));
            Assert.AreEqual(53, slots.WearOf("main_hand").Current);
        }

        [Test]
        public void WornItems_NeverMergeIntoOneStack()
        {
            var bag = new GridInventory(4, 4);
            var tool = Tool();
            var worn = ItemWear.Fresh(tool);
            worn.Use();
            Assert.IsTrue(InventoryOps.TryGive(new List<GridInventory> { bag }, tool, 1, worn));
            Assert.IsTrue(InventoryOps.TryGive(new List<GridInventory> { bag }, tool, 1));
            Assert.AreEqual(2, bag.Placements.Count, "two separate tools");
            Assert.AreEqual(1, bag.Placements.Count(p => p.Wear == worn));
        }

        [Test]
        public void Wear_SurvivesAutoSortAndMoves()
        {
            var bag = new GridInventory(4, 4);
            var other = new GridInventory(4, 4);
            var tool = Tool();
            var worn = ItemWear.Fresh(tool);
            worn.Use(); worn.Use();
            bag.TryPlace(tool, new Vec2Int(2, 2), false, 1, worn);
            Assert.IsTrue(bag.AutoSort());
            Assert.AreSame(worn, bag.Placements.Single().Wear);
            Assert.IsTrue(bag.TryMoveTo(other, bag.Placements.Single()));
            Assert.AreSame(worn, other.Placements.Single().Wear);
        }
    
        // --- Content (SYS-CRAFT-02 tables) -------------------------------------------------------------------------

        static readonly int[] Durabilities = { 0, 60, 150, 300, 500 };
        static readonly float[] Mults = { 0f, 1f, 1.25f, 1.5f, 1.8f };
        static readonly int[] Levels = { 0, 1, 10, 20, 30 };

        [Test]
        public void Content_TieredGear_FollowsTheTables()
        {
            DefRegistry.Clear();
            try
            {
                Assert.IsEmpty(DefinitionBootstrap.Load(System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, "definitions")));
                var tiered = DefRegistry.All<ItemDef>().Where(i => i.Tier > 0 && i.Durability > 0).ToList();
                // 10 tier-1 pieces + 3 metals × (6 tools/weapons + 4 armor pieces).
                Assert.AreEqual(40, tiered.Count, "tiered gear count");
                foreach (var item in tiered)
                    Assert.AreEqual(Durabilities[item.Tier], item.Durability, $"{item.Id} durability");

                var bases = new Dictionary<string, float> { ["hatchet"] = 22, ["pickaxe"] = 18, ["spear"] = 30, ["knife"] = 16, ["sword"] = 26, ["mace"] = 26 };
                foreach (var (metal, tier) in new[] { ("copper", 2), ("iron", 3), ("steel", 4) })
                foreach (var (kind, power) in bases)
                {
                    var weapon = DefRegistry.Get<WeaponDef>(NamespacedId.Parse($"isle:{metal}_{kind}"));
                    Assert.AreEqual((int)(power * Mults[tier] + 0.5f), (int)weapon.BasePower, $"{metal} {kind} power");
                    var recipe = DefRegistry.Get<CraftRecipeDef>(NamespacedId.Parse($"isle:craft_{metal}_{kind}"));
                    Assert.AreEqual("isle:anvil", recipe.Station.Value);
                    Assert.AreEqual(Levels[tier], recipe.Skills[0].Level, $"{metal} {kind} level");
                }
            }
            finally { DefRegistry.Clear(); }
        }

        [Test]
        public void Content_EveryRecipe_UsesRealItemsAndStations()
        {
            DefRegistry.Clear();
            try
            {
                DefinitionBootstrap.Load(System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, "definitions"));
                foreach (var recipe in DefRegistry.All<CraftRecipeDef>())
                {
                    Assert.IsTrue(DefRegistry.TryGet<ItemDef>(recipe.Output.Item, out _), $"{recipe.Id}: output {recipe.Output.Item}");
                    foreach (var need in recipe.Ingredients.Where(n => n.Item.IsValid))
                        Assert.IsTrue(DefRegistry.TryGet<ItemDef>(need.Item, out _), $"{recipe.Id}: input {need.Item}");
                    if (recipe.Station.IsValid)
                        Assert.IsTrue(DefRegistry.TryGet<WorldObjectDef>(recipe.Station, out _), $"{recipe.Id}: station {recipe.Station}");
                }
                // Every durable item can be repaired: something crafts it.
                foreach (var item in DefRegistry.All<ItemDef>().Where(i => i.Durability > 0))
                    Assert.IsTrue(DefRegistry.All<CraftRecipeDef>().Any(r => r.Output.Item == item.Id), $"{item.Id} has no recipe to repair from");
            }
            finally { DefRegistry.Clear(); }
        }
    }
}
