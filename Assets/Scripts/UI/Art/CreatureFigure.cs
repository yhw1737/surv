using System.Collections.Generic;
using Isle.Data;
using Isle.Gameplay.Hunting;
using UnityEngine;

namespace Isle.UI.Art
{
    /// <summary>
    /// SYS-CHAR-02 §Creatures: one creature drawn as an animated cartoon in the stick-figure style — thick near-black
    /// outlines, flat colours, procedural motion. The body plan and colours come from the def's <c>look</c> block;
    /// the motion from gameplay state only (position, heading, wind-up, lunge, stagger, sleep, last hit). Lives on a
    /// child of the creature's view so it follows it and hides with it.
    /// </summary>
    public sealed class CreatureFigure : MonoBehaviour
    {
        static readonly Color Ink = StickFigureDrawer.Ink;
        const float O = 0.035f; // outline width, tiles [invented]

        Creature _creature;
        readonly VectorMesh _mesh = new();
        Mesh _unityMesh;
        Vector2 _last;
        float _phase, _speed, _speedV, _facing = 1f, _facingV, _crouch, _crouchV, _stretch, _stretchV, _sleep, _sleepV, _time;
        float _seed;
        Color _body, _accent, _belly;

        public static CreatureFigure Attach(Creature creature, Material material)
        {
            var go = new GameObject("Figure");
            go.transform.SetParent(creature.View.transform, false);
            var parentScale = creature.View.transform.localScale.x;
            go.transform.localScale = Vector3.one / Mathf.Max(0.0001f, parentScale);
            var figure = go.AddComponent<CreatureFigure>();
            figure._creature = creature;
            figure._unityMesh = new Mesh { name = "CreatureFigure" };
            figure._unityMesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = figure._unityMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = StickFigureView.SortingOrder;
            if (creature.Renderer != null) creature.Renderer.enabled = false;
            figure._last = creature.Position;
            figure._seed = Random.value * 10f;
            var look = creature.Def.Look;
            figure._body = StickFigureDrawer.ColourOf(look?.Color ?? creature.Def.Visual?.Color, Color.gray);
            figure._accent = StickFigureDrawer.ColourOf(look?.Accent, Color.Lerp(figure._body, Color.black, 0.35f));
            figure._belly = StickFigureDrawer.ColourOf(look?.Belly, Color.Lerp(figure._body, Color.white, 0.4f));
            return figure;
        }

        void OnDestroy()
        {
            if (_unityMesh != null) Destroy(_unityMesh);
        }

