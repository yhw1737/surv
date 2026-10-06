using System.Collections.Generic;
using UnityEngine;

namespace Isle.UI.Prototype
{
    /// <summary>
    /// One look for every window — warm dark wood panels, cream text, an amber accent — generated at runtime as
    /// anti-aliased rounded rects (nothing baked; ART_PIPELINE §Placeholders). Serves both the IMGUI windows
    /// (<see cref="Styles"/>) and the uGUI inventory (<see cref="Sprite"/>). Presentation values only.
    /// </summary>
    public static class UiTheme
    {
        public static readonly Color Panel = Hex("#2A231D", 0.95f);
        public static readonly Color PanelEdge = Hex("#6E5841");
        public static readonly Color Header = Hex("#1D1814", 0.9f);
        public static readonly Color Inset = Hex("#1B1713");
        public static readonly Color InsetEdge = Hex("#4A3D31");
        public static readonly Color Button = Hex("#4B3C2F");
        public static readonly Color ButtonHover = Hex("#5F4B39");
        public static readonly Color ButtonDown = Hex("#3A2F25");
        public static readonly Color ButtonEdge = Hex("#80684D");
        public static readonly Color Text = Hex("#F1E6D2");
        public static readonly Color Muted = Hex("#B3A48C");
        public static readonly Color Accent = Hex("#E9A94B");
        public static readonly Color Good = Hex("#9CCB6A");
        public static readonly Color Bad = Hex("#E2735E");

        static readonly Dictionary<string, Texture2D> Textures = new();
        static readonly Dictionary<string, Sprite> Sprites = new();

        /// <summary>A 9-sliceable rounded rect: <paramref name="radius"/> corners, <paramref name="edge"/> px border.</summary>
        public static Texture2D RoundedTexture(Color fill, Color border, int radius = 10, int edge = 2)
        {
            var key = $"{(Color32)fill}{(Color32)border}{radius}{edge}";
            if (Textures.TryGetValue(key, out var cached) && cached != null) return cached;

            var size = radius * 2 + 4;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                // Signed distance to the rounded rect's edge (negative inside).
                var px = Mathf.Abs(x + 0.5f - size * 0.5f) - (size * 0.5f - radius);
                var py = Mathf.Abs(y + 0.5f - size * 0.5f) - (size * 0.5f - radius);
                var outside = new Vector2(Mathf.Max(px, 0f), Mathf.Max(py, 0f)).magnitude + Mathf.Min(Mathf.Max(px, py), 0f) - radius;
                var coverage = Mathf.Clamp01(0.5f - outside);
                var inBorder = Mathf.Clamp01(outside + edge + 0.5f);
                var c = Color.Lerp(fill, border, edge > 0 ? inBorder : 0f);
                // A faint lighter band along the top edge reads as a bevel.
                if (y > size - radius && edge > 0) c = Color.Lerp(c, Color.white, 0.04f);
                c.a *= coverage;
                pixels[y * size + x] = c;
            }
            texture.SetPixels(pixels);
            texture.Apply();
            Textures[key] = texture;
            return texture;
        }

        /// <summary>The same shape as a sliced uGUI sprite.</summary>
        public static Sprite Sprite(Color fill, Color border, int radius = 10, int edge = 2)
        {
            var key = $"{(Color32)fill}{(Color32)border}{radius}{edge}";
            if (Sprites.TryGetValue(key, out var cached) && cached != null) return cached;
            var texture = RoundedTexture(fill, border, radius, edge);
            var b = radius + 1;
            var sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(b, b, b, b));
            Sprites[key] = sprite;
            return sprite;
        }

        public sealed class StyleSet
        {
            public GUIStyle Window, Header, Title, Text, Muted, Small, Button, ButtonOn, Inset, Slot, SlotOn, Close, Big;
        }

        static StyleSet _styles;

        /// <summary>IMGUI styles. Call from OnGUI only (GUI.skin needs a GUI context).</summary>
        public static StyleSet Styles
        {
            get
            {
                if (_styles != null && _styles.Window.normal.background != null) return _styles;
                var b = new RectOffset(11, 11, 11, 11);
                GUIStyle Box(Color fill, Color edge, int radius = 10) => new()
                {
                    normal = { background = RoundedTexture(fill, edge, radius) },
                    border = new RectOffset(radius + 1, radius + 1, radius + 1, radius + 1),
                };
                GUIStyle Label(int size, Color colour, FontStyle style = FontStyle.Normal) => new(GUI.skin.label)
                {
                    fontSize = size, fontStyle = style, normal = { textColor = colour }, wordWrap = true, richText = true,
                };

                var button = Box(Button, ButtonEdge, 7);
                button.hover.background = RoundedTexture(ButtonHover, Accent, 7);
                button.active.background = RoundedTexture(ButtonDown, ButtonEdge, 7);
                button.normal.textColor = button.hover.textColor = button.active.textColor = Text;
                button.alignment = TextAnchor.MiddleCenter;
                button.fontSize = 13;
                button.fontStyle = FontStyle.Bold;
                button.padding = new RectOffset(8, 8, 4, 4);

                var buttonOn = new GUIStyle(button) { normal = { background = RoundedTexture(Hex("#6B5236"), Accent, 7), textColor = Text } };
                buttonOn.hover.background = buttonOn.normal.background;

                var slot = Box(Inset, InsetEdge, 6);
                var slotOn = new GUIStyle(slot) { normal = { background = RoundedTexture(Hex("#2E261E"), Accent, 6) } };
                slot.hover.background = RoundedTexture(Hex("#262019"), Hex("#8C7254"), 6);

                _styles = new StyleSet
                {
                    Window = Box(Panel, PanelEdge, 12),
                    Header = Box(Header, Header, 8),
                    Inset = Box(Inset, InsetEdge, 8),
                    Title = Label(17, Text, FontStyle.Bold),
                    Text = Label(13, Text),
                    Muted = Label(12, Muted),
                    Small = Label(11, Muted),
                    Big = Label(15, Accent, FontStyle.Bold),
                    Button = button,
                    ButtonOn = buttonOn,
                    Slot = slot,
                    SlotOn = slotOn,
                    Close = new GUIStyle(button) { fontSize = 12, padding = new RectOffset(0, 0, 0, 2) },
                };
                _styles.Window.padding = b;
                return _styles;
            }
        }

        public static string Colour(string text, Color colour) => $"<color=#{ColorUtility.ToHtmlStringRGB(colour)}>{text}</color>";

        /// <summary>A small filled circle with an outline — an "icon" for an item, method or station until art exists.</summary>
        public static void Badge(Rect rect, Color fill)
        {
            var old = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(rect, RoundedTexture(fill, Color.Lerp(fill, Color.black, 0.45f), Mathf.Max(2, (int)(rect.width * 0.5f) - 2), 2));
            GUI.color = old;
        }

        static Color Hex(string hex, float alpha = 1f)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            c.a = alpha;
            return c;
        }
    }
}
