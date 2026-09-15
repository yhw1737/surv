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
    /// This is the netcode skeleton, not the final character controller: SYS-CHAR-01's
    /// <c>weightMult</c>/<c>terrainMult</c>/<c>stanceMult</c> need systems that don't exist yet
    /// (inventory weight is Phase 4, terrain is Phase 3) and that whole spec is itself marked
    /// "needs a rewrite before it is used" pending the Phase 11 rig work. Only
    /// <c>BaseSpeed</c> is used here — see PROJECT_STATE.md §Decided without a spec.
    /// </para>
    /// <para>
    /// No collider/<c>Rigidbody2D</c> either: there is nothing to collide with before T-031's
    /// tilemap. Moving <see cref="Transform"/> directly is the simplest thing that proves the
    /// replicate/reconcile loop; a physics body can replace it once collision exists.
    /// </para>
    /// </summary>
    public sealed class PlayerMovement : TickNetworkBehaviour
    {
        // SYS-CHAR-01 §Movement: BaseSpeed = 4.2 tiles/s. GLOSSARY §Units: 1 Unity unit = 1 tile.
        public const float BaseSpeed = 4.2f;

        struct MoveData : IReplicateData
        {
            public Vector2 Input;
            uint _tick;

            public MoveData(Vector2 input)
            {
                Input = input;
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
            return new MoveData(new Vector2(h, v));
        }

        public override void CreateReconcile()
        {
            PerformReconcile(new ReconcileData(transform.position));
        }

        [Replicate]
        void PerformReplicate(MoveData md, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
        {
            // Never trust magnitude > 1 (diagonal input must not move faster than cardinal — SYS-NET-01
            // "never trust client-supplied values" applies here even though this runs identically on
            // client and server, since the server is what actually resolves the final position).
            var input = md.Input.sqrMagnitude > 1f ? md.Input.normalized : md.Input;
            float delta = (float)TimeManager.TickDelta;
            transform.position += (Vector3)(input * (BaseSpeed * delta));
        }

        [Reconcile]
        void PerformReconcile(ReconcileData rd, Channel channel = Channel.Unreliable)
        {
            transform.position = rd.Position;
        }
    }
}