        void LateUpdate()
        {
            if (_creature == null) return;
            var dt = Time.deltaTime;
            _time += dt;
            var moved = _creature.Position - _last;
            _last = _creature.Position;
            var distance = moved.magnitude;
            Spring.Damp(ref _speed, ref _speedV, dt > 0f ? distance / dt : 0f, 10f, dt);
            var heading = Mathf.Abs(moved.x) > 1e-4f ? moved.x : _creature.Heading.x;
            if (Mathf.Abs(heading) > 1e-4f) Spring.Damp(ref _facing, ref _facingV, Mathf.Sign(heading), 18f, dt);
            Spring.Damp(ref _crouch, ref _crouchV, _creature.IsWindingUp ? 1f : 0f, 14f, dt);
            Spring.Damp(ref _stretch, ref _stretchV, _creature.IsLunging ? 1f : 0f, 25f, dt);
            Spring.Damp(ref _sleep, ref _sleepV, _creature.Asleep ? 1f : 0f, 4f, dt);

            var r = Mathf.Max(0.05f, _creature.Radius);
            _phase = Gait.Advance(_phase, distance, Mathf.Max(0.2f, r * 3f));

            _mesh.Clear();
            var facing = Mathf.Abs(_facing) < 0.08f ? 0.08f * Mathf.Sign(_facing == 0f ? 1f : _facing) : _facing;
            // A carcass lies on its back, legs up — cartoon dead.
            var wobble = _creature.Dead ? Mathf.PI : _creature.IsStaggered ? Mathf.Sin(_time * 28f) * 0.18f : 0f;
            if (_creature.Dead) _speed = 0f;
            var shake = _crouch > 0.5f ? Mathf.Sin(_time * 60f) * 0.015f * r : 0f;
            var cos = Mathf.Cos(wobble);
            var sin = Mathf.Sin(wobble);
            var pivot = new Vector2(0f, r);
            _mesh.Transform = q => new Vector2(q.x * facing, q.y);
            _mesh.Ellipse(new Vector2(0f, 0.02f), r * 1.5f, r * 0.42f, new Color(0f, 0f, 0f, 0.2f));
            _mesh.Transform = q =>
            {
                var d = q - pivot;
                var p = new Vector2(d.x * cos - d.y * sin, d.x * sin + d.y * cos) + pivot;
                return new Vector2((p.x + shake) * facing, p.y);
            };

            var look = _creature.Def.Look;
            var body = _body;
            var bodyStart = _mesh.VertexCount;
            var moving = Mathf.Clamp01(_speed / 1.2f) * (1f - _sleep);
            switch (look?.Body)
            {
                case "reptile": Reptile(r, body, moving); break;
                // Small bodies read better drawn a bit larger than their hit radius.
                case "snake": Snake(r * 1.8f, body, moving); break;
                case "crab": Crab(r * 1.4f, body, moving); break;
                case "bird": Bird(r, body, moving); break;
                case "frog": Frog(r, body, moving); break;
                case "turtle": Turtle(r, body, moving); break;
                default: Quadruped(look, r, body, moving); break;
            }
            DrawStatus(bodyStart, r, facing);
            if (_creature.IsStaggered) Stars(r);
            if (_sleep > 0.5f) Zs(r);
            _mesh.Transform = null;
            _mesh.Fill(_unityMesh);
            // Bounds centred on the feet: the 2D renderer's custom-axis sort uses the bounds centre, so this makes the
            // figure sort by where it stands — the same rule as the trees' foot pivots. Big enough to never cull early.
            _unityMesh.bounds = new Bounds(Vector3.zero, new Vector3(6f, 6f, 1f));
        }

        /// <summary>T-165: hit flash and damage-over-time on the body — tint, then particles. Stars and Zs go on top
        /// untinted.</summary>
        void DrawStatus(int bodyStart, float r, float facing)
        {
            var status = _creature.Status;
            var s = new StatusState
            {
                SinceHit = Time.time - _creature.LastHitAt,
                Poisoned = status.Stacks(Isle.Gameplay.Combat.DamageTypes.Toxic) > 0,
                Bleeding = status.Stacks(Isle.Gameplay.Combat.DamageTypes.Slash) > 0,
                Burning = status.Stacks(Isle.Gameplay.Combat.DamageTypes.Heat) > 0,
                Shelled = _creature.Shell.IsHidden(Time.time),
            };
            var tint = StatusLook.Tint(s, _time);
            if (_creature.Dead)
            {
                // A carcass is greyed, and greener as it spoils; flies once it's going off.
                var rot = Mathf.Clamp01(_creature.Spoilage / Isle.Gameplay.Hunting.CarcassCalculator.SpoilRotten);
                tint = (Color.Lerp(new Color(0.45f, 0.42f, 0.4f), new Color(0.42f, 0.5f, 0.3f), rot), 0.35f + 0.2f * rot);
            }
            _mesh.TintFrom(bodyStart, tint.Colour, tint.Amount);
            if (_creature.Dead)
            {
                if (_creature.Spoilage >= Isle.Gameplay.Hunting.CarcassCalculator.SpoilHalfYield)
                {
                    var keep = _mesh.Transform;
                    _mesh.Transform = null;
                    StatusLook.Flies(_mesh, _time + _seed, new Vector2(0f, r * 1.4f), r * 1.2f);
                    _mesh.Transform = keep;
                }
                return;
            }
            var transform = _mesh.Transform;
            _mesh.Transform = null;
            StatusLook.Particles(_mesh, s, _time + _seed, new StatusBody
            {
                Centre = new Vector2(0f, r),
                HalfWidth = r * 1.1f,
                Top = r * 2f,
                Head = new Vector2(Mathf.Sign(facing) * r * 1.2f, r * 1.4f),
            });
            _mesh.Transform = transform;
        }

        // ------------------------------------------------------------------ helpers

