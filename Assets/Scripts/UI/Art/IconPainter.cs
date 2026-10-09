using System;
using System.Collections.Generic;
using UnityEngine;

namespace Isle.UI.Art
{
    /// <summary>
    /// A tiny CPU vector painter for inventory icons in the stick-figure cartoon style (SYS-CHAR-02): shapes are signed
    /// distance functions, each drawn as a near-black outline then its fill, anti-aliased, into a square texture.
    /// Coordinates are 0..1 with y up. Pure drawing — no game knowledge.
    /// </summary>
    public sealed class IconPainter
    {
        public readonly int Size;
        readonly Color[] _pixels;
        public float Outline = 0.035f;
        public static readonly Color Ink = new(0.086f, 0.075f, 0.06f);

        public IconPainter(int size)
        {
            Size = size;
            _pixels = new Color[size * size];
        }

        public Texture2D ToTexture()
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            // Pencil pass (PencilLook), at a paper cell per ~1/90 of the icon so it reads at slot size.
            var cell = Size / 90f;
            for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
                _pixels[y * Size + x] = Isle.Core.Util.PencilLook.Shade(_pixels[y * Size + x], x / cell, y / cell);
            texture.SetPixels(_pixels);
            texture.Apply(true);
            return texture;
        }

        /// <summary>Draws one shape: its outline (unless <paramref name="outline"/> is false) then its fill.</summary>
        public void Shape(Func<Vector2, float> sdf, Rect bounds, Color fill, bool outline = true)
        {
            var px = 1f / Size;
            var ow = outline ? Outline : 0f;
            var pad = ow + 2f * px;
            int x0 = Mathf.Clamp((int)((bounds.xMin - pad) * Size), 0, Size - 1), x1 = Mathf.Clamp((int)((bounds.xMax + pad) * Size) + 1, 0, Size - 1);
            int y0 = Mathf.Clamp((int)((bounds.yMin - pad) * Size), 0, Size - 1), y1 = Mathf.Clamp((int)((bounds.yMax + pad) * Size) + 1, 0, Size - 1);
            for (var y = y0; y <= y1; y++)
            for (var x = x0; x <= x1; x++)
            {
                var d = sdf(new Vector2((x + 0.5f) * px, (y + 0.5f) * px));
                var i = y * Size + x;
                if (ow > 0f) Blend(i, Ink, Mathf.Clamp01(0.5f - (d - ow * Isle.Core.Util.PencilLook.InkWobble(x * 256f / Size, y * 256f / Size)) / px));
                Blend(i, fill, Mathf.Clamp01(0.5f - d / px));
            }
        }

        void Blend(int i, Color c, float coverage)
        {
            if (coverage <= 0f) return;
            var a = c.a * coverage;
            var dst = _pixels[i];
            var outA = a + dst.a * (1f - a);
            if (outA <= 0f) return;
            var rgb = (new Vector3(c.r, c.g, c.b) * a + new Vector3(dst.r, dst.g, dst.b) * dst.a * (1f - a)) / outA;
            _pixels[i] = new Color(rgb.x, rgb.y, rgb.z, outA);
        }

        // ---- primitives -----------------------------------------------------------------------------------------

        public void Disk(Vector2 c, float r, Color fill, bool outline = true) =>
            Shape(p => (p - c).magnitude - r, new Rect(c.x - r, c.y - r, 2 * r, 2 * r), fill, outline);

        public void Ellipse(Vector2 c, float rx, float ry, float degrees, Color fill, bool outline = true)
        {
            var rad = degrees * Mathf.Deg2Rad;
            var cos = Mathf.Cos(rad);
            var sin = Mathf.Sin(rad);
            var m = Mathf.Max(rx, ry);
            Shape(p =>
            {
                var d = p - c;
                var q = new Vector2(d.x * cos + d.y * sin, -d.x * sin + d.y * cos);
                var k = new Vector2(q.x / rx, q.y / ry).magnitude;
                return (k - 1f) * Mathf.Min(rx, ry);
            }, new Rect(c.x - m, c.y - m, 2 * m, 2 * m), fill, outline);
        }

        /// <summary>A round-capped line.</summary>
        public void Line(Vector2 a, Vector2 b, float width, Color fill, bool outline = true)
        {
            var r = width * 0.5f;
            Shape(p => SegmentDistance(p, a, b) - r,
                Rect.MinMaxRect(Mathf.Min(a.x, b.x) - r, Mathf.Min(a.y, b.y) - r, Mathf.Max(a.x, b.x) + r, Mathf.Max(a.y, b.y) + r), fill, outline);
        }

        public void Polygon(IReadOnlyList<Vector2> points, Color fill, bool outline = true, float round = 0f)
        {
            var bounds = Bounds(points);
            var pts = new List<Vector2>(points);
            Shape(p => PolygonDistance(p, pts) - round, new Rect(bounds.x - round, bounds.y - round, bounds.width + 2 * round, bounds.height + 2 * round), fill, outline);
        }

        public void Box(Vector2 c, Vector2 half, float radius, float degrees, Color fill, bool outline = true)
        {
            var rad = degrees * Mathf.Deg2Rad;
            var cos = Mathf.Cos(rad);
            var sin = Mathf.Sin(rad);
            var m = half.magnitude + radius;
            Shape(p =>
            {
                var d = p - c;
                var q = new Vector2(Mathf.Abs(d.x * cos + d.y * sin), Mathf.Abs(-d.x * sin + d.y * cos)) - half + Vector2.one * radius;
                return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
            }, new Rect(c.x - m, c.y - m, 2 * m, 2 * m), fill, outline);
        }

        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            var pa = p - a;
            var ba = b - a;
            var h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Mathf.Max(1e-6f, Vector2.Dot(ba, ba)));
            return (pa - ba * h).magnitude;
        }

        static float PolygonDistance(Vector2 p, List<Vector2> v)
        {
            var d = Vector2.Dot(p - v[0], p - v[0]);
            var s = 1f;
            for (int i = 0, j = v.Count - 1; i < v.Count; j = i, i++)
            {
                var e = v[j] - v[i];
                var w = p - v[i];
                var b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, Vector2.Dot(b, b));
                var c1 = p.y >= v[i].y;
                var c2 = p.y < v[j].y;
                var c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        }

        static Rect Bounds(IReadOnlyList<Vector2> points)
        {
            float x0 = 1, y0 = 1, x1 = 0, y1 = 0;
            foreach (var p in points)
            {
                x0 = Mathf.Min(x0, p.x);
                y0 = Mathf.Min(y0, p.y);
                x1 = Mathf.Max(x1, p.x);
                y1 = Mathf.Max(y1, p.y);
            }
            return Rect.MinMaxRect(x0, y0, x1, y1);
        }
    }
}
