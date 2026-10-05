using System.Collections.Generic;
using Isle.Data;
using UnityEngine;

namespace Isle.UI.Art
{
    /// <summary>What the figure has on, by slot. Any field may be null — an empty slot, or an item with no
    /// <c>wear</c>/<c>hold</c> block, simply isn't drawn (SYS-CHAR-02 §Equipment).</summary>
    public struct FigureOutfit
    {
        public ItemDef Head, Chest, Legs, Feet, Back, Belt, MainHand, OffHand;
    }

    /// <summary>
    /// SYS-CHAR-02 §Look and §Equipment: turns a <see cref="StickFigurePose"/> and an outfit into vector primitives.
    /// The defs say what to draw (style, colour, length); this class says where, from the joints.
    /// </summary>
    public static class StickFigureDrawer
    {
        // SYS-CHAR-02 §Look.
        public static readonly Color Ink = Hex("#16130F");
        static readonly Color InkBack = Hex("#4A433C");
        static readonly Color HeadFill = Color.white;
        static readonly Color Shadow = new(0f, 0f, 0f, 0.22f);
        const float Limb = 0.085f;
        const float HeadRadius = 0.30f;
        const float HeadOutline = 0.05f;

        // [invented] presentation values.
        const float Edge = 0.032f;       // outline around coloured fills
        const float ItemWidth = 0.065f;

        static readonly Dictionary<string, Color> Colours = new();
        static readonly List<Vector2> Points = new();

        /// <summary>Draws the figure. <paramref name="lineEnd"/>, when set, is the fishing bobber in the figure's
        /// unmirrored local space.</summary>
        public static void Draw(VectorMesh mesh, in StickFigurePose pose, in FigureOutfit outfit, Vector2? lineEnd, float time)
        {
            var facing = pose.FacingScale;
            var cos = Mathf.Cos(pose.Rotation);
            var sin = Mathf.Sin(pose.Rotation);
            var pivot = pose.Pivot;

            mesh.Transform = q => new Vector2(q.x * facing, q.y);
            mesh.Ellipse(new Vector2(0f, 0.02f), 0.42f, 0.13f, new Color(0f, 0f, 0f, 0.10f));
            mesh.Ellipse(new Vector2(0f, 0.02f), 0.32f, 0.09f, Shadow);

            mesh.Transform = q =>
            {
                var d = q - pivot;
                var r = new Vector2(d.x * cos - d.y * sin, d.x * sin + d.y * cos) + pivot;
                return new Vector2(r.x * facing, r.y);
            };

            var torsoUp = (pose.Neck - pose.Hip).normalized;
            var torsoBack = new Vector2(-torsoUp.y, torsoUp.x); // left of "up" = behind the figure (-x)

            // Back items.
            DrawWear(mesh, outfit.Chest, WearLayer.Behind, pose, torsoUp, torsoBack);
            DrawWear(mesh, outfit.Back, WearLayer.Behind, pose, torsoUp, torsoBack);

            // Back arm and whatever the off hand holds.
            mesh.Line(pose.Shoulder, pose.ElbowBack, Limb, InkBack);
            mesh.Line(pose.ElbowBack, pose.HandBack, Limb, InkBack);
            DrawHeld(mesh, outfit.OffHand, pose.HandBack, pose.ItemAngleBack, pose, time, null, back: true);

            // Back leg.
            DrawLeg(mesh, pose.Hip, pose.KneeBack, pose.FootBack, outfit, InkBack);

            // Torso.
            mesh.Line(pose.Hip, pose.Neck, Limb, Ink);
            DrawWear(mesh, outfit.Chest, WearLayer.Body, pose, torsoUp, torsoBack);

            // Front leg, belt.
            DrawLeg(mesh, pose.Hip, pose.KneeFront, pose.FootFront, outfit, Ink);
            DrawWear(mesh, outfit.Belt, WearLayer.Body, pose, torsoUp, torsoBack);
            DrawWear(mesh, outfit.Back, WearLayer.Body, pose, torsoUp, torsoBack);

            // Head and face, then head wear.
            DrawWear(mesh, outfit.Head, WearLayer.Behind, pose, torsoUp, torsoBack);
            DrawHead(mesh, pose, hair: outfit.Head?.Wear == null);
            DrawWear(mesh, outfit.Head, WearLayer.Body, pose, torsoUp, torsoBack);

            // Front arm and the main-hand item on top.
            mesh.Line(pose.Shoulder, pose.ElbowFront, Limb, Ink);
            mesh.Line(pose.ElbowFront, pose.HandFront, Limb, Ink);
            DrawHeld(mesh, outfit.MainHand, pose.HandFront, pose.ItemAngleFront, pose, time, lineEnd, back: false);
            mesh.Disk(pose.HandFront, Limb * 0.62f, Ink);

            mesh.Transform = null;
        }

