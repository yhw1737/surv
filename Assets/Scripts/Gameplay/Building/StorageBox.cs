using Isle.Gameplay.Feedback;
using Isle.Gameplay.Inventory;
using Isle.World.Objects;
using UnityEngine;

namespace Isle.Gameplay.Building
{
    /// <summary>A placed container (SYS-INV-01 §Containers: wooden crate 10×6, a world object). Its grid lives
    /// server-side; <c>E</c> asks the HUD to open it, and items move through <c>PlayerInteraction</c>'s store/take
    /// requests. Host-only like the creatures — no client sync yet.</summary>
    [RequireComponent(typeof(WorldObjectInstance))]
    public sealed class StorageBox : MonoBehaviour, IInteractable
    {
        public GridInventory Contents { get; private set; }

        public void Initialize(int width, int height) => Contents = new GridInventory(width, height);

        public void Interact(GameObject user) => GameFeed.RaiseStorageOpened(this);
    }
}
