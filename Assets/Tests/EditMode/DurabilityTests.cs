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
    
        // --- Content (SYS-CRAFT-02 materials) -----------------------------------------------------------------------

        static readonly Dictionary<string, float> Bases = new() { ["hatchet"] = 22, ["pickaxe"] = 18, ["spear"] = 30, ["knife"] = 16, ["sword"] = 26, ["mace"] = 26, ["club"] = 26 };

        [Test]
        public void Content_Variants_FollowTheMaterialFactors()
        {
            DefRegistry.Clear();
            try
            {
                Assert.IsEmpty(DefinitionBootstrap.Load(System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, "definitions")));
                // The decided durability ladder: stone 60, copper 150, iron 300, steel 500.
                foreach (var (material, durability) in new[] { ("stone", 60), ("copper", 150), ("iron", 300), ("steel", 500) })
                    Assert.AreEqual(durability, DefRegistry.Get<ItemDef>(NamespacedId.Parse($"isle:pickaxe__{material}")).Durability, material);
                foreach (var material in DefRegistry.All<MaterialDef>())
                foreach (var (kind, power) in Bases)
                {
                    if (!DefRegistry.TryGet<WeaponDef>(NamespacedId.Parse($"isle:{kind}__{material.Id.Name}"), out var weapon)) continue;
                    var factor = kind is "mace" or "club" ? material.PowerBlunt : material.PowerSharp;
                    Assert.AreEqual((int)(power * factor + 0.5f), (int)weapon.BasePower, $"{weapon.Id} power");
                }
                // Copper tools work an iron vein's tier; stone doesn't.
                Assert.AreEqual(2, DefRegistry.Get<ItemDef>(NamespacedId.Parse("isle:pickaxe__copper")).Tier);
                Assert.AreEqual(1.8f, DefRegistry.Get<ItemDef>(NamespacedId.Parse("isle:hatchet__steel")).ToolPower, 1e-4f);
                // Fur is warm, metal isn't; metal turns a blade, fur barely.
                var furParka = DefRegistry.Get<ItemDef>(NamespacedId.Parse("isle:parka__wolf_fur"));
                var ironPlate = DefRegistry.Get<ItemDef>(NamespacedId.Parse("isle:breastplate__iron"));
                Assert.Greater(furParka.Warmth, ironPlate.Warmth);
                Assert.Greater(ironPlate.ArmorTypes["slash"], furParka.ArmorTypes["slash"]);
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
                    if (recipe.Stuff != null)
                        Assert.IsNotEmpty(StuffCrafting.MaterialsFor(recipe), $"{recipe.Id}: no material fits");
                }
                foreach (var material in DefRegistry.All<MaterialDef>())
                    Assert.IsTrue(DefRegistry.TryGet<ItemDef>(material.Item, out _), $"{material.Id}: item {material.Item}");
                // Every durable item that exists in the world can be repaired: something crafts it.
                // (Salvage — like the start's bulletproof vest — can't be made, so it can't be mended either.)
                foreach (var item in DefRegistry.All<ItemDef>().Where(i => i.Durability > 0 && i.Stuff == null && !(i.Tags?.Contains("salvage") ?? false)))
                    Assert.IsNotNull(StuffCrafting.RecipeFor(item), $"{item.Id} has no recipe to repair from");
            }
            finally { DefRegistry.Clear(); }
        }

        // --- Materials, in isolation ------------------------------------------------------------------------------

        static MaterialDef Mat(string id, string category, float sharp = 1f, float blunt = 1f, float warmth = 1f, float durability = 1f) => new()
        {
            Id = NamespacedId.Parse("test:" + id), Name = "@material." + id, Item = NamespacedId.Parse("test:" + id + "_unit"),
            Categories = new[] { category }, ArmorSharp = sharp, ArmorBlunt = blunt, ArmorHeat = 1f, Warmth = warmth,
            Durability = durability, Mass = 0.5f, PowerSharp = sharp, PowerBlunt = blunt, ToolSpeed = 1f, Tier = 1,
        };

        [Test]
        public void Expand_MakesOneVariantPerAcceptedMaterial_WithItsFactors()
        {
            var items = new LoadResult<ItemDef>();
            var weapons = new LoadResult<WeaponDef>();
            items.Definitions.Add(new ItemDef
            {
                Id = NamespacedId.Parse("test:shirt"), Name = "@item.shirt", Grid = new GridSize { W = 2, H = 2 }, Weight = 0.1f,
                Durability = 60, Armor = 2f, Warmth = 2f, Stuff = new StuffSpec { Categories = new[] { "fabric" }, Amount = 2 },
            });
            items.SourcePaths.Add("x");
            var materials = new[] { Mat("cloth", "fabric", sharp: 0.5f, blunt: 0.25f, warmth: 1.5f, durability: 0.5f), Mat("iron", "metal") };
            StuffVariants.Expand(items, weapons, materials);

            Assert.AreEqual(2, items.Definitions.Count, "template + the one fabric variant (iron isn't fabric)");
            var shirt = items.Definitions.Single(i => i.Id.Value == "test:shirt__cloth");
            Assert.AreEqual(1f, shirt.ArmorTypes["slash"], 1e-4f);
            Assert.AreEqual(0.5f, shirt.ArmorTypes["blunt"], 1e-4f);
            Assert.AreEqual(3f, shirt.Warmth, 1e-4f);
            Assert.AreEqual(30, shirt.Durability);
            Assert.AreEqual(1.1f, shirt.Weight, 1e-4f, "0.1 + 2 units × 0.5");
            Assert.AreEqual("test:shirt", shirt.StuffTemplate.Value);
            Assert.AreEqual("test:cloth", shirt.Material.Value);
            Assert.IsNull(shirt.Stuff);
            Assert.AreEqual("@pattern.made_of|@material.cloth|@item.shirt", shirt.Name);
        }

        [Test]
        public void Expand_Weapon_RescalesEachAttackByHowTheMaterialHits()
        {
            var items = new LoadResult<ItemDef>();
            var weapons = new LoadResult<WeaponDef>();
            weapons.Definitions.Add(new WeaponDef
            {
                Id = NamespacedId.Parse("test:axe"), BasePower = 20f, DamageType = "slash", Durability = 60,
                Attacks = new[] { new AttackSpec { Type = "slash" }, new AttackSpec { Type = "blunt", PowerMult = 1.4f } },
            });
            weapons.SourcePaths.Add("x");
            items.Definitions.Add(new ItemDef { Id = NamespacedId.Parse("test:axe"), Weapon = NamespacedId.Parse("test:axe"), Durability = 60, Stuff = new StuffSpec { Categories = new[] { "wood" } } });
            items.SourcePaths.Add("x");
            StuffVariants.Expand(items, weapons, new[] { Mat("wood", "wood", sharp: 0.5f, blunt: 1f) });

            var axe = weapons.Definitions.Single(w => w.Id.Value == "test:axe__wood");
            Assert.AreEqual(10f, axe.BasePower, 1e-4f, "slash weapon × sharp 0.5");
            Assert.AreEqual(1f, axe.Attacks[0].PowerMult, 1e-4f);
            Assert.AreEqual(2.8f, axe.Attacks[1].PowerMult, 1e-4f, "the blunt finisher keeps wood's full blunt power: 1.4 × 1 / 0.5");
            Assert.AreEqual("test:axe__wood", items.Definitions.Single(i => i.Id.Value == "test:axe__wood").Weapon.Value);
        }
    }
}