        void Ell(Vector2 c, float rx, float ry, Color fill)
        {
            _mesh.Ellipse(c, rx + O, ry + O, Ink);
            _mesh.Ellipse(c, rx, ry, fill);
        }

        void Limb(Vector2 a, Vector2 b, float w, Color fill)
        {
            _mesh.Line(a, b, w + 2f * O, Ink);
            _mesh.Line(a, b, w, fill);
        }

        void Poly(List<Vector2> points, Color fill)
        {
            _mesh.Polygon(points, fill);
            _mesh.Outline(points, O * 1.6f, Ink);
        }

        void Eye(Vector2 c, float size)
        {
            if (_sleep > 0.5f) _mesh.Line(c - new Vector2(size, 0f), c + new Vector2(size, 0f), size * 0.6f, Ink);
            else _mesh.Disk(c, size, Ink);
        }

        static Vector2 V(float x, float y) => new(x, y);
        static Color Dark(Color c, float t = 0.25f) => Color.Lerp(c, Color.black, t);

        float Tau => Mathf.PI * 2f;

        // ------------------------------------------------------------------ body plans

        void Quadruped(CreatureLook look, float r, Color body, float moving)
        {
            var s = _creature.Def.Combat != null && _creature.Def.Combat.BodyRadiusTiles > 0f ? r / _creature.Def.Combat.BodyRadiusTiles : 1f;
            var L = (look?.BodyLength ?? 0.8f) * s;
            var H = (look?.BodyHeight ?? 0.36f) * s;
            var leg = (look?.LegLength ?? 0.3f) * s;
            var head = (look?.HeadSize ?? 0.2f) * s;
            var hop = look?.Gait == "hop";

            var lift = hop ? Mathf.Abs(Mathf.Sin(_phase * Mathf.PI)) * 0.3f * L * moving : 0f;
            var bob = hop ? 0f : Mathf.Abs(Mathf.Sin(_phase * Tau)) * 0.04f * L * moving;
            var legNow = Mathf.Lerp(leg, leg * 0.15f, _sleep) * (1f - 0.35f * _crouch);
            var c = V(-_crouch * 0.08f * L + _stretch * 0.12f * L, legNow + H * 0.5f + bob + lift);
            var front = c.x + L * 0.32f;
            var back = c.x - L * 0.32f;
            var hipY = c.y - H * 0.2f;

            Vector2 Foot(float hipX, float q)
            {
                if (hop)
                {
                    var tuck = Mathf.Sin(_phase * Mathf.PI) * moving;
                    return V(hipX + (hipX > c.x ? 0.1f : -0.15f) * L * tuck, Mathf.Max(0f, lift - legNow * 0.4f * tuck));
                }
                var swing = Mathf.Sin(Tau * q) * 0.2f * L * moving;
                var up = Mathf.Max(0f, Mathf.Cos(Tau * q)) * 0.1f * leg * moving * 2f;
                return V(hipX + swing, up);
            }

            void Leg(float hipX, float q, bool frontLeg, Color colour)
            {
                if (_sleep > 0.8f) return;
                var hip = V(hipX, hipY);
                var foot = Foot(hipX, q);
                var knee = TwoBoneIk.Solve(hip, foot, (hipY) * 0.55f, (hipY) * 0.55f, frontLeg ? -1f : 1f);
                var w = Mathf.Max(0.05f, Mathf.Min(L * 0.11f, 0.12f));
                Limb(hip, knee, w, colour);
                Limb(knee, foot, w * 0.85f, colour);
                _mesh.Disk(foot + V(0.01f, 0f), w * 0.55f + O, Ink);
            }

            var far = Dark(body, 0.22f);
            Leg(front, _phase + 0.5f, true, far);
            Leg(back, _phase, false, far);

            // Tail.
            var tailBase = V(c.x - L * 0.48f, c.y + H * 0.15f);
            var sway = Mathf.Sin(_time * 4f + _seed) * 0.05f * L;
            switch (look?.Tail)
            {
                case "bushy":
                    Ell(tailBase + V(-L * 0.22f, H * 0.05f + sway), L * 0.24f, H * 0.2f, body);
                    _mesh.Ellipse(tailBase + V(-L * 0.38f, H * 0.05f + sway), L * 0.09f, H * 0.12f, _belly);
                    break;
                case "puff":
                    Ell(tailBase + V(-L * 0.04f, H * 0.12f), H * 0.2f, H * 0.2f, _belly);
                    break;
                case "long":
                    Limb(tailBase, tailBase + V(-L * 0.5f, -H * 0.2f + sway), L * 0.06f, body);
                    break;
                default:
                    Limb(tailBase, tailBase + V(-L * 0.12f, -H * 0.2f + sway), L * 0.05f, body);
                    break;
            }

            Ell(c, L * 0.5f, H * 0.5f, body);
            _mesh.Ellipse(c + V(0f, -H * 0.2f), L * 0.38f, H * 0.24f, _belly);

            Leg(front, _phase, true, body);
            Leg(back, _phase + 0.5f, false, body);

            // Head: lower and forward when winding up or lunging, down on the ground when asleep.
            var neckUp = Mathf.Lerp(H * 0.45f, -H * 0.15f, Mathf.Max(_crouch * 0.6f, _sleep));
            var h = V(c.x + L * 0.5f + head * 0.35f + _stretch * head * 0.4f, c.y + neckUp);
            var snout = h + V(head * 0.75f, -head * 0.2f);

            if (look?.Antlers == true)
            {
                var top = h + V(-head * 0.1f, head * 0.8f);
                Limb(top, top + V(-head * 0.5f, head * 1.4f), head * 0.14f, _accent);
                Limb(top + V(-head * 0.25f, head * 0.7f), top + V(head * 0.35f, head * 1.25f), head * 0.12f, _accent);
                Limb(top + V(-head * 0.4f, head * 1.1f), top + V(-head * 1.0f, head * 1.5f), head * 0.12f, _accent);
            }
            switch (look?.Ears)
            {
                case "long":
                    Ell(h + V(-head * 0.35f, head * 1.2f), head * 0.22f, head * 0.75f, body);
                    _mesh.Ellipse(h + V(-head * 0.35f, head * 1.2f), head * 0.1f, head * 0.55f, Color.Lerp(_belly, new Color(1f, 0.7f, 0.7f), 0.4f));
                    break;
                case "pointed":
                    Poly(new List<Vector2> { h + V(-head * 0.6f, head * 0.5f), h + V(-head * 0.35f, head * 1.25f), h + V(-head * 0.05f, head * 0.6f) }, body);
                    break;
                case "round":
                    Ell(h + V(-head * 0.45f, head * 0.75f), head * 0.28f, head * 0.28f, Dark(body, 0.1f));
                    break;
            }
            Ell(h, head, head * 0.85f, body);
            Ell(snout, head * 0.55f, head * 0.42f, Color.Lerp(body, _belly, 0.5f));
            _mesh.Disk(snout + V(head * 0.45f, head * 0.08f), head * 0.14f, Ink);
            if (look?.Tusks == true)
            {
                _mesh.Line(snout + V(head * 0.15f, -head * 0.25f), snout + V(head * 0.45f, head * 0.25f), head * 0.16f + O, Ink);
                _mesh.Line(snout + V(head * 0.15f, -head * 0.25f), snout + V(head * 0.45f, head * 0.25f), head * 0.12f, new Color(0.97f, 0.94f, 0.85f));
            }
            Eye(h + V(head * 0.3f, head * 0.25f), head * 0.14f);
        }

