using UnityEngine;

namespace Isle.Core.Util
{
    /// <summary>
    /// T-017: ART_PIPELINE §Placeholders. Flat, untextured vector shapes generated at runtime —
    /// no pixel art, nothing baked to disk — so a definition with no art never blocks a system on
    /// a missing sprite (Absolute Rule 7). That includes every mod, which will always ship with
    /// some art missing.
    /// <para>
    /// Reuses <c>PlaceholderRigBuilder</c>'s (T-001) rounded-box signed-distance shape, but draws it
    /// at runtime instead of baking PNGs — a definition's tags aren't known until mods load, so
    /// nothing here can be pre-baked as an asset the way the fixed character rig was.
    /// </para>
    /// <para>
    /// Lives in <c>Isle.Core</c>, not behind a UI/system presentation class, because it has to be
    /// reachable from every layer that draws something (UI for icons, Gameplay/Combat/World for
    /// creatures, weapons, tiles) and Core is the only asmdef all of them already reference.
    /// The presentation boundary still holds for what calls this: nothing here touches a
    /// <c>SpriteRenderer</c> or a <c>GameObject</c>, only pixels.
    /// </para>
    /// </summary>
    public static class PlaceholderVisuals
    {
        // ART_PIPELINE §Style: outline is dark brown, never black. Reused from PlaceholderRigBuilder.
        static readonly Color Outline = new Color32(0x3A, 0x2A, 0x1E, 0xFF);

        /// <summary>
        /// A filled rounded rectangle with a 1px dark outline. <paramref name="cornerRadius"/>
        /// negative picks a default; passed as half the shorter side, it collapses to a circle
        /// (see <see cref="Circle"/>), the same trick <c>PlaceholderRigBuilder</c> uses for the head.
        /// </summary>
        public static Texture2D RoundedRect(int width, int height, Color fill, float cornerRadius = -1f)
        {
            if (cornerRadius < 0f) cornerRadius = Mathf.Min(width, height) * 0.3f;

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false);
            var pixels = new Color[width * height];
            float halfW = width * 0.5f, halfH = height * 0.5f;

            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var px = x + 0.5f - halfW;
                var py = y + 0.5f - halfH;
                var qx = Mathf.Abs(px) - (halfW - cornerRadius);
                var qy = Mathf.Abs(py) - (halfH - cornerRadius);
                var d = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude
                        + Mathf.Min(Mathf.Max(qx, qy), 0f) - cornerRadius;

                pixels[y * width + x] = d < -1f ? fill : d < 0f ? Outline : Color.clear;
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>A filled circle — a square <see cref="RoundedRect"/> whose corner radius is half its side.</summary>
        public static Texture2D Circle(int diameter, Color fill) =>
            RoundedRect(diameter, diameter, fill, cornerRadius: diameter * 0.5f);

        /// <summary>
        /// ART_PIPELINE §Placeholders: "Colours come from tags, not from IDs." Hashes the first tag
        /// so the same tag always draws the same colour and a new tag needs no C# edit (Absolute
        /// Rule 4) — a plain <see cref="string.GetHashCode"/> isn't used because .NET does not
        /// guarantee it's stable across runs. No tags falls back to a fixed neutral grey.
        /// <para>
        /// ponytail: hue-only hashing, fixed saturation/value. Not a real palette — ART_PIPELINE
        /// says placeholder colours "are not spec values" and throws this away at T-160 anyway.
        /// </para>
        /// </summary>
        public static Color ColorForTags(string[] tags)
        {
            if (tags == null || tags.Length == 0) return new Color(0.6f, 0.6f, 0.6f);
            var hue = (Fnv1a(tags[0]) % 360u) / 360f;
            return Color.HSVToRGB(hue, 0.55f, 0.85f);
        }

        public static Sprite AsSprite(Texture2D texture, float pixelsPerUnit = 32f) =>
            Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);

        static uint Fnv1a(string text)
        {
            var hash = 2166136261u;
            foreach (var c in text)
            {
                hash ^= c;
                hash *= 16777619u;
            }
            return hash;
        }
    }
}
