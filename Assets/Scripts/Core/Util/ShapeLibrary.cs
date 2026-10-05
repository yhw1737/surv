using System;
using System.Collections.Generic;
using UnityEngine;

namespace Isle.Core.Util
{
    /// <summary>
    /// Procedural placeholder silhouettes (ART_PIPELINE §Placeholders, Absolute Rule 7): each shape key draws a
    /// recognisable outline — a tree, a rabbit, a crate — from filled primitives on a small canvas, so things can be
    /// told apart before real art exists. Which shape a thing uses and its colour come from its def's
    /// <c>visual</c> block; gameplay never reads these sprites. Thrown away at stage 4 (T-160).
    /// </summary>
    public static class ShapeLibrary
    {
        const int Size = 64;

        // ART_PIPELINE §Style: outline is dark brown, never black.
        static readonly Color Outline = new Color32(0x3A, 0x2A, 0x1E, 0xFF);
        static readonly Color Bark = new Color32(0x6B, 0x45, 0x2A, 0xFF);
        static readonly Color Wood = new Color32(0xB0, 0x82, 0x4E, 0xFF);
        static readonly Color Water = new Color32(0x4A, 0x9C, 0xD8, 0xFF);
        static readonly Color Ivory = new Color32(0xF2, 0xEC, 0xDA, 0xFF);
        static readonly Color Eye = new Color32(0x20, 0x18, 0x10, 0xFF);

        static readonly Dictionary<(string, Color), Sprite> _cache = new();

        /// <summary>Shapes this library knows; anything else falls back to a plain circle.</summary>
        public static readonly string[] Known =
        {
            "tree", "palm", "rock", "bush", "grass", "water", "rabbit", "deer", "boar", "crocodile",
            "campfire", "crate", "warehouse", "workbench", "pot", "rack", "rain_catcher", "plot", "sack",
        };

        /// <summary>A sprite one world unit across at scale 1, cached per (shape, colour).</summary>
        public static Sprite Sprite(string shape, Color colour)
        {
            if (_cache.TryGetValue((shape, colour), out var sprite)) return sprite;
            var texture = Draw(shape, colour);
            return _cache[(shape, colour)] = UnityEngine.Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
        }

        /// <summary>"#RRGGBB" from a def, or <paramref name="fallback"/> when missing or malformed.</summary>
        public static Color ParseColour(string hex, Color fallback) =>
            !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var colour) ? colour : fallback;

