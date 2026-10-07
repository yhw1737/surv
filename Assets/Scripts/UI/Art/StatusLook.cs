using UnityEngine;

namespace Isle.UI.Art
{
    /// <summary>What a body is suffering right now, read from gameplay state by a view.</summary>
    public struct StatusState
    {
        /// <summary>Seconds since it was last hit.</summary>
        public float SinceHit;
        public bool Poisoned, Bleeding, Burning, Cold, Exhausted, Overloaded;

        /// <summary>A boss hiding in its shell (SYS-DUNG-01): greyed, hard to hurt.</summary>
        public bool Shelled;

        /// <summary>0 dry … 1 soaked.</summary>
        public float Wet;
    }

    /// <summary>Where on a body (local figure space, feet at 0) the particles start.</summary>
    public struct StatusBody
    {
        public Vector2 Centre;
        public float HalfWidth;
        public float Top;
        public Vector2 Head;
    }

    /// <summary>
    /// T-165: makes debuffs visible on any figure — a whole-body tint (hit red, burn orange flicker, poison green,
    /// cold blue), a shiver, a hunch, and looping particles (water and blood drops, poison bubbles, smoke, sweat).
    /// Pure and time-driven: particles are a function of the clock, so there is no state to sync or pool.
    /// Every number here is an [invented] presentation value.
    /// </summary>
    public static class StatusLook
    {
        public const float HitFlashSeconds = 0.15f;

        static readonly Color HitRed = new(0.95f, 0.15f, 0.12f);
        static readonly Color PoisonGreen = new(0.35f, 0.85f, 0.25f);
        static readonly Color BurnOrange = new(1f, 0.55f, 0.1f);
        static readonly Color ColdBlue = new(0.45f, 0.65f, 1f);
        static readonly Color ShellGrey = new(0.55f, 0.55f, 0.58f);
        static readonly Color Water = new(0.45f, 0.72f, 1f, 0.9f);
        static readonly Color Blood = new(0.75f, 0.06f, 0.06f, 0.95f);
        static readonly Color Smoke = new(0.35f, 0.33f, 0.32f, 0.55f);
        static readonly Color Flame = new(1f, 0.6f, 0.12f, 0.95f);
        static readonly Color Sweat = new(0.8f, 0.92f, 1f, 0.9f);
        static readonly Color Ink = new(0.086f, 0.075f, 0.06f, 1f);

        /// <summary>The body's tint: a fresh hit wins, then a shell, a burn's flicker, poison, cold.</summary>
        public static (Color Colour, float Amount) Tint(in StatusState s, float time)
        {
            if (s.SinceHit < HitFlashSeconds) return (HitRed, 0.75f);
            if (s.Shelled) return (ShellGrey, 0.6f);
            if (s.Burning) return (BurnOrange, 0.3f + 0.2f * Mathf.Abs(Mathf.Sin(time * 11f)) + 0.1f * Mathf.Sin(time * 27f));
            if (s.Poisoned) return (PoisonGreen, 0.55f);
            if (s.Cold) return (ColdBlue, 0.4f);
            return (Color.clear, 0f);
        }

        /// <summary>Sideways offset (tiles) of a shivering body.</summary>
        public static float Shiver(in StatusState s, float time) => s.Cold ? Mathf.Sin(time * 55f) * 0.018f : 0f;

        /// <summary>0–1 forward hunch of an overloaded body.</summary>
        public static float Hunch(in StatusState s) => s.Overloaded ? 1f : 0f;

        /// <summary>Vertical bob (tiles) of an exhausted body, panting.</summary>
        public static float Pant(in StatusState s, float time) => s.Exhausted ? Mathf.Abs(Mathf.Sin(time * 9f)) * 0.025f : 0f;

        /// <summary>Applies shiver, pant and hunch to the vertices drawn since <paramref name="start"/>.
        /// <paramref name="facing"/> is ±1, the way the body faces. The upper body (hip → shoulder) bends by a smooth
        /// ramp that is flat above the shoulders, so the head moves whole instead of being sheared.</summary>
        public static void Move(VectorMesh mesh, int start, in StatusState s, float time, float facing, float hipHeight, float shoulderHeight)
        {
            var shiver = Shiver(s, time);
            var pant = Pant(s, time);
            var hunch = Hunch(s);
            if (shiver == 0f && pant == 0f && hunch == 0f) return;
            mesh.MoveFrom(start, p =>
            {
                var bend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(hipHeight, shoulderHeight, p.y));
                // Overloaded: the upper body leans forward and sags under the weight.
                p.x += bend * 0.14f * hunch * facing + shiver;
                p.y += bend * (pant - 0.07f * hunch);
                return p;
            });
        }

        /// <summary>Draws the looping particles of every status the body has.</summary>
        public static void Particles(VectorMesh mesh, in StatusState s, float time, in StatusBody body)
        {
            if (s.Wet > 0.01f) Drops(mesh, time, body, Mathf.CeilToInt(s.Wet * 6f), Water, 0.7f, seed: 1);
            if (s.Bleeding) Drops(mesh, time, body, 4, Blood, 0.9f, seed: 7);
            if (s.Poisoned) Bubbles(mesh, time, body);
            if (s.Burning) Fire(mesh, time, body);
            if (s.Exhausted) SweatDrops(mesh, time, body);
        }

