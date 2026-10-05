using System;

namespace Isle.Gameplay.Building
{
    /// <summary>SYS-WORLD-01 §Deferred simulation, crop growth. Static pure.</summary>
    public static class CropCalculator
    {
        const float MinutesPerDay = 1440f;

        public static float Growth(long plantedAtMinutes, long nowMinutes, int growthDays)
        {
            if (growthDays <= 0) return 1f;
            var elapsed = Math.Max(0L, nowMinutes - plantedAtMinutes);
            return Math.Min(1f, elapsed / (growthDays * MinutesPerDay));
        }

        public static bool IsRipe(float growth) => growth >= 1f;
    }
}
