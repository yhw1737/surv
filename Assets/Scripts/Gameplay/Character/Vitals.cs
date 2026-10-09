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
            if (Status.Any)
            {
                var dot = Status.Tick(Time.time, Time.deltaTime);
                if (dot > 0f)
                {
                    Health = Mathf.Clamp(Health - dot, 0f, VitalsCalculator.GaugeMax);
                    if (Health <= 0f && TryGetComponent<DeathHandler>(out var dying)) dying.Die();
                }
            }

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
            // carry_capacity_mult: a strong back carries more before it slows; move_speed_mult: on top of that.
            var buffs = ActiveBuffs();
            var weight = CarriedWeightKg() / Mathf.Max(0.1f, BuffEffects.Mult(buffs, "carry_capacity_mult"));
            var hauling = TryGetComponent<PlayerInteraction>(out var hauler) ? hauler.HaulSpeed : 1f;
            _movement.WeightMultiplier = WeightCalculator.SpeedMultiplier(weight) * BuffEffects.Mult(buffs, "move_speed_mult") * hauling;
            Overloaded = WeightCalculator.IsOverloaded(weight);
            if (Stamina <= 0f) Exhausted = true;
            else if (Exhausted && Stamina >= ExhaustionRecoverStamina) Exhausted = false;
            _movement.SprintAllowed = !Exhausted && Stamina > 0f;
            _movement.RollAllowed = Stamina >= RollStaminaCost && !Overloaded && hauling >= 1f; // no rolling with a carcass

            if (_movement.IsSprinting) SpendStamina(SprintStaminaPerSecond * Time.deltaTime * BuffEffects.Mult(buffs, "sprint_cost_mult"));
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
        /// <summary>The worn items' per-type armor for one type (summed), or null when none lists it.</summary>
        System.Collections.Generic.Dictionary<string, float> WornArmorTypes(string damageType)
        {
            if (!TryGetComponent<InventoryNetwork>(out var inventory)) return null;
            float? total = null;
            foreach (var slot in EquipSlots.All)
                if (inventory.Slots.Working(slot)?.ArmorTypes is { } byType && byType.TryGetValue(damageType, out var value))
                    total = (total ?? 0f) + value;
            return total.HasValue ? new System.Collections.Generic.Dictionary<string, float> { [damageType] = total.Value } : null;
        }

        /// <summary>SYS-CRAFT-02: every worn armor piece loses 1 per hit taken.</summary>
        void WearArmor()
        {
            if (!TryGetComponent<InventoryNetwork>(out var inventory)) return;
            foreach (var slot in EquipSlots.All)
            {
                var item = inventory.Slots.Get(slot);
                if (item == null || (item.Armor <= 0f && item.ArmorTypes == null)) continue;
                if (inventory.Slots.Wear(slot, 1)) Feedback.GameFeed.RaiseNotice($"@ui.item_broke|{item.Name}");
            }
        }

        /// <summary>Product of (1 − value) over active <c>damage_resist</c> buffs for this type — values come from each dish.</summary>
        float FoodResistMult(string damageType)
        {
            var mult = 1f;
            foreach (var buff in ActiveBuffs())
            foreach (var effect in buff.Effects ?? System.Array.Empty<BuffEffect>())
                if (effect.Type == "damage_resist" && effect.DamageType == damageType && effect.Value is { } v) mult *= Mathf.Clamp01(1f - v);
            return mult;
        }

        public float WornArmor()
        {
            if (!TryGetComponent<InventoryNetwork>(out var inventory)) return 0f;
            var total = 0f;
            // Broken armor gives no armor (SYS-CRAFT-02).
            foreach (var slot in EquipSlots.All) total += inventory.Slots.Working(slot)?.Armor ?? 0f;
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
            // Traits are permanent effects (SYS-START-01), read through the same list as dishes' buffs.
            var active = new List<BuffDef>(_traitEffects);
            foreach (var id in Buffs.ActiveBuffIds(NowMinutes))
                if (DefRegistry.TryGet<BuffDef>(id, out var buff)) active.Add(buff);
            return active;
        }

        readonly List<BuffDef> _traitEffects = new();

        /// <summary>SYS-START-01: the survivor's traits, as permanent effects. Replaces any earlier set.</summary>
        public void SetTraits(IEnumerable<TraitDef> traits)
        {
            _traitEffects.Clear();
            foreach (var trait in traits)
                if (trait?.Effects is { Length: > 0 }) _traitEffects.Add(new BuffDef { Id = trait.Id, Name = trait.Name, Effects = trait.Effects });
        }

        /// <summary>Server-side: grants a buff, its duration stretched by a dish's buff_duration / care tag.</summary>
        public void GrantBuff(NamespacedId buffId, float durationMult)
        {
            if (!IsServer || !DefRegistry.TryGet<BuffDef>(buffId, out var buff)) return;
            // buff_duration_mult (traits): a gourmand savours a meal's effects longer, an ascetic shorter.
            durationMult *= BuffEffects.Mult(_traitEffects, "buff_duration_mult");
            Buffs.Grant(new BuffDef { Id = buff.Id, Name = buff.Name, Effects = buff.Effects, DurationMin = (long)(buff.DurationMin * durationMult) }, NowMinutes);
        }

        void Tick()
        {
            var buffs = ActiveBuffs();
            var heatstroke = VitalsCalculator.IsHeatstroke(Temperature);
            var hungerDelta = VitalsCalculator.HungerDrainPerHour(CurrentActivity, Temperature) / 60f * BuffEffects.Mult(buffs, "hunger_drain_mult");
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
        /// <summary>SYS-COMBAT-02 side effects on this player (bleed, burn, poison). A blunt stun isn't applied to
        /// players yet (it needs an input-lock rule; see PROJECT_STATE).</summary>
        public Combat.CombatStatus Status { get; } = new();

        /// <summary>When a hit last landed (not damage over time) — for the red hit flash.</summary>
        public float LastHitAt { get; private set; } = float.NegativeInfinity;

        public void TakeDamage(float amount) => TakeDamage(amount, Combat.DamageTypes.Blunt);

        /// <summary>A typed hit: armor against that type (SYS-COMBAT-02), then any food resistance for it
        /// (<c>damage_resist</c> buffs), then the type's damage over time.</summary>
        public void TakeDamage(float amount, string damageType)
        {
            if (!IsServer || amount <= 0f) return;
            if (_movement != null && _movement.IsInvulnerable) return; // dodge roll i-frames
            var armor = Combat.DamageTypes.ArmorAgainst(WornArmor(), WornArmorTypes(damageType), damageType);
            var taken = Combat.DamageTypes.Damage(amount, damageType, FoodResistMult(damageType), armor) * BuffEffects.Mult(ActiveBuffs(), "damage_taken_mult");
            Status.OnHit(damageType, taken, Time.time, out _);
            Health = Mathf.Clamp(Health - taken, 0f, VitalsCalculator.GaugeMax);
            LastHitAt = Time.time;
            WearArmor();
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
            Status.Clear();
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
