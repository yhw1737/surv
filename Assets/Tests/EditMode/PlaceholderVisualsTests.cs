using Isle.Core.Util;
using NUnit.Framework;
using UnityEngine;

namespace Isle.Tests.EditMode
{
    /// <summary>ART_PIPELINE §Placeholders, T-017. 2026-10-05: placeholders are drawn at
    /// <see cref="PlaceholderVisuals.Supersample"/>× resolution with anti-aliased edges (developer: "모든 이미지는 픽셀이
    /// 아니라 고해상도"), while their world size stays what the caller asked for.</summary>
    public sealed class PlaceholderVisualsTests
    {
        [Test]
        public void RoundedRect_IsSupersampled()
        {
            var tex = PlaceholderVisuals.RoundedRect(20, 12, Color.red);
            Assert.AreEqual(20 * PlaceholderVisuals.Supersample, tex.width);
            Assert.AreEqual(12 * PlaceholderVisuals.Supersample, tex.height);
        }

        [Test]
        public void Circle_EdgeIsAntiAliased()
        {
            var tex = PlaceholderVisuals.Circle(16, Color.blue);
            var partial = 0;
            foreach (var p in tex.GetPixels())
                if (p.a > 0.05f && p.a < 0.95f) partial++;
            Assert.Greater(partial, 0, "no soft edge pixels — the circle is still hard-edged");
        }

        [Test]
        public void RoundedRect_CenterIsFillColor()
        {
            var tex = PlaceholderVisuals.RoundedRect(20, 20, Color.red);
            var center = tex.GetPixel(tex.width / 2, tex.height / 2);
            Assert.AreEqual(Color.red.r, center.r, 0.01f);
            Assert.AreEqual(1f, center.a, 0.01f);
        }

        [Test]
        public void RoundedRect_CornerIsTransparent()
        {
            // A corner outside the rounded radius should fall through to Color.clear.
            var tex = PlaceholderVisuals.RoundedRect(20, 20, Color.red);
            Assert.AreEqual(0f, tex.GetPixel(0, 0).a, 0.01f);
        }

        [Test]
        public void Circle_IsSquareTexture()
        {
            var tex = PlaceholderVisuals.Circle(16, Color.blue);
            Assert.AreEqual(tex.width, tex.height);
        }

        [Test]
        public void ColorForTags_SameTag_SameColor()
        {
            var a = PlaceholderVisuals.ColorForTags(new[] { "meat" });
            var b = PlaceholderVisuals.ColorForTags(new[] { "meat" });
            Assert.AreEqual(a, b);
        }

        [Test]
        public void ColorForTags_DifferentTags_DifferentColor()
        {
            var meat = PlaceholderVisuals.ColorForTags(new[] { "meat" });
            var fish = PlaceholderVisuals.ColorForTags(new[] { "fish" });
            Assert.AreNotEqual(meat, fish);
        }

        [Test]
        public void ColorForTags_NoTags_ReturnsNeutralGrey()
        {
            var color = PlaceholderVisuals.ColorForTags(null);
            Assert.AreEqual(color.r, color.g, 0.01f);
            Assert.AreEqual(color.g, color.b, 0.01f);
        }

        [Test]
        public void AsSprite_KeepsWorldSize()
        {
            // 32 requested pixels at 32 pixels per unit is still one world unit, at any resolution.
            var sprite = PlaceholderVisuals.AsSprite(PlaceholderVisuals.RoundedRect(32, 32, Color.green));
            Assert.AreEqual(1f, sprite.bounds.size.x, 1e-4f);
            Assert.AreEqual(1f, sprite.bounds.size.y, 1e-4f);
        }
    }
}
