using System.Collections;
using System.Linq;
using Isle.Core.Ids;
using Isle.Data;
using Isle.Gameplay.Building;
using Isle.Gameplay.Character;
using Isle.Gameplay.Crafting;
using Isle.Gameplay.Hunting;
using Isle.Gameplay.Inventory;
using Isle.Modding.Defs;
using Isle.UI.Prototype;
using Isle.World.Island;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Isle.Tests.PlayMode
{
    /// <summary>
    /// T-150: quit mid-game and resume with the world, the inventory and the character intact. A rich state is set up,
    /// the game goes back to the menu (saving) and Continue rebuilds it; everything that can't simply be re-rolled must
    /// come back as it was — including timed buffs, wetness, food freshness, satiety, carcasses on the ground.
    /// </summary>
    public sealed class SaveRoundTripTests
    {
        static readonly string ScratchSave = System.IO.Path.Combine(Application.temporaryCachePath, "isle_roundtrip_save.json");

        [SetUp]
        public void SetUp()
        {
            GameSession.SkipMenu = false;
            SaveGame.FilePathOverride = ScratchSave;
            Delete();
            DefRegistry.Clear();
            Assert.IsEmpty(DefinitionBootstrap.Load(System.IO.Path.Combine(Application.streamingAssetsPath, "definitions")));
        }

        [TearDown]
        public void TearDown()
        {
            Isle.Networking.IsleNetworkManager.Find()?.StopHost();
            foreach (var save in Object.FindObjectsByType<SaveGame>(FindObjectsSortMode.None)) Object.DestroyImmediate(save);
            Delete();
            SaveGame.FilePathOverride = null;
            SaveGame.Slot = 1;
            LootPiles.Clear();
            StructureFactory.Clear();
            DefRegistry.Clear();
        }

        static void Delete()
        {
            if (System.IO.File.Exists(ScratchSave)) System.IO.File.Delete(ScratchSave);
        }

        [UnityTest]
        public IEnumerator Save_BackToMenu_Continue_RestoresTheGame()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return WaitUntil(() => Object.FindFirstObjectByType<MainMenu>() != null, 5f, "main menu never appeared");
            GameSession.StartNew(31337);
            yield return WaitUntil(() => PlayerInteraction.Local != null && PlayerInteraction.Local.Survivor != null, 15f, "player never started");
            for (var i = 0; i < 90; i++) yield return null;

            var player = PlayerInteraction.Local;
            var inventory = player.GetComponent<InventoryNetwork>();
            var vitals = player.GetComponent<Vitals>();
            var world = IslandWorld.Instance;
            var clock = Isle.World.Time.WorldTime.Instance.Clock;
            var crafting = NamespacedId.Parse("isle:crafting");

            // Character.
            player.Skills.Restore(crafting, 1234d);
            var buff = DefRegistry.All<BuffDef>().First(b => b.DurationMin > 60);
            vitals.Buffs.Grant(buff, clock.TotalMinutes);
            var buffExpires = vitals.Buffs.ExpiresAt(buff.Id);
            vitals.SetWet();
            vitals.Satiety.Eat("grill|meat", clock.TotalMinutes);
            vitals.Satiety.Eat("grill|meat", clock.TotalMinutes);

            // Inventory: worn crafted gear, a carried carcass, food partway to spoiling.
            inventory.Bag.Clear();
            var knife = DefRegistry.Get<ItemDef>(NamespacedId.Parse("isle:knife__copper"));
            Assert.IsTrue(InventoryOps.TryGive(inventory.Containers(), knife, 1, new ItemWear(37, 150) { Quality = QualityTier.Fine }));
            var rabbit = DefRegistry.Get<CreatureDef>(NamespacedId.Parse("isle:rabbit"));
            var carcassItem = DefRegistry.Get<ItemDef>(Isle.Modding.Defs.CarcassItems.IdFor(rabbit.Id));
            Assert.IsTrue(InventoryOps.TryGive(inventory.Containers(), carcassItem, 1,
                new ItemWear(1, 1) { Carcass = new CarcassState { Def = rabbit, WeightKg = 3.2f, Condition = 0.9f, KillFactor = 0.95f, Spoilage = 0.4f } }));
            var meat = DefRegistry.Get<ItemDef>(NamespacedId.Parse("isle:raw_meat"));
            Assert.IsTrue(InventoryOps.TryGive(inventory.Containers(), meat, 3));
            SpoilageTracker.Live.Tick(inventory.Bag, inGameMinutes: meat.Spoilage.BaseHours * 60f * 0.3f);
            var meatSpoil = SpoilageTracker.Live.SpoilageOf(inventory.Bag, meat);
            Assert.Greater(meatSpoil, 0.25f, "test setup: meat didn't age");

            // World: a carcass on the ground, a felled tree.
            var boar = DefRegistry.Get<CreatureDef>(NamespacedId.Parse("isle:boar"));
            var carcassAt = (Vector2)player.transform.position + new Vector2(1.2f, 0.4f);
            CreatureDirector.Instance.PutCarcass(new CarcassState { Def = boar, WeightKg = 55f, Condition = 0.85f, KillFactor = 0.7f, Spoilage = 0.3f }, carcassAt);
            var tree = world.Nodes.Where(n => n.Def.Id.Value == "isle:tree" && n.UsesLeft > 0).OrderBy(n => Vector2.Distance(n.Position, player.transform.position)).First();
            while (tree.IsHarvestable) world.TryHarvest(tree, out _, out _);

            // Things the save already kept, held here so they stay kept: a crate and what's in it, a planted plot, a
            // loot pile, a map marker.
            var crateAt = (Vector2)player.transform.position + new Vector2(-2f, 0.5f);
            var crate = StructureFactory.Build(DefRegistry.Get<WorldObjectDef>(NamespacedId.Parse("isle:crate")), crateAt);
            var wood = DefRegistry.Get<ItemDef>(NamespacedId.Parse("isle:wood"));
            Assert.IsTrue(crate.GetComponent<StorageBox>().Contents.TryPlace(wood, new Isle.Core.Vec2Int(0, 0), false, 7));
            var plotAt = (Vector2)player.transform.position + new Vector2(-2f, -1.5f);
            var plot = StructureFactory.Build(DefRegistry.Get<WorldObjectDef>(NamespacedId.Parse("isle:crop_plot")), plotAt);
            var flax = DefRegistry.Get<CropDef>(NamespacedId.Parse("isle:flax"));
            var plantedAt = clock.TotalMinutes - 600;
            StructureFactory.Plant(plot, flax, plantedAt);
            var pileAt = (Vector2)player.transform.position + new Vector2(0.5f, -2f);
            LootPiles.Drop(pileAt, new[] { (DefRegistry.Get<ItemDef>(NamespacedId.Parse("isle:stone")), 5) });
            var markerAt = (Vector2)player.transform.position + new Vector2(30f, 12f);
            MapState.Instance.Markers.Add(markerAt, 2);

            // A dungeon's progress: a key taken (its door open), the soft gate burned, the boss slain.
            var dungeons = Isle.Gameplay.Dungeons.DungeonDirector.Instance;
            var site = dungeons.Sites.First(s => s.Def.Id.Value == "isle:rootwood_hollow");
            var first = dungeons.EnsureFloor(site, 0);
            var key = first.Keys.First();
            key.Taken = true;
            foreach (var door in first.Barriers.Where(b => b.LockId == key.LockId)) door.Open = true;
            var lockEdge = first.Barriers.First(b => b.LockId == key.LockId).Edge;
            var softGate = first.Barriers.Single(b => b.LockId == -1);
            softGate.Open = true;
            softGate.Burned = true;
            dungeons.EnsureFloor(site, site.Def.Floors - 1);
            CreatureDirector.Instance.Damage(site.Boss, 1e6f, new System.Collections.Generic.List<(NamespacedId, int)>());
            yield return WaitUntil(() => site.BossDefeated, 3f, "test setup: boss never fell");

            yield return null;
            GameSession.BackToMenu();
            yield return WaitUntil(() => Object.FindFirstObjectByType<MainMenu>() != null && IslandWorld.Instance == null, 10f, "never got back to the menu");
            var saved = GameSession.PeekSave();
            Assert.IsNotNull(saved, "nothing saved");

            GameSession.Continue();
            yield return WaitUntil(() => PlayerInteraction.Local != null && IslandWorld.Instance != null, 15f, "player never spawned after Continue");
            for (var i = 0; i < 60; i++) yield return null;
            player = PlayerInteraction.Local;
            inventory = player.GetComponent<InventoryNetwork>();
            vitals = player.GetComponent<Vitals>();
            world = IslandWorld.Instance;
            clock = Isle.World.Time.WorldTime.Instance.Clock;

            // Character.
            Assert.AreEqual(1234d, player.Skills.TotalXp(crafting), 1e-6, "skill XP");
            Assert.AreEqual(saved.Health, vitals.Health, 1f, "health");
            Assert.AreEqual(saved.Hunger, vitals.Hunger, 1f, "hunger");
            Assert.IsTrue(vitals.Buffs.IsActive(buff.Id, clock.TotalMinutes), "timed buff lost");
            Assert.AreEqual(buffExpires, vitals.Buffs.ExpiresAt(buff.Id), "buff expiry changed");
            Assert.Greater(vitals.WetPenalty, 0f, "wetness lost");
            Assert.AreEqual(1f - 2 * Isle.Gameplay.Cooking.SatietyTracker.FatigueStep, vitals.Satiety.Eat("grill|meat", clock.TotalMinutes), 1e-5f, "satiety forgotten");

            // Inventory.
            var restoredKnife = inventory.Containers().SelectMany(c => c.Placements).FirstOrDefault(p => p.Item.Id == knife.Id);
            Assert.IsNotNull(restoredKnife.Item, "knife lost");
            Assert.AreEqual(37, restoredKnife.Wear?.Current, "knife wear");
            Assert.AreEqual(150, restoredKnife.Wear?.Max, "knife max");
            Assert.AreEqual(QualityTier.Fine, restoredKnife.Wear?.Quality, "knife quality");
            var restoredCarcass = inventory.Containers().SelectMany(c => c.Placements).FirstOrDefault(p => p.Item.Id == carcassItem.Id);
            Assert.IsNotNull(restoredCarcass.Wear?.Carcass, "carried carcass lost");
            Assert.AreEqual(3.2f, restoredCarcass.Wear.Carcass.WeightKg, 1e-4f, "carried carcass weight");
            Assert.AreEqual(0.4f, restoredCarcass.Wear.Carcass.Spoilage, 0.05f, "carried carcass spoilage");
            var meatBag = inventory.Containers().First(c => c.Placements.Any(p => p.Item.Id == meat.Id));
            Assert.AreEqual(meatSpoil, SpoilageTracker.Live.SpoilageOf(meatBag, meat), 0.03f, "food freshness reset by a reload");

            // World.
            var ground = CreatureDirector.Instance.Carcasses.FirstOrDefault(c => c.Def.Id == boar.Id && Vector2.Distance(c.Position, carcassAt) < 0.1f);
            Assert.IsNotNull(ground, "carcass on the ground lost");
            Assert.AreEqual(55f, ground.Weight, 1e-3f, "ground carcass weight");
            Assert.AreEqual(0.3f, ground.Spoilage, 0.05f, "ground carcass spoilage");
            Assert.AreEqual(0.7f, ground.KillFactor, 1e-4f, "ground carcass kill factor");
            Assert.IsFalse(world.NodeAt(tree.Tile).IsHarvestable, "felled tree grew back on reload");
            Assert.GreaterOrEqual(clock.TotalMinutes, saved.TotalMinutes, "clock went back");

            var restoredCrate = StructureFactory.Built.FirstOrDefault(b => b.Def.Id.Value == "isle:crate" && Vector2.Distance(b.transform.position, crateAt) < 0.1f);
            Assert.IsNotNull(restoredCrate, "crate lost");
            Assert.AreEqual(7, restoredCrate.GetComponent<StorageBox>().Contents.Placements.Where(p => p.Item.Id == wood.Id).Sum(p => p.Count), "crate contents");
            var restoredPlot = StructureFactory.Built.FirstOrDefault(b => b.Def.Id.Value == "isle:crop_plot" && Vector2.Distance(b.transform.position, plotAt) < 0.1f);
            Assert.AreEqual(flax.Id, restoredPlot?.GetComponent<CropPlot>()?.Crop?.Id, "planted crop lost");
            Assert.AreEqual(plantedAt, restoredPlot.GetComponent<CropPlot>().PlantedAtMinutes, "crop's planting time");
            Assert.IsTrue(LootPiles.All.Any(p => Vector2.Distance(p.Position, pileAt) < 0.1f && p.Items.Any(i => i.Item.Id.Value == "isle:stone" && i.Count == 5)), "loot pile lost");
            Assert.IsTrue(MapState.Instance.Markers.All.Any(m => Vector2.Distance(m.Position, markerAt) < 0.1f && m.Colour == 2), "map marker lost");

            // The dungeon remembers.
            var dungeonsAfter = Isle.Gameplay.Dungeons.DungeonDirector.Instance;
            var siteAfter = dungeonsAfter.Sites.First(s => s.Def.Id.Value == "isle:rootwood_hollow");
            Assert.IsTrue(siteAfter.BossDefeated, "a slain boss came back after a reload");
            var lastAfter = dungeonsAfter.EnsureFloor(siteAfter, siteAfter.Def.Floors - 1);
            Assert.IsNull(siteAfter.Boss, "the boss respawned on its floor");
            Assert.IsTrue(lastAfter.Portals.Any(p => p.Kind == Isle.Gameplay.Dungeons.DungeonDirector.DoorKind.Exit), "the boss room's way out isn't open");
            var firstAfter = dungeonsAfter.EnsureFloor(siteAfter, 0);
            Assert.IsTrue(firstAfter.Keys.First(k => k.LockId == key.LockId).Taken, "a taken key is back");
            Assert.IsTrue(firstAfter.Barriers.First(b => b.Edge == lockEdge).Open, "an unlocked door locked again");
            var gateAfter = firstAfter.Barriers.Single(b => b.LockId == -1);
            Assert.IsTrue(gateAfter.Open && gateAfter.Burned, "the burned gate is back");
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
