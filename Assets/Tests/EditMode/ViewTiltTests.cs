using Isle.Core.Util;
using Isle.UI.Prototype;
using NUnit.Framework;
using UnityEngine;

namespace Isle.Tests.EditMode
{
    /// <summary>The tilted perspective view (ART_PIPELINE §Camera view) and the pencil look: mouse picking lands on the
    /// ground under the cursor, standing things face the camera, and the pencil pass keeps transparency.</summary>
    public sealed class ViewTiltTests
    {
        GameObject _go;
        Camera _camera;
        RenderTexture _target;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestCamera");
            _camera = _go.AddComponent<Camera>();
            _target = new RenderTexture(1600, 1000, 0);
            _camera.targetTexture = _target;
            _camera.aspect = 1.6f;
        }

        [TearDown]
        public void TearDown()
        {
            _camera.targetTexture = null;
            Object.DestroyImmediate(_target);
            Object.DestroyImmediate(_go);
            ViewTilt.Focus = null;
        }

        [Test]
        public void ScreenToGround_ScreenCentre_ReturnsFocus()
        {
            PlayerCamera.Frame(_camera, new Vector2(12f, -40f), 8.5f);
            var hit = ViewTilt.ScreenToGround(_camera, new Vector2(_camera.pixelWidth * 0.5f, _camera.pixelHeight * 0.5f));
            Assert.That(hit.HasValue);
            Assert.That(hit.Value.x, Is.EqualTo(12f).Within(0.01f));
            Assert.That(hit.Value.y, Is.EqualTo(-40f).Within(0.01f));
            Assert.That(ViewTilt.Focus, Is.EqualTo(new Vector2(12f, -40f)));
        }

        [Test]
        public void ScreenToGround_RoundTrip_LandsOnTheProjectedPoint()
        {
            PlayerCamera.Frame(_camera, Vector2.zero, 8.5f);
            foreach (var p in new[] { new Vector2(3f, 5f), new Vector2(-6f, -4f), new Vector2(0f, 9f) })
            {
                var screen = _camera.WorldToScreenPoint(p);
                var back = ViewTilt.ScreenToGround(_camera, screen);
                Assert.That(back.HasValue);
                Assert.That(Vector2.Distance(back.Value, p), Is.LessThan(0.01f), $"{p}");
            }
        }

        [Test]
        public void ScreenToGround_FarSideLooksFurther_PerspectiveNotOrthographic()
        {
            PlayerCamera.Frame(_camera, Vector2.zero, 8.5f);
            var top = ViewTilt.ScreenToGround(_camera, new Vector2(_camera.pixelWidth * 0.5f, _camera.pixelHeight)).Value;
            var bottom = ViewTilt.ScreenToGround(_camera, new Vector2(_camera.pixelWidth * 0.5f, 0f)).Value;
            Assert.That(top.y, Is.GreaterThan(-bottom.y), "the top of the screen reaches further than the bottom");
        }

        [Test]
        public void Stand_FacesTheCamera()
        {
            PlayerCamera.Frame(_camera, Vector2.zero, 8.5f);
            var thing = new GameObject("Standing");
            ViewTilt.Stand(thing.transform);
            // A standing sprite's face is perpendicular to the view direction: its normal points back along the view.
            Assert.That(Vector3.Dot(thing.transform.forward, _camera.transform.forward), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(thing.transform.up.z, Is.LessThan(0f), "up leans toward the camera (−Z)");
            Object.DestroyImmediate(thing);
        }

        [Test]
        public void PencilShade_Transparent_StaysTransparent()
        {
            Assert.That(PencilLook.Shade(new Color(0.5f, 0.6f, 0.2f, 0f), 3.3f, 7.1f).a, Is.EqualTo(0f));
        }

        [Test]
        public void PencilShade_Fill_StaysNearItsColour()
        {
            var c = new Color(0.3f, 0.6f, 0.3f, 1f);
            for (var i = 0; i < 50; i++)
            {
                var s = PencilLook.Shade(c, i * 1.37f, i * 0.71f);
                Assert.That(Mathf.Abs(s.g - c.g), Is.LessThan(0.3f));
                Assert.That(s.a, Is.EqualTo(1f));
            }
        }

        [Test]
        public void PencilShade_Ink_BecomesGraphite()
        {
            var s = PencilLook.Shade(new Color(0.086f, 0.075f, 0.06f, 1f), 10f, 4f);
            Assert.That(s.r, Is.GreaterThan(0.086f), "lighter than pure ink");
            Assert.That(s.a, Is.InRange(0.85f, 1f));
        }
    }
}
