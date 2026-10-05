using Isle.Gameplay.Character;
using Isle.Gameplay.Inventory;
using Isle.World.Generation;
using Isle.World.Island;
using Isle.World.Objects;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Isle.UI.Prototype
{
    /// <summary>
    /// Corner minimap that reuses the island's painted texture, with the player, campfires and loot piles
    /// marked. <c>M</c> toggles a large view. Presentation only.
    /// </summary>
    public sealed class Minimap : MonoBehaviour
    {
        const float SmallSize = 160f;
        const float LargeFraction = 0.8f;
        const float Margin = 12f;
        const string CampfireTag = "station/campfire";

        bool _large;

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.mKey.wasPressedThisFrame) _large = !_large;
        }

        void OnGUI()
        {
            var world = IslandWorld.Instance;
            if (world == null || world.Map == null) return;

            var size = _large ? Mathf.Min(Screen.width, Screen.height) * LargeFraction : SmallSize;
            var rect = _large
                ? new Rect((Screen.width - size) * 0.5f, (Screen.height - size) * 0.5f, size, size)
                : new Rect(Margin, Screen.height - size - Margin, size, size);

            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, rect.height + 4f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.DrawTexture(rect, world.Map);

            foreach (var instance in FindObjectsByType<WorldObjectInstance>(FindObjectsSortMode.None))
                if (instance.HasTag(CampfireTag)) Mark(rect, instance.transform.position, instance.IsActive ? Color.red : Color.gray, 5f);
            foreach (var pile in LootPiles.All) Mark(rect, pile.Position, new Color(0.55f, 0.38f, 0.2f), 5f);
            foreach (var player in FindObjectsByType<PlayerInteraction>(FindObjectsSortMode.None))
                Mark(rect, player.transform.position, player.IsOwner ? Color.white : Color.cyan, 6f);
        }

        /// <summary>World position (island centred on origin) to a dot inside <paramref name="rect"/>.</summary>
        static void Mark(Rect rect, Vector2 world, Color colour, float dot)
        {
            var size = IslandGenerator.Size;
            var u = (world.x + size / 2f) / size;
            var v = (world.y + size / 2f) / size;
            var x = rect.x + u * rect.width;
            var y = rect.y + (1f - v) * rect.height;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(x - dot / 2f - 1f, y - dot / 2f - 1f, dot + 2f, dot + 2f), Texture2D.whiteTexture);
            GUI.color = colour;
            GUI.DrawTexture(new Rect(x - dot / 2f, y - dot / 2f, dot, dot), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
