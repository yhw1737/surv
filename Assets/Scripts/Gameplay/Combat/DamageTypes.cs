using System.Collections.Generic;
using Isle.Data;
using UnityEngine;

namespace Isle.Gameplay.Combat
{
    /// <summary>SYS-COMBAT-02 §Formula: typed damage on top of SYS-COMBAT-01's armor curve. Static pure.</summary>
    public static class DamageTypes
    {
        public const string Blunt = "blunt", Slash = "slash", Pierce = "pierce", Heat = "heat", Toxic = "toxic";

        /// <summary>SYS-COMBAT-02 decided: pierce ignores 40% of the target's pierce armor.</summary>
        public const float PierceBypass = 0.4f;

        public static bool IsPhysical(string type) => type is Blunt or Slash or Pierce;

        /// <summary><c>FinalPower × typeMult × (1 − armorReduction(effectiveArmor))</c>.</summary>
        public static float Damage(float finalPower, string type, float resistMult, float armor)
        {
            var effective = armor * (type == Pierce ? 1f - PierceBypass : 1f);
            return DamageResolver.Damage(finalPower * resistMult, effective);
        }

        public static float ResistOf(IReadOnlyDictionary<string, float> resist, string type) =>
            resist != null && type != null && resist.TryGetValue(type, out var mult) ? mult : 1f;

        /// <summary>Armor against <paramref name="type"/>: a per-type entry if there is one, else the flat armor for the
        /// physical types and nothing for heat/toxic.</summary>
        public static float ArmorAgainst(float flatArmor, IReadOnlyDictionary<string, float> byType, string type)
        {
            if (byType != null && type != null && byType.TryGetValue(type, out var value)) return value;
            return IsPhysical(type ?? Blunt) ? flatArmor : 0f;
        }
    }

    /// <summary>SYS-COMBAT-02 §Hit shapes: is a target (centre + body radius) touched by an attack from
    /// <paramref name="origin"/> aimed along <paramref name="aim"/>? With no aim, anything within the shape's reach.</summary>
    public static class HitShapes
    {
        public static float Reach(AttackSpec a) => a.Shape switch
        {
            "thrust" or "line" => a.Length,
            "smash" => a.Offset + a.Radius,
            _ => a.Radius,
        };

        public static bool Contains(AttackSpec a, Vector2 origin, Vector2 aim, Vector2 target, float targetRadius)
        {
            var offset = target - origin;
            if (aim.sqrMagnitude < 1e-6f) return offset.magnitude - targetRadius <= Reach(a);
            var dir = aim.normalized;
            switch (a.Shape)
            {
                case "thrust":
                case "line":
                {
                    var along = Vector2.Dot(offset, dir);
                    var across = Mathf.Abs(dir.x * offset.y - dir.y * offset.x);
                    return along >= -targetRadius && along <= a.Length + targetRadius && across <= a.Width * 0.5f + targetRadius;
                }
                case "smash":
                    return Vector2.Distance(origin + dir * a.Offset, target) <= a.Radius + targetRadius;
                case "sweep":
                    return offset.magnitude <= a.Radius + targetRadius;
                default: // arc
                    if (offset.magnitude - targetRadius > a.Radius) return false;
                    return a.Degrees <= 0f || MeleeCone.Contains(dir, offset, targetRadius, a.Degrees);
            }
        }
    }

    /// <summary>Which <see cref="AttackSpec"/> a combo step uses. A weapon without <c>attacks</c> is one arc of its
    /// cone and reach (SYS-COMBAT-02 verification 4), keeping SYS-COMBAT-01's finisher multiplier.</summary>
    public static class WeaponAttacks
    {
        public static AttackSpec For(WeaponDef weapon, int step, int length)
        {
            var attacks = weapon.Attacks;
            if (attacks == null || attacks.Length == 0)
                return new AttackSpec
                {
                    Shape = "arc",
                    Degrees = weapon.ConeDegrees,
                    Radius = weapon.Reach,
                    Type = weapon.DamageType ?? DamageTypes.Blunt,
                    PowerMult = MeleeCombo.PowerMult(step, length),
                };
            if (attacks.Length == 1) return attacks[0];
            // The last entry is the finisher; the others cycle for the steps before it (a Lv20 4-hit combo).
            if (step >= length) return attacks[attacks.Length - 1];
            return attacks[(step - 1) % (attacks.Length - 1)];
        }
    }

    /// <summary>SYS-COMBAT-02 side effects on one combatant: bleed (slash), burn (heat) and poison (toxic, stacks to 3)
    /// at 8% of the hit per second for 4 s; three blunt hits each within the combo window of the last → a 1 s stun.
    /// Pure — the owner ticks it and applies the damage.</summary>
    public sealed class CombatStatus
    {
        public const float DotShare = 0.08f;
        public const float DotSeconds = 4f;
        public const int PoisonMaxStacks = 3;
        public const int StaggerHits = 3;
        public const float StunSeconds = 1f;

        readonly List<(string Type, float PerSecond, float Until)> _dots = new();
        int _staggerCount;
        float _lastBluntAt = float.NegativeInfinity;

        public bool Any => _dots.Count > 0;

        public int Stacks(string type)
        {
            var n = 0;
            foreach (var d in _dots) if (d.Type == type) n++;
            return n;
        }

        /// <summary>Records a hit of <paramref name="damage"/> (as dealt). <paramref name="stunned"/> is true when this
        /// hit completes a stagger.</summary>
        public void OnHit(string type, float damage, float now, out bool stunned)
        {
            stunned = false;
            switch (type)
            {
                case DamageTypes.Slash:
                case DamageTypes.Heat:
                    // Bleed and burn refresh rather than stack; the stronger hit wins.
                    var existing = _dots.FindIndex(d => d.Type == type);
                    var entry = (type, damage * DotShare, now + DotSeconds);
                    if (existing < 0) _dots.Add(entry);
                    else _dots[existing] = (type, Mathf.Max(_dots[existing].PerSecond, entry.Item2), entry.Item3);
                    break;
                case DamageTypes.Toxic:
                    if (Stacks(type) >= PoisonMaxStacks) _dots.RemoveAt(_dots.FindIndex(d => d.Type == type)); // oldest goes
                    _dots.Add((type, damage * DotShare, now + DotSeconds));
                    break;
                case DamageTypes.Blunt:
                    _staggerCount = now - _lastBluntAt <= MeleeCombo.WindowSeconds ? _staggerCount + 1 : 1;
                    _lastBluntAt = now;
                    if (_staggerCount >= StaggerHits)
                    {
                        stunned = true;
                        _staggerCount = 0;
                    }
                    break;
            }
        }

        /// <summary>Damage the running effects deal over <paramref name="dt"/> ending at <paramref name="now"/>.</summary>
        public float Tick(float now, float dt)
        {
            var total = 0f;
            for (var i = _dots.Count - 1; i >= 0; i--)
            {
                var d = _dots[i];
                var start = now - dt;
                var active = Mathf.Clamp(d.Until - start, 0f, dt);
                total += d.PerSecond * active;
                if (now >= d.Until) _dots.RemoveAt(i);
            }
            return total;
        }

        public void Clear()
        {
            _dots.Clear();
            _staggerCount = 0;
        }
    }
}
