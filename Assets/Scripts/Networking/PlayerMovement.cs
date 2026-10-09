using System;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using FishNet.Utility.Template;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Isle.Networking
{
    /// <summary>
    /// T-021: server-authoritative position with client-side prediction (SYS-NET-01 §Authority —
    /// Position row, "server-validated" + prediction, 20 Hz ticks). Uses FishNet's Prediction v2
    /// template (<see cref="TickNetworkBehaviour"/>, <c>[Replicate]</c>/<c>[Reconcile]</c>) the
    /// same way the package's own CharacterController demo does.
    /// <para>
    /// SYS-CHAR-01 §Movement: <c>BaseSpeed × weightMult × stanceMult</c> (<see cref="MovementCalculator"/>),
    /// sprint on Shift, dodge roll on Space. <c>terrainMult</c> and crouch aren't built. Gameplay owns the
    /// rules this layer can't see (stamina, carried weight) and pushes them in through
    /// <see cref="WeightMultiplier"/>, <see cref="SprintAllowed"/> and <see cref="RollAllowed"/>; it pays the
    /// stamina when <see cref="IsSprinting"/> is set or <see cref="RollStarted"/> fires.
    /// </para>
    /// <para>
    /// No collider/<c>Rigidbody2D</c>: water and standing nodes (tree trunks, rocks) are the walls, checked through
    /// <see cref="IsWalkable"/>.
    /// </para>
    /// </summary>
    public sealed class PlayerMovement : TickNetworkBehaviour
    {
        // SYS-CHAR-01 §Movement: BaseSpeed = 4.2 tiles/s. GLOSSARY §Units: 1 Unity unit = 1 tile.
        public const float BaseSpeed = MovementCalculator.BaseSpeed;

        struct MoveData : IReplicateData
        {
            public Vector2 Input;
            public bool Sprint;
            public bool Roll;
            uint _tick;

            public MoveData(Vector2 input, bool sprint, bool roll)
            {
                Input = input;
                Sprint = sprint;
                Roll = roll;
                _tick = 0;
            }

            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }

        struct ReconcileData : IReconcileData
        {
            public Vector3 Position;
            uint _tick;

            public ReconcileData(Vector3 position)
            {
                Position = position;
                _tick = 0;
            }

            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }

        /// <summary>Set by the world layer (Networking can't reference it): true where a player may stand.
        /// Null means everywhere. Runs identically on client and server, so prediction stays in step as long as
        /// both build the same island (same seed — true for a listen-server host).</summary>
        public static Func<Vector2, bool> IsWalkable { get; set; }

        /// <summary>Set by the world layer: a speed multiplier for the ground here (swimming in a flooded dungeon
        /// room is ×0.6, SYS-DUNG-01). Null means 1 everywhere.</summary>
        public static Func<Vector2, float> TerrainSpeed { get; set; }

        // ponytail: the four flags below are pushed by gameplay on the server and not synced — on a
        // listen-server host the owning client shares this instance. Phase 10 needs SyncVars.

        /// <summary>True while the player can't move (dead, waiting to respawn).</summary>
        public bool Frozen { get; set; }

        /// <summary>SYS-INV-01 §Weight speedMult. 1 = unburdened.</summary>
        public float WeightMultiplier { get; set; } = 1f;

        /// <summary>False when out of stamina.</summary>
        public bool SprintAllowed { get; set; } = true;

        /// <summary>False when stamina is short or the player is overloaded (SYS-CHAR-01: "disabled when overweight").</summary>
        public bool RollAllowed { get; set; } = true;

        /// <summary>True for the last tick the player actually sprinted (moving with Shift held).</summary>
        public bool IsSprinting { get; private set; }

        /// <summary>True for the last tick the player moved under their own input (walk or sprint).</summary>
        public bool IsMoving { get; private set; }

        public bool IsRolling => _rollElapsed >= 0f;

        /// <summary>Inside the roll's i-frame window (SYS-CHAR-01: 0.1–0.45 s).</summary>
        public bool IsInvulnerable => IsRolling && MovementCalculator.IsRollInvulnerable(_rollElapsed);

        /// <summary>Raised once per roll, on the tick it starts, so gameplay can charge its stamina.</summary>
        public event Action RollStarted;

        float _rollElapsed = -1f;
        Vector2 _rollDirection;
        Vector2 _lastDirection = Vector2.down;

        /// <summary>The last direction moved in (unit length) — what a dragged load trails behind.</summary>
        public Vector2 LastDirection => _lastDirection;

        static bool CanStand(Vector2 position) => IsWalkable == null || IsWalkable(position);

        void Awake()
        {
            SetTickCallbacks(TickCallback.Tick | TickCallback.PostTick);
        }

        protected override void TimeManager_OnTick()
        {
            PerformReplicate(BuildMoveData());
        }

        protected override void TimeManager_OnPostTick()
        {
            CreateReconcile();
        }

        MoveData BuildMoveData()
        {
            // Only the owner reads input; the server (and other clients' copies) replay whatever
            // was sent, same split the FishNet demo uses.
            if (!IsOwner) return default;

            var kb = Keyboard.current;
            if (kb == null) return default;

            float h = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
            float v = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
            return new MoveData(new Vector2(h, v), kb.leftShiftKey.isPressed, kb.spaceKey.isPressed);
        }

        public override void CreateReconcile()
        {
            PerformReconcile(new ReconcileData(transform.position));
        }

        [Replicate]
        void PerformReplicate(MoveData md, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
        {
            float delta = (float)TimeManager.TickDelta;
            IsSprinting = false;
            IsMoving = false;
            if (Frozen)
            {
                _rollElapsed = -1f;
                return;
            }

            // Never trust magnitude > 1 (diagonal input must not move faster than cardinal — SYS-NET-01
            // "never trust client-supplied values" applies here even though this runs identically on
            // client and server, since the server is what actually resolves the final position).
            var input = md.Input.sqrMagnitude > 1f ? md.Input.normalized : md.Input;
            if (input.sqrMagnitude > 0f) _lastDirection = input.normalized;

            Vector2 step;
            if (IsRolling)
            {
                // Held direction keys bend the roll's path mid-roll.
                var steered = MovementCalculator.SteerRoll(_rollDirection.x, _rollDirection.y, input.x, input.y);
                _rollDirection = new Vector2(steered.X, steered.Y);
                step = _rollDirection * (BaseSpeed * MovementCalculator.RollMult * delta);
                _rollElapsed += delta;
                if (_rollElapsed >= MovementCalculator.RollSeconds) _rollElapsed = -1f;
            }
            else if (md.Roll && RollAllowed)
            {
                // SYS-CHAR-01: "Direction = movement input" — or the last direction faced when standing still.
                _rollDirection = _lastDirection;
                _rollElapsed = 0f;
                RollStarted?.Invoke();
                step = Vector2.zero;
            }
            else
            {
                IsMoving = input.sqrMagnitude > 0f;
                IsSprinting = md.Sprint && SprintAllowed && IsMoving;
                var terrain = TerrainSpeed?.Invoke(transform.position) ?? 1f;
                step = input * (MovementCalculator.Speed(WeightMultiplier, IsSprinting) * terrain * delta);
            }

            // Each axis is checked on its own, so walking diagonally into the shore slides along it.
            var position = transform.position;
            // Somewhere you can't stand (a tree grew back on you, an old save): walk out freely.
            var free = !CanStand(position);
            if (free || CanStand(position + new Vector3(step.x, 0f))) position.x += step.x;
            if (free || CanStand(position + new Vector3(0f, step.y))) position.y += step.y;
            transform.position = position;
        }

        [Reconcile]
        void PerformReconcile(ReconcileData rd, Channel channel = Channel.Unreliable)
        {
            transform.position = rd.Position;
        }
    }
}
