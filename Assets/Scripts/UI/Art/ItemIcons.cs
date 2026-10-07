using System.Collections.Generic;
using System.Linq;
using Isle.Core.Ids;
using Isle.Data;
using Isle.Gameplay.Cooking;
using Isle.Modding.Defs;
using UnityEngine;

namespace Isle.UI.Art
{
    /// <summary>
    /// Cartoon icons for every item, drawn once and cached (SYS-CHAR-02 §Icons). What to draw comes from the def:
    /// <c>icon_style</c>, else the <c>hold</c>/<c>wear</c> block (so a held spear and its icon match), else a tag
    /// fallback. Dishes are drawn in their cook method's vessel, coloured by their ingredients.
    /// </summary>
    public static class ItemIcons
    {
        const int SizePx = 128;
        static readonly Dictionary<ItemDef, Texture2D> Cache = new();
        static readonly Dictionary<ItemDef, Sprite> Sprites = new();
        static readonly Dictionary<string, Texture2D> MethodCache = new();

        public static Texture2D Texture(ItemDef item)
        {
            if (item == null) return null;
            if (Cache.TryGetValue(item, out var cached) && cached != null) return cached;
            var painter = new IconPainter(SizePx);
            Paint(painter, item);
            var texture = painter.ToTexture();
            Cache[item] = texture;
            return texture;
        }

