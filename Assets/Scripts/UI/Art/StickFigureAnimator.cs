using UnityEngine;

namespace Isle.UI.Art
{
    /// <summary>What the figure is doing, read from gameplay data by the view (SYS-CHAR-02 §Animation).</summary>
    public enum FigureAction { None, Swing, Gather, Cast, Bite, Reel, Draw, Block, Roll, Dead }

    /// <summary>One frame of input to <see cref="StickFigureAnimator"/>. Presentation data only.</summary>
    public struct FigureInput
    {
        /// <summary>Tiles per second, smoothed.</summary>
        public float Speed;

        /// <summary>Tiles travelled this frame — drives the gait phase.</summary>
        public float Distance;

        /// <summary>-1 face left, +1 face right, 0 keep the current facing.</summary>
        public float FacingTarget;

        public FigureAction Action;

        /// <summary>Seconds since the current action started.</summary>
        public float ActionTime;

        /// <summary>Bow draw 0..1.</summary>
        public float DrawProgress;

        /// <summary>Length of a roll in seconds, for the spin.</summary>
        public float RollDuration;

        /// <summary>The off hand holds something carried raised (a torch).</summary>
        public bool OffHandRaised;

        /// <summary>Melee combo step of the current swing (1-based) and the combo's length; the last step is the
        /// finisher. 0 = a plain swing.</summary>
        public int ComboStep, ComboLength;

        /// <summary>Where the mouse points, as an angle from straight ahead in the figure's own facing (radians, + up).
        /// Only meaningful with <see cref="HasAim"/> — the local player; others have no aim to show.</summary>
        public bool HasAim;
        public float AimAngle;

        /// <summary>Something is in the main hand: it is held out toward the aim instead of hanging at the side.</summary>
        public bool HoldsItem;
    }

    /// <summary>Joint positions for one frame, in the figure's local space: origin at the feet, +x the way it faces
    /// (the drawer mirrors by <see cref="FacingScale"/>), tiles.</summary>
    public struct StickFigurePose
    {
        public Vector2 Hip, Neck, Shoulder, Head;
        public Vector2 KneeFront, FootFront, KneeBack, FootBack;
        public Vector2 ElbowFront, HandFront, ElbowBack, HandBack;

        /// <summary>Absolute angle (radians) of what each hand holds.</summary>
        public float ItemAngleFront, ItemAngleBack;

        public float HeadTilt;
        public float CloakFlare;
        public bool EyesClosed;
        public bool Dead;

        /// <summary>Whole-figure rotation (roll spin, falling over) about <see cref="Pivot"/>.</summary>
        public float Rotation;
        public Vector2 Pivot;

        /// <summary>Horizontal scale: ±1 facing, passing through 0 while turning (the squash).</summary>
        public float FacingScale;

        public float DrawProgress;
        public FigureAction Action;
        public float ActionTime;
    }

    /// <summary>
    /// SYS-CHAR-02: procedural animation for the stick figure. No keyframes — every pose is computed from the
    /// gait phase, the action and the clock, and every target is chased with a critically damped spring so states
    /// blend instead of snapping. Pure C# (no scene access), so it is testable and frame-rate independent.
    /// </summary>
    public sealed class StickFigureAnimator
    {
        // SYS-CHAR-02 §Skeleton [invented].
        // Fancy Pants proportions: a small head on a long body, long legs and arms (developer, 2026-10-06: "the head
        // reads far too big").
        public const float HipHeight = 0.74f, TorsoLength = 0.44f, NeckToHead = 0.19f;
        public const float Thigh = 0.38f, Shin = 0.38f, UpperArm = 0.29f, ForeArm = 0.29f;

        // Presentation tuning [invented] — none of these touch gameplay.
        // Gait: a walk keeps a foot down half the cycle; a run shortens ground contact to a third and adds a flight
        // phase, so the stride itself lengthens with speed instead of the feet just spinning faster.
        const float RunFrom = 4.6f, RunSpan = 2.0f;           // tiles/s: walking speed is 4.2, sprinting 6.9
        const float WalkHalfSpan = 0.38f, RunHalfSpan = 0.47f; // how far a planted foot travels either side of the hip
        const float WalkStance = 0.5f, RunStance = 0.32f;      // share of the cycle a foot spends on the ground
        const float FullWalkSpeed = 1.5f;   // tiles/s at which the gait is fully blended in
        const float SwingSeconds = 0.4f;
        const float GatherLoopSeconds = 0.7f;
        const float Deg = Mathf.Deg2Rad;