        void Reptile(float r, Color body, float moving)
        {
            var y = r * 0.3f * (1f - 0.3f * _crouch);
            var c = V(_stretch * 0.25f * r, y + r * 0.05f);
            var walk = _phase * Tau;
            var far = Dark(body, 0.22f);
            for (var i = 0; i < 2; i++)
            {
                var x = c.x + (i == 0 ? 0.55f : -0.55f) * r;
                Limb(V(x, c.y - r * 0.1f), V(x + Mathf.Sin(walk + i * Mathf.PI) * 0.18f * r * moving, 0.02f), r * 0.16f, far);
            }
            var sway = Mathf.Sin(_time * 3f + walk) * 0.15f * r;
            Poly(new List<Vector2> { c + V(-0.75f * r, r * 0.22f), c + V(-2.1f * r, r * 0.05f + sway), c + V(-0.75f * r, -r * 0.18f) }, body);
            Ell(c, 1.0f * r, 0.3f * r, body);
            _mesh.Ellipse(c + V(0f, -0.12f * r), 0.8f * r, 0.13f * r, _belly);
            for (var i = 0; i < 5; i++) _mesh.Disk(c + V((-0.6f + i * 0.3f) * r, 0.27f * r), 0.07f * r, _accent);
            // Jaws: they gape on the wind-up and snap on the lunge.
            var gape = _crouch * 0.35f;
            var h = c + V(1.05f * r, 0.02f * r);
            Poly(new List<Vector2> { h + V(0f, -0.15f * r), h + V(0.85f * r, -0.12f * r - gape * r * 0.3f), h + V(0.85f * r, -0.02f * r - gape * r * 0.3f), h + V(0f, 0.02f * r) }, Dark(body, 0.1f));
            Poly(new List<Vector2> { h + V(-0.05f * r, -0.02f * r), h + V(0.9f * r, 0.0f + gape * r * 0.5f), h + V(0.85f * r, 0.12f * r + gape * r * 0.5f), h + V(0.1f * r, 0.22f * r) }, body);
            Ell(h + V(0.12f * r, 0.22f * r), 0.1f * r, 0.08f * r, body);
            Eye(h + V(0.14f * r, 0.24f * r), 0.045f * r);
            for (var i = 0; i < 2; i++)
            {
                var x = c.x + (i == 0 ? 0.55f : -0.55f) * r;
                Limb(V(x, c.y - r * 0.1f), V(x + Mathf.Sin(walk + i * Mathf.PI + Mathf.PI) * 0.18f * r * moving, 0.02f), r * 0.16f, body);
            }
        }

