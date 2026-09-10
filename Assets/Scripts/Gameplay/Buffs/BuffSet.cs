using System.Collections.Generic;
using Isle.Core.Ids;
using Isle.Data;

namespace Isle.Gameplay.Buffs
{
    /// <summary>
    /// Per-character active buffs, keyed by <see cref="BuffDef.Id"/> (SYS-BUFF-01). Tracks
    /// activity and expiry only — applying an effect to a real gauge or formula is the consuming
    /// system's job (Vitals, combat sway, ...), none of which exist in code yet.
    /// </summary>
    public sealed class BuffSet
    {
        readonly Dictionary<NamespacedId, long> _expiresAtMin = new();

        /// <summary>Re-granting an already-active buff refreshes its expiry rather than stacking.</summary>
        public void Grant(BuffDef buff, long currentTimeMin) =>
            _expiresAtMin[buff.Id] = buff.DurationMin <= 0 ? 0 : currentTimeMin + buff.DurationMin;

        public void Clear(NamespacedId buffId) => _expiresAtMin.Remove(buffId);

        public bool IsActive(NamespacedId buffId, long currentTimeMin) =>
            _expiresAtMin.TryGetValue(buffId, out var expiresAt) && (expiresAt == 0 || expiresAt > currentTimeMin);

        public IEnumerable<NamespacedId> ActiveBuffIds(long currentTimeMin)
        {
            foreach (var (id, expiresAt) in _expiresAtMin)
                if (expiresAt == 0 || expiresAt > currentTimeMin) yield return id;
        }
    }
}
