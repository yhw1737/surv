using System.Linq;
using Isle.Gameplay.Character;
using Isle.Modding.Defs;
using Isle.Data;
using UnityEngine;

namespace Isle.UI.Prototype
{
    /// <summary>
    /// T-151 playtest: a column of buttons on the left edge — status, equipment, crafting, cooking, skills, map, help —
    /// each a click to open or close what its key already does. Plus the status window: the body part by part (wounds,
    /// infections, fractures, scars, the armor covering it) and what's acting on you.
    /// </summary>
    public sealed partial class PrototypeHud
    {
        const float ToolButtonPx = 52f;
        bool _statusOpen;

        static readonly (string Id, string Key)[] Tools =
        {
            ("status", "O"), ("equipment", "Tab"), ("craft", "C"), ("cook", "K"), ("skills", "P"), ("map", "M"), ("help", "H"),
        };

        void DrawToolbar()
        {
            var st = UiTheme.Styles;
            var x = Margin;
            var y = Screen.height * 0.5f - Tools.Length * (ToolButtonPx + 6f) * 0.5f;
            foreach (var (id, key) in Tools)
            {
                var rect = new Rect(x, y, ToolButtonPx, ToolButtonPx);
                if (Event.current.type == EventType.Layout) _panels.Add(rect);
                var on = ToolOpen(id);
                if (GUI.Button(rect, GUIContent.none, on ? st.ButtonOn : st.Button)) ToggleTool(id);
                GUI.Label(new Rect(rect.x, rect.y + 6f, rect.width, 22f), Lang.Get("@tool." + id), new GUIStyle(st.Text) { alignment = TextAnchor.MiddleCenter, fontSize = 13 });
                GUI.Label(new Rect(rect.x, rect.y + 28f, rect.width, 18f), key, new GUIStyle(st.Muted) { alignment = TextAnchor.MiddleCenter, fontSize = 11 });
                // The bleeding mark on the status button: look here.
                if (id == "status" && _vitals.Injuries.Bleeding)
                {
                    GUI.color = UiTheme.Bad;
                    GUI.DrawTexture(new Rect(rect.xMax - 12f, rect.y + 4f, 8f, 8f), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
                y += ToolButtonPx + 6f;
            }
        }

        bool ToolOpen(string id) => id switch
        {
            "status" => _statusOpen,
            "equipment" => Isle.UI.Inventory.InventoryScreen.Instance is { Open: true },
            "craft" => _craftOpen,
            "cook" => _cookOpen,
            "skills" => _skillsOpen,
            "map" => Minimap.Large,
            "help" => _helpOpen,
            _ => false,
        };

        /// <summary>Opens or closes a tool (the toolbar's click, and tests).</summary>
        public void ToggleTool(string id)
        {
            switch (id)
            {
                case "status": _statusOpen = !_statusOpen; break;
                case "equipment": Isle.UI.Inventory.InventoryScreen.Instance?.Toggle(); break;
                case "craft": _craftOpen = !_craftOpen; break;
                case "cook": _cookOpen = !_cookOpen; break;
                case "skills": _skillsOpen = !_skillsOpen; break;
                case "map": Minimap.Large = !Minimap.Large; break;
                case "help": _helpOpen = !_helpOpen; break;
            }
        }

        public bool IsToolOpen(string id) => ToolOpen(id);

        void OnStationOpened(string kind)
        {
            if (kind == Isle.Gameplay.Building.StationInteraction.Cook) _cookOpen = true;
            else _craftOpen = true;
        }

        // ------------------------------------------------------------------ status window

        static readonly BodyPart[] Parts = { BodyPart.Head, BodyPart.Torso, BodyPart.LeftArm, BodyPart.RightArm, BodyPart.LeftLeg, BodyPart.RightLeg };

        void DrawStatusWindow()
        {
            var st = UiTheme.Styles;
            var rect = Window(520f, 470f, 120f, Lang.Get("@tool.status"), $"{Lang.Get("@ui.health")} {_vitals.Health:0} / {_vitals.MaxHealth:0}", ref _statusOpen);
            var y = rect.y + WindowHeaderPx + 10f;
            var injuries = _vitals.Injuries;
            foreach (var part in Parts)
            {
                var wounds = injuries.Wounds.Where(w => w.Part == part).ToList();
                var scars = injuries.Scars.Count(s => s == part);
                var armor = _vitals.WornArmorFor(part);
                var head = $"{Lang.Get("@body." + part.ToString().ToLowerInvariant())}";
                var colour = wounds.Any(w => w.Bleeds || w.Infected) ? UiTheme.Bad : wounds.Count > 0 ? UiTheme.Accent : UiTheme.Good;
                GUI.Label(new Rect(rect.x + 16f, y, 120f, 22f), UiTheme.Colour(head, colour), st.Text);
                GUI.Label(new Rect(rect.xMax - 140f, y, 124f, 22f), $"{Lang.Get("@ui.armor")} {armor:0}" + (scars > 0 ? $"  {Lang.Get("@ui.scars")} {scars}" : ""), new GUIStyle(st.Muted) { alignment = TextAnchor.UpperRight });
                y += 22f;
                foreach (var w in wounds)
                {
                    var notes = new System.Collections.Generic.List<string>();
                    if (w.Bleeds) notes.Add(UiTheme.Colour(Lang.Get("@ui.bleeding"), UiTheme.Bad));
                    if (w.Infected) notes.Add(UiTheme.Colour($"{Lang.Get("@ui.infected")} {w.Infection * 100f:0}%", UiTheme.Bad));
                    if (w.Fracture) notes.Add(UiTheme.Colour(Lang.Get(w.Splinted ? "@ui.splinted" : "@ui.fracture"), w.Splinted ? UiTheme.Good : UiTheme.Bad));
                    if (w.Tended) notes.Add(UiTheme.Colour($"{Lang.Get("@ui.tended_short")} {w.Quality * 100f:0}%", UiTheme.Good));
                    GUI.Label(new Rect(rect.x + 34f, y, rect.width - 50f, 20f), $"{Lang.Get("@wound." + w.Kind.ToString().ToLowerInvariant())} {w.Severity:0}   {string.Join("  ", notes)}", st.Small);
                    y += 19f;
                }
                y += 4f;
            }
            var buffs = _vitals.ActiveBuffs().Select(b => Lang.Get(b.Name)).ToList();
            if (buffs.Count > 0)
                GUI.Label(new Rect(rect.x + 16f, rect.yMax - 40f, rect.width - 32f, 22f), $"{Lang.Get("@ui.tt_buffs")}: {string.Join(", ", buffs)}", st.Small);
        }

        /// <summary>One line under the gauges when hurt; the status window has the detail.</summary>
        void DrawWoundSummary(ref float y)
        {
            var wounds = _vitals.Injuries.Wounds;
            if (wounds.Count == 0 && _vitals.Injuries.Scars.Count == 0) return;
            var line = $"{Lang.Get("@ui.wounds")} {wounds.Count}  (max {_vitals.MaxHealth:0})";
            if (_vitals.Injuries.Bleeding) line += "  " + UiTheme.Colour(Lang.Get("@ui.bleeding"), UiTheme.Bad);
            if (wounds.Any(w => w.Infected)) line += "  " + UiTheme.Colour(Lang.Get("@ui.infected"), UiTheme.Bad);
            if (wounds.Any(w => w.Fracture && !w.Splinted)) line += "  " + UiTheme.Colour(Lang.Get("@ui.fracture"), UiTheme.Bad);
            var rect = new Rect(Margin, y, 360f, RowHeight);
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) _statusOpen = true;
            GUI.Label(rect, line, _label);
            y += RowHeight;
        }
    }
}