        public static Texture2D Draw(string shape, Color c)
        {
            var canvas = new Canvas();
            var light = Color.Lerp(c, Color.white, 0.35f);
            var dark = Color.Lerp(c, Color.black, 0.35f);
            switch (shape)
            {
                case "tree":
                    canvas.Rect(0.43f, 0.04f, 0.57f, 0.42f, Bark);
                    canvas.Circle(0.5f, 0.62f, 0.35f, c);
                    canvas.Circle(0.38f, 0.72f, 0.13f, light);
                    break;
                case "palm":
                    canvas.Line(0.45f, 0.04f, 0.56f, 0.66f, 0.06f, Bark);
                    for (var i = 0; i < 5; i++)
                    {
                        var a = Mathf.PI * (0.1f + i * 0.2f);
                        canvas.Tri(0.56f, 0.7f, 0.56f + Mathf.Cos(a) * 0.44f, 0.7f + Mathf.Sin(a) * 0.28f - 0.06f, 0.56f + Mathf.Cos(a + 0.25f) * 0.3f, 0.7f + Mathf.Sin(a + 0.25f) * 0.2f, c);
                    }
                    canvas.Circle(0.5f, 0.64f, 0.05f, Bark);
                    canvas.Circle(0.61f, 0.63f, 0.05f, Bark);
                    break;
                case "rock":
                    canvas.Ellipse(0.5f, 0.4f, 0.42f, 0.28f, c);
                    canvas.Ellipse(0.62f, 0.5f, 0.2f, 0.16f, c);
                    canvas.Ellipse(0.4f, 0.48f, 0.14f, 0.07f, light);
                    break;
                case "bush":
                    canvas.Circle(0.33f, 0.42f, 0.21f, c);
                    canvas.Circle(0.67f, 0.42f, 0.21f, c);
                    canvas.Circle(0.5f, 0.58f, 0.24f, c);
                    foreach (var (x, y) in new[] { (0.38f, 0.55f), (0.6f, 0.5f), (0.5f, 0.38f), (0.56f, 0.7f), (0.3f, 0.4f) })
                        canvas.Circle(x, y, 0.05f, new Color(0.85f, 0.15f, 0.3f));
                    break;
                case "grass":
                    for (var i = 0; i < 7; i++)
                    {
                        var x = 0.18f + i * 0.11f;
                        canvas.Tri(x - 0.05f, 0.08f, x + 0.05f, 0.08f, x + (i % 2 == 0 ? -0.06f : 0.06f), 0.62f + (i % 3) * 0.1f, i % 2 == 0 ? c : light);
                    }
                    break;
                case "water":
                    canvas.Ellipse(0.5f, 0.5f, 0.46f, 0.32f, c);
                    canvas.Ellipse(0.5f, 0.5f, 0.3f, 0.18f, light);
                    canvas.Ellipse(0.5f, 0.5f, 0.24f, 0.13f, c);
                    break;
                case "rabbit":
                    canvas.Ellipse(0.44f, 0.34f, 0.27f, 0.19f, c);
                    canvas.Circle(0.7f, 0.48f, 0.14f, c);
                    canvas.Ellipse(0.65f, 0.72f, 0.045f, 0.15f, c);
                    canvas.Ellipse(0.76f, 0.71f, 0.045f, 0.14f, light);
                    canvas.Circle(0.17f, 0.38f, 0.07f, Ivory);
                    canvas.Circle(0.75f, 0.5f, 0.025f, Eye);
                    break;
                case "deer":
                    foreach (var x in new[] { 0.25f, 0.34f, 0.56f, 0.65f }) canvas.Rect(x - 0.03f, 0.06f, x + 0.03f, 0.42f, dark);
                    canvas.Ellipse(0.45f, 0.46f, 0.3f, 0.14f, c);
                    canvas.Line(0.7f, 0.5f, 0.78f, 0.72f, 0.05f, c);
                    canvas.Ellipse(0.83f, 0.73f, 0.09f, 0.055f, c);
                    canvas.Line(0.8f, 0.78f, 0.7f, 0.96f, 0.02f, Bark);
                    canvas.Line(0.84f, 0.78f, 0.92f, 0.96f, 0.02f, Bark);
                    canvas.Circle(0.86f, 0.74f, 0.018f, Eye);
                    break;
                case "boar":
                    foreach (var x in new[] { 0.25f, 0.38f, 0.55f, 0.66f }) canvas.Rect(x - 0.035f, 0.08f, x + 0.035f, 0.26f, dark);
                    canvas.Ellipse(0.45f, 0.42f, 0.35f, 0.24f, c);
                    canvas.Ellipse(0.42f, 0.6f, 0.22f, 0.07f, dark);
                    canvas.Circle(0.77f, 0.42f, 0.16f, c);
                    canvas.Ellipse(0.92f, 0.38f, 0.06f, 0.07f, light);
                    canvas.Tri(0.86f, 0.32f, 0.9f, 0.3f, 0.96f, 0.5f, Ivory);
                    canvas.Circle(0.8f, 0.5f, 0.022f, Eye);
                    break;
                case "crocodile":
                    canvas.Tri(0.0f, 0.5f, 0.22f, 0.6f, 0.22f, 0.4f, c);
                    foreach (var x in new[] { 0.3f, 0.6f }) canvas.Ellipse(x, 0.33f, 0.07f, 0.05f, dark);
                    canvas.Ellipse(0.46f, 0.5f, 0.3f, 0.13f, c);
                    canvas.Ellipse(0.84f, 0.49f, 0.16f, 0.065f, c);
                    for (var i = 0; i < 4; i++) canvas.Circle(0.3f + i * 0.1f, 0.6f, 0.03f, dark);
                    canvas.Circle(0.74f, 0.55f, 0.03f, Ivory);
                    canvas.Circle(0.745f, 0.555f, 0.014f, Eye);
                    break;
                case "campfire":
                    canvas.Line(0.18f, 0.12f, 0.82f, 0.32f, 0.06f, Bark);
                    canvas.Line(0.18f, 0.32f, 0.82f, 0.12f, 0.06f, Bark);
                    canvas.Tri(0.28f, 0.28f, 0.72f, 0.28f, 0.5f, 0.92f, new Color(1f, 0.45f, 0.1f));
                    canvas.Tri(0.38f, 0.28f, 0.62f, 0.28f, 0.5f, 0.66f, new Color(1f, 0.85f, 0.3f));
                    break;
                case "crate":
                    canvas.Rect(0.1f, 0.1f, 0.9f, 0.9f, c);
                    canvas.Rect(0.1f, 0.36f, 0.9f, 0.4f, dark);
                    canvas.Rect(0.1f, 0.6f, 0.9f, 0.64f, dark);
                    canvas.Line(0.14f, 0.14f, 0.86f, 0.86f, 0.03f, dark);
                    break;
                case "warehouse":
                    canvas.Rect(0.06f, 0.05f, 0.94f, 0.62f, c);
                    canvas.Tri(0.0f, 0.6f, 1.0f, 0.6f, 0.5f, 0.96f, dark);
                    canvas.Rect(0.4f, 0.05f, 0.6f, 0.38f, Bark);
                    break;
                case "workbench":
                    canvas.Rect(0.12f, 0.08f, 0.2f, 0.52f, Bark);
                    canvas.Rect(0.8f, 0.08f, 0.88f, 0.52f, Bark);
                    canvas.Rect(0.05f, 0.5f, 0.95f, 0.64f, c);
                    canvas.Rect(0.3f, 0.64f, 0.5f, 0.7f, new Color(0.6f, 0.6f, 0.65f));
                    canvas.Line(0.6f, 0.66f, 0.8f, 0.84f, 0.03f, Bark);
                    break;
                case "pot":
                    canvas.Ellipse(0.5f, 0.38f, 0.36f, 0.3f, c);
                    canvas.Ellipse(0.5f, 0.64f, 0.36f, 0.09f, light);
                    canvas.Ellipse(0.5f, 0.64f, 0.29f, 0.06f, Water);
                    break;
                case "rack":
                    canvas.Rect(0.12f, 0.05f, 0.18f, 0.9f, Bark);
                    canvas.Rect(0.82f, 0.05f, 0.88f, 0.9f, Bark);
                    canvas.Rect(0.1f, 0.82f, 0.9f, 0.88f, Bark);
                    foreach (var x in new[] { 0.32f, 0.5f, 0.68f }) canvas.Rect(x - 0.04f, 0.45f, x + 0.04f, 0.82f, c);
                    break;
                case "rain_catcher":
                    canvas.Rect(0.2f, 0.05f, 0.26f, 0.45f, Bark);
                    canvas.Rect(0.74f, 0.05f, 0.8f, 0.45f, Bark);
                    canvas.Ellipse(0.5f, 0.5f, 0.42f, 0.2f, c);
                    canvas.Ellipse(0.5f, 0.55f, 0.32f, 0.1f, Water);
                    break;
                case "plot":
                    canvas.Rect(0.06f, 0.06f, 0.94f, 0.94f, c);
                    foreach (var y in new[] { 0.28f, 0.5f, 0.72f }) canvas.Rect(0.1f, y - 0.025f, 0.9f, y + 0.025f, dark);
                    break;
                case "sack":
                    canvas.Ellipse(0.5f, 0.38f, 0.34f, 0.3f, c);
                    canvas.Tri(0.38f, 0.62f, 0.62f, 0.62f, 0.5f, 0.82f, c);
                    canvas.Rect(0.42f, 0.62f, 0.58f, 0.67f, dark);
                    break;
                default:
                    canvas.Circle(0.5f, 0.5f, 0.45f, c);
                    break;
            }
            return canvas.Finish();
        }

