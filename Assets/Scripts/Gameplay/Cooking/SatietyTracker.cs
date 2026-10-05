using System.Collections.Generic;

namespace Isle.Gameplay.Cooking
{
    /// <summary>
    /// SYS-COOK-01 §Satiety fatigue: the same kind of dish (method + main ingredient tag) gives less each time,
    /// down to 0.40×, and the count forgets one eat every 2 in-game days. Per player.
    /// </summary>
    public sealed class SatietyTracker
    {
        public const float FatigueStep = 0.15f;
        public const float FatigueFloor = 0.40f;
        public const long DecayMinutes = 2 * 1440;

        readonly Dictionary<string, (int Count, long Since)> _eaten = new();

        /// <summary>Records one eat of <paramref name="signature"/> and returns the nutrition multiplier for it.</summary>
        public float Eat(string signature, long nowMinutes)
        {
            var (count, since) = _eaten.GetValueOrDefault(signature, (0, nowMinutes));
            var decays = (int)((nowMinutes - since) / DecayMinutes);
            if (decays > 0)
            {
                count = System.Math.Max(0, count - decays);
                since += decays * DecayMinutes;
            }
            count++;
            _eaten[signature] = (count, count == 1 ? nowMinutes : since);
            return System.Math.Max(FatigueFloor, 1f - FatigueStep * (count - 1));
        }
    }
}
