using System.Linq;
using Isle.World.Objects;
using UnityEngine;

namespace Isle.Gameplay.World
{
    /// <summary>A campfire (SYS-SURV-01 §Fire). It burns fuel — in-game minutes put in from items with
    /// <c>fuel_minutes</c> (wood, charcoal, coal) — and goes out when that runs dry. E at an unlit fire feeds it one fuel
    /// from the bag and lights it; E at a lit one opens the cook window, which shows the fuel left and adds more.</summary>
    [RequireComponent(typeof(WorldObjectInstance))]
    public sealed class CampfireInteraction : MonoBehaviour, IInteractable
    {
        /// <summary>The most a fire holds: a night's burning.</summary>
        public const float MaxFuelMinutes = 480f;

        WorldObjectInstance _instance;
        SpriteRenderer _renderer;
        long _lastMinutes = -1;

        /// <summary>In-game minutes of burning left. Server state.</summary>
        public float Fuel { get; set; }

        /// <summary>Unlit fires are drawn dim; lit ones also cast light (LightRig), so the two read apart.</summary>
        static readonly Color UnlitTint = new(0.45f, 0.45f, 0.45f);

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
            if (_renderer != null) _renderer.color = _instance.IsActive ? Color.white : UnlitTint;
            var clock = Isle.World.Time.WorldTime.Instance != null ? Isle.World.Time.WorldTime.Instance.Clock : null;
            if (clock == null) return;
            var now = clock.TotalMinutes;
            if (_lastMinutes >= 0 && _instance.IsActive)
            {
                Fuel -= Mathf.Max(0, now - _lastMinutes);
                if (Fuel <= 0f)
                {
                    Fuel = 0f;
                    _instance.IsActive = false; // burned out
                }
            }
            _lastMinutes = now;
        }

        /// <summary>Puts one fuel item from <paramref name="user"/>'s bags on the fire — the one burning longest — up to
        /// <see cref="MaxFuelMinutes"/>. False when they carry none or it's full.</summary>
        public bool AddFuel(GameObject user)
        {
            if (Fuel >= MaxFuelMinutes || user == null || !user.TryGetComponent<Inventory.InventoryNetwork>(out var inventory)) return false;
            var fuel = inventory.Containers().SelectMany(c => c.Placements).Select(p => p.Item).Where(i => i.FuelMinutes > 0f)
                .OrderByDescending(i => i.FuelMinutes).FirstOrDefault();
            if (fuel == null || Inventory.InventoryOps.TakeOne(inventory.Containers(), fuel.Id) == null) return false;
            Fuel = Mathf.Min(MaxFuelMinutes, Fuel + fuel.FuelMinutes);
            return true;
        }

        /// <summary>E lights an unlit fire; at a lit one it opens the cook window (T-151 playtest) — the cook window puts it out.</summary>
        public void Interact(GameObject user)
        {
            if (_instance.IsActive)
            {
                Feedback.GameFeed.RaiseStationOpened(Building.StationInteraction.Cook);
                return;
            }
            if (Fuel <= 0f && !AddFuel(user))
            {
                Feedback.GameFeed.RaiseNotice("@ui.fire_needs_fuel");
                return;
            }
            _instance.IsActive = true;
        }
    }
}
