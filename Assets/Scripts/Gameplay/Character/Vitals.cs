using FishNet.Object;
using Isle.World.Objects;
using Isle.World.Time;
using Isle.World.Weather;
using UnityEngine;

namespace Isle.Gameplay.Character
{
    /// <summary>
    /// SYS-SURV-01, server authority (Absolute Rule 2). One instance per player
    /// <c>NetworkObject</c>, alongside <c>InventoryNetwork</c> and <see cref="DeathHandler"/>.
    /// Owns all 5 gauges and ticks them server-side once per in-game minute — never per frame, per
    /// spec's own §Location note — using <see cref="WorldClock.MinutesPerRealSecond"/> so the two
    /// systems can't drift out of step with each other.
    /// <para>
    /// <see cref="AmbientTemp"/>, <see cref="NearestCampfireDistanceTiles"/> and
    /// <see cref="WetPenalty"/> are computed each <see cref="Tick"/> from
    /// <see cref="WeatherController.Instance"/>/<see cref="WorldObjectRegistry"/> (SYS-WORLD-02).
    /// <see cref="CurrentActivity"/>, <see cref="ClothingBonus"/> and <see cref="MinutesSinceSaltyFood"/>
    /// stay plain server-set properties — nothing produces real values for them yet (movement has no
    /// run key, cooking's salty-food tag isn't wired up), so they default to "nothing is happening"
    /// (idle, no clothing, never salted) until those systems land and start setting them.
    /// </para>
    /// </summary>
    public sealed class Vitals : NetworkBehaviour
    {
        /// <summary>Comfortable-band midpoint (§Temperature: 36–38), used as the starting and
        /// post-respawn value — the gauge table's "all start at 100" line is about the four
        /// percentage gauges, not this one. See PROJECT_STATE.md §Decided without a spec.</summary>
        public const float ComfortableTemperature = 37f;

        static readonly float TickIntervalSeconds = (float)(1.0 / WorldClock.MinutesPerRealSecond);

        public float Hunger { get; private set; } = VitalsCalculator.GaugeMax;
        public float Thirst { get; private set; } = VitalsCalculator.GaugeMax;
        public float Temperature { get; private set; } = ComfortableTemperature;
        public float Stamina { get; private set; } = VitalsCalculator.GaugeMax;
        public float Health { get; private set; } = VitalsCalculator.GaugeMax;

        public Activity CurrentActivity { get; set; } = Activity.Idle;
        public float ClothingBonus { get; set; }

        /// <summary>Pulled from <see cref="WeatherController.Instance"/> each tick (SYS-WORLD-02);
        /// comfortable midpoint if no controller has spawned yet.</summary>
        public float AmbientTemp { get; private set; } = ComfortableTemperature;

        /// <summary>Tiles to the nearest lit campfire; <see cref="float.PositiveInfinity"/> means
        /// none in range. Pulled from <see cref="WorldObjectRegistry"/> each tick — drives
        /// <see cref="VitalsCalculator.FireBonusAtDistance"/>.</summary>
        public float NearestCampfireDistanceTiles { get; private set; } = float.PositiveInfinity;

        /// <summary>§Temperature's wetPenalty magnitude (0 = dry). Pinned at
        /// <see cref="VitalsCalculator.WetPenaltyMagnitude"/> while <see cref="WeatherController.IsPrecipitating"/>
        /// is true (rain or snow — no separate "cold but dry" mechanic exists, Absolute Rule 6);
        /// otherwise dries back towards 0 via <see cref="VitalsCalculator.DecayWetPenalty"/> — same
        /// value <see cref="SetWet"/> sets manually for a future swimming event.</summary>
        public float WetPenalty { get; private set; }

        public float MinutesSinceSaltyFood { get; set; } = float.MaxValue;

        // SCHEMA.md §World objects — Absolute Rule 4 (connect via tags), not a hardcoded def id.
        const string CampfireTag = "station/campfire";

        float _accumulatedSeconds;
        float _secondsSinceLastStaminaSpend = float.MaxValue;

        void Update()
        {
            if (!IsServer) return;

            _accumulatedSeconds += Time.deltaTime;
            while (_accumulatedSeconds >= TickIntervalSeconds)
            {
                _accumulatedSeconds -= TickIntervalSeconds;
                Tick();
            }
        }