        enum WearLayer { Behind, Body }

        static void DrawLeg(VectorMesh mesh, Vector2 hip, Vector2 knee, Vector2 foot, in FigureOutfit outfit, Color ink)
        {
            var pants = outfit.Legs?.Wear;
            if (pants != null)
            {
                var fill = ColourOf(pants.Color, Ink);
                mesh.Line(hip, knee, 0.15f + Edge * 2f, Ink);
                mesh.Line(knee, foot, 0.13f + Edge * 2f, Ink);
                mesh.Line(hip, knee, 0.15f, ink == Ink ? fill : Darken(fill));
                mesh.Line(knee, foot, 0.13f, ink == Ink ? fill : Darken(fill));
            }
            else
            {
                mesh.Line(hip, knee, Limb, ink);
                mesh.Line(knee, foot, Limb, ink);
            }

            var boots = outfit.Feet?.Wear;
            if (boots != null)
            {
                var fill = ColourOf(boots.Color, Ink);
                var c = foot + new Vector2(0.05f, 0.03f);
                mesh.Ellipse(c, 0.12f + Edge, 0.075f + Edge, Ink);
                mesh.Ellipse(c, 0.12f, 0.075f, ink == Ink ? fill : Darken(fill));
            }
            else
            {
                mesh.Line(foot, foot + new Vector2(0.09f, 0f), Limb, ink);
            }
        }

        static void DrawHead(VectorMesh mesh, in StickFigurePose pose, bool hair)
        {
            var h = pose.Head;
            var tilt = pose.HeadTilt;
            Vector2 At(float x, float y) => h + Rotate(new Vector2(x, y), tilt);

            if (hair)
                for (var i = 0; i < 3; i++)
                {
                    var a = (95f + i * 28f) * Mathf.Deg2Rad - tilt;
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    mesh.Line(h + dir * (HeadRadius - 0.04f), h + dir * (HeadRadius + 0.11f) + new Vector2(-0.03f, 0f), 0.05f, Ink);
                }

            mesh.Disk(h, HeadRadius + HeadOutline, Ink);
            mesh.Disk(h, HeadRadius, HeadFill);

            if (pose.Dead)
            {
                foreach (var x in new[] { 0.09f, 0.2f })
                {
                    mesh.Line(At(x - 0.035f, 0.02f), At(x + 0.035f, 0.09f), 0.03f, Ink);
                    mesh.Line(At(x - 0.035f, 0.09f), At(x + 0.035f, 0.02f), 0.03f, Ink);
                }
                mesh.Line(At(0.1f, -0.11f), At(0.2f, -0.11f), 0.03f, Ink);
                return;
            }

            if (pose.EyesClosed)
            {
                mesh.Line(At(0.07f, 0.05f), At(0.12f, 0.05f), 0.025f, Ink);
                mesh.Line(At(0.17f, 0.05f), At(0.22f, 0.05f), 0.025f, Ink);
            }
            else
            {
                mesh.Ellipse(At(0.095f, 0.055f), 0.03f, 0.045f, Ink);
                mesh.Ellipse(At(0.195f, 0.055f), 0.03f, 0.045f, Ink);
            }

            // Mouth: a small smile, open while straining (reeling, striking).
            if (pose.Action is FigureAction.Reel or FigureAction.Swing)
                mesh.Ellipse(At(0.16f, -0.1f), 0.045f, 0.035f, Ink);
            else
                mesh.Line(At(0.1f, -0.1f), At(0.21f, -0.085f), 0.026f, Ink);
        }

