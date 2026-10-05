using System;

namespace Isle.Gameplay.Combat
{
    /// <summary>SYS-HUNT-01 §Weight: each individual rolls its own weight from a lognormal around
    /// the def's mean, clamped to its min/max. The uniform inputs are parameters so tests can pin the
    /// Box-Muller step; the live caller passes <c>UnityEngine.Random</c> values.</summary>
    public static class WeightRoll
    {
        public static float Sample(float mean, float sigma, float min, float max, double u1, double u2)
        {
            // Box-Muller: a standard normal z from two uniforms in (0, 1].
            var z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            var weight = Math.Exp(Math.Log(mean) + sigma * z);
            return (float)Math.Clamp(weight, min, max);
        }
    }
}