        void Tick()
        {
            var heatstroke = VitalsCalculator.IsHeatstroke(Temperature);
            var hungerDelta = VitalsCalculator.HungerDrainPerHour(CurrentActivity, Temperature) / 60f;
            var thirstDelta = VitalsCalculator.ThirstDrainPerHour(CurrentActivity, AmbientTemp, MinutesSinceSaltyFood) / 60f
                * (heatstroke ? 2f : 1f);
            Hunger = Mathf.Clamp(Hunger - hungerDelta, 0f, VitalsCalculator.GaugeMax);
            Thirst = Mathf.Clamp(Thirst - thirstDelta, 0f, VitalsCalculator.GaugeMax);

            var weather = WeatherController.Instance;
            AmbientTemp = weather != null ? weather.AmbientTemp : ComfortableTemperature;
            NearestCampfireDistanceTiles = WorldObjectRegistry.NearestDistanceTiles(transform.position, CampfireTag);
            WetPenalty = weather != null && weather.IsPrecipitating
                ? VitalsCalculator.WetPenaltyMagnitude
                : VitalsCalculator.DecayWetPenalty(WetPenalty, inGameMinutes: 1f);

            var fireBonus = VitalsCalculator.FireBonusAtDistance(NearestCampfireDistanceTiles);
            var target = VitalsCalculator.TargetTemperature(AmbientTemp, ClothingBonus, fireBonus, WetPenalty);
            Temperature = VitalsCalculator.ApproachTemperature(Temperature, target, inGameMinutes: 1f);

            var hpDelta = (VitalsCalculator.ZeroGaugeHpDrainPerSecond(Hunger, Thirst)
                + VitalsCalculator.TemperatureHpDrainPerSecond(Temperature)) * TickIntervalSeconds;
            Health = Mathf.Clamp(Health + hpDelta, 0f, VitalsCalculator.GaugeMax);

            _secondsSinceLastStaminaSpend += TickIntervalSeconds;
            if (_secondsSinceLastStaminaSpend >= VitalsCalculator.StaminaRegenDelaySeconds)
            {
                // ponytail: overweight factor hardcoded to 1 (no penalty) — SYS-INV-01's weight
                // limit has no consumer yet (InventoryNetwork remarks); wire it in once it does.
                var regen = VitalsCalculator.StaminaRegenPerSecondAt(overweightFactor: 1f, Hunger, Thirst) * TickIntervalSeconds;
                var maxStamina = VitalsCalculator.GaugeMax * VitalsCalculator.MaxStaminaFactor(Hunger, Thirst);
                Stamina = Mathf.Clamp(Stamina + regen, 0f, maxStamina);
            }

            if (Health <= 0f && TryGetComponent<DeathHandler>(out var death)) death.Die();
        }

        /// <summary>Server-side stamina spend for whatever action system calls it (none do yet —
        /// combat/movement sprint aren't built). Resets the regen delay, same as any action would.</summary>
        public void SpendStamina(float amount)
        {
            if (!IsServer) return;
            Stamina = Mathf.Max(0f, Stamina - amount);
            _secondsSinceLastStaminaSpend = 0f;
        }

        /// <summary>Server-side drink for T-052's water-source interaction to call once it exists.</summary>
        public void Drink(WaterSource source)
        {
            if (!IsServer) return;
            Thirst = Mathf.Clamp(Thirst + VitalsCalculator.DrinkThirstDelta(source), 0f, VitalsCalculator.GaugeMax);
        }

        /// <summary>Server-side "got wet" event for a future swimming system to call (§Temperature:
        /// "wetPenalty −6 from rain or swimming") — rain itself is handled automatically each
        /// <see cref="Tick"/> via <see cref="WeatherController.IsRaining"/> now (SYS-WORLD-02).
        /// Re-wetting while already wet just resets the magnitude rather than stacking — the spec
        /// gives one fixed penalty, not a cumulative one.</summary>
        public void SetWet()
        {
            if (!IsServer) return;
            WetPenalty = VitalsCalculator.WetPenaltyMagnitude;
        }

        /// <summary>Called by <see cref="DeathHandler"/> once respawn completes. Full reset, not
        /// specced explicitly — see PROJECT_STATE.md §Decided without a spec.</summary>
        public void ResetOnRespawn()
        {
            Hunger = VitalsCalculator.GaugeMax;
            Thirst = VitalsCalculator.GaugeMax;
            Temperature = ComfortableTemperature;
            Stamina = VitalsCalculator.GaugeMax;
            Health = VitalsCalculator.GaugeMax;
            WetPenalty = 0f;
        }
    }
}