        public static Sprite Sprite(ItemDef item)
        {
            if (item == null) return null;
            if (Sprites.TryGetValue(item, out var cached) && cached != null) return cached;
            var texture = Texture(item);
            var sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, SizePx, SizePx), new Vector2(0.5f, 0.5f), SizePx);
            Sprites[item] = sprite;
            return sprite;
        }

        /// <summary>A cook method's empty vessel, for the cooking window's method list.</summary>
        public static Texture2D MethodTexture(CookMethodDef method)
        {
            var key = method.Id.Value;
            if (MethodCache.TryGetValue(key, out var cached) && cached != null) return cached;
            var painter = new IconPainter(SizePx);
            PaintDish(painter, method.IconStyle?.Shape ?? "plate", new List<Color>());
            var texture = painter.ToTexture();
            MethodCache[key] = texture;
            return texture;
        }

        static readonly Dictionary<string, Texture2D> PreviewCache = new();

        /// <summary>What a dish of <paramref name="method"/> from these ingredients will look like, before it's cooked.</summary>
        public static Texture2D DishPreview(CookMethodDef method, IReadOnlyList<ItemDef> ingredients)
        {
            var key = method.Id.Value + ":" + string.Join(",", ingredients.Select(i => i.Id.Value));
            if (PreviewCache.TryGetValue(key, out var cached) && cached != null) return cached;
            if (PreviewCache.Count > 64) PreviewCache.Clear();
            var painter = new IconPainter(SizePx);
            PaintDish(painter, method.IconStyle?.Shape ?? "plate", ingredients.Select(MainColour).ToList());
            var texture = painter.ToTexture();
            PreviewCache[key] = texture;
            return texture;
        }

        /// <summary>The colour an item reads as — its icon's main colour — for tinting dishes made from it.</summary>
        public static Color MainColour(ItemDef item)
        {
            if (item == null) return Color.gray;
            if (item.IconStyle?.Color is { } c) return Parse(c, Color.gray);
            if (item.Hold?.Color is { } h) return Parse(h, Color.gray);
            if (item.Wear?.Color is { } w) return Parse(w, Color.gray);
            return Isle.Core.Util.PlaceholderVisuals.ColorForTags(item.Tags);
        }

        static void Paint(IconPainter p, ItemDef item)
        {
            if (DishFactory.IsDish(item))
            {
                var methodId = item.DishSignature?.Split('|')[0];
                var method = methodId != null && DefRegistry.TryGet<CookMethodDef>(NamespacedId.Parse(methodId), out var m) ? m : null;
                var colours = DishFactory.IngredientsOf(item)
                    .Select(id => DefRegistry.TryGet<ItemDef>(NamespacedId.Parse(id), out var d) ? MainColour(d) : Color.gray).ToList();
                PaintDish(p, method?.IconStyle?.Shape ?? "plate", colours);
                return;
            }
            if (item.IconStyle != null)
            {
                PaintStyle(p, item.IconStyle.Shape, Parse(item.IconStyle.Color, Color.gray), Parse(item.IconStyle.Accent, Color.white));
                return;
            }
            if (item.Hold != null)
            {
                PaintHeld(p, item.Hold.Style, Parse(item.Hold.Color, Color.gray), Parse(item.Hold.Tip, Color.gray));
                return;
            }
            if (item.Wear != null)
            {
                PaintWorn(p, item.Wear.Style, Parse(item.Wear.Color, Color.gray));
                return;
            }
            // Fallback: a rounded tile in the tag colour.
            p.Box(new Vector2(0.5f, 0.5f), new Vector2(0.3f, 0.3f), 0.08f, 0f, MainColour(item));
        }

        static Vector2 V(float x, float y) => new(x, y);
        static Color Light(Color c, float t = 0.35f) => Color.Lerp(c, Color.white, t);
        static Color Dark(Color c, float t = 0.3f) => Color.Lerp(c, Color.black, t);

        static void PaintStyle(IconPainter p, string shape, Color c, Color a)
        {
            switch (shape)
            {
                case "log":
                    p.Box(V(0.46f, 0.47f), V(0.32f, 0.15f), 0.13f, 25f, c);
                    p.Line(V(0.25f, 0.38f), V(0.55f, 0.52f), 0.02f, Dark(c), false);
                    p.Line(V(0.32f, 0.47f), V(0.6f, 0.6f), 0.02f, Dark(c), false);
                    p.Ellipse(V(0.74f, 0.6f), 0.11f, 0.15f, 25f, a);
                    p.Ellipse(V(0.74f, 0.6f), 0.05f, 0.07f, 25f, Dark(a, 0.15f), false);
                    p.Line(V(0.42f, 0.28f), V(0.36f, 0.18f), 0.04f, Light(Color.green * 0.6f + c * 0.4f));
                    break;
                case "rock":
                    p.Polygon(new[] { V(0.18f, 0.3f), V(0.3f, 0.66f), V(0.55f, 0.78f), V(0.8f, 0.62f), V(0.84f, 0.32f), V(0.6f, 0.2f) }, c, true, 0.04f);
                    p.Polygon(new[] { V(0.34f, 0.6f), V(0.52f, 0.7f), V(0.64f, 0.6f), V(0.46f, 0.5f) }, a, false, 0.02f);
                    p.Line(V(0.55f, 0.42f), V(0.68f, 0.3f), 0.02f, Dark(c), false);
                    break;
                case "fiber":
                    for (var i = 0; i < 7; i++)
                    {
                        var t = i / 6f;
                        p.Line(V(0.3f + t * 0.4f, 0.15f), V(0.22f + t * 0.56f, 0.85f), 0.045f, i % 2 == 0 ? c : Light(c, 0.2f));
                    }
                    p.Box(V(0.5f, 0.48f), V(0.24f, 0.06f), 0.03f, 0f, a);
                    break;
                case "pelt":
                    p.Polygon(new[] { V(0.2f, 0.25f), V(0.14f, 0.55f), V(0.28f, 0.8f), V(0.5f, 0.72f), V(0.72f, 0.82f), V(0.86f, 0.55f), V(0.8f, 0.25f), V(0.5f, 0.16f) }, c, true, 0.03f);
                    p.Disk(V(0.4f, 0.48f), 0.05f, a, false);
                    p.Disk(V(0.6f, 0.4f), 0.04f, a, false);
                    p.Disk(V(0.58f, 0.6f), 0.035f, a, false);
                    break;
                case "steak":
                    p.Line(V(0.66f, 0.62f), V(0.86f, 0.82f), 0.07f, a);
                    p.Disk(V(0.88f, 0.86f), 0.05f, a);
                    p.Ellipse(V(0.45f, 0.42f), 0.32f, 0.24f, 20f, c);
                    p.Line(V(0.24f, 0.38f), V(0.6f, 0.56f), 0.05f, a, false);
                    p.Ellipse(V(0.36f, 0.32f), 0.06f, 0.035f, 20f, Light(c), false);
                    break;
                case "fish":
                    p.Polygon(new[] { V(0.72f, 0.5f), V(0.92f, 0.7f), V(0.92f, 0.3f) }, Dark(c, 0.1f), true, 0.02f);
                    p.Ellipse(V(0.45f, 0.5f), 0.32f, 0.17f, 0f, c);
                    p.Ellipse(V(0.45f, 0.43f), 0.24f, 0.07f, 0f, a, false);
                    p.Disk(V(0.25f, 0.54f), 0.04f, Color.white, false);
                    p.Disk(V(0.25f, 0.54f), 0.02f, IconPainter.Ink, false);
                    p.Line(V(0.4f, 0.62f), V(0.52f, 0.66f), 0.02f, Dark(c), false);
                    break;
                case "berries":
                    p.Polygon(new[] { V(0.5f, 0.78f), V(0.72f, 0.9f), V(0.66f, 0.72f) }, a, true, 0.02f);
                    p.Disk(V(0.36f, 0.4f), 0.15f, c);
                    p.Disk(V(0.62f, 0.38f), 0.15f, Dark(c, 0.1f));
                    p.Disk(V(0.5f, 0.6f), 0.15f, Light(c, 0.1f));
                    foreach (var h in new[] { V(0.31f, 0.45f), V(0.57f, 0.43f), V(0.45f, 0.65f) }) p.Disk(h, 0.035f, Light(c, 0.6f), false);
                    break;
                case "seeds":
                    p.Polygon(new[] { V(0.24f, 0.2f), V(0.2f, 0.55f), V(0.38f, 0.7f), V(0.62f, 0.7f), V(0.8f, 0.55f), V(0.76f, 0.2f) }, a, true, 0.04f);
                    p.Line(V(0.36f, 0.7f), V(0.64f, 0.7f), 0.05f, Dark(a));
                    for (var i = 0; i < 6; i++) p.Ellipse(V(0.32f + i % 3 * 0.18f, 0.32f + i / 3 * 0.16f), 0.05f, 0.035f, 30f, c);
                    break;
                case "coconut":
                    p.Disk(V(0.5f, 0.48f), 0.3f, c);
                    p.Ellipse(V(0.4f, 0.58f), 0.08f, 0.05f, 30f, Light(c, 0.25f), false);
                    foreach (var h in new[] { V(0.44f, 0.36f), V(0.56f, 0.36f), V(0.5f, 0.27f) }) p.Disk(h, 0.035f, Dark(c, 0.5f), false);
                    break;
                case "rotten":
                    p.Polygon(new[] { V(0.2f, 0.25f), V(0.22f, 0.5f), V(0.4f, 0.62f), V(0.66f, 0.6f), V(0.82f, 0.42f), V(0.74f, 0.22f) }, c, true, 0.05f);
                    p.Disk(V(0.42f, 0.4f), 0.06f, a, false);
                    p.Disk(V(0.62f, 0.36f), 0.04f, a, false);
                    for (var i = 0; i < 3; i++) p.Line(V(0.35f + i * 0.15f, 0.7f), V(0.38f + i * 0.15f, 0.85f), 0.025f, Dark(c, 0.5f), false);
                    break;
                case "arrow":
                    p.Line(V(0.2f, 0.2f), V(0.74f, 0.74f), 0.05f, c);
                    p.Polygon(new[] { V(0.86f, 0.86f), V(0.64f, 0.8f), V(0.8f, 0.64f) }, a, true, 0.01f);
                    p.Polygon(new[] { V(0.14f, 0.3f), V(0.26f, 0.26f), V(0.3f, 0.14f), V(0.2f, 0.12f), V(0.12f, 0.2f) }, Light(Color.red, 0.4f), true, 0.01f);
                    break;
                case "pearl":
                    p.Polygon(new[] { V(0.18f, 0.3f), V(0.3f, 0.62f), V(0.5f, 0.7f), V(0.7f, 0.62f), V(0.82f, 0.3f), V(0.5f, 0.2f) }, a, true, 0.04f);
                    p.Disk(V(0.5f, 0.46f), 0.17f, c);
                    p.Disk(V(0.44f, 0.52f), 0.05f, Light(c, 0.7f), false);
                    break;
                case "sigil":
                    p.Polygon(new[] { V(0.5f, 0.86f), V(0.82f, 0.5f), V(0.5f, 0.14f), V(0.18f, 0.5f) }, c, true, 0.05f);
                    p.Disk(V(0.5f, 0.5f), 0.12f, a);
                    p.Line(V(0.5f, 0.72f), V(0.5f, 0.28f), 0.03f, Light(a, 0.4f), false);
                    break;
                case "kit":
                    p.Box(V(0.5f, 0.42f), V(0.32f, 0.24f), 0.04f, 0f, c);
                    p.Line(V(0.2f, 0.42f), V(0.8f, 0.42f), 0.02f, Dark(c), false);
                    p.Line(V(0.2f, 0.3f), V(0.8f, 0.54f), 0.04f, Light(c, 0.2f));
                    p.Disk(V(0.7f, 0.72f), 0.15f, a);
                    p.Line(V(0.64f, 0.72f), V(0.76f, 0.72f), 0.035f, Color.white, false);
                    p.Line(V(0.7f, 0.66f), V(0.7f, 0.78f), 0.035f, Color.white, false);
                    break;
                default:
                    p.Box(V(0.5f, 0.5f), V(0.3f, 0.3f), 0.08f, 0f, c);
                    break;
            }
        }

        // Tools and weapons point from bottom-left to top-right, like they're lying in a slot.
        static void PaintHeld(IconPainter p, string style, Color c, Color tip)
        {
            var a = V(0.2f, 0.18f);
            var b = V(0.8f, 0.82f);
            var d = (b - a).normalized;
            var n = new Vector2(-d.y, d.x);
            switch (style)
            {
                case "spear":
                    p.Line(a, b - d * 0.12f, 0.06f, c);
                    p.Polygon(new[] { b + d * 0.06f, b - d * 0.18f + n * 0.08f, b - d * 0.18f - n * 0.08f }, tip, true, 0.01f);
                    p.Line(b - d * 0.2f + n * 0.05f, b - d * 0.2f - n * 0.05f, 0.03f, Dark(c));
                    break;
                case "hatchet":
                    p.Line(a, b - d * 0.05f, 0.07f, c);
                    p.Polygon(new[] { b - d * 0.2f + n * 0.02f, b - d * 0.28f + n * 0.26f, b + d * 0.05f + n * 0.26f, b + n * 0.02f }, tip, true, 0.015f);
                    break;
                case "pickaxe":
                    p.Line(a, b - d * 0.05f, 0.07f, c);
                    p.Line(b - n * 0.32f - d * 0.14f, b + d * 0.02f, 0.07f, tip);
                    p.Line(b + d * 0.02f, b + n * 0.32f - d * 0.14f, 0.07f, tip);
                    break;
                case "rod":
                    p.Line(a, b, 0.045f, c);
                    p.Line(a + d * 0.06f, a + d * 0.22f, 0.08f, Dark(c));
                    p.Disk(a + d * 0.3f + n * 0.06f, 0.05f, Color.gray);
                    p.Line(b, b - n * 0.25f + d * 0.02f, 0.012f, Color.white, false);
                    p.Disk(b - n * 0.27f, 0.035f, new Color(0.9f, 0.2f, 0.18f));
                    break;
                case "bow":
                {
                    var pts = new List<Vector2>();
                    for (var i = 0; i <= 10; i++)
                    {
                        var s = Mathf.Lerp(-1f, 1f, i / 10f);
                        pts.Add(V(0.5f, 0.5f) + n * (s * 0.38f) + d * (0.16f * (1f - s * s) - 0.04f) * -1f + d * 0.06f);
                    }
                    p.Line(pts[0], pts[10], 0.012f, IconPainter.Ink, false);
                    // One continuous outline: all the ink first, then all the fill, so the joints don't bead.
                    for (var i = 0; i < 10; i++) p.Line(pts[i], pts[i + 1], 0.06f + 2f * p.Outline, IconPainter.Ink, false);
                    for (var i = 0; i < 10; i++) p.Line(pts[i], pts[i + 1], 0.06f, c, false);
                    break;
                }
                case "torch":
                    p.Line(a, b - d * 0.2f, 0.07f, c);
                    p.Line(b - d * 0.26f + n * 0.05f, b - d * 0.26f - n * 0.05f, 0.04f, Dark(c));
                    p.Ellipse(b - d * 0.08f, 0.13f, 0.17f, -45f, new Color(1f, 0.55f, 0.15f));
                    p.Ellipse(b - d * 0.1f, 0.07f, 0.1f, -45f, tip, false);
                    p.Ellipse(b - d * 0.12f, 0.035f, 0.05f, -45f, new Color(1f, 0.95f, 0.6f), false);
                    break;
                case "sword":
                    p.Polygon(new[] { a + d * 0.3f + n * 0.05f, b - d * 0.04f + n * 0.05f, b + d * 0.06f, b - d * 0.04f - n * 0.05f, a + d * 0.3f - n * 0.05f }, tip, true, 0.005f);
                    p.Line(a + d * 0.28f + n * 0.13f, a + d * 0.28f - n * 0.13f, 0.05f, Dark(c));
                    p.Line(a, a + d * 0.26f, 0.06f, c);
                    break;
                default:
                    p.Line(a, b, 0.07f, c);
                    break;
            }
        }

        static void PaintWorn(IconPainter p, string style, Color c)
        {
            switch (style)
            {
                case "cap":
                    p.Polygon(Arc(V(0.46f, 0.42f), 0.3f, 0f, 180f, 10), c, true, 0.01f);
                    p.Box(V(0.6f, 0.42f), V(0.3f, 0.05f), 0.04f, 0f, Dark(c, 0.15f));
                    p.Line(V(0.3f, 0.5f), V(0.6f, 0.5f), 0.02f, Light(c), false);
                    break;
                case "hood":
                    p.Polygon(Arc(V(0.5f, 0.45f), 0.32f, -30f, 210f, 12), c, true, 0.02f);
                    p.Ellipse(V(0.5f, 0.42f), 0.18f, 0.2f, 0f, Dark(c, 0.5f));
                    break;
                case "shirt":
                    p.Polygon(new[] { V(0.3f, 0.18f), V(0.3f, 0.6f), V(0.16f, 0.52f), V(0.24f, 0.74f), V(0.4f, 0.82f), V(0.6f, 0.82f), V(0.76f, 0.74f), V(0.84f, 0.52f), V(0.7f, 0.6f), V(0.7f, 0.18f) }, c, true, 0.02f);
                    break;
                case "cloak":
                    p.Polygon(new[] { V(0.4f, 0.82f), V(0.6f, 0.82f), V(0.82f, 0.16f), V(0.5f, 0.1f), V(0.18f, 0.16f) }, c, true, 0.03f);
                    p.Line(V(0.38f, 0.8f), V(0.62f, 0.8f), 0.06f, Dark(c, 0.2f));
                    p.Disk(V(0.5f, 0.78f), 0.04f, new Color(0.85f, 0.7f, 0.3f));
                    p.Line(V(0.4f, 0.6f), V(0.32f, 0.2f), 0.02f, Dark(c), false);
                    p.Line(V(0.6f, 0.6f), V(0.68f, 0.2f), 0.02f, Dark(c), false);
                    break;
                case "pants":
                    p.Polygon(new[] { V(0.28f, 0.84f), V(0.72f, 0.84f), V(0.76f, 0.14f), V(0.56f, 0.14f), V(0.5f, 0.56f), V(0.44f, 0.14f), V(0.24f, 0.14f) }, c, true, 0.02f);
                    p.Line(V(0.28f, 0.76f), V(0.72f, 0.76f), 0.03f, Dark(c), false);
                    break;
                case "boots":
                    p.Polygon(new[] { V(0.3f, 0.8f), V(0.5f, 0.8f), V(0.52f, 0.36f), V(0.82f, 0.3f), V(0.82f, 0.18f), V(0.28f, 0.18f) }, c, true, 0.03f);
                    break;
                case "backpack":
                    p.Box(V(0.5f, 0.46f), V(0.26f, 0.32f), 0.1f, 0f, c);
                    p.Box(V(0.5f, 0.32f), V(0.18f, 0.12f), 0.05f, 0f, Dark(c, 0.15f));
                    p.Line(V(0.36f, 0.78f), V(0.64f, 0.78f), 0.06f, Dark(c, 0.3f));
                    p.Line(V(0.5f, 0.44f), V(0.5f, 0.38f), 0.04f, new Color(0.85f, 0.7f, 0.3f));
                    break;
                case "pouch":
                    p.Ellipse(V(0.5f, 0.42f), 0.28f, 0.24f, 0f, c);
                    p.Polygon(Arc(V(0.5f, 0.5f), 0.26f, 0f, 180f, 8), Dark(c, 0.15f), true, 0.01f);
                    p.Line(V(0.2f, 0.62f), V(0.8f, 0.62f), 0.04f, Dark(c, 0.4f));
                    break;
                default:
                    p.Disk(V(0.5f, 0.5f), 0.3f, c);
                    break;
            }
        }

        static void PaintDish(IconPainter p, string vessel, List<Color> food)
        {
            Color F(int i) => food.Count == 0 ? new Color(0.55f, 0.5f, 0.45f) : Color.Lerp(food[i % food.Count], new Color(0.6f, 0.35f, 0.2f), 0.25f);
            switch (vessel)
            {
                case "bowl":
                    if (food.Count > 0)
                    {
                        p.Ellipse(V(0.5f, 0.55f), 0.34f, 0.1f, 0f, Color.Lerp(F(0), new Color(0.75f, 0.55f, 0.3f), 0.4f));
                        for (var i = 0; i < Mathf.Min(food.Count, 4); i++) p.Disk(V(0.32f + i * 0.12f, 0.57f + (i % 2) * 0.03f), 0.05f, F(i));
                        for (var i = 0; i < 3; i++) p.Line(V(0.38f + i * 0.12f, 0.7f), V(0.4f + i * 0.12f, 0.86f), 0.025f, new Color(1f, 1f, 1f, 0.6f), false);
                    }
                    p.Polygon(Arc(V(0.5f, 0.55f), 0.36f, 180f, 360f, 12), new Color(0.62f, 0.42f, 0.28f), true, 0.01f);
                    p.Line(V(0.3f, 0.36f), V(0.7f, 0.36f), 0.025f, new Color(0.5f, 0.32f, 0.2f), false);
                    break;
                case "skewer":
                    p.Line(V(0.14f, 0.18f), V(0.86f, 0.82f), 0.03f, new Color(0.62f, 0.45f, 0.3f));
                    for (var i = 0; i < 3; i++)
                    {
                        var at = Vector2.Lerp(V(0.3f, 0.32f), V(0.7f, 0.68f), i / 2f);
                        p.Box(at, V(0.1f, 0.09f), 0.04f, 42f, food.Count == 0 ? new Color(0.55f, 0.5f, 0.45f) : Color.Lerp(F(i), new Color(0.45f, 0.22f, 0.1f), 0.35f));
                        p.Line(at + V(-0.05f, 0.03f), at + V(0.03f, -0.05f), 0.015f, new Color(0.25f, 0.12f, 0.05f), false);
                    }
                    break;
                case "strips":
                    for (var i = 0; i < 3; i++)
                        p.Polygon(new[] { V(0.22f + i * 0.2f, 0.18f), V(0.18f + i * 0.2f, 0.8f), V(0.3f + i * 0.2f, 0.82f), V(0.34f + i * 0.2f, 0.2f) },
                            Color.Lerp(F(i), new Color(0.4f, 0.2f, 0.12f), 0.45f), true, 0.02f);
                    break;
                default: // plate
                    p.Ellipse(V(0.5f, 0.42f), 0.4f, 0.16f, 0f, new Color(0.92f, 0.9f, 0.84f));
                    p.Ellipse(V(0.5f, 0.44f), 0.28f, 0.1f, 0f, new Color(0.84f, 0.82f, 0.76f), false);
                    for (var i = 0; i < Mathf.Min(food.Count, 3); i++) p.Ellipse(V(0.38f + i * 0.12f, 0.5f), 0.1f, 0.07f, 10f, F(i));
                    break;
            }
        }

        static Vector2[] Arc(Vector2 c, float r, float from, float to, int steps)
        {
            var pts = new Vector2[steps + 1];
            for (var i = 0; i <= steps; i++)
            {
                var a = Mathf.Lerp(from, to, i / (float)steps) * Mathf.Deg2Rad;
                pts[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            }
            return pts;
        }

        static Color Parse(string hex, Color fallback) => StickFigureDrawer.ColourOf(hex, fallback);
    }
}