        public StickFigurePose Pose;

        float _phase, _time;
        float _moveBlend, _moveBlendV;
        float _facing = 1f, _facingV, _facingGoal = 1f;
        float _lean, _leanV;

        /// <summary>How much the aim pulls the head, and the most it may tip off the spine. [invented]</summary>
        const float HeadLookWeight = 0.3f;
        const float MaxHeadLookDeg = 18f;
        float _tuck, _tuckV;
        float _fall, _fallV;
        float _cloak, _cloakV;
        float _crouch, _crouchV;
        float _run, _runV;
        float _itemFront = 75f * Deg, _itemFrontV, _itemBack = 75f * Deg, _itemBackV;
        Vector2 _handFront, _handFrontV, _handBack, _handBackV;
        Vector2 _look = Vector2.up, _lookV;
        bool _handsPlaced;
        float _nextBlink = 2.5f;
        uint _seed = 0x9E3779B9u;

        public float Facing => _facingGoal;

        public void Step(in FigureInput input, float dt)
        {
            dt = Mathf.Max(0f, dt);
            _time += dt;
            var action = input.Action;
            var t = input.ActionTime;

            Spring.Damp(ref _run, ref _runV, Mathf.Clamp01((input.Speed - RunFrom) / RunSpan), 6f, dt);
            var halfSpan = Mathf.Lerp(WalkHalfSpan, RunHalfSpan, _run);
            var stance = Mathf.Lerp(WalkStance, RunStance, _run);
            // No foot slides: a planted foot covers 2·halfSpan in `stance` of a cycle, so the cycle covers this.
            _phase = Gait.Advance(_phase, input.Distance, 2f * halfSpan / stance);
            var moving = action is FigureAction.Roll or FigureAction.Dead ? 0f : Mathf.Clamp01(input.Speed / FullWalkSpeed);
            Spring.Damp(ref _moveBlend, ref _moveBlendV, moving, 12f, dt);
            if (input.FacingTarget != 0f && action != FigureAction.Dead) _facingGoal = Mathf.Sign(input.FacingTarget);
            Spring.Damp(ref _facing, ref _facingV, _facingGoal, 22f, dt);

            // Fancy Pants runs bent hard into the motion.
            var leanGoal = Mathf.Clamp(input.Speed * 0.075f, 0f, 0.42f);
            var finisher = action == FigureAction.Swing && input.ComboStep > 1 && input.ComboStep >= input.ComboLength;
            if (action is FigureAction.Swing or FigureAction.Gather) leanGoal += finisher ? 0.28f : 0.12f;
            if (action == FigureAction.Block) leanGoal = -0.06f;
            if (action == FigureAction.Reel) leanGoal -= 0.15f;
            // Looking up rocks the upper body back a little, looking down tips it forward — the head leads, the body follows.
            if (input.HasAim && action is FigureAction.None or FigureAction.Draw or FigureAction.Block) leanGoal -= input.AimAngle * 0.08f;
            Spring.Damp(ref _lean, ref _leanV, leanGoal, 10f, dt);
            Spring.Damp(ref _tuck, ref _tuckV, action == FigureAction.Roll ? 1f : 0f, 25f, dt);
            Spring.Damp(ref _fall, ref _fallV, action == FigureAction.Dead ? 1f : 0f, 7f, dt);
            Spring.Damp(ref _cloak, ref _cloakV, Mathf.Clamp(input.Speed * 0.08f, 0f, 0.35f) + 0.04f * Mathf.Sin(_time * 3.1f), 6f, dt);

            var p = new StickFigurePose { Action = action, ActionTime = t, DrawProgress = input.DrawProgress };
            var m = _moveBlend;
            var tau = Mathf.PI * 2f;

            // Body: bob twice per cycle while walking, breathe while still, crouch into a ball while rolling.
            // Walking dips on each step; running sinks the hips and springs up into the flight between strides.
            var bob = -m * 0.05f * (1f - _run) * (0.5f + 0.5f * Mathf.Cos(2f * tau * _phase))
                      + m * _run * (0.09f * Mathf.Max(0f, Mathf.Sin(2f * tau * _phase - 0.6f)) - 0.09f)
                      + 0.012f * Mathf.Sin(_time * 2.2f) * (1f - m);
            Spring.Damp(ref _crouch, ref _crouchV, action == FigureAction.Block ? 0.06f : finisher ? 0.05f : 0f, 14f, dt);
            var hipY = Mathf.Lerp(HipHeight + bob - _crouch, 0.42f, _tuck);
            var lean = Mathf.Lerp(_lean, 0.9f, _tuck);
            p.Hip = new Vector2(0f, hipY);
            var up = new Vector2(Mathf.Sin(lean), Mathf.Cos(lean));
            p.Neck = p.Hip + up * Mathf.Lerp(TorsoLength, 0.3f, _tuck);
            p.Shoulder = p.Neck - up * 0.05f;
            p.HeadTilt = lean * 0.6f;
            // The head looks where the mouse is: the neck bends toward the aim (up, ahead, down).
            var aim = input.HasAim ? input.AimAngle : 0f;
            var aimDir = new Vector2(Mathf.Cos(aim), Mathf.Sin(aim));
            var headDir = new Vector2(Mathf.Sin(p.HeadTilt), Mathf.Cos(p.HeadTilt));
            // The head nods toward the mouse rather than craning out to it: a little of the aim, and never more than
            // MaxHeadLookDeg off the spine, so standing and looking ahead stays upright.
            if (input.HasAim && action is not (FigureAction.Roll or FigureAction.Dead))
            {
                var spine = headDir;
                headDir = (spine + aimDir * HeadLookWeight).normalized;
                var off = Vector2.SignedAngle(spine, headDir);
                if (Mathf.Abs(off) > MaxHeadLookDeg) headDir = Rotate(spine, Mathf.Sign(off) * MaxHeadLookDeg * Deg);
            }
            Spring.Damp(ref _look, ref _lookV, headDir, 12f, dt);
            p.Head = p.Neck + _look.normalized * Mathf.Lerp(NeckToHead + 0.03f, 0.17f, _tuck);

            // Legs: gait arcs blended with the idle stance, tucked under the hip while rolling.
            var lift = Mathf.Lerp(0.12f, 0.32f, _run);
            var footF = Vector2.Lerp(new Vector2(0.13f, 0f), FootAt(_phase, halfSpan, lift, stance), m);
            var footB = Vector2.Lerp(new Vector2(-0.12f, 0f), FootAt(Mathf.Repeat(_phase + 0.5f, 1f), halfSpan, lift, stance), m);
            footF = Vector2.Lerp(footF, p.Hip + new Vector2(0.12f, -0.22f), _tuck);
            footB = Vector2.Lerp(footB, p.Hip + new Vector2(0.02f, -0.26f), _tuck);
            p.FootFront = footF;
            p.FootBack = footB;
            p.KneeFront = TwoBoneIk.Solve(p.Hip, footF, Thigh, Shin, 1f);
            p.KneeBack = TwoBoneIk.Solve(p.Hip, footB, Thigh, Shin, 1f);

            // Arms: walking swing opposite to the legs, idle sway, then each action overrides the hand targets.
            // Arms pump wider the faster it goes.
            // Running pumps the arms in wide arcs with the elbows bent (shorter reach), not just faster.
            var swing = Mathf.Lerp(0.7f, 1.4f, _run) * m * Mathf.Cos(tau * _phase);
            var sway = 0.05f * Mathf.Sin(_time * 1.7f) * (1f - m);
            var reach = Mathf.Lerp(0.56f, 0.42f, _run);
            var handF = p.Shoulder + reach * new Vector2(Mathf.Sin(-swing + 0.12f + sway), -Mathf.Cos(-swing + 0.12f + sway));
            var handB = p.Shoulder + reach * new Vector2(Mathf.Sin(swing - 0.12f - sway), -Mathf.Cos(swing - 0.12f - sway));
            float? itemFrontAbs = null, itemFrontRel = 75f * Deg;
            float? itemBackAbs = null;
            var handFreq = 14f;

            switch (action)
            {
                case FigureAction.Swing:
                case FigureAction.Gather:
                {
                    var st = action == FigureAction.Swing ? t : Mathf.Repeat(t, GatherLoopSeconds) * (SwingSeconds / GatherLoopSeconds);
                    var style = action != FigureAction.Swing ? 0 : finisher ? 2 : input.ComboStep == 2 ? 1 : 0;
                    // The arc is turned to the aim, so the strike lands where the mouse points.
                    var a = SwingAngle(st, style, out var striking) + (action == FigureAction.Swing ? aim : 0f);
                    handF = p.Shoulder + 0.54f * new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    itemFrontRel = striking ? 10f * Deg : 40f * Deg;
                    handB = action == FigureAction.Gather ? handF + new Vector2(-0.07f, -0.06f) : p.Shoulder + new Vector2(0.16f, -0.3f);
                    handFreq = 40f;
                    break;
                }
                case FigureAction.Cast:
                {
                    var a = t < 0.35f ? Mathf.Lerp(130f, 40f, Smooth(t / 0.35f)) : 40f;
                    handF = p.Shoulder + new Vector2(0.34f, t < 0.35f ? Mathf.Lerp(0.2f, -0.08f, t / 0.35f) : -0.08f);
                    itemFrontAbs = a * Deg;
                    handB = p.Shoulder + new Vector2(0.18f, -0.22f);
                    handFreq = 30f;
                    break;
                }
                case FigureAction.Bite:
                    handF = p.Shoulder + new Vector2(0.34f, -0.12f + 0.03f * Mathf.Sin(t * 45f));
                    itemFrontAbs = (34f + 10f * Mathf.Sin(t * 40f)) * Deg;
                    handB = p.Shoulder + new Vector2(0.18f, -0.22f);
                    handFreq = 40f;
                    break;
                case FigureAction.Reel:
                    handF = p.Shoulder + new Vector2(0.3f, -0.04f);
                    itemFrontAbs = (62f + 4f * Mathf.Sin(t * 9f)) * Deg;
                    handB = handF + new Vector2(-0.06f, -0.12f) + 0.06f * new Vector2(Mathf.Cos(t * 14f), Mathf.Sin(t * 14f));
                    handFreq = 40f;
                    break;
                case FigureAction.Draw:
                    // Bow arm straight at the aim, string hand drawing back along the same line.
                    handF = p.Shoulder + aimDir * 0.55f;
                    itemFrontAbs = aim;
                    handB = p.Shoulder + aimDir * Mathf.Lerp(0.48f, 0.04f, Mathf.Clamp01(input.DrawProgress));
                    handFreq = 30f;
                    break;
                case FigureAction.Block:
                    // Guard up: weapon held upright in front of the face, off hand braced behind it.
                    handF = p.Shoulder + Rotate(new Vector2(0.32f, 0.1f), aim);
                    handB = p.Shoulder + Rotate(new Vector2(0.26f, -0.02f), aim);
                    itemFrontAbs = aim + 100f * Deg;
                    handFreq = 35f;
                    break;
                case FigureAction.Roll:
                    handF = p.Hip + new Vector2(0.2f, -0.05f);
                    handB = p.Hip + new Vector2(0.1f, -0.1f);
                    handFreq = 30f;
                    break;
                case FigureAction.Dead:
                    handF = p.Shoulder + new Vector2(0.06f, -0.46f);
                    handB = p.Shoulder + new Vector2(-0.08f, -0.45f);
                    break;
            }

            // Holding something with nothing else to do: it's levelled at the mouse — a spear's tip points where you aim.
            if (action == FigureAction.None && input.HasAim && input.HoldsItem)
            {
                handF = p.Shoulder + aimDir * 0.46f + new Vector2(0f, -0.04f + 0.015f * Mathf.Sin(_time * 2.2f));
                handB = p.Shoulder + aimDir * 0.22f + new Vector2(0f, -0.1f);
                itemFrontAbs = aim;
                handFreq = 22f;
            }

            // A carried torch is held up and forward whenever the off hand isn't busy with the action.
            if (input.OffHandRaised && action is FigureAction.None or FigureAction.Cast or FigureAction.Bite or FigureAction.Swing)
            {
                handB = p.Shoulder + new Vector2(0.3f, -0.14f + 0.02f * Mathf.Sin(_time * 2f));
                itemBackAbs = 70f * Deg;
            }

            if (!_handsPlaced)
            {
                _handFront = handF;
                _handBack = handB;
                _handsPlaced = true;
            }
            Spring.Damp(ref _handFront, ref _handFrontV, handF, handFreq, dt);
            Spring.Damp(ref _handBack, ref _handBackV, handB, handFreq, dt);
            p.HandFront = _handFront;
            p.HandBack = _handBack;
            p.ElbowFront = TwoBoneIk.Solve(p.Shoulder, p.HandFront, UpperArm, ForeArm, -1f);
            p.ElbowBack = TwoBoneIk.Solve(p.Shoulder, p.HandBack, UpperArm, ForeArm, -1f);
            p.HandFront = ReachedHand(p.Shoulder, p.ElbowFront, p.HandFront, ForeArm);
            p.HandBack = ReachedHand(p.Shoulder, p.ElbowBack, p.HandBack, ForeArm);

            // Held items: relative to the forearm unless the action fixes an absolute angle.
            var forearmF = AngleOf(p.HandFront - p.ElbowFront);
            var forearmB = AngleOf(p.HandBack - p.ElbowBack);
            ChaseAngle(ref _itemFront, ref _itemFrontV, itemFrontAbs ?? forearmF + itemFrontRel.Value, handFreq >= 40f ? 50f : 30f, dt);
            ChaseAngle(ref _itemBack, ref _itemBackV, itemBackAbs ?? forearmB + 75f * Deg, 30f, dt);
            p.ItemAngleFront = _itemFront;
            p.ItemAngleBack = _itemBack;

            // Whole-figure rotation: a forward roll about the body's centre, or toppling backwards when dead.
            if (action == FigureAction.Roll && input.RollDuration > 0f)
            {
                p.Rotation = -tau * Mathf.Clamp01(t / input.RollDuration);
                p.Pivot = new Vector2(0f, 0.45f);
            }
            else
            {
                p.Rotation = _fall * 90f * Deg;
                p.Pivot = Vector2.zero;
            }

            p.Dead = action == FigureAction.Dead;
            p.CloakFlare = _cloak;
            p.FacingScale = Mathf.Abs(_facing) < 0.06f ? 0.06f * (_facing < 0f ? -1f : 1f) : Mathf.Clamp(_facing, -1f, 1f);
            p.EyesClosed = Blink();
            Pose = p;
        }