        void Snake(float r, Color body, float moving)
        {
            var points = new List<Vector2>();
            var raise = _crouch * 0.6f * r + _stretch * 0.2f * r;
            for (var i = 0; i <= 12; i++)
            {
                var t = i / 12f;
                var x = Mathf.Lerp(-1.6f * r, 1.1f * r + _stretch * 0.8f * r, t);
                var wave = Mathf.Sin(_phase * Tau * 2f + t * 9f + _time * (1f - moving) * 1.5f) * 0.14f * r * (0.4f + moving);
                var y = 0.12f * r + wave * (1f - t * 0.3f) + (t > 0.75f ? (t - 0.75f) * 4f * raise : 0f);
                points.Add(V(x, y));
            }
            var width = 0.3f * r;
            for (var i = 0; i < points.Count - 1; i++) _mesh.Line(points[i], points[i + 1], width * (0.5f + 0.5f * i / 12f) + 2f * O, Ink);
            for (var i = 0; i < points.Count - 1; i++) _mesh.Line(points[i], points[i + 1], width * (0.5f + 0.5f * i / 12f), i % 3 == 1 ? _accent : body);
            var h = points[^1] + V(0.12f * r, 0.02f * r);
            Ell(h, 0.24f * r, 0.17f * r, body);
            Eye(h + V(0.08f * r, 0.07f * r), 0.04f * r);
            if (Mathf.Repeat(_time + _seed, 1.6f) < 0.25f || _crouch > 0.5f)
            {
                _mesh.Line(h + V(0.22f * r, -0.02f * r), h + V(0.42f * r, -0.02f * r), 0.03f * r, new Color(0.85f, 0.15f, 0.2f));
                _mesh.Line(h + V(0.42f * r, -0.02f * r), h + V(0.5f * r, 0.04f * r), 0.025f * r, new Color(0.85f, 0.15f, 0.2f));
            }
        }

