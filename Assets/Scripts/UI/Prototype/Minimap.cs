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

        /// <summary>The big map is open (M, or the toolbar).</summary>
        public static bool Large { get; set; }
        int _miniZoom = 1;
        int _mapZoom = MapSpans.Length - 1;

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.mKey.wasPressedThisFrame) Large = !Large;
            if (kb.equalsKey.wasPressedThisFrame || kb.numpadPlusKey.wasPressedThisFrame) Zoom(-1);
            if (kb.minusKey.wasPressedThisFrame || kb.numpadMinusKey.wasPressedThisFrame) Zoom(+1);
        }

        /// <summary>-1 zooms in (fewer tiles across), +1 out.</summary>
        void Zoom(int step)
        {
            if (Large) _mapZoom = Mathf.Clamp(_mapZoom + step, 0, MapSpans.Length - 1);
            else _miniZoom = Mathf.Clamp(_miniZoom + step, 0, MiniSpans.Length - 1);
        }

        void OnGUI()
        {
            var world = IslandWorld.Instance;
            var state = MapState.Instance;
            if (world == null || world.Map == null || state == null) return;
            var me = LocalPlayer();
            if (me == null) return;

            var size = Large ? Mathf.Min(Screen.width, Screen.height) * LargeFraction : SmallSize;
            var rect = Large
                ? new Rect((Screen.width - size) * 0.5f, (Screen.height - size) * 0.5f, size, size)
                : new Rect(Margin, Screen.height - size - Margin, size, size);
            var span = Large ? MapSpans[_mapZoom] : MiniSpans[_miniZoom];
            Vector2 centre = me.transform.position;
            var view = new View(rect, centre, span, Isle.Core.Util.ViewTilt.YawDegrees);

            HandleInput(rect, view, state);
            if (Event.current.type != EventType.Repaint) return;
            PointerGate.Captured |= Large || rect.Contains(Event.current.mousePosition);

            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, rect.height + 4f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            // T-307: the map turns with the view (screen-up on the map is screen-up in the world).
            DrawTurned(view, world.Map);
            DrawTurned(view, state.FogTexture);

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

            if (Large)
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
            else if (Large && e.type == EventType.MouseDown)
            {
                var worldPoint = view.ToWorld(e.mousePosition);
                if (e.button == 0) state.Markers.Add(worldPoint);
                else if (e.button == 1) state.Markers.RemoveNear(worldPoint, RemoveRadiusPixels * view.Span / rect.width);
                e.Use();
            }
        }

        static Material _quad;

        /// <summary>The map window as one quad whose texture coordinates are turned — no corners poke out of the frame.</summary>
        static void DrawTurned(View view, Texture texture)
        {
            if (texture == null) return;
            if (_quad == null) _quad = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
            _quad.mainTexture = texture;
            var r = view.Rect;
            var corners = new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax) };
            GL.PushMatrix();
            GL.LoadPixelMatrix();
            _quad.SetPass(0);
            GL.Begin(GL.QUADS);
            GL.Color(Color.white);
            foreach (var corner in corners)
            {
                var uv = view.ToUv(corner);
                var screen = GUIUtility.GUIToScreenPoint(corner);
                GL.TexCoord2(uv.x, uv.y);
                GL.Vertex3(screen.x, Screen.height - screen.y, 0f);
            }
            GL.End();
            GL.PopMatrix();
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

        /// <summary>A square window of <see cref="Span"/> tiles centred on a world point, drawn into <see cref="Rect"/>,
        /// turned by <see cref="Yaw"/> degrees with the view (T-307).</summary>
        readonly struct View
        {
            public View(Rect rect, Vector2 centre, int span, float yaw)
            {
                Rect = rect;
                Span = span;
                Yaw = yaw;
                _centre = centre;
            }

            readonly Vector2 _centre;
            public Rect Rect { get; }
            public int Span { get; }
            public float Yaw { get; }

            float TilesPerPixel => Span / Rect.width;

            public Vector2 ToGui(Vector2 world)
            {
                var screen = (Vector2)(Quaternion.Euler(0f, 0f, -Yaw) * (world - _centre)) / TilesPerPixel;
                return new Vector2(Rect.center.x + screen.x, Rect.center.y - screen.y);
            }

            public Vector2 ToWorld(Vector2 gui)
            {
                var screen = new Vector2(gui.x - Rect.center.x, Rect.center.y - gui.y) * TilesPerPixel;
                return _centre + (Vector2)(Quaternion.Euler(0f, 0f, Yaw) * screen);
            }

            /// <summary>Map-texture coordinates under a GUI point (the map spans ±size/2 around the world origin).</summary>
            public Vector2 ToUv(Vector2 gui)
            {
                var size = IslandGenerator.Size;
                var world = ToWorld(gui);
                return new Vector2((world.x + size / 2f) / size, (world.y + size / 2f) / size);
            }
        }
    }
}
