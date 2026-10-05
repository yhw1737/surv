using System.Collections;
using System.Linq;
using FishNet;
using Isle.Core.Ids;
using Isle.Data;
using Isle.Gameplay.Building;
using Isle.Gameplay.Character;
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

            Assert.Greater(CreatureDirector.Instance.Creatures.Count, 0, "no creatures spawned in the live scene");
            Assert.IsNotNull(FindLocalPlayer().GetComponent<Vitals>());

            // Same session: FishNet can't re-initialise SampleScene's placed Campfire on a second load, so the
            // gameplay flow runs here rather than in its own test.
            yield return GatherCraftDrinkHunt();
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

            yield return HarvestNearest(player, world, "isle:tree");
            yield return HarvestNearest(player, world, "isle:rock");
            Assert.AreEqual(2, CountOf(inventory, "isle:wood"), "tree harvest");
            Assert.AreEqual(1, CountOf(inventory, "isle:stone"), "rock harvest");

            player.RequestCraft("isle:craft_stone_spear");
            yield return WaitUntil(() => CountOf(inventory, "isle:stone_spear") == 1, 3f, "spear never crafted");
            Assert.AreEqual(0, CountOf(inventory, "isle:wood"), "wood not consumed");

            var sea = world.Nodes.First(n => n.Def.Id.Value == "isle:seawater");
            Teleport(player, sea.Position);
            var thirstBefore = vitals.Thirst;
            player.RequestInteract();
            yield return WaitUntil(() => vitals.Thirst < thirstBefore - 10f, 3f, "seawater didn't lower thirst");

            // Rabbit weight rolls 1.5–5 kg, so a heavy one survives a single punch: keep after it.
            var rabbit = CreatureDirector.Instance.Creatures.First(c => c.Def.Id.Value == "isle:rabbit");
            for (var attempt = 0; attempt < 5 && CountOf(inventory, "isle:raw_meat") == 0; attempt++)
            {
                Teleport(player, rabbit.Position + Vector2.right * 0.5f);
                player.RequestAttack();
                yield return new WaitForSeconds(0.8f);
            }
            Assert.Greater(CountOf(inventory, "isle:raw_meat"), 0, "rabbit kill dropped no meat");

            player.RequestEquipItem("isle:stone_spear");
            yield return WaitUntil(() => inventory.Slots.Get("main_hand")?.Id.Value == "isle:stone_spear", 3f, "spear never wielded");
            Assert.AreEqual(0, CountOf(inventory, "isle:stone_spear"), "wielded spear still in bag");

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
            player.RequestStore("isle:wood");
            yield return WaitUntil(() => crate.Contents.Placements.Any(pl => pl.Item.Id.Value == "isle:wood"), 3f, "wood never stored");
            Assert.AreEqual(0, CountOf(inventory, "isle:wood"));
            player.RequestTake("isle:wood");
            yield return WaitUntil(() => CountOf(inventory, "isle:wood") == 5, 3f, "wood never taken back");

            // Clothing: a worn cloak feeds Vitals.ClothingBonus.
            Give(inventory, "isle:fur_cloak", 1);
            player.RequestEquipItem("isle:fur_cloak");
            yield return WaitUntil(() => Mathf.Approximately(vitals.ClothingBonus, 6f), 3f, "cloak warmth never applied");

            // Armor: the cloak's 10 armor cuts damage to 60 / (60 + 10) of it (SYS-COMBAT-01 §Damage).
            Assert.AreEqual(10f, vitals.WornArmor(), 0.01f);
            var healthBefore = vitals.Health;
            vitals.TakeDamage(10f);
            Assert.AreEqual(10f * 60f / 70f, healthBefore - vitals.Health, 0.3f, "armor not applied");

            // Backpack: equipping one adds a second container that items can go into.
            Give(inventory, "isle:straw_backpack", 1);
            player.RequestEquipItem("isle:straw_backpack");
            yield return WaitUntil(() => inventory.Containers().Count == 2, 3f, "backpack never opened a grid");

            // Tool: a hatchet in hand adds the tree's tool_bonus (2 → 4 wood).
            Give(inventory, "isle:stone_hatchet", 1);
            player.RequestEquipItem("isle:stone_hatchet");
            yield return WaitUntil(() => inventory.Slots.Get("main_hand")?.Id.Value == "isle:stone_hatchet", 3f, "hatchet never equipped");
            var woodBefore = CountOf(inventory, "isle:wood");
            yield return HarvestNearest(player, world, "isle:tree");
            Assert.AreEqual(woodBefore + 4, CountOf(inventory, "isle:wood"), "hatchet bonus not applied");

            // Ranged: equip a bow, draw for a full charge, loose. Sway makes the hit itself random at level 0, so
            // the check is that an arrow was spent and a projectile flew and expired.
            Give(inventory, "isle:short_bow", 1);
            Give(inventory, "isle:arrow", 3);
            Assert.AreEqual(3, CountOf(inventory, "isle:arrow"), "test setup: arrows didn't fit");
            player.RequestEquipItem("isle:short_bow");
            yield return WaitUntil(() => player.HoldsRangedWeapon(), 3f, "bow never equipped");
            player.RequestBeginDraw();
            yield return new WaitForSeconds(0.9f);
            player.RequestLoose((Vector2)player.transform.position + Vector2.left * 5f);
            yield return WaitUntil(() => CountOf(inventory, "isle:arrow") == 2, 3f, "no arrow spent");
            Assert.AreEqual(1, Isle.Gameplay.Combat.Projectiles.Instance.InFlight, "no projectile in flight");
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
            Give(inventory, "isle:fishing_rod", 1);
            player.RequestEquipItem("isle:fishing_rod");
            yield return WaitUntil(() => inventory.Slots.Get("main_hand")?.Id.Value == "isle:fishing_rod", 3f, "rod never equipped");
            var seaSpot = world.Nodes.First(n => n.Def.Id.Value == "isle:seawater");
            Teleport(player, seaSpot.Position);
            yield return new WaitForSeconds(3.1f); // handline cast earlier set the cast cooldown
            player.RequestFish();
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
            Assert.IsTrue(pile.Items.Any(i => i.Item.Id.Value == "isle:stone_spear"), "equipped spear not in the pile");
            Assert.IsTrue(player.GetComponent<PlayerMovement>().Frozen, "dead player can still move");

            yield return WaitUntil(() => !death.IsDead, DeathHandler.RespawnDelaySeconds + 3f, "never respawned");
            Assert.AreEqual(VitalsCalculator.GaugeMax, vitals.Health, 0.01f);
            Assert.IsFalse(player.GetComponent<PlayerMovement>().Frozen);

            Teleport(player, deathSpot);
            player.RequestInteract();
            yield return WaitUntil(() => LootPiles.All.Count == 0, 3f, "pile never picked up");
            Assert.AreEqual(1, CountOf(inventory, "isle:stone_spear"), "spear not recovered from the pile");
        }

        IEnumerator HarvestNearest(PlayerInteraction player, IslandWorld world, string defId)
        {
            var node = world.Nodes.First(n => n.Def.Id.Value == defId && n.IsHarvestable &&
                !world.Nodes.Any(w => w.IsDrinkable && Vector2.Distance(w.Position, n.Position) < 3f) &&
                !world.Nodes.Any(o => o != n && o.IsHarvestable && Vector2.Distance(o.Position, n.Position) < 2.5f));
            Teleport(player, node.Position);
            var usesBefore = node.UsesLeft;
            player.RequestInteract();
            yield return WaitUntil(() => node.UsesLeft < usesBefore, 3f, defId + " was never harvested");
        }

        static ItemDef Dish(InventoryNetwork inventory) =>
            inventory.Containers().SelectMany(c => c.Placements).Select(p => p.Item).FirstOrDefault(i => i.Tags != null && i.Tags.Contains("dish"));

        static void Give(InventoryNetwork inventory, string itemId, int count)
        {
            var item = DefRegistry.Get<ItemDef>(NamespacedId.Parse(itemId));
            Assert.IsTrue(InventoryOps.TryGive(inventory.Containers(), item, count), $"test setup: no room for {itemId}");
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
