using Isle.Gameplay.Hunting;
using Isle.World.Island;
using UnityEngine;
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
            if (Object.FindFirstObjectByType<IslandWorld>() != null) return;

            // Read the save first: it decides the island seed, which IslandWorld consumes in Awake.
            var pending = SaveGame.ReadPending();

            var root = new GameObject("Prototype");
            root.AddComponent<IslandWorld>();
            root.AddComponent<SaveGame>().Begin(pending);
            root.AddComponent<CreatureDirector>();
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
