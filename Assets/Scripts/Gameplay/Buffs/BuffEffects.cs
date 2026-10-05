using System.Collections.Generic;
using Isle.Data;

namespace Isle.Gameplay.Buffs
{
    /// <summary>Reads active buffs' effects by <c>type</c> (SYS-BUFF-01). Multipliers multiply together, shifts add,
    /// and a type no active buff carries is neutral (1 or 0). Static pure.</summary>
    public static class BuffEffects
    {
        public static float Mult(IEnumerable<BuffDef> active, string type)
        {
            var result = 1f;
            foreach (var buff in active)
            foreach (var effect in buff.Effects ?? System.Array.Empty<BuffEffect>())
                if (effect.Type == type && effect.Value is { } value) result *= value;
            return result;
        }

        public static float Sum(IEnumerable<BuffDef> active, string type)
        {
            var result = 0f;
            foreach (var buff in active)
            foreach (var effect in buff.Effects ?? System.Array.Empty<BuffEffect>())
                if (effect.Type == type && effect.Value is { } value) result += value;
            return result;
        }
    }
}
