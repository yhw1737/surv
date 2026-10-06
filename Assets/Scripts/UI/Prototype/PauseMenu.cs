using Isle.Gameplay.Character;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Isle.UI.Prototype
{
    /// <summary>
    /// Esc menu: resume, save now, start a new island next launch, quit. Pausing stops game time
    /// (<see cref="Time.timeScale"/> = 0), which is only right for a solo host — co-op will need a different
    /// answer. Esc while placing a structure cancels the placement instead (the HUD handles that).
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        const float Width = 260f;
        const float ButtonHeight = 36f;

        bool _open;
        GUIStyle _title;

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame || PrototypeHud.IsPlacing) return;
            if (!_open && Isle.UI.Inventory.InventoryScreen.Instance is { Open: true } inventory)
            {
                inventory.Close(); // Esc backs out of the inventory before it pauses
                return;
            }
            SetOpen(!_open);
        }

        void SetOpen(bool open)
        {
            _open = open;
            Time.timeScale = open ? 0f : 1f;
        }

        void OnDisable() => Time.timeScale = 1f;

        void OnGUI()
        {
            if (Event.current.type == EventType.Repaint) PointerGate.Captured |= _open;
            if (!_open) return;
            _title ??= new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };

            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            var x = (Screen.width - Width) * 0.5f;
            var y = Screen.height * 0.3f;
            GUI.Label(new Rect(x, y, Width, 40f), Lang.Get("@ui.paused"), _title);
            y += 52f;

            if (GUI.Button(new Rect(x, y, Width, ButtonHeight), Lang.Get("@ui.resume"))) SetOpen(false);
            y += ButtonHeight + 8f;
            if (GUI.Button(new Rect(x, y, Width, ButtonHeight), Lang.Get("@ui.save_now"))) FindFirstObjectByType<SaveGame>()?.SaveNow();
            y += ButtonHeight + 8f;
            if (GUI.Button(new Rect(x, y, Width, ButtonHeight), Lang.Get("@ui.new_island"))) FindFirstObjectByType<SaveGame>()?.DeleteSave();
            y += ButtonHeight + 8f;
            if (GUI.Button(new Rect(x, y, Width, ButtonHeight), Lang.Get("@ui.menu_to_title")))
            {
                SetOpen(false);
                GameSession.BackToMenu();
                return;
            }
            y += ButtonHeight + 8f;
            if (GUI.Button(new Rect(x, y, Width, ButtonHeight), Lang.Get("@ui.quit")))
            {
                FindFirstObjectByType<SaveGame>()?.SaveNow();
                Application.Quit();
            }
        }
    }
}
