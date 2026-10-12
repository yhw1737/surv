using Isle.Gameplay.Feedback;
using Isle.World.Objects;
using UnityEngine;

namespace Isle.Gameplay.Building
{
    /// <summary>SYS-CRAFT-01 §Enchanting: the station enchants are set at. E opens the enchanting window; the work itself
    /// is <c>PlayerInteraction.RequestEnchant*</c>, server-side.</summary>
    public sealed class EnchantingTable : MonoBehaviour, IInteractable
    {
        /// <summary>World-object tag of an enchanting station.</summary>
        public const string Tag = "station/enchanting";

        public void Interact(GameObject user) => GameFeed.RaiseEnchantOpened(this);
    }
}
