using UnityEngine;

namespace Isle.Gameplay.Combat
{
    /// <summary>SYS-COMBAT-01 §Damage. Static pure, no Unity refs beyond Mathf.</summary>
    public static class DamageResolver
    {
        /// <summary>SYS-COMBAT-01: "ArmorConstant = 60".</summary>
        public const float ArmorConstant = 60f;

        /// <summary>SYS-COMBAT-01: reduction is capped at 75%.</summary>
        public const float MaxArmorReduction = 0.75f;

        public static float Damage(float finalPower, float totalArmor, float partMult = 1f)
        {
            var reduction = Mathf.Clamp(totalArmor / (totalArmor + ArmorConstant), 0f, MaxArmorReduction);
            return finalPower * (1f - reduction) * partMult;
        }
    }
}
