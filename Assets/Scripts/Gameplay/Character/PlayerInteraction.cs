using FishNet.Object;
using Isle.World.Objects;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Isle.Gameplay.Character
{
    /// <summary>
    /// World-object-interaction work (PROJECT_STATE.md §Decided without a spec) — no SYS-* spec
    /// covers this yet. Owner reads the interact key and finds the nearest in-reach
    /// <see cref="IInteractable"/>; the server re-validates reach before calling
    /// <see cref="IInteractable.Interact"/> (Absolute Rule 2 — never trust a client-supplied target
    /// blindly, same reasoning as <c>InventoryNetwork</c>'s server-side reach check).
    /// </summary>
    public sealed class PlayerInteraction : NetworkBehaviour
    {
        /// <summary>Tiles (GLOSSARY §Units). Picked, not specced — separate from SYS-SURV-01's
        /// 5-tile fire bonus radius, which is ambient warmth, not an interact reach.</summary>
        public const float ReachTiles = 2f;

        void Update()
        {
            if (!IsOwner) return;

            var kb = Keyboard.current;
            if (kb == null || !kb.eKey.wasPressedThisFrame) return;

            var target = WorldObjectRegistry.NearestInteractable(transform.position, ReachTiles);
            if (target != null) CmdInteract(target);
        }

        [ServerRpc]
        void CmdInteract(WorldObjectInstance target)
        {
            if (target == null) return;
            if (Vector2.Distance(transform.position, target.transform.position) > ReachTiles) return;
            if (target.TryGetComponent<IInteractable>(out var interactable)) interactable.Interact();
        }
    }
}
