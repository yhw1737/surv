using UnityEngine;

namespace Isle.Core.Util
{
    /// <summary>
    /// The pencil-drawn look (developer, 2026-10-09): coloured pencil on toothy paper. Paper shows through the pigment
    /// on the tooth's peaks (more in light colours), darker colours get diagonal hatching (cross-hatching when very
    /// dark), and ink lines turn to graphite that breaks up along the stroke. The same maths runs in the vector shader
    /// (<c>Resources/IsleVector.shader</c>), the shape canvas, the ground and the icons, so everything reads as one
    /// drawing. Pure and thread-safe (the ground computes on workers). Presentation only. All values [invented look].
    /// </summary>
    public static class PencilLook
    {
        public static readonly Color Paper = new(0.96f, 0.94f, 0.88f, 1f);
        public static readonly Color Graphite = new(0.2f, 0.2f, 0.22f, 1f);

        /// <summary>How strongly paper tooth shows through pigment, at full brightness.</summary>
        const float ToothShow = 0.42f;
        /// <summary>Darkening of a hatch stroke at its centre.</summary>
        const float HatchDepth = 0.24f;
        /// <summary>Colours darker than this (luminance) count as ink.</summary>
        public const float InkLuminance = 0.14f;

        /// <summary>Shades <paramref name="c"/> at position <paramref name="x"/>, <paramref name="y"/>, in "paper
        /// cells" — about one per 2–3 screen pixels; the hatch spacing is <see cref="HatchCells"/> cells.</summary>
        public static Color Shade(Color c, float x, float y)
        {
            if (c.a <= 0f) return c;
            var lum = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
            var grain = 0.6f * Noise(x, y) + 0.4f * Noise(x * 2.3f + 17.1f, y * 2.3f + 5.3f);

            if (lum < InkLuminance)
            {
                // Graphite: a little lighter than pure ink, and patchy where the tooth skips.
                var g = Color.Lerp(c, Graphite, 0.4f);
                g.a = c.a * (0.85f + 0.15f * Mathf.Clamp01(grain * 1.6f));
                return g;
            }

            var rgb = new Vector3(c.r, c.g, c.b);
            // Paper tooth: pigment skips the peaks, more so in light passes.
            var tooth = Mathf.Clamp01((grain - 0.55f) * 3f) * ToothShow * (0.35f + 0.65f * lum);
            rgb = Vector3.Lerp(rgb, new Vector3(Paper.r, Paper.g, Paper.b), tooth);

            // Hatching in the darker tones, wobbling a little like a hand-drawn stroke.
            var dark = 1f - lum;
            var wobble = Noise(x * 0.13f + 3.7f, y * 0.13f) * 0.9f;
            var h1 = Mathf.Abs(Frac((x + y) / HatchCells + wobble) - 0.5f) * 2f;
            var line1 = Mathf.Clamp01((0.32f - h1) * 5f) * Mathf.Clamp01((dark - 0.3f) * 2.5f);
            var h2 = Mathf.Abs(Frac((x - y) / HatchCells + wobble * 0.7f + 0.37f) - 0.5f) * 2f;
            var line2 = Mathf.Clamp01((0.26f - h2) * 5f) * Mathf.Clamp01((dark - 0.62f) * 3f);
            var hatch = Mathf.Max(line1, line2) * (0.55f + 0.45f * grain);
            rgb *= 1f - HatchDepth * hatch;

            // Fine grain over everything.
            rgb *= 1f - 0.1f * (grain - 0.5f);
            return new Color(Mathf.Clamp01(rgb.x), Mathf.Clamp01(rgb.y), Mathf.Clamp01(rgb.z), c.a);
        }

        /// <summary>Paper cells between hatch strokes.</summary>
        public const float HatchCells = 3.2f;

        /// <summary>How far a pencil outline wanders in and out, 0..1 around 1 — multiplies an ink line's width.</summary>
        public static float InkWobble(float x, float y) => 0.7f + 0.6f * Noise(x * 0.09f + 11.3f, y * 0.09f + 2.9f);

        /// <summary>Smooth value noise in 0..1, period-free.</summary>
        public static float Noise(float x, float y)
        {
            var ix = Mathf.FloorToInt(x);
            var iy = Mathf.FloorToInt(y);
            var fx = x - ix;
            var fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            var a = Hash(ix, iy);
            var b = Hash(ix + 1, iy);
            var c = Hash(ix, iy + 1);
            var d = Hash(ix + 1, iy + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Hash(int x, int y)
        {
            unchecked
            {
                var h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        static float Frac(float v) => v - Mathf.Floor(v);
    }
}
