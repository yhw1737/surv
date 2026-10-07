using Isle.Gameplay.Hunting;
using Isle.World.Island;
using UnityEngine;
using Isle.Networking;
using UnityEngine.SceneManagement;

namespace Isle.UI.Prototype
{
    /// <summary>
    /// Installs the solo prototype's runtime systems after the scene loads. Code-installed, the same
    /// way <c>DefinitionBootstrapRunner</c> is, so the scene needs no hand-placed objects: a missing
    /// drag-and-drop step can't silently leave the island empty.
    /// </summary>
    public static class PrototypeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            // AfterSceneLoad only fires for the first scene of a session in some launch paths, so also
            // reinstall on every later scene load. Each install checks for an existing copy first.
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureInstalled();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureInstalled();

        static void EnsureInstalled()
        {
            GameOptions.Apply();
            if (Object.FindFirstObjectByType<IslandWorld>() != null) return;

            // T-153: the main menu comes first; the island is built when the player picks New game or Continue.
            if (!GameSession.InGame && !GameSession.SkipMenu)
            {
                if (Object.FindFirstObjectByType<MainMenu>() == null) new GameObject("MainMenu").AddComponent<MainMenu>();
                return;
            }

            // Tests skip the menu: build the island from whatever save exists and start the host straight away.
            InstallWorld(SaveGame.ReadPending());
            IsleNetworkManager.Find()?.StartHost();
        }

        /// <summary>Builds the island and every runtime system. <paramref name="pending"/> resumes a save (its seed is
        /// already in <c>IslandWorld.NextSeed</c>); null starts fresh.</summary>
        public static void InstallWorld(SaveData pending)
        {
            var root = new GameObject("Prototype");
            root.AddComponent<IslandWorld>();
            root.AddComponent<SaveGame>().Begin(pending);
            root.AddComponent<CreatureDirector>();
            root.AddComponent<Isle.Gameplay.Dungeons.DungeonDirector>();
            Isle.Gameplay.Dungeons.DungeonDirector.Localize = Lang.Get;
            root.AddComponent<Isle.Gameplay.Combat.Projectiles>();
            root.AddComponent<CampfireSpawner>();
            root.AddComponent<DayNightLight>();
            root.AddComponent<PrototypeHud>();
            root.AddComponent<Isle.UI.Inventory.InventoryScreen>();
            root.AddComponent<PlayerCamera>();
            root.AddComponent<Isle.UI.Art.StickFigureDirector>();
            root.AddComponent<Isle.UI.Art.CreatureFigureDirector>();
            root.AddComponent<FeedbackOverlay>();
            root.AddComponent<MapState>();
            root.AddComponent<Minimap>();
            root.AddComponent<LightRig>();
            root.AddComponent<WeatherOverlay>();
            root.AddComponent<PauseMenu>();
        }
    }
}