        /// <summary>Stance (first half): the planted foot slides back along the ground as the body passes over it.
        /// Swing (second half): it lifts in an arc and lands ahead.</summary>
        public static Vector2 FootAt(float phase, float halfSpan, float lift, float stance = 0.5f)
        {
            if (phase < stance) return new Vector2(Mathf.Lerp(halfSpan, -halfSpan, phase / stance), 0f);
            var s = (phase - stance) / (1f - stance);
            return new Vector2(Mathf.Lerp(-halfSpan, halfSpan, Smooth(s)), Mathf.Sin(s * Mathf.PI) * lift);
        }

        // Swing arcs, degrees from +x: start → wind-up → strike end. Style 0 overhead chop, 1 rising backhand (combo
        // step 2), 2 finisher — a bigger wind-up and a longer, lower follow-through.
        static readonly (float Start, float Wind, float End, float WindTime, float StrikeTime)[] Swings =
        {
            (-60f, 140f, -35f, 0.12f, 0.14f),
            (-60f, -150f, 70f, 0.10f, 0.13f),
            (-60f, 175f, -70f, 0.15f, 0.15f),
        };

        /// <summary>Wind-up, fast strike, brief follow-through. Radians from +x.</summary>
        static float SwingAngle(float t, int style, out bool striking)
        {
            var s = Swings[Mathf.Clamp(style, 0, Swings.Length - 1)];
            striking = false;
            if (t < s.WindTime) return Mathf.Lerp(s.Start, s.Wind, Smooth(t / s.WindTime)) * Deg;
            striking = t < s.WindTime + s.StrikeTime;
            if (striking) return Mathf.Lerp(s.Wind, s.End, Smooth((t - s.WindTime) / s.StrikeTime)) * Deg;
            return s.End * Deg;
        }

        static Vector2 Rotate(Vector2 v, float radians)
        {
            var c = Mathf.Cos(radians);
            var s = Mathf.Sin(radians);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        static float AngleOf(Vector2 v) => Mathf.Atan2(v.y, v.x);

        static void ChaseAngle(ref float angle, ref float velocity, float target, float frequency, float dt)
        {
            var goal = angle + Mathf.DeltaAngle(angle * Mathf.Rad2Deg, target * Mathf.Rad2Deg) * Deg;
            Spring.Damp(ref angle, ref velocity, goal, frequency, dt);
        }

        /// <summary>When the target was out of reach, the hand sits at the end of the straightened arm.</summary>
        static Vector2 ReachedHand(Vector2 shoulder, Vector2 elbow, Vector2 target, float fore)
        {
            var dir = target - elbow;
            return dir.sqrMagnitude < 1e-8f ? elbow : elbow + dir.normalized * fore;
        }

        bool Blink()
        {
            if (_time < _nextBlink) return false;
            if (_time < _nextBlink + 0.12f) return true;
            _seed = _seed * 1664525u + 1013904223u;
            _nextBlink = _time + 2.5f + (_seed >> 8) / (float)(1 << 24) * 3f;
            return false;
        }
    }
}
