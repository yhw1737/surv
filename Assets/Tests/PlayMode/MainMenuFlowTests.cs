using System.Linq;
using System.Collections;
using FishNet;
using Isle.Gameplay.Building;
using Isle.Gameplay.Character;
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
    /// <summary>T-153: the scene opens on the main menu with no island; New game builds one and spawns the player;
    /// Main menu goes back (saving); Continue resumes that island. Unity fails the test on any error log, so a
    /// broken FishNet restart across the scene reload shows up here.</summary>
    public sealed class MainMenuFlowTests
    {
        static readonly string ScratchSave = System.IO.Path.Combine(Application.temporaryCachePath, "isle_menu_test_save.json");

        [SetUp]
        public void SetUp()
        {
            GameSession.SkipMenu = false;
            SaveGame.FilePathOverride = ScratchSave;
            DeleteScratch();
            DefRegistry.Clear();
            Assert.IsEmpty(DefinitionBootstrap.Load(System.IO.Path.Combine(Application.streamingAssetsPath, "definitions")));
        }

        [TearDown]
        public void TearDown()
        {
            Isle.Networking.IsleNetworkManager.Find()?.StopHost();
            foreach (var save in Object.FindObjectsByType<SaveGame>(FindObjectsSortMode.None)) Object.DestroyImmediate(save);
            DeleteScratch();
            SaveGame.FilePathOverride = null;
            SaveGame.Slot = 1;
            LootPiles.Clear();
            StructureFactory.Clear();
            DefRegistry.Clear();
        }

        [UnityTest]
        public IEnumerator Menu_NewGame_BackToMenu_Continue()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return WaitUntil(() => Object.FindFirstObjectByType<MainMenu>() != null, 5f, "main menu never appeared");
            Assert.IsNull(IslandWorld.Instance, "an island was built before the player chose anything");
            Assert.IsFalse(InstanceFinder.IsServerStarted, "host started on the menu");
            Assert.IsNull(GameSession.PeekSave(), "test setup: a save exists");

            // SYS-START-01: the passenger picked on the menu is who wakes up — on the beach, soaked, in their clothes.
            var picked = StartDirector.Roll(new System.Random(5));
            picked.Name = "Test Passenger";
            GameSession.SetPendingSurvivor(picked);
            GameSession.StartNew(424242);
            yield return WaitUntil(() => PlayerInteraction.Local != null, 15f, "player never spawned after New game");
            Assert.AreEqual(424242, IslandWorld.Instance.Seed);
            yield return WaitUntil(() => PlayerInteraction.Local.Survivor != null, 5f, "survivor never applied");
            var local = PlayerInteraction.Local;
            Assert.AreEqual("Test Passenger", local.Survivor.Name);
            var tile = IslandWorld.WorldToTile(local.transform.position);
            Assert.AreEqual(Isle.World.Chunks.Biome.Coast, IslandWorld.Instance.Island.BiomeAt(tile.X, tile.Y), "didn't wake on the coast");
            Assert.IsNotNull(IslandWorld.Instance.NearestWater(local.transform.position, 4f), "start isn't at the shore");
            Assert.Greater(local.GetComponent<Vitals>().WetPenalty, 0f, "didn't wake up wet");
            var inventory = local.GetComponent<Isle.Gameplay.Inventory.InventoryNetwork>();
            // Wearing exactly the rolled outfit.
            foreach (var id in picked.Outfit.Items)
            {
                var slot = Isle.Modding.Defs.DefRegistry.Get<Isle.Data.ItemDef>(id).EquipSlot;
                Assert.AreEqual(id.Value, inventory.Slots.Get(slot)?.Id.Value, $"not wearing {id}");
            }
            Assert.GreaterOrEqual(Isle.Gameplay.Inventory.LootPiles.All.Count(p => p.Shape == "wreckage"), 4, "no wreckage on the beach");
            Assert.IsNull(Object.FindFirstObjectByType<MainMenu>(), "menu still up in game");
            for (var i = 0; i < 90; i++) yield return null; // let autosave arm (SaveGame applies on the first frames)

            GameSession.BackToMenu();
            yield return WaitUntil(() => Object.FindFirstObjectByType<MainMenu>() != null && IslandWorld.Instance == null, 10f, "never got back to the menu");
            var save = GameSession.PeekSave();
            Assert.IsNotNull(save, "going back to the menu didn't save");
            Assert.AreEqual(424242, save.Seed);

            GameSession.Continue();
            yield return WaitUntil(() => PlayerInteraction.Local != null && IslandWorld.Instance != null, 15f, "player never spawned after Continue");
            Assert.AreEqual(424242, IslandWorld.Instance.Seed, "Continue built a different island");
            for (var i = 0; i < 60; i++) yield return null;
            Assert.AreEqual("Test Passenger", PlayerInteraction.Local.Survivor?.Name, "survivor name not restored");
            CollectionAssert.AreEquivalent(picked.Traits.Select(t => t.Id.Value), PlayerInteraction.Local.Survivor.Traits.Select(t => t.Id.Value), "traits not restored");

            // Slots are independent: a new island in slot 2 leaves slot 1 alone, and becomes the one Continue picks.
            GameSession.BackToMenu();
            yield return WaitUntil(() => Object.FindFirstObjectByType<MainMenu>() != null && IslandWorld.Instance == null, 10f, "never got back to the menu (2)");
            GameSession.StartNew(777, slot: 2);
            yield return WaitUntil(() => PlayerInteraction.Local != null && IslandWorld.Instance != null, 15f, "player never spawned in slot 2");
            for (var i = 0; i < 90; i++) yield return null;
            GameSession.BackToMenu();
            yield return WaitUntil(() => Object.FindFirstObjectByType<MainMenu>() != null && IslandWorld.Instance == null, 10f, "never got back to the menu (3)");
            Assert.AreEqual(424242, GameSession.PeekSave(1)?.Seed, "slot 1 changed");
            Assert.AreEqual(777, GameSession.PeekSave(2)?.Seed, "slot 2 not saved");
            Assert.AreEqual(2, SaveGame.LatestSlot(), "Continue would not pick the last-played slot");
        }

        static void DeleteScratch()
        {
            for (var slot = 1; slot <= SaveGame.MaxSlots; slot++)
            {
                var path = slot == 1 ? ScratchSave : ScratchSave + "." + slot;
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }
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
