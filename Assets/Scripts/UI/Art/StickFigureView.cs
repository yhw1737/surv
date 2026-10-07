using System.Collections.Generic;
using FishNet;
using Isle.Gameplay.Character;
using Isle.Gameplay.Feedback;
using Isle.Gameplay.Fishing;
using Isle.Gameplay.Inventory;
using Isle.Networking;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Isle.UI.Art
{
    /// <summary>
    /// SYS-CHAR-02: draws one player as the cartoon stick figure. Lives on its own GameObject (not under the player)
    /// so it can sit at a position interpolated between network ticks and move every rendered frame. Reads gameplay
    /// state only — movement flags, the current action, equipped defs — and never writes any (Absolute Rule 7).
    /// </summary>
    public sealed class StickFigureView : MonoBehaviour
    {
        // [invented] presentation values.
        const float SnapDistance = 3f;  // further than this between ticks is a teleport (respawn), not a walk
        const float SwingSeconds = 0.4f;
        const float FullDrawSeconds = 1f;
        public const int SortingOrder = 2; // same order as standing nodes, so the custom-axis sort interleaves them

        static readonly Dictionary<PlayerInteraction, StickFigureView> ByPlayer = new();

        PlayerInteraction _player;
        PlayerMovement _movement;
        DeathHandler _death;
        InventoryNetwork _inventory;
        Vitals _vitals;

        readonly StickFigureAnimator _animator = new();
        readonly VectorMesh _vector = new();
        Mesh _mesh;

        Vector2 _previous, _current, _shown;
        float _previousTime, _currentTime;
        Vector2 _velocity, _velocityV;
        float _swingAt = -10f, _rollAt = -10f, _actionAt;
        FigureAction _lastAction;
        bool _wasRolling;

        /// <summary>The drawn (interpolated) position of a player, for the camera. Falls back to the transform.</summary>
        public static Vector2 PositionOf(PlayerInteraction player) =>
            player != null && ByPlayer.TryGetValue(player, out var view) && view != null ? view._shown : player != null ? (Vector2)player.transform.position : Vector2.zero;

        public static bool Has(PlayerInteraction player) => ByPlayer.ContainsKey(player);

        public static StickFigureView Attach(PlayerInteraction player, Material material)
        {
            var go = new GameObject($"StickFigure_{player.name}");
            var view = go.AddComponent<StickFigureView>();
            view.Bind(player, material);
            return view;
        }

        void Bind(PlayerInteraction player, Material material)
        {
            _player = player;
            _movement = player.GetComponent<PlayerMovement>();
            _death = player.GetComponent<DeathHandler>();
            _inventory = player.GetComponent<InventoryNetwork>();
            _vitals = player.GetComponent<Vitals>();
            ByPlayer[player] = this;

            _mesh = new Mesh { name = "StickFigure" };
            _mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = SortingOrder;

            _previous = _current = _shown = player.transform.position;
            _previousTime = _currentTime = Time.time;
            transform.position = _shown;

            GameFeed.PlayerSwing += OnSwing;
        }

        void OnDestroy()
        {
            GameFeed.PlayerSwing -= OnSwing;
            if (_player != null && ByPlayer.TryGetValue(_player, out var v) && v == this) ByPlayer.Remove(_player);
            if (_mesh != null) Destroy(_mesh);
        }

        // The feed doesn't say who swung; the nearest figure takes it.
        void OnSwing(Vector2 at, float reach)
        {
            StickFigureView nearest = null;
            var best = float.MaxValue;
            foreach (var view in ByPlayer.Values)
            {
                if (view == null) continue;
                var d = ((Vector2)view._player.transform.position - at).sqrMagnitude;
                if (d < best) (best, nearest) = (d, view);
            }
            if (nearest == this) _swingAt = Time.time;
        }

        void LateUpdate()
        {
            if (_player == null)
            {
                Destroy(gameObject);
                return;
            }

            var dt = Time.deltaTime;
            var now = Time.time;
            TrackTicks(now);

            var tick = InstanceFinder.TimeManager != null ? (float)InstanceFinder.TimeManager.TickDelta : 1f / 30f;
            var shown = TickInterpolation.Sample(_previous, _current, _previousTime, _currentTime, now, tick);
            var moved = shown - _shown;
            _shown = shown;
            transform.position = new Vector3(shown.x, shown.y, 0f);

            var instantVelocity = dt > 0f ? moved / dt : Vector2.zero;
            Spring.Damp(ref _velocity, ref _velocityV, instantVelocity, 12f, dt);

            var action = CurrentAction(now, out var actionTime, out var drawProgress);
            var input = new FigureInput
            {
                Speed = _velocity.magnitude,
                Distance = moved.magnitude,
                FacingTarget = FacingTarget(action),
                Action = action,
                ActionTime = actionTime,
                DrawProgress = drawProgress,
                RollDuration = MovementCalculator.RollSeconds,
                ComboStep = action == FigureAction.Swing ? _player.ComboStep : 0,
                ComboLength = _player.ComboLength,
            };
            var outfit = Outfit();
            input.OffHandRaised = outfit.OffHand?.Hold?.Style == "torch";
            input.HoldsItem = outfit.MainHand?.Hold != null;
            if (_player.IsOwner && AimPoint() is { } aimAt)
            {
                // Measured from the shoulder, in the figure's own facing: 0 straight ahead, + up.
                var fromShoulder = aimAt - (shown + Vector2.up * (StickFigureAnimator.HipHeight + StickFigureAnimator.TorsoLength - 0.05f));
                input.HasAim = fromShoulder.sqrMagnitude > 0.01f;
                input.AimAngle = Mathf.Clamp(Mathf.Atan2(fromShoulder.y, Mathf.Abs(fromShoulder.x)), -1.45f, 1.45f);
                // A held weapon turns the body to face the mouse; empty-handed it does so while standing still.
                var standing = _velocity.magnitude < 0.3f;
                if ((input.HoldsItem || standing) && action is FigureAction.None or FigureAction.Swing or FigureAction.Draw or FigureAction.Block && Mathf.Abs(fromShoulder.x) > 0.05f)
                    input.FacingTarget = Mathf.Sign(fromShoulder.x);
            }
            _animator.Step(input, dt);

            Vector2? lineEnd = null;
            var pose = _animator.Pose;
            if (_player.Cast != null && Mathf.Abs(pose.FacingScale) > 0.2f)
            {
                var local = _player.CastPoint - shown;
                lineEnd = new Vector2(local.x / pose.FacingScale, local.y);
            }

            _vector.Clear();
            StickFigureDrawer.Draw(_vector, pose, outfit, lineEnd, now);
            DrawStatus(pose, now);
            _vector.Fill(_mesh);
            // Bounds centred on the feet: the 2D renderer's custom-axis sort uses the bounds centre, so this makes the
            // figure sort by where it stands — the same rule as the trees' foot pivots. Big enough to never cull early.
            _mesh.bounds = new Bounds(Vector3.zero, new Vector3(6f, 6f, 1f));
        }

        /// <summary>T-165: debuffs on the body — tint, shiver/pant/hunch, then particles over the top. The shadow (the
        /// first shape drawn) keeps its colour.</summary>
        void DrawStatus(in StickFigurePose pose, float now)
        {
            if (_vitals == null || (_death != null && _death.IsDead)) return;
            var s = new StatusState
            {
                SinceHit = now - _vitals.LastHitAt,
                Poisoned = _vitals.Status.Stacks(Isle.Gameplay.Combat.DamageTypes.Toxic) > 0,
                Bleeding = _vitals.Status.Stacks(Isle.Gameplay.Combat.DamageTypes.Slash) > 0,
                Burning = _vitals.Status.Stacks(Isle.Gameplay.Combat.DamageTypes.Heat) > 0,
                // SYS-SURV-01's hypothermia warning (33°) is where the shiver starts.
                Cold = _vitals.Temperature <= VitalsCalculator.HypothermiaWarningTemp,
                Exhausted = _vitals.Exhausted,
                Overloaded = _vitals.Overloaded,
                Wet = Mathf.Clamp01(_vitals.WetPenalty / VitalsCalculator.WetPenaltyMagnitude),
            };
            const int afterShadow = 8; // the foot shadow is two ellipses, one quad each
            var tint = StatusLook.Tint(s, now);
            _vector.TintFrom(afterShadow, tint.Colour, tint.Amount);
            var facing = Mathf.Abs(pose.FacingScale) > 0.01f ? Mathf.Sign(pose.FacingScale) : 1f;
            StatusLook.Move(_vector, afterShadow, s, now, facing, StickFigureAnimator.HipHeight, StickFigureAnimator.HipHeight + StickFigureAnimator.TorsoLength);
            _vector.Transform = null;
            StatusLook.Particles(_vector, s, now, new StatusBody
            {
                Centre = new Vector2(0f, StickFigureAnimator.HipHeight + StickFigureAnimator.TorsoLength * 0.5f),
                HalfWidth = 0.18f,
                Top = pose.Head.y + 0.15f,
                Head = new Vector2(pose.Head.x * facing, pose.Head.y),
            });
        }

        void TrackTicks(float now)
        {
            Vector2 position = _player.transform.position;
            if (position == _current) return;
            if ((position - _current).sqrMagnitude > SnapDistance * SnapDistance)
            {
                _previous = _current = _shown = position;
                _previousTime = _currentTime = now;
                return;
            }
            // Start the new segment from where the figure is drawn now, so a late tick never jumps backwards.
            _previous = _shown;
            _current = position;
            _previousTime = _currentTime;
            _currentTime = now;
        }

        FigureAction CurrentAction(float now, out float time, out float draw)
        {
            draw = 0f;
            var action = Resolve(now, out var since);
            if (action != _lastAction)
            {
                _lastAction = action;
                _actionAt = now;
            }
            time = since >= 0f ? since : now - _actionAt;
            if (action == FigureAction.Draw) draw = Mathf.Clamp01(time / FullDrawSeconds);
            return action;
        }

        /// <summary>The figure's action from gameplay data, highest priority first. <paramref name="since"/> is the
        /// action's own start time when gameplay knows it, else -1 (the view times it from when it first saw it).</summary>
        FigureAction Resolve(float now, out float since)
        {
            since = -1f;
            if (_death != null && _death.IsDead) return FigureAction.Dead;

            var rolling = _movement != null && _movement.IsRolling;
            if (rolling && !_wasRolling) _rollAt = now;
            _wasRolling = rolling;
            if (rolling)
            {
                since = now - _rollAt;
                return FigureAction.Roll;
            }

            if (_player.Blocking) return FigureAction.Block;
            if (_player.Fight != null) return FigureAction.Reel;
            if (_player.Cast != null) return _player.Cast.State == CastState.Bite ? FigureAction.Bite : FigureAction.Cast;
            if (_player.DrawStartedAt >= 0f)
            {
                since = now - _player.DrawStartedAt;
                return FigureAction.Draw;
            }
            if (now - _swingAt < SwingSeconds)
            {
                since = now - _swingAt;
                return FigureAction.Swing;
            }
            if (_player.Gathering != null) return FigureAction.Gather;
            return FigureAction.None;
        }

        float FacingTarget(FigureAction action)
        {
            Vector2 shown = _shown;
            switch (action)
            {
                case FigureAction.Gather when _player.Gathering != null:
                    return Mathf.Sign(_player.Gathering.Position.x - shown.x);
                case FigureAction.Cast or FigureAction.Bite or FigureAction.Reel:
                    return Mathf.Sign(_player.CastPoint.x - shown.x);
                case FigureAction.Block:
                    return Mathf.Abs(_player.BlockDirection.x) > 0.05f ? Mathf.Sign(_player.BlockDirection.x) : 0f;
                case FigureAction.Swing or FigureAction.Draw when _player.IsOwner && AimPoint() is { } aim:
                    return Mathf.Sign(aim.x - shown.x);
            }
            return Mathf.Abs(_velocity.x) > 0.15f ? Mathf.Sign(_velocity.x) : 0f;
        }

        static Vector2? AimPoint()
        {
            var camera = Camera.main;
            var mouse = Mouse.current;
            if (camera == null || mouse == null) return null;
            var p = mouse.position.ReadValue();
            return camera.ScreenToWorldPoint(new Vector3(p.x, p.y, -camera.transform.position.z));
        }

        FigureOutfit Outfit()
        {
            var slots = _inventory != null ? _inventory.Slots : null;
            if (slots == null) return default;
            return new FigureOutfit
            {
                Head = slots.Get("head"),
                Chest = slots.Get("chest"),
                Legs = slots.Get("legs"),
                Feet = slots.Get("feet"),
                Back = slots.Get("back"),
                Belt = slots.Get("belt"),
                MainHand = slots.Get("main_hand"),
                OffHand = slots.Get("off_hand"),
            };
        }
    }
}
