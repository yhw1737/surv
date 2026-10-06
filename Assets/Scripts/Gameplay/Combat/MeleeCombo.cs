namespace Isle.Gameplay.Combat
{
    /// <summary>
    /// SYS-COMBAT-01 §Melee: "3-hit combo, third hit ×1.4. Stamina 8 / 8 / 14. Combo window 1.2 s." Unlocks: Lv20 4-hit
    /// combo, Lv35 execute below 20% HP, Lv45 combo stamina −30%. Pure — the server owns one per player.
    /// <para>The combo's last hit is the finisher (×1.4, 14/8 of the stamina), so the Lv20 4-hit combo is three normal
    /// hits and a finisher. The spec's 8/8/14 are read as shares of the weapon's own <c>stamina_cost</c> (8 → 8/8/14),
    /// and the 1.2 s window runs from when the weapon is ready to swing again, so slow weapons can still combo.</para>
    /// </summary>
    public sealed class MeleeCombo
    {
        public const float WindowSeconds = 1.2f;
        public const float FinisherPowerMult = 1.4f;
        public const float StaggeredMult = 1.2f; // §Power situationalMult "target staggered 1.20"
        public const float ExecuteHealthFraction = 0.2f;

        const float SpecBaseStamina = 8f;
        const float SpecFinisherStamina = 14f;
        const int FourHitLevel = 20;
        const int ExecuteLevel = 35;
        const int CheapComboLevel = 45;
        const float CheapComboMult = 0.7f;

        /// <summary>1-based step of the last hit, 0 before any.</summary>
        public int Step { get; private set; }


        public static int Length(int level) => level >= FourHitLevel ? 4 : 3;

        public static float PowerMult(int step, int length) => step >= length ? FinisherPowerMult : 1f;

        public static float StaminaCost(float weaponCost, int step, int length, int level)
        {
            var share = step >= length ? SpecFinisherStamina / SpecBaseStamina : 1f;
            return weaponCost * share * (level >= CheapComboLevel ? CheapComboMult : 1f);
        }

        public static float SituationalMult(bool staggered) => staggered ? StaggeredMult : 1f;

        public static bool Executes(int level, float healthFraction) => level >= ExecuteLevel && healthFraction < ExecuteHealthFraction;

        /// <summary>The step this swing lands as. <paramref name="readyAt"/> is when the weapon could swing again after
        /// the previous hit; a press within <see cref="WindowSeconds"/> of that continues the combo.</summary>
        public int Advance(float readyAt, float now, int length)
        {
            var continues = Step > 0 && Step < length && now - readyAt <= WindowSeconds;
            Step = continues ? Step + 1 : 1;
            return Step;
        }

        public void Reset() => Step = 0;
    }

    public enum BlockOutcome { None, Blocked, Parried, GuardBroken }

    /// <summary>
    /// SYS-COMBAT-01 §Melee: "Right-click blocks (frontal 90°, costs stamina). Parry: block input within 0.25 s before
    /// impact → zero damage, 1.0 s enemy stagger, no stamina cost." Lv10 widens the parry window to 0.35 s. Pure.
    /// </summary>
    public static class MeleeDefense
    {
        public const float FrontalHalfAngleDeg = 45f;
        public const float ParryStaggerSeconds = 1f;
        const float BaseParryWindow = 0.25f;
        const float WideParryWindow = 0.35f;
        const int WideParryLevel = 10;

        /// <summary>Stamina a block spends per point of incoming damage (before armor). [invented] — the spec only
        /// says "costs stamina".</summary>
        public const float StaminaPerDamage = 1f;

        public static float ParryWindow(int level) => level >= WideParryLevel ? WideParryWindow : BaseParryWindow;

        public readonly struct Result
        {
            public readonly BlockOutcome Outcome;
            public readonly float DamageThrough;
            public readonly float StaminaCost;

            public Result(BlockOutcome outcome, float damageThrough, float staminaCost)
            {
                Outcome = outcome;
                DamageThrough = damageThrough;
                StaminaCost = staminaCost;
            }
        }

        /// <param name="sinceBlockStart">Seconds since the block button went down.</param>
        /// <param name="angleToAttackerDeg">Angle between where the guard faces and the direction to the attacker.</param>
        public static Result Resolve(bool blocking, float sinceBlockStart, float parryWindow, float angleToAttackerDeg, float damage, float stamina)
        {
            if (!blocking || angleToAttackerDeg > FrontalHalfAngleDeg || damage <= 0f) return new Result(BlockOutcome.None, damage, 0f);
            if (sinceBlockStart <= parryWindow) return new Result(BlockOutcome.Parried, 0f, 0f);

            var cost = damage * StaminaPerDamage;
            if (stamina >= cost) return new Result(BlockOutcome.Blocked, 0f, cost);

            // Not enough stamina: the guard soaks what it can and the rest goes through.
            var spent = System.Math.Max(0f, stamina);
            return new Result(BlockOutcome.GuardBroken, damage - spent / StaminaPerDamage, spent);
        }
    }
}
