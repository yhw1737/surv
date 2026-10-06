using System.IO;
using Isle.Gameplay.Building;
using Isle.Gameplay.Inventory;
using Isle.Networking;
using Isle.World.Island;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Isle.UI.Prototype
{
    /// <summary>
    /// T-153: the menu ↔ game lifecycle. The scene opens in menu mode; <see cref="StartNew"/> / <see cref="Continue"/>
    /// build the island and start the host; <see cref="BackToMenu"/> saves, stops the host and reloads the scene in
    /// menu mode. Static because it outlives the scene reload.
    /// </summary>
    public static class GameSession
    {
        /// <summary>True from New game / Continue until Back to menu.</summary>
        public static bool InGame { get; private set; }

        /// <summary>PlayMode tests set this to start straight into the game the way the scene used to.</summary>
        public static bool SkipMenu { get; set; }

        /// <summary>A slot's save, or null — slot 0 means the current slot.</summary>
        public static SaveData PeekSave(int slot = 0) => SaveGame.Inspect(slot <= 0 ? SaveGame.Slot : slot)?.Save;

        /// <summary>A fresh island on <paramref name="seed"/> in <paramref name="slot"/>. Replaces that slot's save (the
        /// menu asks first).</summary>
        public static void StartNew(int seed, int slot = 1)
        {
            SaveGame.Slot = slot;
            SaveGame.DeleteFile(slot);
            IslandWorld.NextSeed = seed;
            Begin(null);
        }

        /// <summary>Resumes a slot's save (its seed, clock, player and world state).</summary>
        public static void Continue(int slot = 1)
        {
            SaveGame.Slot = slot;
            var pending = SaveGame.ReadPending();
            Begin(pending);
        }

        static void Begin(SaveData pending)
        {
            InGame = true;
            Time.timeScale = 1f;
            // Statics from a previous session in this run must not leak into the new island.
            LootPiles.Clear();
            StructureFactory.Clear();
            foreach (var menu in Object.FindObjectsByType<MainMenu>(FindObjectsSortMode.None)) Object.Destroy(menu.gameObject);
            PrototypeBootstrap.InstallWorld(pending);
            IsleNetworkManager.Find()?.StartHost();
        }

        /// <summary>Saves, stops the host and reloads the scene, which comes back up in menu mode.</summary>
        public static void BackToMenu()
        {
            Object.FindFirstObjectByType<SaveGame>()?.SaveNow();
            InGame = false;
            Time.timeScale = 1f;
            IsleNetworkManager.Find()?.StopHost();
            LootPiles.Clear();
            StructureFactory.Clear();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        /// <summary>A random seed in the same range IslandWorld picks from.</summary>
        public static int RandomSeed() => Random.Range(1, int.MaxValue);
    }
}