        void Crab(float r, Color body, float moving)
        {
            var c = V(0f, 0.55f * r);
            var scuttle = _phase * Tau * 2f;
            for (var side = -1; side <= 1; side += 2)
                for (var i = 0; i < 3; i++)
                {
                    var hip = c + V(side * (0.35f + i * 0.18f) * r, -0.15f * r);
                    var wiggle = Mathf.Sin(scuttle + i * 2f + side) * 0.12f * r * moving;
                    var knee = hip + V(side * 0.35f * r, 0.15f * r + wiggle);
                    Limb(hip, knee, 0.09f * r, Dark(body, 0.15f));
                    Limb(knee, V(knee.x + side * 0.15f * r, 0.02f), 0.08f * r, Dark(body, 0.15f));
                }
            var raise = 0.25f * r + _crouch * 0.35f * r + Mathf.Sin(_time * 3f + _seed) * 0.04f * r;
            for (var side = -1; side <= 1; side += 2)
            {
                var arm = c + V(side * 0.75f * r, 0.15f * r);
                var claw = arm + V(side * 0.3f * r + _stretch * 0.3f * r, raise);
                Limb(arm, claw, 0.12f * r, body);
                Ell(claw, 0.2f * r, 0.16f * r, body);
                var snap = 0.08f * r + Mathf.Abs(Mathf.Sin(_time * 8f)) * 0.06f * r * _crouch;
                _mesh.Line(claw + V(side * 0.12f * r, snap), claw + V(side * 0.12f * r, -snap), 0.04f * r, Ink);
            }
            Ell(c, 0.85f * r, 0.5f * r, body);
            _mesh.Ellipse(c + V(-0.15f * r, 0.18f * r), 0.35f * r, 0.15f * r, _accent);
            for (var side = -1; side <= 1; side += 2)
            {
                var stalk = c + V(side * 0.22f * r, 0.45f * r);
                Limb(c + V(side * 0.18f * r, 0.3f * r), stalk, 0.05f * r, body);
                Ell(stalk, 0.1f * r, 0.1f * r, Color.white);
                Eye(stalk + V(0.02f * r, 0f), 0.05f * r);
            }
        }

        void Bird(float r, Color body, float moving)
        {
            var k = r * 2.6f;
            var flying = Mathf.Clamp01((_speed - 2.5f) / 1f);
            var flap = Mathf.Sin(_time * 18f);
            var c = V(0f, 0.42f * k + flying * (0.55f * k + flap * 0.05f * k));
            if (flying < 0.5f)
                for (var i = 0; i < 2; i++)
                {
                    var step = Mathf.Sin(_phase * Tau * 2f + i * Mathf.PI) * 0.12f * k * moving;
                    Limb(c + V((i - 0.5f) * 0.08f * k, -0.15f * k), V(step + (i - 0.5f) * 0.08f * k, 0.01f), 0.04f * k, _accent);
                }
            Poly(new List<Vector2> { c + V(-0.35f * k, 0.05f * k), c + V(-0.7f * k, 0.15f * k), c + V(-0.65f * k, -0.05f * k) }, _belly);
            Ell(c, 0.42f * k, 0.26f * k, body);
            var wingTip = flying > 0.5f ? c + V(-0.25f * k, flap * 0.55f * k) : c + V(-0.45f * k, 0.02f * k);
            Poly(new List<Vector2> { c + V(0.15f * k, 0.08f * k), wingTip, c + V(-0.2f * k, -0.05f * k) }, _belly);
            var h = c + V(0.35f * k, 0.25f * k);
            Ell(h, 0.17f * k, 0.16f * k, body);
            Poly(new List<Vector2> { h + V(0.13f * k, 0.03f * k), h + V(0.38f * k, -0.02f * k), h + V(0.13f * k, -0.07f * k) }, _accent);
            Eye(h + V(0.06f * k, 0.05f * k), 0.035f * k);
        }

        void Frog(float r, Color body, float moving)
        {
            var k = r * 2.4f;
            var jump = Mathf.Abs(Mathf.Sin(_phase * Mathf.PI)) * 0.4f * k * moving;
            var c = V(0f, 0.3f * k + jump);
            var stretch = Mathf.Sin(_phase * Mathf.PI) * moving;
            Limb(c + V(-0.3f * k, -0.1f * k), V(-0.45f * k - stretch * 0.25f * k, Mathf.Max(0.01f, jump - 0.1f * k)), 0.12f * k, Dark(body, 0.15f));
            Ell(c + V(-0.28f * k, -0.08f * k), 0.25f * k, 0.18f * k, body);
            Ell(c, 0.45f * k, 0.3f * k, body);
            _mesh.Ellipse(c + V(0.05f * k, -0.12f * k), 0.32f * k, 0.12f * k, _belly);
            for (var i = 0; i < 3; i++) _mesh.Disk(c + V((-0.2f + i * 0.15f) * k, 0.18f * k), 0.05f * k, _accent);
            Limb(c + V(0.25f * k, -0.15f * k), V(0.32f * k + stretch * 0.1f * k, Mathf.Max(0.01f, jump - 0.05f * k)), 0.08f * k, body);
            var eye = c + V(0.28f * k, 0.28f * k);
            Ell(eye, 0.13f * k, 0.13f * k, body);
            _mesh.Disk(eye + V(0.02f * k, 0.01f * k), 0.08f * k, Color.white);
            Eye(eye + V(0.04f * k, 0.01f * k), 0.045f * k);
            _mesh.Line(c + V(0.25f * k, 0.02f * k), c + V(0.42f * k, 0.06f * k), 0.025f * k, Ink);
        }

