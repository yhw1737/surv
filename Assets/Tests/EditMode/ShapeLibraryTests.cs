using Isle.Core.Util;
using NUnit.Framework;
using UnityEngine;

namespace Isle.Tests.EditMode
{
    /// <summary>Every placeholder shape draws something visible and outlined, and unknown keys don't throw.</summary>
    public sealed class ShapeLibraryTests
    {
        [Test]
        public void Draw_EveryKnownShape_HasOpaquePixels()
        {
            foreach (var shape in ShapeLibrary.Known)
            {
                var pixels = ShapeLibrary.Draw(shape, Color.green).GetPixels();
                var opaque = 0;
                foreach (var p in pixels) if (p.a > 0f) opaque++;
                Assert.Greater(opaque, 100, shape);
            }
        }

        [Test]
        public void Draw_UnknownShape_FallsBackToCircle()
        {
            Assert.IsNotNull(ShapeLibrary.Draw("not_a_shape", Color.red));
        }

        [Test]
        public void ParseColour_BadHex_UsesFallback()
        {
            Assert.AreEqual(Color.magenta, ShapeLibrary.ParseColour("nope", Color.magenta));
            Assert.AreEqual(Color.red, ShapeLibrary.ParseColour("#FF0000", Color.magenta));
        }
    }
}