        /// <summary>A tiny fill-only rasteriser in normalised coordinates (0..1, y up), plus a final outline pass.</summary>
        sealed class Canvas
        {
            readonly Color[] _pixels = new Color[Size * Size];

            void Fill(Func<float, float, bool> inside, Color colour)
            {
                for (var y = 0; y < Size; y++)
                for (var x = 0; x < Size; x++)
                    if (inside((x + 0.5f) / Size, (y + 0.5f) / Size)) _pixels[y * Size + x] = colour;
            }

            public void Circle(float cx, float cy, float r, Color c) => Ellipse(cx, cy, r, r, c);

            public void Ellipse(float cx, float cy, float rx, float ry, Color c) =>
                Fill((x, y) => (x - cx) * (x - cx) / (rx * rx) + (y - cy) * (y - cy) / (ry * ry) <= 1f, c);

            public void Rect(float x0, float y0, float x1, float y1, Color c) =>
                Fill((x, y) => x >= x0 && x <= x1 && y >= y0 && y <= y1, c);

            public void Tri(float ax, float ay, float bx, float by, float cx, float cy, Color c) =>
                Fill((x, y) =>
                {
                    var d1 = Side(x, y, ax, ay, bx, by);
                    var d2 = Side(x, y, bx, by, cx, cy);
                    var d3 = Side(x, y, cx, cy, ax, ay);
                    var negative = d1 < 0 || d2 < 0 || d3 < 0;
                    var positive = d1 > 0 || d2 > 0 || d3 > 0;
                    return !(negative && positive);
                }, c);

            public void Line(float ax, float ay, float bx, float by, float halfWidth, Color c) =>
                Fill((x, y) =>
                {
                    float vx = bx - ax, vy = by - ay;
                    var t = Mathf.Clamp01(((x - ax) * vx + (y - ay) * vy) / (vx * vx + vy * vy));
                    float dx = x - (ax + vx * t), dy = y - (ay + vy * t);
                    return dx * dx + dy * dy <= halfWidth * halfWidth;
                }, c);

            static float Side(float px, float py, float ax, float ay, float bx, float by) => (px - bx) * (ay - by) - (ax - bx) * (py - by);

            /// <summary>Outlines every filled pixel that touches empty space, then uploads the texture.</summary>
            public Texture2D Finish()
            {
                var result = (Color[])_pixels.Clone();
                for (var y = 0; y < Size; y++)
                for (var x = 0; x < Size; x++)
                {
                    if (_pixels[y * Size + x].a <= 0f) continue;
                    if (Empty(x - 1, y) || Empty(x + 1, y) || Empty(x, y - 1) || Empty(x, y + 1)) result[y * Size + x] = Outline;
                }
                var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, mipChain: false) { filterMode = FilterMode.Bilinear };
                texture.SetPixels(result);
                texture.Apply();
                return texture;
            }

            bool Empty(int x, int y) => x < 0 || y < 0 || x >= Size || y >= Size || _pixels[y * Size + x].a <= 0f;
        }
    }
}
