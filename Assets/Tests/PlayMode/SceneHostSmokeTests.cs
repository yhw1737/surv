using System.Collections;
using System.Linq;
using FishNet;
using Isle.Core.Ids;
using Isle.Data;
using Isle.Gameplay.Building;
using Isle.Gameplay.Character;
using Isle.Gameplay.Combat;
using Isle.Gameplay.Hunting;
using Isle.Gameplay.Inventory;
using Isle.World.Objects;
using Isle.Modding.Defs;
using Isle.Networking;
using Isle.World.Island;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Isle.Tests.PlayMode
{
    /// <summary>
    /// Loads the real <c>SampleScene</c>, starts the host (server + local client, SYS-NET-01 §Transports),
    /// and runs the prototype for a few seconds. Unity fails the test on any unexpected error log, so this
    /// catches runtime exceptions in the spawn path and the AI loop that the EditMode tests can't reach.
    /// </summary>
    public sealed class SceneHostSmokeTests
    {
        static readonly string ScratchSave = System.IO.Path.Combine(Application.temporaryCachePath, "isle_test_save.json");

        [SetUp]
        public void SetUp()
        {
            // Never touch the real save: a leftover one would also change what the test starts with.
            // The scene opens on the main menu now (T-153); this test drives the game itself, so skip it.
            Isle.UI.Prototype.GameSession.SkipMenu = true;
            Isle.UI.Prototype.SaveGame.FilePathOverride = ScratchSave;
            if (System.IO.File.Exists(ScratchSave)) System.IO.File.Delete(ScratchSave);

            // The runtime bootstrap only runs once per domain, but earlier tests' TearDown clears the
            // registry, so reload it here. In a real launch DefinitionBootstrapRunner does this before any scene.
            DefRegistry.Clear();
            var errors = DefinitionBootstrap.Load(System.IO.Path.Combine(Application.streamingAssetsPath, "definitions"));
            Assert.IsEmpty(errors, "definition load errors");
        }

        [TearDown]
        public void TearDown()
        {
            // The loaded scene outlives the test; without this its SaveGame would write on editor quit — to the real
            // path, once the override is cleared.
            foreach (var save in Object.FindObjectsByType<Isle.UI.Prototype.SaveGame>(FindObjectsSortMode.None))
                Object.DestroyImmediate(save);
            if (System.IO.File.Exists(ScratchSave)) System.IO.File.Delete(ScratchSave);
            Isle.UI.Prototype.SaveGame.FilePathOverride = null;
            Isle.UI.Prototype.GameSession.SkipMenu = false;
            LootPiles.Clear();
            StructureFactory.Clear();
            DefRegistry.Clear();
        }

        [UnityTest]
        public IEnumerator SampleScene_HostRuns_WithoutErrors()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");

            yield return WaitUntil(() => IslandWorld.Instance != null, 10f, "IslandWorld never installed");
            yield return WaitUntil(() => InstanceFinder.IsServerStarted && InstanceFinder.IsClientStarted, 15f, "host never started");
            yield return WaitUntil(() => FindLocalPlayer() != null, 15f, "local player never spawned");

            for (var i = 0; i < 180; i++) yield return null;

            // SYS-START-01: a fresh island starts on the beach with wreckage around. The random passenger's traits and
            // skills would skew every number below, so the rest of the run plays a blank survivor.
            Assert.IsNotNull(FindLocalPlayer().Survivor, "no survivor applied on a fresh island");
            Assert.GreaterOrEqual(LootPiles.All.Count(p => p.Shape == "wreckage"), 4, "no wreckage on the beach");
            LootPiles.Clear();
            StartDirector.Become(FindLocalPlayer(), new Survivor { Name = "Test" }, setSkills: false);
            foreach (var skill in DefRegistry.All<SkillDef>()) FindLocalPlayer().Skills.Restore(skill.Id, 0);
            FindLocalPlayer().GetComponent<InventoryNetwork>().Slots.Clear(); // the start clothes would add warmth and armor to every check

            Assert.Greater(CreatureDirector.Instance.Creatures.Count, 0, "no creatures spawned in the live scene");
            Assert.IsNotNull(FindLocalPlayer().GetComponent<Vitals>());

            // SYS-MAP-01: the spawn is revealed within a moment, the far side of the map isn't.
            var map = Isle.UI.Prototype.MapState.Instance;
            Assert.IsNotNull(map, "MapState not installed");
            Assert.IsTrue(map.IsExplored(FindLocalPlayer().transform.position), "spawn never revealed");
            Assert.IsFalse(map.IsExplored(FindLocalPlayer().transform.position + new Vector3(400f, 400f)), "fog revealed too far");

            // Same session: FishNet can't re-initialise SampleScene's placed Campfire on a second load, so the
            // gameplay flow runs here rather than in its own test.
            yield return GatherCraftDrinkHunt();
            yield return EnterClearAndLeaveADungeon();
        }

        /// <summary>Drives the real ServerRpc paths on the host: harvest a tree and a rock, craft a spear from
        /// them, drink seawater, and kill a rabbit with fists. Nodes are picked away from water so the interact
        /// key's drink-first priority doesn't take over.</summary>
        IEnumerator GatherCraftDrinkHunt()
        {
            var player = FindLocalPlayer();
            var inventory = player.GetComponent<Isle.Gameplay.Inventory.InventoryNetwork>();
            var vitals = player.GetComponent<Vitals>();
            var world = IslandWorld.Instance;
            inventory.Bag.Clear();

            // Harvests take time now (SYS-WORLD-03); a master gatherer keeps the test short (tree 10 s → 3 s).
            player.Skills.Restore(NamespacedId.Parse("isle:gathering"), Isle.Gameplay.Skills.XpCurve.TotalXpTo(50));
            yield return HarvestNearest(player, world, "isle:tree");
            // Standing trees block their trunk; a felled one doesn't.
            var standingTree = world.Nodes.First(n => n.Def.Id.Value == "isle:tree" && n.UsesLeft > 0);
            Assert.IsFalse(world.IsWalkable(standingTree.Position), "walked through a tree trunk");
            var felled = world.Nodes.First(n => n.Def.Id.Value == "isle:tree" && n.UsesLeft <= 0);
            Assert.IsTrue(world.IsWalkable(felled.Position), "a felled tree still blocks");
            yield return HarvestNearest(player, world, "isle:rock");
            Assert.AreEqual(4, CountOf(inventory, "isle:wood"), "tree harvest");
            Assert.Greater(player.Skills.TotalXp(NamespacedId.Parse("isle:gathering")), 0d, "harvesting earned no gathering XP");
            Assert.AreEqual(2, CountOf(inventory, "isle:stone"), "rock harvest");

            player.RequestCraft("isle:craft_spear", "isle:stone");
            yield return WaitUntil(() => CountOf(inventory, "isle:spear__stone") == 1, 3f, "spear never crafted");
            Assert.AreEqual(2, CountOf(inventory, "isle:wood"), "wood not consumed");
            // SYS-CRAFT-01 §Quality: crafted gear carries its tier, and its durability is scaled by it.
            var spear = inventory.Bag.Placements.First(p => p.Item.Id.Value == "isle:spear__stone");
            Assert.IsNotNull(spear.Wear?.Quality, "crafted spear has no quality");
            Assert.AreEqual(Isle.Gameplay.Crafting.QualityCalculator.MaxDurability(spear.Item.Durability.Value, spear.Wear.Quality.Value), spear.Wear.Max,
                "durability not scaled by quality");

            // Drinking is from terrain water now: stand on the shore and E drinks the sea.
            Teleport(player, CoastSpot(world).Shore);
            var thirstBefore = vitals.Thirst;
            player.RequestInteract();
            yield return WaitUntil(() => vitals.Thirst < thirstBefore - 10f, 3f, "seawater didn't lower thirst");

            // Rabbit weight rolls 1.5–5 kg, so a heavy one survives a single punch: keep after it.
            var rabbit = CreatureDirector.Instance.Creatures.First(c => c.Def.Id.Value == "isle:rabbit");
            for (var attempt = 0; attempt < 5 && !rabbit.Dead; attempt++)
            {
                Teleport(player, rabbit.Position + Vector2.right * 0.5f);
                player.RequestAttack();
                yield return new WaitForSeconds(0.8f);
            }
            // SYS-HUNT-01: the kill leaves a carcass; butchering it (E, takes a moment) gives the meat — at least one piece.
            Assert.IsTrue(rabbit.Dead, "rabbit never died");
            Assert.Contains(rabbit, CreatureDirector.Instance.Carcasses.ToList(), "no carcass left");
            Assert.AreEqual(0, CountOf(inventory, "isle:raw_meat"), "the kill itself handed out meat");
            // SYS-HUNT-01 §Carrying: a rabbit is light — G puts it in the bag; dropping it lays the carcass back down.
            Teleport(player, rabbit.Position + Vector2.right * 0.3f);
            player.RequestHaul();
            yield return WaitUntil(() => CountOf(inventory, "isle:carcass__rabbit") == 1, 2f, "rabbit carcass never picked up");
            Assert.IsFalse(CreatureDirector.Instance.Carcasses.Contains(rabbit), "picked-up carcass still on the ground");
            var carried = inventory.Containers().SelectMany(c => c.Placements).First(pl => pl.Item.Id.Value == "isle:carcass__rabbit");
            Assert.AreEqual(rabbit.Weight, carried.Wear.Carcass.WeightKg, 1e-4f, "carried body lost its weight");
            var bagIndex = inventory.Containers().IndexOf(inventory.Containers().First(c => c.Placements.Contains(carried)));
            var carcassDropped = false;
            inventory.RequestDrop(bagIndex, carried.Position, 1, ok => carcassDropped = ok);
            yield return WaitUntil(() => carcassDropped, 2f, "carcass never dropped");
            rabbit = CreatureDirector.Instance.Carcasses.OrderBy(c => Vector2.Distance(c.Position, player.transform.position)).First();
            Assert.AreEqual("isle:rabbit", rabbit.Def.Id.Value, "dropping the bag item didn't lay a carcass down");
            player.RequestInteract();
            yield return WaitUntil(() => player.Butchering != null, 2f, "butchering never started");
            yield return WaitUntil(() => CountOf(inventory, "isle:raw_meat") > 0, 5f, "butchering gave no meat");
            Assert.IsFalse(CreatureDirector.Instance.Carcasses.Contains(rabbit), "carcass still there after butchering");

            // A heavy carcass is dragged (×0.4 speed, follows behind), and left a day it rots: only rotten meat comes off.
            var boarDef = DefRegistry.Get<CreatureDef>(NamespacedId.Parse("isle:boar"));
            var boar = CreatureDirector.Instance.PutCarcass(new Isle.Gameplay.Hunting.CarcassState { Def = boarDef, WeightKg = 62f }, (Vector2)player.transform.position + Vector2.right * 0.5f);
            player.RequestHaul();
            yield return WaitUntil(() => player.Hauling == boar, 2f, "boar never dragged");
            Assert.AreEqual(Isle.Gameplay.Hunting.CarcassCalculator.DragSpeedMult, player.HaulSpeed, 1e-5f);
            Teleport(player, (Vector2)player.transform.position + Vector2.up * 3f);
            yield return WaitUntil(() => Vector2.Distance(boar.Position, player.transform.position) < 1.2f, 2f, "dragged carcass didn't follow");
            player.RequestHaul();
            yield return WaitUntil(() => player.Hauling == null, 2f, "boar never put down");
            boar.ScavengersDrawn = int.MaxValue; // the scent's predators are checked in EditMode; here they'd only interrupt
            var dayClock = Isle.World.Time.WorldTime.Instance.Clock;
            // Half an hour at a time, so it stops in the rotten band instead of rotting away entirely (heat speeds it up).
            for (var step = 0; step < 80 && boar.Spoilage < Isle.Gameplay.Hunting.CarcassCalculator.SpoilRotten; step++)
            {
                dayClock.SetTotalMinutes(dayClock.TotalMinutes + 30);
                yield return null;
                yield return null;
            }
            Assert.That(boar.Spoilage, Is.InRange(Isle.Gameplay.Hunting.CarcassCalculator.SpoilRotten, Isle.Gameplay.Hunting.CarcassCalculator.SpoilGone), "carcass never spoiled");
            var rottenBefore = CountOf(inventory, "isle:rotten_food");
            Teleport(player, boar.Position + Vector2.right * 0.4f);
            player.RequestInteract();
            yield return WaitUntil(() => !CreatureDirector.Instance.Carcasses.Contains(boar), 30f, "rotten boar never butchered");
            Assert.Greater(CountOf(inventory, "isle:rotten_food"), rottenBefore, "a rotten carcass gave no rotten meat");
            Assert.AreEqual(0, CountOf(inventory, "isle:boar_hide"), "a rotten carcass still gave hide");

            player.RequestEquipItem("isle:spear__stone");
            yield return WaitUntil(() => inventory.Slots.Get("main_hand")?.Id.Value == "isle:spear__stone", 3f, "spear never wielded");
            Assert.AreEqual(0, CountOf(inventory, "isle:spear__stone"), "wielded spear still in bag");

            // SYS-COMBAT-01 §Melee: raise the guard toward a strike from the right — a fresh guard parries, a held one
            // blocks for stamina, and a hit from behind gets through. Then three swings in rhythm make a full combo.
            var guardAt = (Vector2)player.transform.position;
            player.RequestBlock(true, Vector2.right);
            yield return WaitUntil(() => player.Blocking, 2f, "guard never went up");
            // Read just before the strike: vitals ticks (cold, hunger) may shave health while the guard goes up.
            var guardHealth = vitals.Health;
            Assert.AreEqual(BlockOutcome.Parried, player.ReceiveCreatureStrike(guardAt + Vector2.right, 10f), "fresh guard didn't parry");
            Assert.AreEqual(guardHealth, vitals.Health, 1e-3f, "parry let damage through");
            yield return new WaitForSeconds(0.5f);
            guardHealth = vitals.Health;
            var guardStamina = vitals.Stamina;
            Assert.AreEqual(BlockOutcome.Blocked, player.ReceiveCreatureStrike(guardAt + Vector2.right, 10f), "held guard didn't block");
            Assert.Less(vitals.Stamina, guardStamina, "block cost no stamina");
            Assert.AreEqual(guardHealth, vitals.Health, 1e-3f, "block let damage through");
            Assert.AreEqual(BlockOutcome.None, player.ReceiveCreatureStrike(guardAt + Vector2.left, 10f), "guard covered the back");
            Assert.Less(vitals.Health, guardHealth, "hit from behind did no damage");
            player.RequestBlock(false, Vector2.right);
            yield return WaitUntil(() => !player.Blocking, 2f, "guard never came down");

            Teleport(player, CoastSpot(world).Shore); // open ground, nothing to hit
            vitals.ResetOnRespawn();
            for (var swing = 1; swing <= 3; swing++)
            {
                player.RequestAttack();
                var expected = swing;
                yield return WaitUntil(() => player.ComboStep == expected, 3f, $"combo step {expected} never landed");
                yield return new WaitForSeconds(1.3f); // spear: 0.8 swings/s
            }
            Assert.AreEqual(3, player.ComboLength);

            // SYS-INV-01 grid inventory: the Tab screen is bound to this player, and a stack moves from the base carry
            // into a worn backpack through the server (InventoryNetwork.RequestMove).
            Assert.IsNotNull(Isle.UI.Inventory.InventoryScreen.Instance, "inventory screen not installed");
            Give(inventory, "isle:straw_backpack", 1);
            player.RequestEquipItem("isle:straw_backpack");
            yield return WaitUntil(() => inventory.Slots.BagFor("back") != null, 3f, "backpack never worn");
            Give(inventory, "isle:fiber", 3);
            var fiber = inventory.Bag.Placements.First(p => p.Item.Id.Value == "isle:fiber");
            bool? moved = null;
            inventory.RequestMove(0, fiber.Position, 1, new Isle.Core.Vec2Int(0, 0), false, fiber.Count, ok => moved = ok);
            yield return WaitUntil(() => moved.HasValue, 3f, "move never acknowledged");
            Assert.IsTrue(moved.Value, "server refused the bag → backpack move");
            Assert.IsTrue(inventory.Slots.BagFor("back").Placements.Any(p => p.Item.Id.Value == "isle:fiber"), "fiber not in the backpack");

            // Dropping: the stack leaves the backpack and lands in a pile at the player's feet.
            var inPack = inventory.Slots.BagFor("back").Placements.First(p => p.Item.Id.Value == "isle:fiber");
            var pilesBefore = LootPiles.All.Count;
            bool? dropped = null;
            inventory.RequestDrop(1, inPack.Position, inPack.Count, ok => dropped = ok);
            yield return WaitUntil(() => dropped.HasValue, 3f, "drop never acknowledged");
            Assert.IsTrue(dropped.Value, "server refused the drop");
            Assert.IsFalse(inventory.Slots.BagFor("back").Placements.Any(p => p.Item.Id.Value == "isle:fiber"), "dropped fiber still in the backpack");
            Assert.AreEqual(pilesBefore + 1, LootPiles.All.Count, "no pile for the dropped fiber");
            player.RequestInteract(); // pick it back up so the later loot-pile checks start clean
            yield return WaitUntil(() => LootPiles.All.Count == pilesBefore, 3f, "dropped pile never picked up");

            // Lit directly: walking up and pressing E could hit a tree first if one stands within reach.
            var fire = Object.FindObjectsByType<WorldObjectInstance>(FindObjectsSortMode.None).First(f => f.HasTag("station/campfire"));
            fire.IsActive = true;
            Teleport(player, fire.transform.position + Vector3.down);
            var rawBefore = CountOf(inventory, "isle:raw_meat");
            // SYS-COOK-01: grill one raw meat at the lit campfire → a generated dish; eating it grants the grill's
            // meat reaction (endurance) and its cooked nutrition.
            player.RequestCook("isle:grill", new[] { "isle:raw_meat" });
            yield return WaitUntil(() => Dish(inventory) != null, 3f, "meat never cooked");
            Assert.AreEqual(rawBefore - 1, CountOf(inventory, "isle:raw_meat"));
            var dish = Dish(inventory);
            Assert.Greater(dish.Nutrition.Hunger, 0f);

            player.RequestUse(dish.Id.Value);
            yield return WaitUntil(() => Dish(inventory) == null, 3f, "dish never eaten");
            Assert.IsTrue(vitals.ActiveBuffs().Any(b => b.Id.Value == "isle:endurance"), "grilled meat granted no endurance");

            // Building: place a crate, store wood in it, take it back. Kits are given directly — the craft path is
            // already covered above.
            Give(inventory, "isle:crate_kit", 1);
            var crateAt = (Vector2)player.transform.position + Vector2.right * 1.5f;
            Assert.IsTrue(player.CanPlaceAt(crateAt), "crate spot rejected");
            player.RequestPlace("isle:crate_kit", crateAt);
            yield return WaitUntil(() => StructureFactory.NearestStorage(player.transform.position, 2f) != null, 3f, "crate never placed");
            var crate = StructureFactory.NearestStorage(player.transform.position, 2f);
            Assert.AreEqual(0, CountOf(inventory, "isle:crate_kit"), "kit not consumed");

            Give(inventory, "isle:wood", 5);
            var woodHeld = CountOf(inventory, "isle:wood");
            player.RequestStore("isle:wood");
            yield return WaitUntil(() => crate.Contents.Placements.Any(pl => pl.Item.Id.Value == "isle:wood"), 3f, "wood never stored");
            Assert.AreEqual(0, CountOf(inventory, "isle:wood"));
            player.RequestTake("isle:wood");
            yield return WaitUntil(() => CountOf(inventory, "isle:wood") == woodHeld, 3f, "wood never taken back");

            // Clothing (SYS-CRAFT-02): a wolf-fur parka — warmth and per-type armor come from the template × the material.
            var parka = DefRegistry.Get<ItemDef>(NamespacedId.Parse("isle:parka__wolf_fur"));
            Assert.AreEqual(6f * 2.5f, parka.Warmth, 0.01f, "parka warmth = 6 × wolf fur 2.5");
            Give(inventory, parka.Id.Value, 1);
            player.RequestEquipItem(parka.Id.Value);
            yield return WaitUntil(() => Mathf.Approximately(vitals.ClothingBonus, parka.Warmth), 3f, "parka warmth never applied");

            // Armor: the hit is blunt, so the parka's blunt armor applies (SYS-COMBAT-01 §Damage curve).
            var healthBefore = vitals.Health;
            vitals.TakeDamage(10f);
            var expectedHit = Isle.Gameplay.Combat.DamageTypes.Damage(10f, "blunt", 1f, parka.ArmorTypes["blunt"]);
            Assert.AreEqual(expectedHit, healthBefore - vitals.Health, 0.3f, "armor not applied");

            // Backpack: equipping one adds a second container that items can go into.
            Give(inventory, "isle:straw_backpack", 1);
            player.RequestEquipItem("isle:straw_backpack");
            yield return WaitUntil(() => inventory.Containers().Count == 2, 3f, "backpack never opened a grid");

            // Tool: a hatchet in hand adds the tree's tool_bonus (2 → 4 wood).
            Give(inventory, "isle:hatchet__stone", 1);
            player.RequestEquipItem("isle:hatchet__stone");
            yield return WaitUntil(() => inventory.Slots.Get("main_hand")?.Id.Value == "isle:hatchet__stone", 3f, "hatchet never equipped");
            var woodBefore = CountOf(inventory, "isle:wood");
            yield return HarvestNearest(player, world, "isle:tree");
            Assert.AreEqual(woodBefore + 6, CountOf(inventory, "isle:wood"), "hatchet bonus not applied");

            // SYS-CRAFT-02: the harvest wore the hatchet and the hit wore the cloak; a broken hatchet is no tool; a repair
            // costs half the recipe and lowers the maximum to ×0.92.
            Assert.AreEqual(59, inventory.Slots.WearOf("main_hand").Current, "harvest didn't wear the hatchet");
            Assert.AreEqual(59, inventory.Slots.WearOf("chest").Current, "hit didn't wear the cloak");
            inventory.Slots.Wear("main_hand", 59);
            Assert.IsNull(inventory.Slots.Working("main_hand"), "broken hatchet still works");
            woodBefore = CountOf(inventory, "isle:wood");
            yield return HarvestNearest(player, world, "isle:tree");
            Assert.AreEqual(woodBefore + 4, CountOf(inventory, "isle:wood"), "broken hatchet still gave its bonus");
            Give(inventory, "isle:stone", 1);
            Give(inventory, "isle:fiber", 1);
            player.RequestRepairEquipped("main_hand");
            yield return WaitUntil(() => inventory.Slots.WearOf("main_hand").Max == 55, 3f, "hatchet never repaired");
            Assert.AreEqual(55, inventory.Slots.WearOf("main_hand").Current);

            // Tiered veins: an iron vein refuses a stone pickaxe and yields to a copper one.
            Give(inventory, "isle:pickaxe__stone", 1);
            player.RequestEquipItem("isle:pickaxe__stone");
            yield return WaitUntil(() => inventory.Slots.Get("main_hand")?.Id.Value == "isle:pickaxe__stone", 3f, "pickaxe never equipped");
            var vein = world.Nodes.FirstOrDefault(n => n.Def.Id.Value == "isle:iron_vein" && n.IsHarvestable && world.NearestWater(n.Position, 3f) == null);
            Assert.IsNotNull(vein, "no iron vein on the island");
            Teleport(player, vein.Position);
            player.RequestInteract();
            yield return new WaitForSeconds(0.5f);
            Assert.IsNull(player.Gathering, "stone pickaxe started on an iron vein");
            // The better pickaxe only sits in the bag: working the vein takes it out on its own.
            Give(inventory, "isle:pickaxe__copper", 1);
            yield return HarvestNearest(player, world, "isle:iron_vein");
            Assert.AreEqual("isle:pickaxe__copper", inventory.Slots.Get("main_hand")?.Id.Value, "best pickaxe wasn't taken out");
            Assert.AreEqual(1, CountOf(inventory, "isle:pickaxe__stone"), "the stone pickaxe didn't go back in the bag");
            Assert.AreEqual(3, CountOf(inventory, "isle:iron_ore"), "iron vein: 2 + 1 tool bonus");

            // Smelting at a furnace.
            player.Skills.Restore(NamespacedId.Parse("isle:crafting"), Isle.Gameplay.Skills.XpCurve.TotalXpTo(20));
            Give(inventory, "isle:furnace_kit", 1);
            var furnaceAt = (Vector2)player.transform.position + Vector2.right * 1.5f;
            if (!player.CanPlaceAt(furnaceAt)) furnaceAt = (Vector2)player.transform.position + Vector2.left * 1.5f;
            player.RequestPlace("isle:furnace_kit", furnaceAt);
            yield return WaitUntil(() => StructureFactory.Built.Any(b => b != null && b.Def != null && b.Def.Id.Value == "isle:furnace"), 3f, "furnace never placed");
            Give(inventory, "isle:coal", 1);
            player.RequestCraft("isle:craft_iron_ingot");
            yield return WaitUntil(() => CountOf(inventory, "isle:iron_ingot") == 1, 3f, "no iron ingot smelted");
            Assert.AreEqual(1, CountOf(inventory, "isle:iron_ore"), "smelting took 2 ore");

            // Ranged: equip a bow, draw for a full charge, loose. Sway makes the hit itself random at level 0, so
            // the check is that an arrow was spent and a projectile flew and expired.
            Give(inventory, "isle:short_bow", 1);
            Give(inventory, "isle:arrow__stone", 3);
            Assert.AreEqual(3, CountOf(inventory, "isle:arrow__stone"), "test setup: arrows didn't fit");
            player.RequestEquipItem("isle:short_bow");
            yield return WaitUntil(() => player.HoldsRangedWeapon(), 3f, "bow never equipped");
            player.RequestBeginDraw();
            yield return new WaitForSeconds(0.9f);
            player.RequestLoose((Vector2)player.transform.position + Vector2.left * 5f);
            yield return WaitUntil(() => CountOf(inventory, "isle:arrow__stone") == 2, 3f, "no arrow spent");
            // In flight — unless it already struck something close (after the day skip above, animals roam nearby).
            Assert.LessOrEqual(Isle.Gameplay.Combat.Projectiles.Instance.InFlight, 1, "more than one projectile from one shot");
            yield return WaitUntil(() => Isle.Gameplay.Combat.Projectiles.Instance.InFlight == 0, 3f, "arrow never landed");

            // Farming: plant seeds, let a full growth period pass on the world clock, harvest.
            Give(inventory, "isle:berry_seeds", 1);
            var plotAt = (Vector2)player.transform.position + Vector2.down * 1.5f;
            Assert.IsTrue(player.CanPlaceAt(plotAt), "plot spot rejected");
            player.RequestPlace("isle:berry_seeds", plotAt);
            yield return WaitUntil(() => StructureFactory.Built.Any(b => b != null && b.GetComponent<CropPlot>() != null), 3f, "seeds never planted");
            var plot = StructureFactory.Built.First(b => b != null && b.GetComponent<CropPlot>() != null).GetComponent<CropPlot>();
            Assert.IsFalse(CropCalculator.IsRipe(plot.Growth), "crop ripe at planting");
            var clock = Isle.World.Time.WorldTime.Instance.Clock;
            clock.SetTotalMinutes(clock.TotalMinutes + 1440);
            Assert.IsTrue(CropCalculator.IsRipe(plot.Growth), "crop not ripe after its growth days");
            var berriesBefore = CountOf(inventory, "isle:berries");
            plot.Interact(player.gameObject);
            Assert.AreEqual(berriesBefore + 6, CountOf(inventory, "isle:berries"), "harvest didn't give output.base_count");

            // Rod fishing: a fight starts, runs on the server, and ends. No mouse in batchmode, so nobody reels and the
            // hook slips — the point is the session lifecycle, not the catch.
            // No rod, no fishing; a rod in the bag comes out on its own when casting.
            var (shore, sea) = CoastSpot(world);
            Teleport(player, shore);
            player.RequestCast(sea);
            yield return new WaitForSeconds(0.5f);
            Assert.IsNull(player.Cast, "cast without a rod");
            Give(inventory, "isle:fishing_rod", 1);
            // Cast → bite → hook (SYS-FISH-01 prototype revision). Below Fishing 5 the rod fishes as a handline:
            // hooking lands the fish with no fight.
            var fishBefore = CountOf(inventory, "isle:raw_fish");
            yield return CastAndHook(player, sea);
            Assert.IsNull(player.Fight, "rod worked below Fishing 5");
            Assert.AreEqual("isle:fishing_rod", inventory.Slots.Get("main_hand")?.Id.Value, "rod wasn't taken out to cast");
            yield return WaitUntil(() => CountOf(inventory, "isle:raw_fish") > fishBefore, 2f, "handline hook landed nothing");

            player.Skills.Restore(NamespacedId.Parse("isle:fishing"), Isle.Gameplay.Skills.XpCurve.TotalXpTo(5));
            yield return CastAndHook(player, sea);
            yield return WaitUntil(() => player.Fight != null, 3f, "no fight started");
            Assert.Greater(player.FightWeightKg, 0f);
            yield return WaitUntil(() => player.Fight == null, 8f, "fight never ended");

            // Death: everything drops into one pile, the body can't move, and respawn brings it back.
            var death = player.GetComponent<DeathHandler>();
            var deathSpot = (Vector2)player.transform.position;
            vitals.TakeDamage(1000f);
            yield return WaitUntil(() => death.IsDead, 3f, "player never died");
            Assert.AreEqual(0, inventory.Bag.Placements.Count, "bag not emptied on death");
            Assert.IsNull(inventory.Slots.Get("main_hand"), "hand not emptied on death");
            var pile = LootPiles.Nearest(deathSpot, 1f);
            Assert.IsNotNull(pile, "no loot pile at the death spot");
            Assert.IsTrue(pile.Items.Any(i => i.Item.Id.Value == "isle:spear__stone"), "equipped spear not in the pile");
            Assert.IsTrue(player.GetComponent<PlayerMovement>().Frozen, "dead player can still move");

            yield return WaitUntil(() => !death.IsDead, DeathHandler.RespawnDelaySeconds + 3f, "never respawned");
            Assert.AreEqual(VitalsCalculator.GaugeMax, vitals.Health, 0.01f);
            Assert.IsFalse(player.GetComponent<PlayerMovement>().Frozen);

            Teleport(player, deathSpot);
            player.RequestInteract();
            yield return WaitUntil(() => LootPiles.All.Count == 0, 3f, "pile never picked up");
            // The repaired hatchet came back through unequip → bag → death pile with its wear — into the bag, or straight
            // back into the empty main hand (recovering a body re-equips gear first).
            var hatchetWear = inventory.Containers().SelectMany(c => c.Placements).Where(pl => pl.Item.Id.Value == "isle:hatchet__stone").Select(pl => pl.Wear)
                .Append(inventory.Slots.Get("main_hand")?.Id.Value == "isle:hatchet__stone" ? inventory.Slots.WearOf("main_hand") : null);
            Assert.IsTrue(hatchetWear.Any(w => w != null && w.Max == 55), "hatchet wear lost through the death pile");
            // Recovering a body re-equips gear into empty slots first, so the spear may be back in hand instead of the bag.
            var spearBack = CountOf(inventory, "isle:spear__stone") + (inventory.Slots.Get("main_hand")?.Id.Value == "isle:spear__stone" ? 1 : 0);
            Assert.AreEqual(1, spearBack, "spear not recovered from the pile");
        }

        /// <summary>SYS-DUNG-01 (T-201): walk into an entrance, take each lock's key and see the door open, clear the
        /// vault's soft gate as a specialist, then leave by the entrance stairs.</summary>
        IEnumerator EnterClearAndLeaveADungeon()
        {
            var player = FindLocalPlayer();
            var world = IslandWorld.Instance;
            var dungeons = Isle.Gameplay.Dungeons.DungeonDirector.Instance;
            Assert.IsNotNull(dungeons, "DungeonDirector not installed");
            yield return WaitUntil(() => dungeons.Sites.Count > 0, 5f, "no dungeon entrance placed");
            var site = dungeons.Sites.FirstOrDefault(s => s.Def.Id.Value == "isle:tidal_grotto") ?? dungeons.Sites[0];
            Assert.IsTrue(world.IsWalkable(site.Entrance), "entrance placed off land");

            Teleport(player, site.Entrance + Vector2.down);
            player.RequestInteract();
            yield return WaitUntil(() => dungeons.IsUnderground(player.transform.position), 3f, "never went down");
            var floor = dungeons.FloorAt(player.transform.position);
            Assert.IsTrue(world.IsWalkable(player.transform.position), "landed on a wall");

            foreach (var barrier in floor.Barriers.Where(b => b.LockId >= 0))
            {
                Assert.IsFalse(world.IsWalkable(barrier.Position), "a locked door can be walked through");
                var key = floor.Keys.First(k => k.LockId == barrier.LockId);
                Teleport(player, key.Position);
                player.RequestInteract();
                yield return WaitUntil(() => barrier.Open, 3f, "taking the key never opened its door");
                Assert.IsTrue(world.IsWalkable(barrier.Position), "opened door still blocks");
            }

            var gate = floor.Barriers.Single(b => b.LockId < 0);
            var skill = site.Def.Gate.Skill;
            player.Skills.Restore(skill, Isle.Gameplay.Skills.XpCurve.TotalXpTo(50));
            var edge = floor.Layout.Edges[gate.Edge];
            var a = floor.Layout.Rooms[edge.A].Cell;
            var b2 = floor.Layout.Rooms[edge.B].Cell;
            var door = Isle.World.Generation.DungeonTiles.DoorwayCentre(a, b2);
            var stand = a.Y == b2.Y ? new Isle.Core.Vec2Int(door.X + 1, door.Y) : new Isle.Core.Vec2Int(door.X, door.Y + 1);
            Teleport(player, floor.TileToWorld(stand));
            player.RequestInteract();
            yield return WaitUntil(() => gate.Open, 8f, "specialist never cleared the soft gate");

            // T-202 (Tidal Grotto only): high tide floods water tiles and slows swimmers; low tide opens the cache.
            if (site.Def.Tides != null)
            {
                var clock = Isle.World.Time.WorldTime.Instance.Clock;
                var day = clock.TotalMinutes / Isle.World.Time.WorldClock.MinutesPerDay;
                clock.SetTotalMinutes(day * Isle.World.Time.WorldClock.MinutesPerDay + 6 * 60);
                yield return WaitUntil(() => site.TideHigh, 2f, "tide never came in at 06:00");
                var water = floor.TileToWorld(floor.WaterTiles.First());
                Assert.IsTrue(dungeons.IsWater(water), "high tide left a flooded room dry");
                Assert.AreEqual(site.Def.Tides.SwimSpeed, Isle.Networking.PlayerMovement.TerrainSpeed(water), 1e-4f, "swimming isn't slowed");
                Assert.IsFalse(floor.Cache != null && floor.Cache.View.activeSelf, "cache visible under water");

                clock.SetTotalMinutes(day * Isle.World.Time.WorldClock.MinutesPerDay + 12 * 60);
                yield return WaitUntil(() => !site.TideHigh, 2f, "tide never went out at 12:00");
                Assert.IsFalse(dungeons.IsWater(water));
                Assert.IsNotNull(floor.Cache, "no low-tide cache on the floor");
                Teleport(player, floor.Cache.Position);
                player.RequestInteract();
                yield return WaitUntil(() => floor.Cache.Taken, 3f, "low-tide cache never opened");
                Assert.That(CountOf(player.GetComponent<InventoryNetwork>(), "isle:tide_pearl") + LootPiles.All.Sum(pile => pile.Items.Where(i => i.Item.Id.Value == "isle:tide_pearl").Sum(i => i.Count)), Is.InRange(1, 2), "cache didn't hold 1–2 tide pearls");
            }

            // The boss holds the last floor's way out until it falls; its drops land where it died.
            if (site.Def.Boss.IsValid && site.Def.Floors == 1)
            {
                var boss = site.Boss;
                Assert.IsNotNull(boss, "boss never spawned");
                Assert.AreEqual(600f, boss.MaxHealth, 0.01f, "Hermit Colossus HP");
                var exits = floor.Portals.Count(p => p.Kind == Isle.Gameplay.Dungeons.DungeonDirector.DoorKind.Exit);
                var bossAt = boss.Position;
                CreatureDirector.Instance.Damage(boss, 100000f, new System.Collections.Generic.List<(NamespacedId, int)>());
                yield return WaitUntil(() => site.BossDefeated, 2f, "boss death never registered");
                Assert.AreEqual(exits + 1, floor.Portals.Count(p => p.Kind == Isle.Gameplay.Dungeons.DungeonDirector.DoorKind.Exit), "no way out opened in the boss room");
                var pile = LootPiles.Nearest(bossAt, 1.5f);
                Assert.IsNotNull(pile, "boss dropped nothing");
                Assert.IsTrue(pile.Items.Any(i => i.Item.Id.Value == "isle:tide_sigil"), "no sigil in the boss drop");
                Assert.AreEqual(3, pile.Items.Where(i => i.Item.Id.Value == "isle:tide_pearl").Sum(i => i.Count), "boss pearls");
            }

            var up = floor.Portals.First(p => p.Kind == Isle.Gameplay.Dungeons.DungeonDirector.DoorKind.Exit);
            Teleport(player, up.Position);
            player.RequestInteract();
            yield return WaitUntil(() => !dungeons.IsUnderground(player.transform.position), 3f, "never came back up");
            Assert.Less(Vector2.Distance(player.transform.position, site.Entrance), 4f, "didn't surface at the entrance");
        }

        IEnumerator HarvestNearest(PlayerInteraction player, IslandWorld world, string defId)
        {
            var node = world.Nodes.First(n => n.Def.Id.Value == defId && n.IsHarvestable &&
                world.NearestWater(n.Position, 3f) == null &&
                !world.Nodes.Any(o => o != n && o.IsHarvestable && Vector2.Distance(o.Position, n.Position) < 2.5f));
            Teleport(player, node.Position);
            var usesBefore = node.UsesLeft;
            player.RequestInteract();
            yield return WaitUntil(() => node.UsesLeft < usesBefore, 15f, defId + " was never harvested");
        }

        static ItemDef Dish(InventoryNetwork inventory) =>
            inventory.Containers().SelectMany(c => c.Placements).Select(p => p.Item).FirstOrDefault(i => i.Tags != null && i.Tags.Contains("dish"));

        static void Give(InventoryNetwork inventory, string itemId, int count)
        {
            var item = DefRegistry.Get<ItemDef>(NamespacedId.Parse(itemId));
            Assert.IsTrue(InventoryOps.TryGive(inventory.Containers(), item, count), $"test setup: no room for {itemId}");
        }

        /// <summary>A dry, node-free land tile right next to the sea, and that sea tile.</summary>
        static (Vector2 Shore, Vector2 Sea) CoastSpot(IslandWorld world)
        {
            var size = Isle.World.Generation.IslandGenerator.Size;
            foreach (var direction in new[] { new Isle.Core.Vec2Int(1, 0), new Isle.Core.Vec2Int(-1, 0), new Isle.Core.Vec2Int(0, 1), new Isle.Core.Vec2Int(0, -1) })
                for (var offset = -20; offset <= 20; offset += 4)
                {
                    var tile = new Isle.Core.Vec2Int(size / 2 + (direction.Y != 0 ? offset : 0), size / 2 + (direction.X != 0 ? offset : 0));
                    // Out from the middle until the sea (trees and ponds on the way don't stop the search).
                    for (var steps = 0; world.WaterAt(tile)?.Ocean != true && steps < size; steps++)
                        tile = new Isle.Core.Vec2Int(tile.X + direction.X, tile.Y + direction.Y);
                    if (world.WaterAt(tile)?.Ocean != true) continue;
                    var shore = new Isle.Core.Vec2Int(tile.X - direction.X, tile.Y - direction.Y);
                    var shoreWorld = IslandWorld.TileToWorld(shore);
                    if (!world.IsWalkable(shoreWorld) || world.Nodes.Any(n => Vector2.Distance(n.Position, shoreWorld) < 2.5f)) continue;
                    return (shoreWorld, IslandWorld.TileToWorld(tile));
                }
            Assert.Fail("no clear coast spot");
            return default;
        }

        static IEnumerator CastAndHook(PlayerInteraction player, Vector2 water)
        {
            player.RequestCast(water);
            yield return WaitUntil(() => player.Cast != null, 3f, "cast never landed");
            yield return WaitUntil(() => player.Cast == null || player.Cast.State == Isle.Gameplay.Fishing.CastState.Bite, 14f, "nothing ever bit");
            player.RequestHook();
            yield return WaitUntil(() => player.Cast == null, 3f, "hook never resolved");
        }

        static void Teleport(PlayerInteraction player, Vector2 position) =>
            player.transform.position = new Vector3(position.x, position.y, player.transform.position.z);

        static int CountOf(Isle.Gameplay.Inventory.InventoryNetwork inventory, string itemId) =>
            InventoryOps.Count(inventory.Containers(), NamespacedId.Parse(itemId));

        static PlayerInteraction FindLocalPlayer()
        {
            foreach (var player in Object.FindObjectsByType<PlayerInteraction>(FindObjectsSortMode.None))
                if (player.IsOwner) return player;
            return null;
        }

        static IEnumerator WaitUntil(System.Func<bool> condition, float timeoutSeconds, string failure)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail(failure);
                yield return null;
            }
        }
    }
}
