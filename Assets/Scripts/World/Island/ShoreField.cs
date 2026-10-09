using System;

namespace Isle.World.Island
{
    /// <summary>
    /// The smooth shoreline, shared by what is drawn and what blocks: per-tile water (1) / land (0) blurred with the
    /// 5×5 binomial kernel (the ground renderer's two [1 2 1] passes) and interpolated bilinearly between tile centres.
    /// Water is where the field reaches <see cref="Shoreline"/> — the same line <see cref="GroundRenderer"/> paints, so
    /// you walk up to the drawn water's edge instead of a tile's square corner. Pure.
    /// </summary>
    public static class ShoreField
    {
        /// <summary>Field value of the drawn water's edge.</summary>
        public const float Shoreline = 0.5f;

        static readonly float[] Kernel = { 1f, 4f, 6f, 4f, 1f };

        /// <summary>The blurred water value at a tile's centre, 0..1.</summary>
        public static float AtTile(Func<int, int, bool> isWater, int x, int y)
        {
            var sum = 0f;
            for (var dy = -2; dy <= 2; dy++)
            for (var dx = -2; dx <= 2; dx++)
                if (isWater(x + dx, y + dy)) sum += Kernel[dx + 2] * Kernel[dy + 2];
            return sum / 256f;
        }

        /// <summary>The field at a continuous tile coordinate, where integers are tile centres.</summary>
        public static float At(Func<int, int, float> tileValue, float u, float v)
        {
            var x = (int)Math.Floor(u);
            var y = (int)Math.Floor(v);
            var tx = u - x;
            var ty = v - y;
            var a = tileValue(x, y);
            var b = tileValue(x + 1, y);
            var c = tileValue(x, y + 1);
            var d = tileValue(x + 1, y + 1);
            return (a + (b - a) * tx) * (1f - ty) + (c + (d - c) * tx) * ty;
        }

        public static bool IsWater(float field) => field >= Shoreline;
    }
}
