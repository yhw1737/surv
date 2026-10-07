using Isle.Data;

namespace Isle.Gameplay.Combat
{
    /// <summary>
    /// SYS-DUNG-01 boss shell phase (Hermit Colossus): once below <see cref="BossShellSpec.BelowHealth"/> it hides at
    /// once, then again every <see cref="BossShellSpec.EverySeconds"/> (counted from the start of the last hide), each
    /// time for <see cref="BossShellSpec.Seconds"/>. While hidden it takes <see cref="BossShellSpec.DamageMult"/>;
    /// a stun cracks it open early. Pure; the caller passes the time.
    /// </summary>
    public sealed class BossShell
    {
        float _nextAt = -1f;
        float _until = float.NegativeInfinity;

        public bool IsHidden(float now) => now < _until;

        public float DamageMult(float now, BossShellSpec spec) => spec != null && IsHidden(now) ? spec.DamageMult : 1f;

        public void Tick(float now, float healthFraction, BossShellSpec spec)
        {
            if (spec == null || spec.Seconds <= 0f || healthFraction >= spec.BelowHealth) return;
            if (_nextAt >= 0f && now < _nextAt) return;
            _until = now + spec.Seconds;
            _nextAt = now + spec.EverySeconds;
        }

        /// <summary>A stun cracks the shell: out now, next hide still on schedule.</summary>
        public void Break(float now)
        {
            if (IsHidden(now)) _until = now;
        }
    }
}
