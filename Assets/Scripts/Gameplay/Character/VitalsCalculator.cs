using System;

namespace Isle.Gameplay.Character
{
    /// <summary>What the player was doing over the last drain tick (SYS-SURV-01 §Drain modifiers).
    /// Nothing sets this from real movement/combat/gathering state yet — those systems don't exist
    /// (movement is <c>PlayerMovement</c>'s WASD input only, no run key; combat/gathering are Phase 6+) —
    /// so <see cref="Vitals"/> exposes it as a plain server-set property for whichever system drives
    /// it first. <c>Combat</c>, <c>Gathering</c> and <c>Butchering</c> all carry the same 1.4× the spec
    /// gives them, so one <see cref="Active"/> member covers all three until they need to diverge.</summary>
    public enum Activity { Idle, Walking, Sprinting, Active }

    /// <summary>SYS-SURV-01 §Water sources. The interaction that lets a player drink one (reach,
    /// disease roll, herb-halving) is T-052 — this enum only carries the thirst-delta lookup that
    /// spec table already gives, so it can be verified now.</summary>
    public enum WaterSource { Seawater, StandingWater, Stream, RainCatcher, BoiledWater, Coconut }

    /// <summary>
    /// SYS-SURV-01. Static pure, no Unity/FishNet refs — EditMode test target. Formulas for all 5
    /// gauges (Hunger, Thirst, Temperature, Stamina, Health); <see cref="Vitals"/> is the server-side
    /// state holder that calls these once per in-game minute.
    /// <para>
    /// Environmental inputs (<c>ambientTemp</c>, clothing bonus, wet-penalty magnitude, fire-bonus
    /// distance) are parameters here, not computed here: there's still no weather system to drive
    /// ambient temperature or rain, and no world-object/interaction system (<c>WorldObject</c> is a
    /// bare def-id + position, T-163) to place an actual campfire or read a real distance to one.
    /// <see cref="DecayWetPenalty"/> and <see cref="FireBonusAtDistance"/> are T-051's own formulas —
    /// the "dries over 20 minutes" and "falls off with distance" rules the spec text gives — ready
    /// for whichever system eventually supplies rain/swimming events and campfire proximity.
    /// </para>
    /// <para>
    /// The six water sources' disease risk and herb-halving, and the drink interaction itself, are
    /// T-052's job; this class only has the thirst-delta lookup the spec's own table gives.
    /// </para>
    /// </summary>
    public static class VitalsCalculator
    {
        public const float GaugeMax = 100f;

        // §Gauges — base drain, per in-game hour.
        public const float HungerBaseRate = 1.5f;
        public const float ThirstBaseRate = 3.0f;

        // §Gauges — "at zero", HP per real second.
        public const float HungerZeroHpDrainPerSecond = 0.4f;
        public const float ThirstZeroHpDrainPerSecond = 1.0f;

        // §Drain modifiers.
        public const float ColdMultThreshold = 35f;
        public const float ColdMultValue = 1.5f;
        public const float HeatMultThreshold = 28f;
        public const float HeatMultValue = 1.6f;
        public const float SaltMultValue = 1.4f;
        public const float SaltMultWindowMinutes = 30f;

        // §Temperature.
        public const float TempApproachRatePerMinute = 2.0f;
        public const float HypothermiaWarningTemp = 33f;
        public const float HypothermiaHpTemp = 28f;
        public const float HypothermiaHpDrainPerSecond = 0.5f;
        public const float HeatstrokeWarningTemp = 41f;
        public const float HeatstrokeHpTemp = 44f;
        public const float HeatstrokeHpDrainPerSecond = 0.5f;

        // §Temperature — wetPenalty and fireBonus (T-051).
        public const float WetPenaltyMagnitude = 6f;
        public const float WetDryMinutes = 20f;
        public const float FireBonusMax = 8f;
        public const float FireBonusRangeTiles = 5f;

        // §Stamina.
        public const float StaminaRegenDelaySeconds = 1.2f;
        public const float StaminaRegenPerSecondBase = 18f;
        public const float LowGaugeThreshold = 25f;
        public const float LowGaugeRegenMult = 0.5f;
        public const float LowGaugeMaxStaminaMult = 0.7f;

        public static float ActivityMult(Activity activity) => activity switch
        {
            Activity.Idle => 0.7f,
            Activity.Walking => 1.0f,
            Activity.Sprinting => 1.8f,
            Activity.Active => 1.4f,
            _ => throw new ArgumentOutOfRangeException(nameof(activity)),
        };

        public static float ColdMultiplier(float temperature) => temperature < ColdMultThreshold ? ColdMultValue : 1f;

        public static float HeatMultiplier(float ambientTemp) => ambientTemp > HeatMultThreshold ? HeatMultValue : 1f;

        public static float SaltMultiplier(float minutesSinceSaltyFood) =>
            minutesSinceSaltyFood <= SaltMultWindowMinutes ? SaltMultValue : 1f;

