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
        /// <summary>Texture side in pixels. High enough to stay crisp at the game's zoom (≈120 px per tile) with
        /// anti-aliased edges — the developer asked for high-resolution images, not pixel art.</summary>
        const int Size = 256;

        // ART_PIPELINE 2026-10-06 direction: the cartoon's near-black ink, the same as the stick figure's (SYS-CHAR-02).
        static readonly Color Outline = new Color32(0x16, 0x13, 0x0F, 0xFF);
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
            "sea_cave", "giant_tree", "sinkhole", "ruin_gate", "stairs_down", "stairs_up", "key", "locked_door",
            "gate_water", "gate_roots", "gate_bars", "gate_runes", "chest", "ore_vein", "furnace", "anvil", "wreckage",
        };

        /// <summary>A sprite one world unit across at scale 1, cached per (shape, colour).</summary>
        public static Sprite Sprite(string shape, Color colour)
        {
            if (_cache.TryGetValue((shape, colour), out var sprite)) return sprite;
            var texture = Draw(shape, colour);
            return _cache[(shape, colour)] = UnityEngine.Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size, 0, SpriteMeshType.FullRect);
        }

        static readonly Dictionary<(string, Color, int), Sprite> _standing = new();

        /// <summary>The same drawing with its pivot at the foot of the shape (shapes stand on the bottom of their canvas),
        /// so a tree is placed by its trunk and sorts by where it meets the ground. <paramref name="variant"/> picks one
        /// of a shape's alternative drawings (<see cref="VariantCount"/>); shapes without any ignore it.</summary>
        public static Sprite StandingSprite(string shape, Color colour, int variant = 0)
        {
            variant = VariantCount(shape) > 1 ? Mathf.Abs(variant) % VariantCount(shape) : 0;
            if (_standing.TryGetValue((shape, colour, variant), out var sprite)) return sprite;
            var texture = variant == 0 ? Sprite(shape, colour).texture : Draw(shape, colour, variant);
            return _standing[(shape, colour, variant)] = UnityEngine.Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, FootPivot), Size, 0, SpriteMeshType.FullRect);
        }

        /// <summary>How many drawings a shape has: trees 4 (round, tall, spreading, conifer), palms 2.</summary>
        public static int VariantCount(string shape) => shape switch
        {
            "tree" => 4,
            "palm" => 2,
            _ => 1,
        };

        /// <summary>The tree's other drawings: 1 tall and narrow, 2 low and spreading, 3 a conifer.</summary>
        static void DrawTreeVariant(Canvas canvas, int variant, Color c, Color light, Color dark)
        {
            switch (variant)
            {
                case 1:
                    canvas.Tri(0.38f, 0.04f, 0.62f, 0.04f, 0.5f, 0.16f, Bark);
                    canvas.Rect(0.46f, 0.04f, 0.54f, 0.42f, Bark);
                    canvas.Ellipse(0.5f, 0.46f, 0.17f, 0.12f, dark);
                    canvas.Ellipse(0.5f, 0.6f, 0.2f, 0.2f, c);
                    canvas.Ellipse(0.46f, 0.76f, 0.16f, 0.16f, c);
                    canvas.Ellipse(0.55f, 0.85f, 0.12f, 0.12f, c);
                    canvas.Inked = false;
                    canvas.Ellipse(0.43f, 0.72f, 0.05f, 0.08f, light);
                    canvas.Circle(0.55f, 0.86f, 0.04f, light);
                    canvas.Inked = true;
                    break;
                case 2:
                    canvas.Tri(0.3f, 0.04f, 0.7f, 0.04f, 0.5f, 0.22f, Bark);
                    canvas.Rect(0.42f, 0.04f, 0.58f, 0.4f, Bark);
                    canvas.Line(0.46f, 0.32f, 0.26f, 0.46f, 0.03f, Bark);
                    canvas.Line(0.54f, 0.32f, 0.76f, 0.46f, 0.03f, Bark);
                    canvas.Circle(0.22f, 0.48f, 0.15f, dark);
                    canvas.Circle(0.78f, 0.48f, 0.15f, dark);
                    canvas.Circle(0.5f, 0.46f, 0.17f, dark);
                    canvas.Circle(0.17f, 0.6f, 0.15f, c);
                    canvas.Circle(0.83f, 0.6f, 0.15f, c);
                    canvas.Circle(0.36f, 0.66f, 0.19f, c);
                    canvas.Circle(0.64f, 0.66f, 0.19f, c);
                    canvas.Circle(0.5f, 0.76f, 0.16f, c);
                    canvas.Inked = false;
                    canvas.Circle(0.32f, 0.72f, 0.06f, light);
                    canvas.Circle(0.6f, 0.78f, 0.05f, light);
                    canvas.Circle(0.16f, 0.64f, 0.04f, light);
                    canvas.Inked = true;
                    break;
                default:
                    // Conifer: four stacked tiers, darker underneath.
                    canvas.Rect(0.46f, 0.04f, 0.54f, 0.26f, Bark);
                    canvas.Tri(0.14f, 0.18f, 0.86f, 0.18f, 0.5f, 0.52f, dark);
                    canvas.Tri(0.19f, 0.34f, 0.81f, 0.34f, 0.5f, 0.68f, Color.Lerp(c, dark, 0.5f));
                    canvas.Tri(0.25f, 0.5f, 0.75f, 0.5f, 0.5f, 0.83f, c);
                    canvas.Tri(0.32f, 0.66f, 0.68f, 0.66f, 0.5f, 0.97f, c);
                    canvas.Inked = false;
                    canvas.Tri(0.4f, 0.7f, 0.47f, 0.7f, 0.47f, 0.9f, light);
                    canvas.Inked = true;
                    break;
            }
        }

        /// <summary>Fraction of the canvas below a standing shape's foot (trunks start at about 4%).</summary>
        public const float FootPivot = 0.06f;

        /// <summary>"#RRGGBB" from a def, or <paramref name="fallback"/> when missing or malformed.</summary>
        public static Color ParseColour(string hex, Color fallback) =>
            !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var colour) ? colour : fallback;

        public static Texture2D Draw(string shape, Color c, int variant = 0)
        {
            var canvas = new Canvas();
            var light = Color.Lerp(c, Color.white, 0.35f);
            var dark = Color.Lerp(c, Color.black, 0.35f);
            if (shape == "tree" && variant > 0)
            {
                DrawTreeVariant(canvas, variant, c, light, dark);
                return canvas.Finish();
            }
            switch (shape)
            {
                case "tree":
                {
                    // A chunky cartoon tree: flared trunk, a cloud of overlapping leaf blobs, a darker underside.
                    canvas.Tri(0.34f, 0.04f, 0.66f, 0.04f, 0.5f, 0.2f, Bark);
                    canvas.Rect(0.44f, 0.04f, 0.56f, 0.5f, Bark);
                    canvas.Line(0.52f, 0.36f, 0.66f, 0.5f, 0.025f, Bark);
                    canvas.Inked = false;
                    canvas.Rect(0.465f, 0.08f, 0.49f, 0.42f, Color.Lerp(Bark, Color.white, 0.15f));
                    canvas.Inked = true;
                    canvas.Circle(0.3f, 0.56f, 0.17f, dark);
                    canvas.Circle(0.7f, 0.56f, 0.17f, dark);
                    canvas.Circle(0.5f, 0.52f, 0.2f, dark);
                    canvas.Circle(0.28f, 0.68f, 0.18f, c);
                    canvas.Circle(0.72f, 0.68f, 0.18f, c);
                    canvas.Circle(0.5f, 0.74f, 0.23f, c);
                    canvas.Circle(0.4f, 0.86f, 0.12f, c);
                    canvas.Circle(0.6f, 0.86f, 0.11f, c);
                    canvas.Inked = false;
                    canvas.Circle(0.4f, 0.8f, 0.07f, light);
                    canvas.Circle(0.26f, 0.7f, 0.05f, light);
                    canvas.Circle(0.64f, 0.76f, 0.04f, light);
                    canvas.Inked = true;
                    break;
                }
                case "palm":
                {
                    // A leaning, segmented trunk under a burst of drooping fronds and two coconuts. Variant 1 is taller,
                    // straighter, with a fuller crown.
                    var tall = variant == 1;
                    var px = tall ? new[] { 0.48f, 0.49f, 0.5f, 0.52f, 0.54f } : new[] { 0.46f, 0.48f, 0.52f, 0.57f, 0.6f };
                    var py = tall ? new[] { 0.04f, 0.22f, 0.4f, 0.57f, 0.72f } : new[] { 0.04f, 0.2f, 0.36f, 0.52f, 0.66f };
                    for (var i = 0; i < 4; i++) canvas.Line(px[i], py[i], px[i + 1], py[i + 1], 0.045f - i * 0.004f, i % 2 == 0 ? Bark : Color.Lerp(Bark, Color.white, 0.12f));
                    // Fronds fan out from the crown and droop at the tips — the side ones most.
                    var crownX = px[4];
                    var crownY = py[4];
                    var fronds = tall ? 9 : 7;
                    for (var i = 0; i < fronds; i++)
                    {
                        var a = Mathf.Lerp(12f, 168f, i / (fronds - 1f)) * Mathf.Deg2Rad;
                        var side = Mathf.Abs(Mathf.Cos(a));
                        var tipX = crownX + Mathf.Cos(a) * 0.36f;
                        var tipY = crownY + Mathf.Sin(a) * 0.2f - 0.2f * side * side;
                        var midX = crownX + Mathf.Cos(a) * 0.22f;
                        var midY = crownY + Mathf.Sin(a) * 0.16f;
                        var colour = i % 2 == 0 ? c : dark;
                        canvas.Tri(crownX, crownY, midX, midY + 0.06f, tipX, tipY, colour);
                        canvas.Tri(crownX, crownY, midX, midY - 0.04f, tipX, tipY, colour);
                    }
                    canvas.Circle(crownX - 0.04f, crownY - 0.04f, 0.045f, Bark);
                    canvas.Circle(crownX + 0.05f, crownY - 0.05f, 0.045f, Bark);
                    break;
                }
                case "rock":
                    // A faceted boulder with a lit top face and a smaller stone leaning on it.
                    canvas.Ellipse(0.5f, 0.3f, 0.42f, 0.24f, c);
                    canvas.Inked = false;
                    canvas.Tri(0.22f, 0.36f, 0.5f, 0.52f, 0.62f, 0.4f, light);
                    canvas.Tri(0.62f, 0.4f, 0.5f, 0.52f, 0.78f, 0.42f, Color.Lerp(c, Color.white, 0.18f));
                    canvas.Line(0.56f, 0.22f, 0.64f, 0.14f, 0.012f, dark);
                    canvas.Inked = true;
                    canvas.Ellipse(0.82f, 0.18f, 0.12f, 0.09f, Color.Lerp(c, Color.black, 0.15f));
                    break;
                case "bush":
                    canvas.Circle(0.3f, 0.32f, 0.2f, dark);
                    canvas.Circle(0.7f, 0.32f, 0.2f, dark);
                    canvas.Circle(0.5f, 0.48f, 0.26f, c);
                    canvas.Circle(0.28f, 0.42f, 0.18f, c);
                    canvas.Circle(0.72f, 0.42f, 0.18f, c);
                    foreach (var (x, y) in new[] { (0.36f, 0.52f), (0.6f, 0.56f), (0.5f, 0.38f), (0.66f, 0.38f), (0.28f, 0.38f), (0.48f, 0.66f) })
                        canvas.Circle(x, y, 0.045f, new Color(0.86f, 0.16f, 0.3f));
                    canvas.Inked = false;
                    canvas.Circle(0.42f, 0.62f, 0.05f, light);
                    canvas.Inked = true;
                    break;
                case "grass":
                    for (var i = 0; i < 7; i++)
                    {
                        var x = 0.2f + i * 0.1f;
                        var lean = (i - 3) * 0.035f;
                        canvas.Tri(x - 0.04f, 0.06f, x + 0.04f, 0.06f, x + lean + (i % 2 == 0 ? -0.04f : 0.04f), 0.5f + (i % 3) * 0.12f, i % 2 == 0 ? c : light);
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
                case "wreckage":
                    // Broken planks and a split crate washed up on the sand.
                    canvas.Rect(0.12f, 0.12f, 0.72f, 0.24f, c);
                    canvas.Tri(0.62f, 0.3f, 0.95f, 0.38f, 0.9f, 0.48f, Color.Lerp(c, Color.black, 0.15f));
                    canvas.Rect(0.2f, 0.26f, 0.5f, 0.56f, Color.Lerp(c, Color.white, 0.12f));
                    canvas.Line(0.2f, 0.41f, 0.5f, 0.41f, 0.02f, Color.Lerp(c, Color.black, 0.35f));
                    canvas.Line(0.05f, 0.08f, 0.4f, 0.04f, 0.03f, new Color(0.85f, 0.8f, 0.65f));
                    break;
                case "ore_vein":
                {
                    // A grey boulder shot through with the ore's colour (the def's colour).
                    var stone = new Color(0.52f, 0.52f, 0.54f);
                    canvas.Ellipse(0.5f, 0.3f, 0.42f, 0.25f, stone);
                    canvas.Inked = false;
                    canvas.Tri(0.22f, 0.36f, 0.5f, 0.52f, 0.62f, 0.4f, Color.Lerp(stone, Color.white, 0.2f));
                    canvas.Inked = true;
                    foreach (var (x, y, r) in new[] { (0.34f, 0.3f, 0.06f), (0.52f, 0.4f, 0.07f), (0.68f, 0.26f, 0.05f), (0.46f, 0.18f, 0.045f) })
                        canvas.Tri(x - r, y, x, y + r * 1.4f, x + r, y, c);
                    canvas.Ellipse(0.84f, 0.16f, 0.11f, 0.08f, Color.Lerp(stone, Color.black, 0.15f));
                    break;
                }
                case "furnace":
                    canvas.Rect(0.18f, 0.05f, 0.82f, 0.62f, c);
                    canvas.Tri(0.12f, 0.6f, 0.88f, 0.6f, 0.5f, 0.8f, dark);
                    canvas.Rect(0.42f, 0.74f, 0.58f, 0.96f, dark);
                    canvas.Rect(0.34f, 0.1f, 0.66f, 0.36f, new Color(0.15f, 0.1f, 0.08f));
                    canvas.Inked = false;
                    canvas.Ellipse(0.5f, 0.18f, 0.12f, 0.07f, new Color(1f, 0.55f, 0.15f));
                    canvas.Inked = true;
                    break;
                case "anvil":
                    canvas.Rect(0.36f, 0.05f, 0.64f, 0.12f, Bark);
                    canvas.Rect(0.42f, 0.12f, 0.58f, 0.36f, c);
                    canvas.Rect(0.18f, 0.36f, 0.86f, 0.52f, c);
                    canvas.Tri(0.18f, 0.52f, 0.18f, 0.36f, 0.04f, 0.48f, c);
                    canvas.Inked = false;
                    canvas.Rect(0.22f, 0.47f, 0.82f, 0.5f, light);
                    canvas.Inked = true;
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
                // --- SYS-DUNG-01: entrances, stairs and obstacles ---------------------------------------------
                case "sea_cave":
                    canvas.Ellipse(0.5f, 0.32f, 0.46f, 0.28f, dark);
                    canvas.Tri(0.08f, 0.1f, 0.5f, 0.86f, 0.92f, 0.1f, c);
                    canvas.Tri(0.62f, 0.1f, 0.78f, 0.62f, 0.92f, 0.1f, light);
                    canvas.Ellipse(0.5f, 0.24f, 0.2f, 0.18f, new Color(0.06f, 0.06f, 0.08f));
                    canvas.Inked = false;
                    canvas.Ellipse(0.5f, 0.1f, 0.24f, 0.05f, Water);
                    canvas.Inked = true;
                    break;
                case "giant_tree":
                    canvas.Tri(0.2f, 0.04f, 0.8f, 0.04f, 0.5f, 0.24f, Bark);
                    canvas.Rect(0.34f, 0.04f, 0.66f, 0.62f, Bark);
                    canvas.Ellipse(0.5f, 0.2f, 0.11f, 0.15f, new Color(0.06f, 0.05f, 0.04f));
                    canvas.Circle(0.26f, 0.72f, 0.2f, Color.Lerp(c, Color.black, 0.2f));
                    canvas.Circle(0.74f, 0.72f, 0.2f, Color.Lerp(c, Color.black, 0.2f));
                    canvas.Circle(0.5f, 0.8f, 0.24f, c);
                    canvas.Inked = false;
                    canvas.Circle(0.42f, 0.86f, 0.07f, light);
                    canvas.Inked = true;
                    break;
                case "sinkhole":
                    canvas.Ellipse(0.5f, 0.32f, 0.46f, 0.24f, c);
                    canvas.Ellipse(0.5f, 0.3f, 0.34f, 0.16f, dark);
                    canvas.Ellipse(0.5f, 0.28f, 0.22f, 0.09f, new Color(0.05f, 0.06f, 0.05f));
                    canvas.Line(0.12f, 0.42f, 0.2f, 0.6f, 0.02f, light);
                    canvas.Line(0.84f, 0.4f, 0.8f, 0.58f, 0.02f, light);
                    break;
                case "ruin_gate":
                    canvas.Rect(0.12f, 0.04f, 0.3f, 0.74f, c);
                    canvas.Rect(0.7f, 0.04f, 0.88f, 0.66f, c);
                    canvas.Rect(0.08f, 0.66f, 0.92f, 0.78f, light);
                    canvas.Rect(0.3f, 0.04f, 0.7f, 0.5f, new Color(0.07f, 0.06f, 0.06f));
                    canvas.Line(0.16f, 0.3f, 0.26f, 0.36f, 0.01f, dark);
                    break;
                case "stairs_down":
                case "stairs_up":
                    for (var i = 0; i < 4; i++)
                        canvas.Rect(0.18f + i * 0.04f, 0.1f + i * 0.14f, 0.82f - i * 0.04f, 0.22f + i * 0.14f, Color.Lerp(c, shape == "stairs_down" ? Color.black : Color.white, i * 0.16f));
                    break;
                case "key":
                    canvas.Circle(0.36f, 0.6f, 0.16f, c);
                    canvas.Inked = false;
                    canvas.Circle(0.36f, 0.6f, 0.07f, new Color(0, 0, 0, 0.6f));
                    canvas.Inked = true;
                    canvas.Rect(0.48f, 0.55f, 0.86f, 0.64f, c);
                    canvas.Rect(0.72f, 0.42f, 0.78f, 0.56f, c);
                    canvas.Rect(0.82f, 0.44f, 0.86f, 0.56f, c);
                    break;
                case "locked_door":
                    canvas.Rect(0.08f, 0.04f, 0.92f, 0.86f, Wood);
                    for (var i = 0; i < 4; i++) canvas.Rect(0.16f + i * 0.2f, 0.04f, 0.24f + i * 0.2f, 0.86f, Color.Lerp(Wood, Color.black, 0.3f));
                    canvas.Rect(0.08f, 0.46f, 0.92f, 0.56f, new Color(0.45f, 0.45f, 0.5f));
                    canvas.Circle(0.5f, 0.38f, 0.08f, new Color(0.85f, 0.7f, 0.3f));
                    break;
                case "gate_water":
                    canvas.Ellipse(0.5f, 0.4f, 0.46f, 0.3f, c);
                    canvas.Inked = false;
                    for (var i = 0; i < 3; i++) canvas.Line(0.2f + i * 0.2f, 0.4f + (i % 2) * 0.06f, 0.32f + i * 0.2f, 0.4f + (i % 2) * 0.06f, 0.015f, light);
                    canvas.Inked = true;
                    break;
                case "gate_roots":
                    for (var i = 0; i < 6; i++)
                        canvas.Line(0.1f + i * 0.16f, 0.04f, 0.2f + ((i * 37) % 5) * 0.12f, 0.9f, 0.05f, i % 2 == 0 ? c : light);
                    break;
                case "gate_bars":
                    canvas.Rect(0.06f, 0.82f, 0.94f, 0.92f, dark);
                    for (var i = 0; i < 6; i++) canvas.Rect(0.1f + i * 0.15f, 0.04f, 0.15f + i * 0.15f, 0.84f, c);
                    canvas.Rect(0.06f, 0.4f, 0.94f, 0.46f, c);
                    break;
                case "gate_runes":
                    canvas.Rect(0.1f, 0.04f, 0.9f, 0.88f, new Color(0.45f, 0.42f, 0.4f));
                    canvas.Inked = false;
                    canvas.Circle(0.5f, 0.46f, 0.2f, c);
                    canvas.Circle(0.5f, 0.46f, 0.12f, new Color(0.45f, 0.42f, 0.4f));
                    canvas.Line(0.3f, 0.72f, 0.7f, 0.72f, 0.02f, c);
                    canvas.Line(0.3f, 0.2f, 0.7f, 0.2f, 0.02f, c);
                    canvas.Inked = true;
                    break;
                case "chest":
                    canvas.Rect(0.12f, 0.08f, 0.88f, 0.58f, Wood);
                    canvas.Ellipse(0.5f, 0.6f, 0.38f, 0.16f, Color.Lerp(Wood, Color.white, 0.1f));
                    canvas.Rect(0.12f, 0.4f, 0.88f, 0.46f, new Color(0.45f, 0.45f, 0.5f));
                    canvas.Rect(0.44f, 0.3f, 0.56f, 0.46f, new Color(0.85f, 0.7f, 0.3f));
                    break;
                default:
                    canvas.Circle(0.5f, 0.5f, 0.45f, c);
                    break;
            }
            return canvas.Finish();
        }

        /// <summary>
        /// Signed-distance rasteriser in normalised coordinates (0..1, y up). Each primitive is composited with
        /// anti-aliased coverage, only inside its own bounding box; the silhouette's union distance then draws a
        /// soft dark outline (ART_PIPELINE: dark brown, never black).
        /// </summary>
        sealed class Canvas
        {
            const float AaWidth = 1f / Size;          // one pixel of edge softening
            const float OutlineWidth = 5f / Size;     // ink line around every primitive, in normalised units

            /// <summary>Primitives drawn while this is false get no ink line (highlights, small sheen spots).</summary>
            public bool Inked = true;

            readonly Color[] _pixels = new Color[Size * Size];
            readonly float[] _union = Fill(float.MaxValue);

            static float[] Fill(float value)
            {
                var array = new float[Size * Size];
                for (var i = 0; i < array.Length; i++) array[i] = value;
                return array;
            }

            void Paint(float minX, float minY, float maxX, float maxY, Func<float, float, float> sdf, Color colour)
            {
                var margin = OutlineWidth + 2f * AaWidth;
                var x0 = Mathf.Clamp(Mathf.FloorToInt((minX - margin) * Size), 0, Size - 1);
                var x1 = Mathf.Clamp(Mathf.CeilToInt((maxX + margin) * Size), 0, Size - 1);
                var y0 = Mathf.Clamp(Mathf.FloorToInt((minY - margin) * Size), 0, Size - 1);
                var y1 = Mathf.Clamp(Mathf.CeilToInt((maxY + margin) * Size), 0, Size - 1);
                for (var y = y0; y <= y1; y++)
                for (var x = x0; x <= x1; x++)
                {
                    var i = y * Size + x;
                    var d = sdf((x + 0.5f) / Size, (y + 0.5f) / Size);
                    if (d < _union[i]) _union[i] = d;
                    // Cartoon line work: each primitive's own ink ring first, then its fill — overlapping parts keep
                    // the line where one sits on another (a canopy's blobs, a crate's planks).
                    // Pencil: the outline's width wanders along the stroke.
                    if (Inked) Blend(i, Outline, Mathf.Clamp01(0.5f - (d - OutlineWidth * PencilLook.InkWobble(x, y)) / AaWidth));
                    var coverage = Mathf.Clamp01(0.5f - d / AaWidth);
                    if (coverage <= 0f) continue;
                    Blend(i, colour, coverage);
                }
            }

            void Blend(int i, Color colour, float coverage)
            {
                if (coverage <= 0f) return;
                var dst = _pixels[i];
                var a = coverage + dst.a * (1f - coverage);
                var rgb = (new Vector3(colour.r, colour.g, colour.b) * coverage + new Vector3(dst.r, dst.g, dst.b) * dst.a * (1f - coverage)) / Mathf.Max(a, 1e-5f);
                _pixels[i] = new Color(rgb.x, rgb.y, rgb.z, a);
            }

            public void Circle(float cx, float cy, float r, Color c) =>
                Paint(cx - r, cy - r, cx + r, cy + r, (x, y) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r, c);

            /// <summary>Approximate ellipse distance — exact on the axes, close enough between them for a placeholder.</summary>
            public void Ellipse(float cx, float cy, float rx, float ry, Color c) =>
                Paint(cx - rx, cy - ry, cx + rx, cy + ry, (x, y) =>
                {
                    var u = (x - cx) / rx;
                    var v = (y - cy) / ry;
                    return (Mathf.Sqrt(u * u + v * v) - 1f) * Mathf.Min(rx, ry);
                }, c);

            public void Rect(float x0, float y0, float x1, float y1, Color c) =>
                Paint(x0, y0, x1, y1, (x, y) =>
                {
                    var hx = (x1 - x0) * 0.5f;
                    var hy = (y1 - y0) * 0.5f;
                    var qx = Mathf.Abs(x - (x0 + hx)) - hx;
                    var qy = Mathf.Abs(y - (y0 + hy)) - hy;
                    return new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f);
                }, c);

            public void Tri(float ax, float ay, float bx, float by, float cx, float cy, Color c) =>
                Paint(Mathf.Min(ax, Mathf.Min(bx, cx)), Mathf.Min(ay, Mathf.Min(by, cy)), Mathf.Max(ax, Mathf.Max(bx, cx)), Mathf.Max(ay, Mathf.Max(by, cy)),
                    (x, y) => TriangleDistance(new Vector2(x, y), new Vector2(ax, ay), new Vector2(bx, by), new Vector2(cx, cy)), c);

            public void Line(float ax, float ay, float bx, float by, float halfWidth, Color c) =>
                Paint(Mathf.Min(ax, bx) - halfWidth, Mathf.Min(ay, by) - halfWidth, Mathf.Max(ax, bx) + halfWidth, Mathf.Max(ay, by) + halfWidth, (x, y) =>
                {
                    float vx = bx - ax, vy = by - ay;
                    var t = Mathf.Clamp01(((x - ax) * vx + (y - ay) * vy) / (vx * vx + vy * vy));
                    float dx = x - (ax + vx * t), dy = y - (ay + vy * t);
                    return Mathf.Sqrt(dx * dx + dy * dy) - halfWidth;
                }, c);

            /// <summary>Exact signed distance to a triangle (negative inside).</summary>
            static float TriangleDistance(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
            {
                Vector2 e0 = b - a, e1 = c - b, e2 = a - c;
                Vector2 v0 = p - a, v1 = p - b, v2 = p - c;
                var pq0 = v0 - e0 * Mathf.Clamp01(Vector2.Dot(v0, e0) / Vector2.Dot(e0, e0));
                var pq1 = v1 - e1 * Mathf.Clamp01(Vector2.Dot(v1, e1) / Vector2.Dot(e1, e1));
                var pq2 = v2 - e2 * Mathf.Clamp01(Vector2.Dot(v2, e2) / Vector2.Dot(e2, e2));
                var s = Mathf.Sign(e0.x * e2.y - e0.y * e2.x);
                var d0 = new Vector2(Vector2.Dot(pq0, pq0), s * (v0.x * e0.y - v0.y * e0.x));
                var d1 = new Vector2(Vector2.Dot(pq1, pq1), s * (v1.x * e1.y - v1.y * e1.x));
                var d2 = new Vector2(Vector2.Dot(pq2, pq2), s * (v2.x * e2.y - v2.y * e2.x));
                var dmin = Vector2.Min(Vector2.Min(d0, d1), d2);
                return -Mathf.Sqrt(dmin.x) * Mathf.Sign(dmin.y);
            }

            /// <summary>Canvas pixels per pencil paper cell.</summary>
            const float PixelsPerPaperCell = 2.2f;

            /// <summary>Pencil pass (<see cref="PencilLook"/>), then uploads (with mipmaps, trilinear). The ink is already
            /// drawn per primitive.</summary>
            public Texture2D Finish()
            {
                for (var y = 0; y < Size; y++)
                for (var x = 0; x < Size; x++)
                    _pixels[y * Size + x] = PencilLook.Shade(_pixels[y * Size + x], x / PixelsPerPaperCell, y / PixelsPerPaperCell);
                var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, mipChain: true) { filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp };
                texture.SetPixels(_pixels);
                texture.Apply(updateMipmaps: true);
                return texture;
            }
        }
    }
}
