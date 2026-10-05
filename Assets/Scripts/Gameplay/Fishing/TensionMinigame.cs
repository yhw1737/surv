using System;

namespace Isle.Gameplay.Fishing
{
    public enum FightResult { Ongoing, Caught, LineBroke, HookSlipped }

    /// <summary>
    /// SYS-FISH-01 §Tension minigame. Pure state machine stepped by the server; the client only sends whether the
    /// reel button is held. Spec values are used verbatim; the meter rise rate is the one number the spec leaves
    /// out.
    /// </summary>
    public sealed class TensionMinigame
    {
        public const float StartTension = 0.5f;
        public const float ReelRate = 0.55f;
        public const float SlackRate = 0.40f;
        public const float DrainRate = 18f;
        public const float SoloHeavyDrainMult = 0.4f;
        public const int MaxLevel = 50;

        /// <summary>Meter gain per second while outside the band — not in the spec, so 2 s outside fails. [invented]</summary>
        public const float MeterRate = 0.5f;

        const float StraightSpike = 0.25f, StraightPeriod = 1.5f;
        const float ZigzagAmplitude = 0.18f, ZigzagPeriod = 0.8f;
        const float DiveDrop = 0.35f, DivePeriod = 3f;

        readonly string _pattern;
        readonly float _drainMult;
        float _time;

        public TensionMinigame(string pattern, float tensionWindow, int fishingLevel, float fishStamina, float drainMult)
        {
            _pattern = pattern ?? string.Empty;
            HalfWindowWidth = HalfWindow(tensionWindow, fishingLevel);
            FishStamina = fishStamina;
            MaxFishStamina = fishStamina;
            _drainMult = drainMult;
        }

        public float Tension { get; private set; } = StartTension;
        public float HalfWindowWidth { get; }
        public float FishStamina { get; private set; }
        public float MaxFishStamina { get; }
        public float LineBreak { get; private set; }
        public float HookSlip { get; private set; }
        public FightResult Result { get; private set; } = FightResult.Ongoing;

        public float BandLow => StartTension - HalfWindowWidth;
        public float BandHigh => StartTension + HalfWindowWidth;

        public static float HalfWindow(float tensionWindow, int fishingLevel) => tensionWindow * (1f + (float)fishingLevel / MaxLevel);

        /// <summary>SYS-FISH-01 co-op fight: at or above the threshold a lone angler drains at ×0.4.</summary>
        public static float DrainMult(float weightKg, float coopThresholdKg, int anglers) =>
            coopThresholdKg > 0f && weightKg >= coopThresholdKg && anglers < 2 ? SoloHeavyDrainMult : 1f;

        public void Step(float seconds, bool reeling)
        {
            if (Result != FightResult.Ongoing) return;

            var before = _time;
            _time += seconds;
            Tension += (reeling ? ReelRate : -SlackRate) * seconds + Perturbation(before, _time);
            Tension = Math.Clamp(Tension, 0f, 1f);

            if (Tension > BandHigh) LineBreak += MeterRate * seconds;
            else if (Tension < BandLow) HookSlip += MeterRate * seconds;
            else FishStamina -= DrainRate * _drainMult * seconds;

            if (LineBreak >= 1f) Result = FightResult.LineBroke;
            else if (HookSlip >= 1f) Result = FightResult.HookSlipped;
            else if (FishStamina <= 0f) Result = FightResult.Caught;
        }

        /// <summary>The fish's resistance between two times, per its fight pattern.</summary>
        float Perturbation(float from, float to) => _pattern switch
        {
            "straight" => Crossings(from, to, StraightPeriod) * StraightSpike,
            "dive" => -Crossings(from, to, DivePeriod) * DiveDrop,
            "zigzag" => ZigzagAmplitude * (float)(Math.Sin(2 * Math.PI * to / ZigzagPeriod) - Math.Sin(2 * Math.PI * from / ZigzagPeriod)),
            _ => 0f,
        };

        static int Crossings(float from, float to, float period) => (int)Math.Floor(to / period) - (int)Math.Floor(from / period);
    }
}