        static float Frac(float x) => x - Mathf.Floor(x);
        static float Hash(int i, int seed) => Frac(Mathf.Sin(i * 12.9898f + seed * 78.233f) * 43758.5453f);

        /// <summary>Teardrops falling from random points on the body to the ground.</summary>
        static void Drops(VectorMesh mesh, float time, in StatusBody body, int count, Color colour, float rate, int seed)
        {
            for (var i = 0; i < count; i++)
            {
                var phase = Frac(time * rate + i / (float)count + Hash(i, seed) * 0.3f);
                var x = body.Centre.x + (Hash(i, seed + 1) * 2f - 1f) * body.HalfWidth;
                var startY = Mathf.Lerp(body.Centre.y, body.Top, Hash(i, seed + 2));
                var y = Mathf.Lerp(startY, 0.02f, phase * phase);
                var alpha = phase < 0.85f ? 1f : (1f - phase) / 0.15f;
                var c = colour;
                c.a *= alpha;
                var ink = Ink;
                ink.a = alpha * 0.8f;
                Teardrop(mesh, new Vector2(x, y), DropRadius, c, ink);
            }
        }

        /// <summary>Particle sizes are drawn for the game camera (half-height 8.5 tiles). [invented]</summary>
        const float DropRadius = 0.06f;

        static void Teardrop(VectorMesh mesh, Vector2 at, float r, Color fill, Color ink)
        {
            mesh.Disk(at, r + 0.016f, ink);
            mesh.Polygon(new[] { at + new Vector2(-r - 0.016f, 0f), at + new Vector2(r + 0.016f, 0f), at + new Vector2(0f, r * 2.6f + 0.02f) }, ink);
            mesh.Disk(at, r, fill);
            mesh.Polygon(new[] { at + new Vector2(-r, 0f), at + new Vector2(r, 0f), at + new Vector2(0f, r * 2.6f) }, fill);
        }

        /// <summary>Green bubbles rising off the body and popping.</summary>
        static void Bubbles(VectorMesh mesh, float time, in StatusBody body)
        {
            const int count = 4;
            for (var i = 0; i < count; i++)
            {
                var phase = Frac(time * 0.8f + i / (float)count);
                var x = body.Centre.x + (Hash(i, 3) * 2f - 1f) * body.HalfWidth + Mathf.Sin(time * 4f + i) * 0.04f;
                var y = Mathf.Lerp(body.Centre.y, body.Top + 0.35f, phase);
                var r = 0.04f + 0.045f * phase;
                var alpha = phase < 0.8f ? 1f : (1f - phase) / 0.2f;
                mesh.Disk(new Vector2(x, y), r + 0.016f, new Color(Ink.r, Ink.g, Ink.b, 0.7f * alpha));
                mesh.Disk(new Vector2(x, y), r, new Color(PoisonGreen.r, PoisonGreen.g, PoisonGreen.b, 0.9f * alpha));
                mesh.Disk(new Vector2(x - r * 0.35f, y + r * 0.35f), r * 0.3f, new Color(1f, 1f, 1f, 0.8f * alpha));
            }
        }

        /// <summary>Little flames licking the body and smoke puffs drifting up.</summary>
        static void Fire(VectorMesh mesh, float time, in StatusBody body)
        {
            for (var i = 0; i < 3; i++)
            {
                var x = body.Centre.x + (i - 1) * body.HalfWidth * 0.7f;
                var h = 0.2f + 0.1f * Mathf.Abs(Mathf.Sin(time * 13f + i * 2.1f));
                var baseY = body.Centre.y - 0.05f + Hash(i, 11) * 0.2f;
                var tip = new Vector2(x + Mathf.Sin(time * 9f + i) * 0.03f, baseY + h);
                mesh.Polygon(new[] { new Vector2(x - 0.085f, baseY), new Vector2(x + 0.085f, baseY), tip }, Ink);
                mesh.Polygon(new[] { new Vector2(x - 0.062f, baseY + 0.016f), new Vector2(x + 0.062f, baseY + 0.016f), tip - new Vector2(0f, 0.025f) }, Flame);
            }
            for (var i = 0; i < 3; i++)
            {
                var phase = Frac(time * 0.6f + i / 3f);
                var x = body.Centre.x + (Hash(i, 13) - 0.5f) * body.HalfWidth + phase * 0.15f;
                var y = Mathf.Lerp(body.Top, body.Top + 0.6f, phase);
                var c = Smoke;
                c.a *= 1f - phase;
                mesh.Disk(new Vector2(x, y), 0.08f + 0.1f * phase, c);
            }
        }

        /// <summary>Sweat flicking off both sides of the head.</summary>
        static void SweatDrops(VectorMesh mesh, float time, in StatusBody body)
        {
            for (var i = 0; i < 2; i++)
            {
                var side = i == 0 ? -1f : 1f;
                var phase = Frac(time * 1.4f + i * 0.5f);
                var at = body.Head + new Vector2(side * (0.2f + 0.25f * phase), 0.1f + 0.15f * phase - 0.45f * phase * phase);
                var alpha = 1f - phase;
                var ink = Ink;
                ink.a = alpha * 0.8f;
                var c = Sweat;
                c.a *= alpha;
                Teardrop(mesh, at, DropRadius * 0.8f, c, ink);
            }
        }
    }
}
