using Isle.Gameplay.Feedback;
using Isle.World.Objects;
using UnityEngine;

namespace Isle.Gameplay.Building
{
    /// <summary>T-151 playtest: E at a crafting station (workbench, furnace, anvil, apothecary…) opens the craft window, at
    /// a cooking station (pot, rack, cauldron…) the cook window. Which one comes from the definitions: a station some cook
    /// method names is a cooking station, any other <c>station/*</c> a crafting one.</summary>
    public sealed class StationInteraction : MonoBehaviour, IInteractable
    {
        public const string Craft = "craft";
        public const string Cook = "cook";

        public string Kind { get; set; } = Craft;

        public void Interact(GameObject user) => GameFeed.RaiseStationOpened(Kind);
    }
}