        void Turtle(float r, Color body, float moving)
        {
            var c = V(0f, 0.18f * r);
            var walk = _phase * Tau;
            for (var i = 0; i < 2; i++)
            {
                var x = (i == 0 ? 0.55f : -0.55f) * r;
                Ell(V(x + Mathf.Sin(walk + i * Mathf.PI) * 0.12f * r * moving, 0.08f * r), 0.18f * r, 0.12f * r, Dark(_belly, 0.25f));
            }
            var headOut = Mathf.Lerp(1f, 0.5f, _sleep);
            var h = c + V(0.95f * r * headOut, 0.12f * r + Mathf.Sin(_time * 2f + _seed) * 0.03f * r);
            Limb(c + V(0.6f * r, 0.08f * r), h, 0.16f * r, _belly);
            Ell(h, 0.22f * r, 0.17f * r, _belly);
            Eye(h + V(0.08f * r, 0.06f * r), 0.04f * r);
            var dome = new List<Vector2>();
            for (var i = 0; i <= 12; i++)
            {
                var a = Mathf.Lerp(0f, 180f, i / 12f) * Mathf.Deg2Rad;
                dome.Add(c + V(Mathf.Cos(a) * 0.85f * r, Mathf.Sin(a) * 0.6f * r));
            }
            Poly(dome, body);
            _mesh.Ellipse(c + V(0f, 0.28f * r), 0.22f * r, 0.15f * r, _accent);
            _mesh.Ellipse(c + V(-0.45f * r, 0.18f * r), 0.16f * r, 0.12f * r, _accent);
            _mesh.Ellipse(c + V(0.45f * r, 0.18f * r), 0.16f * r, 0.12f * r, _accent);
            _mesh.Line(c + V(-0.85f * r, 0f), c + V(0.85f * r, 0f), 0.08f * r, Dark(body, 0.3f));
            for (var i = 0; i < 2; i++)
            {
                var x = (i == 0 ? 0.55f : -0.55f) * r;
                Ell(V(x + Mathf.Sin(walk + i * Mathf.PI + Mathf.PI) * 0.12f * r * moving, 0.07f * r), 0.18f * r, 0.12f * r, _belly);
            }
        }

        // Dizzy stars circling a staggered creature; a few z's over a sleeping one.
        void Stars(float r)
        {
            for (var i = 0; i < 3; i++)
            {
                var a = _time * 6f + i * Tau / 3f;
                var at = V(Mathf.Cos(a) * r * 0.8f, r * 2.2f + Mathf.Sin(a) * r * 0.25f);
                _mesh.Disk(at, r * 0.12f + O * 0.5f, Ink);
                _mesh.Disk(at, r * 0.12f, new Color(1f, 0.85f, 0.25f));
            }
        }

        void Zs(float r)
        {
            for (var i = 0; i < 2; i++)
            {
                var t = Mathf.Repeat(_time * 0.4f + i * 0.5f, 1f);
                var size = r * (0.18f + 0.12f * t);
                var at = V(r * (0.6f + t * 0.5f), r * (1.6f + t * 1.2f));
                var col = new Color(1f, 1f, 1f, 1f - t);
                _mesh.Line(at + V(-size, size), at + V(size, size), size * 0.35f, col);
                _mesh.Line(at + V(size, size), at + V(-size, -size), size * 0.35f, col);
                _mesh.Line(at + V(-size, -size), at + V(size, -size), size * 0.35f, col);
            }
        }
    }
}
