using System;

namespace Isle.World.Generation
{
    /// <summary>
    /// SYS-DUNG-01 §Decided values — the Abyss: each sigil missing from the seal is a curse (more enemies, a weaker
    /// light), and every floor down presses harder (slower stamina regen). The rates come from the dungeon's data; the
    /// decided ones are +25% enemies and −15% light per missing sigil, −15% stamina regen per floor. Static pure.
    /// </summary>
    public static class AbyssCalculator
    {
        public static int Missing(int placed, int required) => Math.Clamp(required - placed, 0, Math.Max(0, required));

        /// <summary>×(1 + rate × missing): all four missing at 0.25 doubles the enemies.</summary>
        public static float EnemyMult(int missing, float perMissing) => 1f + perMissing * Math.Max(0, missing);

        /// <summary>×(1 − rate × missing), never below 0: all four missing at 0.15 leaves 40% of the light.</summary>
        public static float LightMult(int missing, float perMissing) => Math.Max(0f, 1f - perMissing * Math.Max(0, missing));

        /// <summary>×(1 − rate × floor number), floor number = index + 1: floor 3 at 0.15 is 55%.</summary>
        public static float StaminaRegenMult(int floorIndex, float perFloor) => Math.Max(0f, 1f - perFloor * (floorIndex + 1));

        /// <summary>How many creatures a spawn mark of <paramref name="group"/> holds under a multiplier: the whole part,
        /// plus one more when <paramref name="roll"/> (0..1, seed-fixed) falls under the fraction.</summary>
        public static int CursedCount(int group, float mult, double roll)
        {
            var exact = Math.Max(1, group) * Math.Max(0f, mult);
            var whole = (int)Math.Floor(exact);
            return whole + (roll < exact - whole ? 1 : 0);
        }
    }
}
