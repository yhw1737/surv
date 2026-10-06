using FishNet.Object;
using Isle.World.Objects;
using Isle.World.Time;
using Isle.World.Weather;
using Isle.Gameplay.Inventory;
using Isle.Networking;
using System.Collections.Generic;
using Isle.Core.Ids;
using Isle.Data;
using Isle.Gameplay.Buffs;
using Isle.Gameplay.Cooking;
using Isle.Modding.Defs;
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

        /// <summary>0–1, §Temperature's "Hypothermia severity" (2026-09-19) — grows while
        /// <see cref="Temperature"/> is below <see cref="VitalsCalculator.HypothermiaHpTemp"/>, decays
        /// back towards 0 once it isn't. Drives <see cref="VitalsCalculator.TemperatureHpDrainPerSecond"/>'s
        /// hypothermia component; heatstroke has no equivalent (see spec, no benchmarked curve exists).</summary>
        public float HypothermiaSeverity { get; private set; }

        public float MinutesSinceSaltyFood { get; set; } = float.MaxValue;

        // SCHEMA.md §World objects — Absolute Rule 4 (connect via tags), not a hardcoded def id.
        const string CampfireTag = "station/campfire";

        float _accumulatedSeconds;
        float _secondsSinceLastStaminaSpend = float.MaxValue;

        /// <summary>SYS-SURV-01 §Stamina table: sprint 12/s, dodge roll 25.</summary>
        public const float SprintStaminaPerSecond = 12f;
        public const float RollStaminaCost = 25f;

        /// <summary>Running dry locks sprinting until stamina is back to this much. Without it a held Shift spends every
        /// point the moment it regenerates, so stamina never climbs and the 25-point roll never comes back.
        /// [invented] — SYS-SURV-01 only says "actions blocked" at 0.</summary>
        public const float ExhaustionRecoverStamina = 30f;

        /// <summary>True from the moment stamina hits 0 until it recovers to <see cref="ExhaustionRecoverStamina"/>.</summary>
        public bool Exhausted { get; private set; }

        /// <summary>Carrying more than <see cref="WeightCalculator.MaxWeightKg"/>: no roll, no stamina regen (SYS-INV-01).</summary>
        public bool Overloaded { get; private set; }

        PlayerMovement _movement;

        void Awake()
        {
            if (TryGetComponent(out _movement)) _movement.RollStarted += OnRollStarted;
        }

        void OnDestroy()
        {
            if (_movement != null) _movement.RollStarted -= OnRollStarted;
        }

        void OnRollStarted()
        {
            if (IsServer) SpendStamina(RollStaminaCost);
        }

        void Update()
        {
            if (!IsServer) return;
            UpdateMovementRules();

            _accumulatedSeconds += Time.deltaTime;
            while (_accumulatedSeconds >= TickIntervalSeconds)
            {
                _accumulatedSeconds -= TickIntervalSeconds;
                Tick();
            }
        }

        /// <summary>Pushes the rules the movement layer can't see: carried weight (SYS-INV-01 §Weight) and whether
        /// there's stamina to sprint or roll; pays for sprinting and sets the activity hunger/thirst drain reads.</summary>
        void UpdateMovementRules()
        {
            if (_movement == null) return;

            ClothingBonus = WornWarmth();
            var weight = CarriedWeightKg();
            _movement.WeightMultiplier = WeightCalculator.SpeedMultiplier(weight);
            Overloaded = WeightCalculator.IsOverloaded(weight);
            if (Stamina <= 0f) Exhausted = true;
            else if (Exhausted && Stamina >= ExhaustionRecoverStamina) Exhausted = false;
            _movement.SprintAllowed = !Exhausted && Stamina > 0f;
            _movement.RollAllowed = Stamina >= RollStaminaCost && !Overloaded;

            if (_movement.IsSprinting) SpendStamina(SprintStaminaPerSecond * Time.deltaTime);
            CurrentActivity = _movement.IsSprinting ? Activity.Sprinting : _movement.IsMoving ? Activity.Walking : Activity.Idle;
        }

        /// <summary>Sum of every equipped item's <c>warmth</c> — SYS-SURV-01's <c>clothingBonus</c>.</summary>
        float WornWarmth()
        {
            if (!TryGetComponent<InventoryNetwork>(out var inventory)) return 0f;
            var total = 0f;
            foreach (var slot in EquipSlots.All) total += inventory.Slots.Get(slot)?.Warmth ?? 0f;
            return total;
        }

        /// <summary>SYS-COMBAT-01 §Damage <c>totalArmor</c>: every worn item's <c>armor</c>.</summary>
        public float WornArmor()
        {
            if (!TryGetComponent<InventoryNetwork>(out var inventory)) return 0f;
            var total = 0f;
            foreach (var slot in EquipSlots.All) total += inventory.Slots.Get(slot)?.Armor ?? 0f;
            return total;
        }

        float CarriedWeightKg()
        {
            if (!TryGetComponent<InventoryNetwork>(out var inventory)) return 0f;
            var total = inventory.Bag.TotalWeightKg();
            foreach (var slot in EquipSlots.All)
            {
                total += inventory.Slots.Get(slot)?.Weight ?? 0f;
                total += inventory.Slots.BagFor(slot)?.TotalWeightKg() ?? 0f;
            }
            return total;
        }

        /// <summary>Active buffs (SYS-BUFF-01), keyed by def, with expiry on the world clock.</summary>
        public BuffSet Buffs { get; } = new();

        /// <summary>SYS-COOK-01 §Satiety fatigue, per player.</summary>
        public SatietyTracker Satiety { get; } = new();

        public static long NowMinutes => WorldTime.Instance != null ? WorldTime.Instance.Clock.TotalMinutes : 0;

        /// <summary>Defs of every buff active now — what each formula below reads its multipliers from.</summary>
        public List<BuffDef> ActiveBuffs()
        {
            var active = new List<BuffDef>();
            foreach (var id in Buffs.ActiveBuffIds(NowMinutes))
                if (DefRegistry.TryGet<BuffDef>(id, out var buff)) active.Add(buff);
            return active;
        }

        /// <summary>Server-side: grants a buff, its duration stretched by a dish's buff_duration / care tag.</summary>
        public void GrantBuff(NamespacedId buffId, float durationMult)
        {
            if (!IsServer || !DefRegistry.TryGet<BuffDef>(buffId, out var buff)) return;
            Buffs.Grant(new BuffDef { Id = buff.Id, Name = buff.Name, Effects = buff.Effects, DurationMin = (long)(buff.DurationMin * durationMult) }, NowMinutes);
        }

        void Tick()
        {
            var buffs = ActiveBuffs();
            var heatstroke = VitalsCalculator.IsHeatstroke(Temperature);
            var hungerDelta = VitalsCalculator.HungerDrainPerHour(CurrentActivity, Temperature) / 60f;
            var thirstDelta = VitalsCalculator.ThirstDrainPerHour(CurrentActivity, AmbientTemp, MinutesSinceSaltyFood) / 60f
                * (heatstroke ? 2f : 1f) * BuffEffects.Mult(buffs, "thirst_drain_mult");
            if (TryGetComponent<InventoryNetwork>(out var carried))
                foreach (var container in carried.Containers()) SpoilageTracker.Live.Tick(container, inGameMinutes: 1f);

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
            // SYS-BUFF-01 warm: the whole approach rate is scaled, so a minute moves Temperature less in either direction.
            Temperature = VitalsCalculator.ApproachTemperature(Temperature, target, inGameMinutes: BuffEffects.Mult(buffs, "temp_approach_rate_mult"));

            // SYS-BUFF-01 cold_resist shifts the hypothermia line down; feeding a correspondingly warmer temperature
            // into the unchanged formula is the same thing.
            var hypothermiaShift = BuffEffects.Sum(buffs, "hypothermia_threshold_shift");
            var severityDelta = VitalsCalculator.HypothermiaSeverityDeltaPerSecond(Temperature - hypothermiaShift, HypothermiaSeverity) * TickIntervalSeconds;
            HypothermiaSeverity = Mathf.Clamp01(HypothermiaSeverity + severityDelta);

            var hpDelta = (VitalsCalculator.ZeroGaugeHpDrainPerSecond(Hunger, Thirst)
                + VitalsCalculator.TemperatureHpDrainPerSecond(Temperature - hypothermiaShift, HypothermiaSeverity)
                - BuffEffects.Sum(buffs, "hp_drain_per_sec")) * TickIntervalSeconds;
            Health = Mathf.Clamp(Health + hpDelta, 0f, VitalsCalculator.GaugeMax);
            // A lower stamina ceiling (food poisoning) applies at once, not only when regenerating.
            Stamina = Mathf.Min(Stamina, VitalsCalculator.GaugeMax * BuffEffects.Mult(buffs, "stamina_max_mult"));

            _secondsSinceLastStaminaSpend += TickIntervalSeconds;
            if (_secondsSinceLastStaminaSpend >= VitalsCalculator.StaminaRegenDelaySeconds)
            {
                // SYS-INV-01 §Weight: "stamina regen halted" when overloaded.
                var overweightFactor = WeightCalculator.IsOverloaded(CarriedWeightKg()) ? 0f : 1f;
                var regen = VitalsCalculator.StaminaRegenPerSecondAt(overweightFactor, Hunger, Thirst) * TickIntervalSeconds
                    * BuffEffects.Mult(buffs, "stamina_regen_mult");
                var maxStamina = VitalsCalculator.GaugeMax * VitalsCalculator.MaxStaminaFactor(Hunger, Thirst)
                    * BuffEffects.Mult(buffs, "stamina_max_mult");
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
            Thirst = VitalsCalculator.Replenish(Thirst, VitalsCalculator.DrinkThirstDelta(source));
        }

        /// <summary>Server-side eating. Hunger and thirst deltas come from the item's nutrition block
        /// (SCHEMA §Items), applied as-is — no per-item numbers live in C#.</summary>
        public void Eat(float hungerDelta, float thirstDelta)
        {
            if (!IsServer) return;
            Hunger = VitalsCalculator.Replenish(Hunger, hungerDelta);
            Thirst = VitalsCalculator.Replenish(Thirst, thirstDelta);
        }

        /// <summary>Server-side rest at a campfire: stamina comes back in full. The time skip itself is
        /// <c>WorldClock.AdvanceToNextMinuteOfDay</c>; hunger and thirst are not ticked during it (prototype shortcut).</summary>
        public void Rest()
        {
            if (!IsServer) return;
            Stamina = VitalsCalculator.GaugeMax;
        }

        /// <summary>Server-side: loading a save puts every gauge back as recorded.</summary>
        public void Restore(float health, float hunger, float thirst, float stamina, float temperature, float hypothermiaSeverity)
        {
            if (!IsServer) return;
            Health = health;
            Hunger = hunger;
            Thirst = thirst;
            Stamina = stamina;
            Temperature = temperature;
            HypothermiaSeverity = hypothermiaSeverity;
        }

        /// <summary>Server-side damage from a creature or any other source. Health 0 is handled by
        /// <see cref="Tick"/>, which calls <see cref="DeathHandler.Die"/>.</summary>
        public void TakeDamage(float amount)
        {
            if (!IsServer || amount <= 0f) return;
            if (_movement != null && _movement.IsInvulnerable) return; // dodge roll i-frames
            var taken = Combat.DamageResolver.Damage(amount, WornArmor());
            Health = Mathf.Clamp(Health - taken, 0f, VitalsCalculator.GaugeMax);
            Feedback.GameFeed.RaisePlayerHit(taken);
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
            HypothermiaSeverity = 0f;
            foreach (var id in new List<NamespacedId>(Buffs.ActiveBuffIds(NowMinutes))) Buffs.Clear(id);
        }
    }
}