        static void DrawWear(VectorMesh mesh, ItemDef item, WearLayer layer, in StickFigurePose pose, Vector2 up, Vector2 back)
        {
            var wear = item?.Wear;
            if (wear == null) return;
            var fill = ColourOf(wear.Color, Ink);

            switch (wear.Style)
            {
                case "shirt":
                    if (layer != WearLayer.Body) return;
                    mesh.Line(pose.Shoulder, pose.Hip + up * 0.02f, 0.27f + Edge * 2f, Ink);
                    mesh.Line(pose.Shoulder, pose.Hip + up * 0.02f, 0.27f, fill);
                    return;

                case "cloak":
                    if (layer == WearLayer.Behind)
                    {
                        var flare = pose.CloakFlare;
                        Points.Clear();
                        Points.Add(pose.Shoulder + up * 0.04f + back * 0.06f);
                        Points.Add(pose.Shoulder + up * 0.04f - back * 0.07f);
                        Points.Add(pose.Hip - up * 0.22f - back * (0.04f - flare * 0.3f));
                        Points.Add(pose.Hip - up * 0.28f + back * (0.2f + flare));
                        Outlined(mesh, Points, fill);
                    }
                    else
                    {
                        mesh.Line(pose.Shoulder + up * 0.03f - back * 0.08f, pose.Shoulder + up * 0.03f + back * 0.08f, 0.08f + Edge * 2f, Ink);
                        mesh.Line(pose.Shoulder + up * 0.03f - back * 0.08f, pose.Shoulder + up * 0.03f + back * 0.08f, 0.08f, fill);
                    }
                    return;

                case "pants":
                case "boots":
                    return; // drawn with the legs

                case "backpack":
                    if (layer == WearLayer.Behind)
                    {
                        var centre = Vector2.Lerp(pose.Shoulder, pose.Hip, 0.42f) + back * 0.17f;
                        Box(centre, up, back, 0.19f, 0.15f);
                        Outlined(mesh, Points, fill);
                        mesh.Line(centre + up * 0.05f + back * 0.15f, centre + up * 0.05f - back * 0.05f, 0.03f, Ink);
                    }
                    else
                    {
                        mesh.Line(pose.Shoulder + back * 0.02f, Vector2.Lerp(pose.Shoulder, pose.Hip, 0.7f) + back * 0.05f, 0.05f, Ink);
                    }
                    return;

                case "pouch":
                    if (layer != WearLayer.Body) return;
                    var at = pose.Hip + up * 0.04f - back * 0.1f;
                    mesh.Ellipse(at, 0.085f + Edge, 0.075f + Edge, Ink);
                    mesh.Ellipse(at, 0.085f, 0.075f, fill);
                    return;

                case "cap":
                {
                    if (layer != WearLayer.Body) return;
                    var h = pose.Head;
                    var tilt = pose.HeadTilt;
                    Points.Clear();
                    for (var i = 0; i <= 8; i++)
                    {
                        var a = Mathf.Lerp(8f, 172f, i / 8f) * Mathf.Deg2Rad - tilt;
                        Points.Add(h + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (HeadRadius + 0.04f) + Rotate(new Vector2(0f, 0.06f), tilt));
                    }
                    Outlined(mesh, Points, fill);
                    mesh.Line(h + Rotate(new Vector2(0.08f, 0.13f), tilt), h + Rotate(new Vector2(0.46f, 0.11f), tilt), 0.055f + Edge * 2f, Ink);
                    mesh.Line(h + Rotate(new Vector2(0.08f, 0.13f), tilt), h + Rotate(new Vector2(0.46f, 0.11f), tilt), 0.055f, fill);
                    return;
                }

                case "hood":
                    if (layer == WearLayer.Behind)
                    {
                        var c = pose.Head + new Vector2(-0.05f, 0.02f);
                        mesh.Disk(c, HeadRadius + 0.1f + Edge, Ink);
                        mesh.Disk(c, HeadRadius + 0.1f, fill);
                    }
                    return;

                default:
                    if (layer != WearLayer.Body) return;
                    var mid = Vector2.Lerp(pose.Shoulder, pose.Hip, 0.5f);
                    mesh.Disk(mid, 0.1f + Edge, Ink);
                    mesh.Disk(mid, 0.1f, fill);
                    return;
            }
        }

