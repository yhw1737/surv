using System.Linq;
using Isle.Data;
using Isle.Gameplay.World;
using Isle.Modding.Defs;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Isle.UI.Prototype
{
    /// <summary>SYS-DIFF-01 Unlimited: I opens the item palette — every item in the definitions, searchable; a click takes
    /// one, Shift+click a full stack (the server refuses outside Unlimited).</summary>
    public sealed partial class PrototypeHud
    {
        const float PaletteCell = 54f;
        const int PaletteColumns = 11;

        bool _creativeOpen;
        string _creativeSearch = "";
        Vector2 _creativeScroll;

        void TickCreativeWindow()
        {
            if (!Difficulty.IsUnlimited)
            {
                _creativeOpen = false;
                return;
            }
            var kb = Keyboard.current;
            // While the search box has focus, an "i" is a letter, not the toggle.
            if (kb != null && kb.iKey.wasPressedThisFrame && !(_creativeOpen && GUIUtility.keyboardControl != 0))
                _creativeOpen = !_creativeOpen;
        }

        void DrawCreativeWindow()
        {
            var st = UiTheme.Styles;
            var width = PaletteColumns * PaletteCell + 44f;
            var rect = Window(width, 470f, 0f, Lang.Get("@ui.creative_title"), Lang.Get("@ui.creative_hint"), ref _creativeOpen);
            var y = rect.y + WindowHeaderPx + 8f;
            GUI.Label(new Rect(rect.x + 14f, y + 4f, 80f, 22f), Lang.Get("@ui.creative_search"), st.Text);
            _creativeSearch = GUI.TextField(new Rect(rect.x + 90f, y, 260f, 28f), _creativeSearch ?? "", 40);
            y += 38f;

            var query = (_creativeSearch ?? "").Trim().ToLowerInvariant();
            var items = DefRegistry.All<ItemDef>()
                .Where(i => i.Tags == null || !i.Tags.Contains(Isle.Modding.Defs.CarcassItems.Tag)) // a carcass needs a body
                .Select(i => (Item: i, Name: Lang.Get(i.Name)))
                .Where(x => query.Length == 0 || x.Name.ToLowerInvariant().Contains(query) || x.Item.Id.Value.Contains(query))
                .OrderBy(x => x.Name).ToList();

            var area = new Rect(rect.x + 12f, y, rect.width - 24f, rect.yMax - y - 12f);
            GUI.Box(area, GUIContent.none, st.Inset);
            var rows = Mathf.CeilToInt(items.Count / (float)PaletteColumns);
            var view = new Rect(0, 0, PaletteColumns * PaletteCell, rows * PaletteCell);
            _creativeScroll = GUI.BeginScrollView(new Rect(area.x + 4f, area.y + 4f, area.width - 6f, area.height - 8f), _creativeScroll, view, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);
            var shift = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
            for (var i = 0; i < items.Count; i++)
            {
                var (item, name) = items[i];
                var cell = new Rect(i % PaletteColumns * PaletteCell, i / PaletteColumns * PaletteCell, PaletteCell - 4f, PaletteCell - 4f);
                if (GUI.Button(cell, new GUIContent("", name), st.Slot))
                    _player.RequestCreativeTake(item.Id.Value, shift ? Mathf.Max(1, item.Stack) : 1);
                DrawIcon(new Rect(cell.x + 4f, cell.y + 4f, cell.width - 8f, cell.height - 8f), Isle.UI.Art.ItemIcons.Texture(item));
            }
            GUI.EndScrollView();
            if (!string.IsNullOrEmpty(GUI.tooltip))
                GUI.Label(new Rect(rect.x + 370f, rect.y + WindowHeaderPx + 12f, rect.width - 384f, 22f), GUI.tooltip, st.Text);
        }
    }
}
