using Isle.World.Objects;
using UnityEngine;

namespace Isle.Gameplay.World
{
    /// <summary>First real <see cref="IInteractable"/> — toggles the campfire's
    /// <see cref="WorldObjectInstance.IsActive"/> lit state. Nothing gates re-lighting (no fuel
    /// item exists yet) — a plain on/off flip is the whole interaction until one does
    /// (PROJECT_STATE.md §Decided without a spec).</summary>
    [RequireComponent(typeof(WorldObjectInstance))]
    public sealed class CampfireInteraction : MonoBehaviour, IInteractable
    {
        WorldObjectInstance _instance;
        SpriteRenderer _renderer;

        void Awake()
        {
            _instance = GetComponent<WorldObjectInstance>();
            _renderer = GetComponent<SpriteRenderer>();
        }

        // Debug-only visual for manual testing (PROJECT_STATE.md §Decided without a spec) —
        // IsActive has no client sync yet (WorldObjectInstance's own doc comment), so this only
        // reads correctly where server and client are the same process (host mode).
        void Update()
        {
            if (_renderer != null) _renderer.color = _instance.IsActive ? Color.red : Color.gray;
        }

        public void Interact() => _instance.IsActive = !_instance.IsActive;
    }
}