        /// <summary>hungerDrain = HungerBaseRate * activityMult * coldMult (§Drain modifiers).
        /// <paramref name="temperature"/> is the player's own <see cref="Vitals.Temperature"/> gauge,
        /// not ambient — cold drains hunger only once the player is actually cold.</summary>
        public static float HungerDrainPerHour(Activity activity, float temperature) =>
            HungerBaseRate * ActivityMult(activity) * ColdMultiplier(temperature);

        /// <summary>thirstDrain = ThirstBaseRate * activityMult * heatMult * saltMult (§Drain modifiers).
        /// Heatstroke's ×2 thirst penalty (§Temperature, &gt;44) is applied by the caller on top of this,
        /// since it isn't one of the three named modifiers this formula covers.</summary>
        public static float ThirstDrainPerHour(Activity activity, float ambientTemp, float minutesSinceSaltyFood) =>
            ThirstBaseRate * ActivityMult(activity) * HeatMultiplier(ambientTemp) * SaltMultiplier(minutesSinceSaltyFood);

        public static float TargetTemperature(float ambientTemp, float clothingBonus, float fireBonus, float wetPenalty) =>
            ambientTemp + clothingBonus + fireBonus - wetPenalty;

        /// <summary>Temperature approaches its target at a fixed rate per in-game minute, never
        /// overshooting (§Temperature).</summary>
        public static float ApproachTemperature(float current, float target, float inGameMinutes)
        {
            var maxStep = TempApproachRatePerMinute * inGameMinutes;
            var delta = Math.Clamp(target - current, -maxStep, maxStep);
            return current + delta;
        }

        /// <summary>§Temperature: "wetPenalty −6 from rain or swimming; dries over 20 in-game
        /// minutes." Linear decay of the magnitude back to 0, never past it — same clamped-step
        /// shape as <see cref="ApproachTemperature"/>, just decaying towards a fixed 0 target rather
        /// than a caller-supplied one.</summary>
        public static float DecayWetPenalty(float current, float inGameMinutes)
        {
            var step = (WetPenaltyMagnitude / WetDryMinutes) * inGameMinutes;
            return Math.Max(0f, current - step);
        }

        /// <summary>§Temperature: "fireBonus +8 within 5 tiles of a campfire, falling off with
        /// distance." The spec doesn't give the falloff's exact shape, only its two endpoints —
        /// linear between them is the plain reading of "falling off" and needs no invented number
        /// beyond what §Temperature already gives (PROJECT_STATE.md §Decided without a spec).</summary>
        public static float FireBonusAtDistance(float distanceTiles) =>
            Math.Max(0f, FireBonusMax * (1f - distanceTiles / FireBonusRangeTiles));

        /// <summary>§Temperature band table's two HP-drain rows, in HP per real second.</summary>
        public static float TemperatureHpDrainPerSecond(float temperature) =>
            temperature < HypothermiaHpTemp ? -HypothermiaHpDrainPerSecond :
            temperature > HeatstrokeHpTemp ? -HeatstrokeHpDrainPerSecond : 0f;

        public static bool IsHeatstroke(float temperature) => temperature > HeatstrokeHpTemp;

        /// <summary>§Gauges "at zero" rows, combined — both can apply at once.</summary>
        public static float ZeroGaugeHpDrainPerSecond(float hunger, float thirst)
        {
            var drain = 0f;
            if (hunger <= 0f) drain -= HungerZeroHpDrainPerSecond;
            if (thirst <= 0f) drain -= ThirstZeroHpDrainPerSecond;
            return drain;
        }

        static bool IsLow(float hunger, float thirst) => hunger < LowGaugeThreshold || thirst < LowGaugeThreshold;

        /// <summary>§Stamina regen formula. <paramref name="overweightFactor"/> is 0–1 from SYS-INV-01's
        /// weight system, which has no consumer wired up yet (see <c>InventoryNetwork</c> remarks) —
        /// callers pass 1 (no penalty) until it exists.</summary>
        public static float StaminaRegenPerSecondAt(float overweightFactor, float hunger, float thirst) =>
            StaminaRegenPerSecondBase * overweightFactor * (IsLow(hunger, thirst) ? LowGaugeRegenMult : 1f);

        public static float MaxStaminaFactor(float hunger, float thirst) => IsLow(hunger, thirst) ? LowGaugeMaxStaminaMult : 1f;

        /// <summary>§Water sources table's thirst column.</summary>
        public static float DrinkThirstDelta(WaterSource source) => source switch
        {
            WaterSource.Seawater => -15f,
            WaterSource.StandingWater => 25f,
            WaterSource.Stream => 35f,
            WaterSource.RainCatcher => 40f,
            WaterSource.BoiledWater => 45f,
            WaterSource.Coconut => 30f,
            _ => throw new ArgumentOutOfRangeException(nameof(source)),
        };
    }
}
