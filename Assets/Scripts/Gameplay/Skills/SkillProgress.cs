using System.Collections.Generic;
using Isle.Core.Ids;
using Isle.Data;

namespace Isle.Gameplay.Skills
{
    /// <summary>
    /// One player's earned XP per skill and the levels it buys along <see cref="XpCurve"/> (SYS-SKILL-01). Wraps
    /// the existing <see cref="SkillSet"/>, which stays the level source the focus formula reads. Server-side
    /// state; pure, no Unity refs.
    /// </summary>
    public sealed class SkillProgress
    {
        readonly Dictionary<NamespacedId, double> _xp = new();

        public SkillProgress(IEnumerable<SkillDef> skills)
        {
            Skills = new List<SkillDef>(skills);
            Levels = new SkillSet(Skills);
            foreach (var skill in Skills) _xp[skill.Id] = 0;
        }

        public IReadOnlyList<SkillDef> Skills { get; }
        public SkillSet Levels { get; }

        public static float Amount(XpAward award, float units) => award == null ? 0f : award.Base + award.PerUnit * units;

        public int Level(NamespacedId skill) => Levels.LevelOf(skill);
        public double TotalXp(NamespacedId skill) => _xp.GetValueOrDefault(skill);

        /// <summary>Adds XP and returns the resulting level. A skill not in the roster is ignored (a removed mod's skill).</summary>
        public int AddXp(NamespacedId skill, double xp)
        {
            if (!_xp.ContainsKey(skill) || xp <= 0) return Level(skill);
            _xp[skill] += xp;
            Levels.SetLevel(skill, LevelFor(_xp[skill]));
            return Level(skill);
        }

        /// <summary>Loading a save.</summary>
        public void Restore(NamespacedId skill, double totalXp)
        {
            if (!_xp.ContainsKey(skill)) return;
            _xp[skill] = totalXp;
            Levels.SetLevel(skill, LevelFor(totalXp));
        }

        /// <summary>0..1 through the current level; 1 at the level cap.</summary>
        public float ProgressToNext(NamespacedId skill)
        {
            var level = Level(skill);
            if (level >= XpCurve.MaxLevel) return 1f;
            var into = TotalXp(skill) - XpCurve.TotalXpTo(level);
            return (float)(into / XpCurve.XpToNext(level));
        }

        static int LevelFor(double totalXp)
        {
            var level = 1;
            while (level < XpCurve.MaxLevel && totalXp >= XpCurve.TotalXpTo(level + 1)) level++;
            return level;
        }
    }
}
