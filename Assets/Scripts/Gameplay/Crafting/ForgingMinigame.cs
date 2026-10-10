using System;

namespace Isle.Gameplay.Crafting
{
    /// <summary>
    /// SYS-CRAFT-01 §Forging minigame. Pure state machine stepped by the server; the client only sends whether the
    /// bellows are held and when the hammer falls. Heat rises with the bellows and falls on its own; a strike while the
    /// heat is inside the success window counts. After the recipe's strikes the score is successes / required.
    /// The window is centred on the spec's target band [0.55, 0.75] and is exactly that band at Lv 0
    /// (half-width = successWindow = 0.10 × (1 + level / 50)), widening with skill.
    /// </summary>
    public sealed class ForgingMinigame
    {
        /// <summary>The recipe <c>minigame</c> key (its name part) that runs this.</summary>
        public const string Key = "forging";

        // SYS-CRAFT-01 constants.
        public const float BandCentre = 0.65f;
        public const float BaseWindow = 0.10f;
        public const int MinStrikes = 3;
        public const int MaxStrikes = 8;
        public const float CoopScoreBonus = 0.08f;
        const float MaxLevel = 50f;

        /// <summary>Heat gained per second with the bellows held. [invented]</summary>
        public const float BellowsRate = 0.5f;

        /// <summary>Heat lost per second (halved forging together). [invented]</summary>
        public const float DecayRate = 0.2f;

        /// <summary>Seconds between hammer strikes. [invented]</summary>
        public const float StrikeCooldown = 0.4f;

        readonly bool _coop;
        float _time, _lastStrikeAt = float.NegativeInfinity;

        public ForgingMinigame(int requiredStrikes, int craftingLevel, bool coop = false)
        {
            Required = Math.Clamp(requiredStrikes, MinStrikes, MaxStrikes);
            HalfWindow = BaseWindow * (1f + Math.Clamp(craftingLevel, 0, (int)MaxLevel) / MaxLevel);
            _coop = coop;
        }

        public float Heat { get; private set; }
        public int Required { get; }
        public int Strikes { get; private set; }
        public int Successes { get; private set; }
        public float HalfWindow { get; }
        public float WindowLow => BandCentre - HalfWindow;
        public float WindowHigh => BandCentre + HalfWindow;
        public bool Done => Strikes >= Required;

        /// <summary>Whether each strike so far landed, in order (for the HUD's pips).</summary>
        public bool[] Results
        {
            get
            {
                var results = new bool[Strikes];
                Array.Copy(_results, results, Strikes);
                return results;
            }
        }
        readonly bool[] _results = new bool[MaxStrikes];

        /// <summary>Whether the latest strike landed (false before any).</summary>
        public bool LastHit => Strikes > 0 && _results[Strikes - 1];

        public bool InWindow => Heat >= WindowLow - 1e-6f && Heat <= WindowHigh + 1e-6f;

        public void Step(float dt, bool bellows)
        {
            if (Done || dt <= 0f) return;
            _time += dt;
            var decay = DecayRate * (_coop ? 0.5f : 1f);
            Heat = Math.Clamp(Heat + (bellows ? BellowsRate : 0f) * dt - decay * dt, 0f, 1f);
        }

        /// <summary>A hammer strike. Returns false when it can't be taken (finished, or too soon after the last).</summary>
        public bool Strike()
        {
            if (Done || _time - _lastStrikeAt < StrikeCooldown) return false;
            _lastStrikeAt = _time;
            var hit = InWindow;
            _results[Strikes] = hit;
            Strikes++;
            if (hit) Successes++;
            return true;
        }

        /// <summary>SYS-CRAFT-01 <c>minigameScore</c>, 0..1 (co-op +0.08).</summary>
        public float Score => Math.Clamp(Successes / (float)Required + (_coop ? CoopScoreBonus : 0f), 0f, 1f);
    }
}