        static void DrawHeld(VectorMesh mesh, ItemDef item, Vector2 hand, float angle, in StickFigurePose pose, float time, Vector2? lineEnd, bool back)
        {
            var hold = item?.Hold;
            if (hold == null) return;
            var d = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var n = new Vector2(-d.y, d.x);
            var len = hold.Length <= 0f ? 1f : hold.Length;
            var shaft = ColourOf(hold.Color, Ink);
            var tip = ColourOf(hold.Tip, shaft);
            if (back) shaft = Darken(shaft);
            var end = hand + d * len;

            switch (hold.Style)
            {
                case "spear":
                {
                    var butt = hand - d * len * 0.3f;
                    var head = hand + d * len * 0.7f;
                    Shaft(mesh, butt, head, shaft);
                    Points.Clear();
                    Points.Add(head - n * 0.08f);
                    Points.Add(head + d * 0.24f);
                    Points.Add(head + n * 0.08f);
                    Outlined(mesh, Points, tip);
                    return;
                }

                case "hatchet":
                {
                    Shaft(mesh, hand - d * 0.08f, end, shaft);
                    Points.Clear();
                    Points.Add(end - d * 0.2f + n * 0.03f);
                    Points.Add(end - d * 0.26f + n * 0.2f);
                    Points.Add(end + d * 0.06f + n * 0.22f);
                    Points.Add(end + d * 0.02f + n * 0.03f);
                    Outlined(mesh, Points, tip);
                    return;
                }

                case "pickaxe":
                {
                    Shaft(mesh, hand - d * 0.08f, end, shaft);
                    Polyline(mesh, end - n * 0.3f - d * 0.12f, end + d * 0.03f, end + n * 0.3f - d * 0.12f, 0.07f, tip);
                    return;
                }

                case "sword":
                {
                    mesh.Line(hand - d * 0.1f, hand + d * 0.08f, ItemWidth + Edge * 2f, Ink);
                    mesh.Line(hand - d * 0.1f, hand + d * 0.08f, ItemWidth, shaft);
                    Points.Clear();
                    Points.Add(hand + d * 0.1f - n * 0.05f);
                    Points.Add(end - n * 0.05f);
                    Points.Add(end + d * 0.12f);
                    Points.Add(end + n * 0.05f);
                    Points.Add(hand + d * 0.1f + n * 0.05f);
                    Outlined(mesh, Points, tip);
                    mesh.Line(hand + d * 0.1f - n * 0.13f, hand + d * 0.1f + n * 0.13f, 0.06f, Ink);
                    return;
                }

                case "torch":
                {
                    Shaft(mesh, hand - d * 0.1f, end, shaft);
                    var flicker = 1f + 0.12f * Mathf.Sin(time * 23f) + 0.08f * Mathf.Sin(time * 37f);
                    var flame = end + d * 0.08f;
                    mesh.Ellipse(flame + n * 0.02f, 0.13f * flicker, 0.17f * flicker, new Color(1f, 0.55f, 0.15f, 0.35f));
                    mesh.Ellipse(flame, 0.09f * flicker, 0.12f * flicker, tip);
                    mesh.Ellipse(flame - d * 0.02f, 0.045f * flicker, 0.065f * flicker, new Color(1f, 0.95f, 0.6f));
                    return;
                }

                case "rod":
                {
                    // A rod bends toward its tip; the curve deepens while a fish pulls.
                    var bendAmount = pose.Action == FigureAction.Reel ? 0.18f : pose.Action == FigureAction.Bite ? 0.1f : 0.04f;
                    var mid = hand + d * len * 0.55f - n * bendAmount * 0.5f;
                    var rodTip = hand + d * len - n * bendAmount;
                    Polyline(mesh, hand - d * 0.12f, mid, rodTip, 0.045f, shaft);
                    if (lineEnd is { } bobber)
                    {
                        var taut = pose.Action is FigureAction.Reel or FigureAction.Bite;
                        var sag = Vector2.Lerp(rodTip, bobber, 0.5f) + Vector2.down * (taut ? 0.02f : 0.35f);
                        Points.Clear();
                        for (var i = 0; i <= 8; i++)
                        {
                            var s = i / 8f;
                            Points.Add(Vector2.Lerp(Vector2.Lerp(rodTip, sag, s), Vector2.Lerp(sag, bobber, s), s));
                        }
                        mesh.Polyline(Points, 0.018f, new Color(0.95f, 0.95f, 0.92f, 0.9f));
                        mesh.Disk(bobber, 0.07f, Ink);
                        mesh.Disk(bobber, 0.05f, new Color(0.9f, 0.2f, 0.18f));
                    }
                    return;
                }

                case "bow":
                {
                    // Limbs curve toward the target; the string runs to the drawing hand while the bow is drawn.
                    var half = len * 0.5f;
                    Points.Clear();
                    for (var i = 0; i <= 10; i++)
                    {
                        var s = Mathf.Lerp(-1f, 1f, i / 10f);
                        Points.Add(hand + n * (s * half) + d * (0.16f * (1f - s * s) - 0.05f));
                    }
                    var top = Points[10];
                    var bottom = Points[0];
                    mesh.Polyline(Points, ItemWidth + Edge * 2f, Ink);
                    mesh.Polyline(Points, ItemWidth, shaft);
                    var nock = pose.Action == FigureAction.Draw ? pose.HandBack : Vector2.Lerp(top, bottom, 0.5f);
                    mesh.Stroke(top, nock, 0.02f, Ink);
                    mesh.Stroke(nock, bottom, 0.02f, Ink);
                    if (pose.Action == FigureAction.Draw)
                    {
                        mesh.Line(nock, hand + d * 0.3f, 0.03f, Ink);
                        Points.Clear();
                        Points.Add(hand + d * 0.3f + n * 0.05f);
                        Points.Add(hand + d * 0.44f);
                        Points.Add(hand + d * 0.3f - n * 0.05f);
                        mesh.Polygon(Points, Hex("#9FA4AA"));
                    }
                    return;
                }

                default:
                    Shaft(mesh, hand - d * 0.08f, end, shaft);
                    return;
            }
        }

