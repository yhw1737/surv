using System.Linq;
using Isle.World.Objects;
using UnityEngine;

namespace Isle.Gameplay.World
{
    /// <summary>A campfire (SYS-SURV-01 §Fire). It holds fuel in a small grid — wood, coal, anything with
    /// <c>fuel_minutes</c> — and, lit, burns it one piece at a time; with nothing left it goes out. E opens it (the fuel grid
    /// beside your bags, a light / put-out button); the moves are <c>PlayerInteraction.Request*Fuel</c>, server-side.</summary>
    [RequireComponent(typeof(WorldObjectInstance))]
    public sealed class CampfireInteraction : MonoBehaviour, IInteractable
    {
        public const int GridWidth = 3;
        public const int GridHeight = 2;

        WorldObjectInstance _instance;
        SpriteRenderer _renderer;
        long _lastMinutes = -1;

        /// <summary>Unlit fires are drawn dim; lit ones also cast light (LightRig), so the two read apart.</summary>
        static readonly Color UnlitTint = new(0.45f, 0.45f, 0.45f);

        /// <summary>The fuel waiting to burn. Server state.</summary>
        public Inventory.GridInventory Grid { get; } = new(GridWidth, GridHeight);

        /// <summary>In-game minutes left of the piece burning now.</summary>
        public float Fuel { get; set; }

        public bool Lit => _instance != null && _instance.IsActive;

        /// <summary>Everything left to burn: the current piece and the grid.</summary>
        public float TotalFuelMinutes => Fuel + Grid.Placements.Sum(p => p.Item.FuelMinutes * p.Count);

        void Awake()
        {
            _instance = GetComponent<WorldObjectInstance>();
            _renderer = GetComponent<SpriteRenderer>();
        }

        // IsActive has no client sync yet (WorldObjectInstance's own doc comment), so this only reads correctly where
        // server and client are the same process (host mode).
        void Update()
        {
            if (_renderer != null) _renderer.color = _instance.IsActive ? Color.white : UnlitTint;
            var clock = Isle.World.Time.WorldTime.Instance != null ? Isle.World.Time.WorldTime.Instance.Clock : null;
            if (clock == null) return;
            var now = clock.TotalMinutes;
            if (_lastMinutes >= 0 && _instance.IsActive)
            {
                Fuel -= Mathf.Max(0, now - _lastMinutes);
                while (Fuel <= 0f)
                {
                    if (BurnNext()) continue;
                    Fuel = 0f;
                    _instance.IsActive = false; // burned out
                    break;
                }
            }
            _lastMinutes = now;
        }

        /// <summary>Puts the next piece from the grid on the fire. False when the grid holds no fuel.</summary>
        bool BurnNext()
        {
            foreach (var p in Grid.Placements)
            {
                if (p.Item.FuelMinutes <= 0f) continue;
                Grid.Remove(p);
                if (p.Count > 1) Grid.TryPlace(p.Item, p.Position, p.Rotated, p.Count - 1, p.Wear);
                Fuel += p.Item.FuelMinutes;
                return true;
            }
            return false;
        }

        /// <summary>Lights it if there's anything to burn. False when there isn't.</summary>
        public bool TryLight()
        {
            if (_instance.IsActive) return true;
            if (Fuel <= 0f && !BurnNext()) return false;
            _instance.IsActive = true;
            return true;
        }

        public void PutOut() => _instance.IsActive = false;

        /// <summary>E: open the fire (its fuel grid beside your bags).</summary>
        public void Interact(GameObject user) => Feedback.GameFeed.RaiseCampfireOpened(this);
    }
}
