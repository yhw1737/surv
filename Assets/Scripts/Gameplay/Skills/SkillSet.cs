using System.Collections.Generic;
using Isle.Core.Ids;
using Isle.Data;

namespace Isle.Gameplay.Skills
{
    /// <summary>
    /// Server-authoritative skill levels for one player (SYS-SKILL-01). The tracked set is
    /// whatever <c>DefRegistry</c> has loaded as <see cref="SkillDef"/>, not a fixed array — a
    /// mod adding a skill needs no change here (T-018).
    /// </summary>
    public sealed class SkillSet
    {
        readonly Dictionary<NamespacedId, int> _levels = new();

        public SkillSet(IEnumerable<SkillDef> skills)
        {
            foreach (var skill in skills) _levels[skill.Id] = 1; // everyone starts at level 1
        }

        /// <summary>1 for a skill this set doesn't know about, rather than throwing — a skill a mod removed mid-session shouldn't crash a lookup.</summary>
        public int LevelOf(NamespacedId skillId) => _levels.TryGetValue(skillId, out var level) ? level : 1;

        public void SetLevel(NamespacedId skillId, int level) =>
            _levels[skillId] = level < 1 ? 1 : level > XpCurve.MaxLevel ? XpCurve.MaxLevel : level;
    }
}
