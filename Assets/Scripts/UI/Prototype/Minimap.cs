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
    /// SYS-MAP-01 minimap and map. Both are centred on the player and zoom in steps (mouse wheel over them, or
    /// <c>=</c>/<c>-</c>); unexplored ground draws as fog. On the map (<c>M</c>) a left-click drops a marker and a
    /// right-click removes the nearest one. Presentation only — the marker list and fog live in <see cref="MapState"/>.
    /// </summary>
    public sealed class Minimap : MonoBehaviour
    {
        const float SmallSize = 180f;
        const float LargeFraction = 0.85f;
        const float Margin = 12f;
        const string CampfireTag = "station/campfire";

        /// <summary>SYS-MAP-01 zoom levels, in tiles across.</summary>
        static readonly int[] MiniSpans = { 64, 128, 256, 512, IslandGenerator.Size };
        static readonly int[] MapSpans = { 128, 256, 512, IslandGenerator.Size };

        /// <summary>Right-click removes the nearest marker within this many screen pixels, at any zoom. [invented]</summary>
        const float RemoveRadiusPixels = 14f;

        public static readonly Color[] MarkerColours =
        {
            new(1f, 0.3f, 0.3f), new(1f, 0.85f, 0.2f), new(0.3f, 0.9f, 1f), new(0.95f, 0.4f, 1f),
        };

        bool _large;
        int _miniZoom = 1;
        int _mapZoom = MapSpans.Length - 1;

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.mKey.wasPressedThisFrame) _large = !_large;
            if (kb.equalsKey.wasPressedThisFrame || kb.numpadPlusKey.wasPressedThisFrame) Zoom(-1);
            if (kb.minusKey.wasPressedThisFrame || kb.numpadMinusKey.wasPressedThisFrame) Zoom(+1);
        }

        /// <summary>-1 zooms in (fewer tiles across), +1 out.</summary>
        void Zoom(int step)
        {
            if (_large) _mapZoom = Mathf.Clamp(_mapZoom + step, 0, MapSpans.Length - 1);
            else _miniZoom = Mathf.Clamp(_miniZoom + step, 0, MiniSpans.Length - 1);
        }

        void OnGUI()
        {
            var world = IslandWorld.Instance;
            var state = MapState.Instance;
            if (world == null || world.Map == null || state == null) return;
            var me = LocalPlayer();
            if (me == null) return;

            var size = _large ? Mathf.Min(Screen.width, Screen.height) * LargeFraction : SmallSize;
            var rect = _large
                ? new Rect((Screen.width - size) * 0.5f, (Screen.height - size) * 0.5f, size, size)
                : new Rect(Margin, Screen.height - size - Margin, size, size);
            var span = _large ? MapSpans[_mapZoom] : MiniSpans[_miniZoom];
            Vector2 centre = me.transform.position;
            var view = new View(rect, centre, span);

            HandleInput(rect, view, state);
            if (Event.current.type != EventType.Repaint) return;
            PointerGate.Captured |= _large || rect.Contains(Event.current.mousePosition);

            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, rect.height + 4f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.DrawTextureWithTexCoords(rect, world.Map, view.Uv);
            GUI.DrawTextureWithTexCoords(rect, state.FogTexture, view.Uv);

            foreach (var instance in WorldObjectRegistry.All)
                if (instance.HasTag(CampfireTag) && state.IsExplored(instance.transform.position))
                    Dot(view, instance.transform.position, instance.IsActive ? Color.red : Color.gray, 5f);
            foreach (var pile in LootPiles.All) Dot(view, pile.Position, new Color(0.62f, 0.45f, 0.26f), 5f);
            for (var i = 0; i < state.Markers.All.Count; i++)
            {
                var marker = state.Markers.All[i];
                Dot(view, marker.Position, MarkerColours[marker.Colour % MarkerColours.Length], 8f);
            }
            foreach (var player in PlayerInteraction.All)
                Dot(view, player.transform.position, player.IsOwner ? Color.white : Color.cyan, 6f);

            if (_large)
                GUI.Label(new Rect(rect.x, rect.yMax + 4f, rect.width, 20f), Lang.Get("@ui.map_hint"));
        }

        void HandleInput(Rect rect, View view, MapState state)
        {
            var e = Event.current;
            if (!rect.Contains(e.mousePosition)) return;
            if (e.type == EventType.ScrollWheel)
            {
                Zoom(e.delta.y > 0 ? +1 : -1);
                e.Use();
            }
            else if (_large && e.type == EventType.MouseDown)
            {
                var worldPoint = view.ToWorld(e.mousePosition);
                if (e.button == 0) state.Markers.Add(worldPoint);
                else if (e.button == 1) state.Markers.RemoveNear(worldPoint, RemoveRadiusPixels * view.Span / rect.width);
                e.Use();
            }
        }

        static void Dot(View view, Vector2 world, Color colour, float dot)
        {
            var p = view.ToGui(world);
            if (!view.Rect.Contains(p)) return;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(p.x - dot / 2f - 1f, p.y - dot / 2f - 1f, dot + 2f, dot + 2f), Texture2D.whiteTexture);
            GUI.color = colour;
            GUI.DrawTexture(new Rect(p.x - dot / 2f, p.y - dot / 2f, dot, dot), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        static PlayerInteraction LocalPlayer()
        {
            foreach (var player in PlayerInteraction.All)
                if (player.IsOwner) return player;
            return null;
        }

        /// <summary>A square window of <see cref="Span"/> tiles centred on a world point, drawn into <see cref="Rect"/>.</summary>
        readonly struct View
        {
            public View(Rect rect, Vector2 centre, int span)
            {
                Rect = rect;
                Span = span;
                var size = IslandGenerator.Size;
                // Bottom-left of the window in map-texture tiles (the map spans ±size/2 around the world origin).
                _left = centre.x + size / 2f - span / 2f;
                _bottom = centre.y + size / 2f - span / 2f;
                Uv = new Rect(_left / size, _bottom / size, (float)span / size, (float)span / size);
            }

            readonly float _left, _bottom;
            public Rect Rect { get; }
            public int Span { get; }
            public Rect Uv { get; }

            public Vector2 ToGui(Vector2 world)
            {
                var size = IslandGenerator.Size;
                var u = (world.x + size / 2f - _left) / Span;
                var v = (world.y + size / 2f - _bottom) / Span;
                return new Vector2(Rect.x + u * Rect.width, Rect.y + (1f - v) * Rect.height);
            }

            public Vector2 ToWorld(Vector2 gui)
            {
                var size = IslandGenerator.Size;
                var u = (gui.x - Rect.x) / Rect.width;
                var v = 1f - (gui.y - Rect.y) / Rect.height;
                return new Vector2(_left + u * Span - size / 2f, _bottom + v * Span - size / 2f);
            }
        }
    }
}
