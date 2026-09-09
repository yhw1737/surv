using Isle.Core.Util;
using NUnit.Framework;
using UnityEngine;

namespace Isle.Tests.EditMode
{
    /// <summary>ART_PIPELINE §Placeholders, T-017.</summary>
    public sealed class PlaceholderVisualsTests
    {
        [Test]
        public void RoundedRect_ReturnsRequestedSize()
        {
            var tex = PlaceholderVisuals.RoundedRect(20, 12, Color.red);
            Assert.AreEqual(20, tex.width);
            Assert.AreEqual(12, tex.height);
        }

        [Test]
        public void RoundedRect_CenterIsFillColor()
        {
            var tex = PlaceholderVisuals.RoundedRect(20, 20, Color.red);
            var center = tex.GetPixel(10, 10);
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
            Assert.AreEqual(16, tex.width);
            Assert.AreEqual(16, tex.height);
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
        public void AsSprite_MatchesTextureDimensions()
        {
            var tex = PlaceholderVisuals.RoundedRect(32, 32, Color.green);
            var sprite = PlaceholderVisuals.AsSprite(tex);
            Assert.AreEqual(32, sprite.rect.width);
            Assert.AreEqual(32, sprite.rect.height);
        }
    }
}