        static void Shaft(VectorMesh mesh, Vector2 a, Vector2 b, Color colour)
        {
            mesh.Line(a, b, ItemWidth + Edge * 2f, Ink);
            mesh.Line(a, b, ItemWidth, colour);
        }

        static void Polyline(VectorMesh mesh, Vector2 a, Vector2 b, Vector2 c, float width, Color colour)
        {
            mesh.Line(a, b, width + Edge * 2f, Ink);
            mesh.Line(b, c, width + Edge * 2f, Ink);
            mesh.Line(a, b, width, colour);
            mesh.Line(b, c, width, colour);
        }

        static void Outlined(VectorMesh mesh, List<Vector2> points, Color fill)
        {
            mesh.Polygon(points, fill);
            mesh.Outline(points, Edge * 1.6f, Ink);
        }

        static void Box(Vector2 centre, Vector2 up, Vector2 side, float halfHeight, float halfWidth)
        {
            Points.Clear();
            Points.Add(centre - up * halfHeight - side * halfWidth);
            Points.Add(centre - up * halfHeight + side * halfWidth);
            Points.Add(centre + up * halfHeight + side * halfWidth);
            Points.Add(centre + up * halfHeight - side * halfWidth);
        }

        /// <summary>Clockwise by <paramref name="radians"/> — the way a forward lean tips the head.</summary>
        static Vector2 Rotate(Vector2 v, float radians)
        {
            var c = Mathf.Cos(radians);
            var s = Mathf.Sin(radians);
            return new Vector2(v.x * c + v.y * s, -v.x * s + v.y * c);
        }

        static Color Darken(Color c) => new(c.r * 0.72f, c.g * 0.72f, c.b * 0.72f, c.a);

        public static Color ColourOf(string hex, Color fallback)
        {
            if (string.IsNullOrEmpty(hex)) return fallback;
            if (Colours.TryGetValue(hex, out var c)) return c;
            c = ColorUtility.TryParseHtmlString(hex, out var parsed) ? parsed : fallback;
            Colours[hex] = c;
            return c;
        }

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
    }
}
