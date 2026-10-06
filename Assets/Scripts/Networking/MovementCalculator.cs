namespace Isle.Networking
{
    /// <summary>SYS-CHAR-01 §Movement. Static pure, no Unity refs — EditMode test target.</summary>
    public static class MovementCalculator
    {
        /// <summary>SYS-CHAR-01: "BaseSpeed = 4.2 tiles/s".</summary>
        public const float BaseSpeed = 4.2f;

        /// <summary>SYS-CHAR-01 stance table: sprint 1.65.</summary>
        public const float SprintMult = 1.65f;

        /// <summary>SYS-CHAR-01: "Dodge roll (Space): 0.6 s total, i-frames 0.1–0.45 s".</summary>
        public const float RollSeconds = 0.6f;
        public const float RollInvulnerableFrom = 0.1f;
        public const float RollInvulnerableTo = 0.45f;

        /// <summary>The spec doesn't give a roll speed. 2.6× base (≈ 6.6 tiles in the 0.6 s roll) — faster than a sprint,
        /// so a roll reads as a burst (developer, 2026-10-06: "구르기는 이것보단 빠르게"). [invented]</summary>
        public const float RollMult = 2.6f;

        /// <summary>Share of the way the roll turns toward held input each tick — steerable mid-roll (SYS-CHAR-01
        /// verification 6: "Change direction mid-roll") without snapping. [invented]</summary>
        public const float RollSteer = 0.35f;

        /// <summary>The roll's direction after one tick of steering toward <paramref name="input"/> (unchanged with no input).</summary>
        public static (float X, float Y) SteerRoll(float dirX, float dirY, float inputX, float inputY)
        {
            var inputLength = (float)System.Math.Sqrt(inputX * inputX + inputY * inputY);
            if (inputLength < 1e-4f) return (dirX, dirY);
            var x = dirX + (inputX / inputLength - dirX) * RollSteer;
            var y = dirY + (inputY / inputLength - dirY) * RollSteer;
            var length = (float)System.Math.Sqrt(x * x + y * y);
            return length < 1e-4f ? (inputX / inputLength, inputY / inputLength) : (x / length, y / length);
        }

        public static float Speed(float weightMult, bool sprinting) =>
            BaseSpeed * weightMult * (sprinting ? SprintMult : 1f);

        public static bool IsRollInvulnerable(float elapsedSeconds) =>
            elapsedSeconds >= RollInvulnerableFrom && elapsedSeconds <= RollInvulnerableTo;
    }
}
