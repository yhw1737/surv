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

        /// <summary>The spec doesn't give a roll speed; it reuses the sprint multiplier. [invented]</summary>
        public const float RollMult = SprintMult;

        public static float Speed(float weightMult, bool sprinting) =>
            BaseSpeed * weightMult * (sprinting ? SprintMult : 1f);

        public static bool IsRollInvulnerable(float elapsedSeconds) =>
            elapsedSeconds >= RollInvulnerableFrom && elapsedSeconds <= RollInvulnerableTo;
    }
}
