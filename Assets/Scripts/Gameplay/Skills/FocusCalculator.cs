using System;
using System.Collections.Generic;
using Isle.Core.Ids;
using Isle.Data;

namespace Isle.Gameplay.Skills
{
    /// <summary>
    /// SYS-SKILL-01 §Focus. Static pure, no Unity refs — EditMode test target.
    /// <para>
    /// Generalised over any number of skills, not a fixed array: <see cref="SkillLevel"/> only
    /// carries a level and a pool, so a mod adding a ninth, tenth, ... skill needs no change here
    /// (BACKLOG T-018 — "not a rename job"). The interference weight <see cref="InterferenceWeight"/>
    /// is what makes that safe: a skill below <see cref="NoviceCap"/> contributes nothing, so
    /// modder-added skills the player never touches can't change anyone's focus.
    /// </para>
    /// </summary>
    public static class FocusCalculator
    {
        public const float FocusMin = 0.35f;
        public const float FocusMax = 2.00f;
        public const float FocusGamma = 0.70f;
        public const float CrossPoolFactor = 0.35f;
        public const int NoviceCap = 15;
        public const int AdeptCap = 35;

        public readonly struct SkillLevel
        {
            public readonly int Level;
            public readonly string Pool;

            public SkillLevel(int level, string pool)
            {
                Level = level;
                Pool = pool;
            }
        }

        /// <summary>w(L) — Novice below 16 contributes nothing, Adept counts half, Master counts full.</summary>
        public static float InterferenceWeight(int level) =>
            level <= NoviceCap ? 0f : level <= AdeptCap ? 0.5f : 1.0f;

        /// <summary>
        /// p(n) — 7-day median distinct active players, not concurrent (see <see cref="ActivityTracker"/>).
        /// n &lt;= 1 counts as solo; the formula has no defined value below 1.
        /// </summary>
        public static float ActivePlayerFactor(int activePlayers) => activePlayers switch
        {
            <= 1 => 0.35f,
            2 => 0.60f,
            3 => 0.80f,
            _ => 1.00f,
        };

        /// <summary>Focus_i for one skill, given every other skill's level and pool.</summary>
        public static float Focus(SkillLevel target, IEnumerable<SkillLevel> others, int activePlayers)
        {
            var p = ActivePlayerFactor(activePlayers);
            double denominator = target.Level;
            foreach (var other in others)
            {
                var c = other.Pool == target.Pool ? 1.0f : CrossPoolFactor;
                denominator += other.Level * InterferenceWeight(other.Level) * c * p;
            }
            return (float)(target.Level / denominator);
        }

        /// <summary>Focus_i read straight off a player's <see cref="SkillSet"/> and the loaded skill roster.</summary>
        public static float Focus(SkillSet skillSet, IReadOnlyList<SkillDef> allSkills, NamespacedId targetSkillId, int activePlayers)
        {
            var target = default(SkillLevel);
            var others = new List<SkillLevel>(allSkills.Count > 0 ? allSkills.Count - 1 : 0);
            foreach (var def in allSkills)
            {
                var level = new SkillLevel(skillSet.LevelOf(def.Id), def.Pool);
                if (def.Id == targetSkillId) target = level;
                else others.Add(level);
            }
            return Focus(target, others, activePlayers);
        }

        public static float Multiplier(float focus) =>
            FocusMin + (FocusMax - FocusMin) * (float)Math.Pow(focus, FocusGamma);
    }
}
