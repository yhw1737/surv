using Isle.UI.Art;
using NUnit.Framework;
using UnityEngine;

namespace Isle.Tests.EditMode
{
    /// <summary>T-165 status visuals: every debuff a body can carry is readable at a glance — a red flash on a hit,
    /// green while poisoned, orange flicker while burning, blue and shivering when cold, drops when wet or bleeding,
    /// bubbles for poison, smoke for burns, sweat when exhausted, a hunch when overloaded.</summary>
    public sealed class StatusLookTests
    {
        static StatusState None => new() { SinceHit = 99f };

        [Test]
        public void Tint_NothingWrong_IsNone()
        {
            Assert.AreEqual(0f, StatusLook.Tint(None, 1f).Amount, 1e-6f);
        }

        [Test]
        public void Tint_FreshHit_IsRed_AndBeatsPoison()
        {
            var hit = None;
            hit.SinceHit = 0.05f;
            hit.Poisoned = true;
            var tint = StatusLook.Tint(hit, 1f);
            Assert.Greater(tint.Colour.r, tint.Colour.g);
            Assert.Greater(tint.Amount, 0.5f);

            hit.SinceHit = StatusLook.HitFlashSeconds + 0.01f;
            Assert.Greater(StatusLook.Tint(hit, 1f).Colour.g, StatusLook.Tint(hit, 1f).Colour.r, "after the flash, poison green shows");
        }

        [Test]
        public void Tint_Poison_IsGreen()
        {
            var s = None;
            s.Poisoned = true;
            var tint = StatusLook.Tint(s, 1f);
            Assert.Greater(tint.Colour.g, tint.Colour.r);
            Assert.Greater(tint.Colour.g, tint.Colour.b);
            Assert.Greater(tint.Amount, 0f);
        }

        [Test]
        public void Tint_Shell_IsGrey()
        {
            var s = None;
            s.Shelled = true;
            s.Poisoned = true;
            var tint = StatusLook.Tint(s, 1f);
            Assert.AreEqual(tint.Colour.r, tint.Colour.g, 0.05f, "grey, not poison green");
            Assert.Greater(tint.Amount, 0.3f);
        }

        [Test]
        public void Tint_Burn_FlickersOrange()
        {
            var s = None;
            s.Burning = true;
            var a = StatusLook.Tint(s, 0.0f);
            var b = StatusLook.Tint(s, 0.13f);
            Assert.Greater(a.Colour.r, a.Colour.b);
            Assert.AreNotEqual(a.Amount, b.Amount, "a burn flickers");
        }

        [Test]
        public void Cold_IsBlue_AndShivers()
        {
            var s = None;
            s.Cold = true;
            var tint = StatusLook.Tint(s, 1f);
            Assert.Greater(tint.Colour.b, tint.Colour.r);
            var moved = false;
            for (var t = 0f; t < 1f; t += 0.05f) moved |= Mathf.Abs(StatusLook.Shiver(s, t)) > 1e-4f;
            Assert.IsTrue(moved);
            Assert.AreEqual(0f, StatusLook.Shiver(None, 0.3f), 1e-6f);
        }

        [Test]
        public void Overloaded_Hunches()
        {
            var s = None;
            s.Overloaded = true;
            Assert.Greater(StatusLook.Hunch(s), 0f);
            Assert.AreEqual(0f, StatusLook.Hunch(None), 1e-6f);
        }

        static int ParticleVertices(StatusState s)
        {
            var mesh = new VectorMesh();
            StatusLook.Particles(mesh, s, 0.37f, new StatusBody { Centre = new Vector2(0f, 0.6f), HalfWidth = 0.25f, Top = 1.2f, Head = new Vector2(0f, 1.1f) });
            return mesh.VertexCount;
        }

        [Test]
        public void Particles_OnlyForWhatTheBodyHas()
        {
            Assert.AreEqual(0, ParticleVertices(None));

            var wet = None; wet.Wet = 1f;
            var bleed = None; bleed.Bleeding = true;
            var poison = None; poison.Poisoned = true;
            var burn = None; burn.Burning = true;
            var tired = None; tired.Exhausted = true;
            Assert.Greater(ParticleVertices(wet), 0, "wet: drops");
            Assert.Greater(ParticleVertices(bleed), 0, "bleed: blood drops");
            Assert.Greater(ParticleVertices(poison), 0, "poison: bubbles");
            Assert.Greater(ParticleVertices(burn), 0, "burn: smoke");
            Assert.Greater(ParticleVertices(tired), 0, "exhausted: sweat");
        }

        [Test]
        public void Particles_WetterDripsMore()
        {
            var damp = None; damp.Wet = 0.2f;
            var soaked = None; soaked.Wet = 1f;
            Assert.Greater(ParticleVertices(soaked), ParticleVertices(damp));
        }

        [Test]
        public void TintFrom_RecoloursOnlyLaterVertices()
        {
            var mesh = new VectorMesh();
            mesh.Disk(Vector2.zero, 1f, Color.black);
            var start = mesh.VertexCount;
            mesh.Disk(Vector2.one, 1f, Color.black);
            mesh.TintFrom(start, Color.green, 1f);
            Assert.AreEqual(new Color32(0, 0, 0, 255), mesh.ColourAt(0));
            Assert.AreEqual(new Color32(0, 255, 0, 255), mesh.ColourAt(start));
        }
    }
}
