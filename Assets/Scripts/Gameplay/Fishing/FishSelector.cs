using System.Collections.Generic;
using System.Linq;
using Isle.Data;

namespace Isle.Gameplay.Fishing
{
    /// <summary>The conditions a cast is made under: where, how deep, how warm, when, and how skilled.</summary>
    public readonly struct FishingConditions
    {
        public FishingConditions(float depth, float waterTemp, string terrain, string time, int fishingLevel)
        {
            Depth = depth;
            WaterTemp = waterTemp;
            Terrain = terrain;
            Time = time;
            FishingLevel = fishingLevel;
        }

        public float Depth { get; }
        public float WaterTemp { get; }

        /// <summary>A water tag suffix, e.g. <c>"saltwater"</c> from the <c>water/saltwater</c> tag.</summary>
        public string Terrain { get; }

        /// <summary>Lower-case <c>DayPhase</c> name: <c>"dawn"</c>, <c>"day"</c>, <c>"dusk"</c>, <c>"night"</c>.</summary>
        public string Time { get; }

        public int FishingLevel { get; }
    }

    /// <summary>
    /// SYS-FISH-01 §Species selection. Static pure: builds the probability table over candidate fish and
    /// rolls one. Only the factors the spec names are here, and the ones it leaves open are marked where
    /// they're used.
    /// </summary>
    public static class FishSelector
    {
        /// <summary>Terrain mismatch 0.15 is the spec's own value. Depth and temperature mismatch use the same
        /// number — the spec says "falling off outside" without a curve, so this is an **[invented]** flat
        /// penalty, not a falloff.</summary>
        public const float MismatchFactor = 0.15f;

        /// <summary>SYS-FISH-01: time mismatch ×0.25.</summary>
        public const float TimeMismatchFactor = 0.25f;

        /// <summary>SYS-FISH-01: "baitAffinity … default 0.3". No bait item exists yet, so every cast uses the default.</summary>
        public const float DefaultBaitAffinity = 0.3f;

        /// <summary>SYS-FISH-01: "0.05 below min_skill, else 1.0" — the lucky catch.</summary>
        public const float BelowSkillFactor = 0.05f;

        public static float Weight(FishDef fish, FishingConditions conditions, string rig)
        {
            if (fish.RigAllowed == null || !fish.RigAllowed.Contains(rig)) return 0f;

            var weight = 1f;
            var habitat = fish.Habitat;
            if (habitat != null)
            {
                weight *= InBand(conditions.Depth, habitat.Depth) ? 1f : MismatchFactor;
                weight *= InBand(conditions.WaterTemp, habitat.WaterTemp) ? 1f : MismatchFactor;
                weight *= habitat.Terrain != null && habitat.Terrain.Contains(conditions.Terrain) ? 1f : MismatchFactor;
                weight *= habitat.Time == null || habitat.Time.Contains("any") || habitat.Time.Contains(conditions.Time) ? 1f : TimeMismatchFactor;
            }

            weight *= DefaultBaitAffinity;
            weight *= conditions.FishingLevel < fish.MinSkill ? BelowSkillFactor : 1f;
            return weight;
        }

        /// <summary>Roulette selection over the weights. <paramref name="roll01"/> is a uniform value in [0, 1).
        /// Returns null when no candidate is allowed on this rig.</summary>
        public static FishDef Select(IReadOnlyList<FishDef> candidates, FishingConditions conditions, string rig, float roll01)
        {
            var weights = candidates.Select(f => Weight(f, conditions, rig)).ToArray();
            var total = weights.Sum();
            if (total <= 0f) return null;

            var target = roll01 * total;
            var cumulative = 0f;
            FishDef last = null;
            for (var i = 0; i < candidates.Count; i++)
            {
                if (weights[i] <= 0f) continue;
                cumulative += weights[i];
                last = candidates[i];
                if (target < cumulative) return candidates[i];
            }
            return last;
        }

        /// <summary>Inclusive <c>[min, max]</c> band, as the JSON writes habitat ranges. A missing band
        /// doesn't restrict anything.</summary>
        static bool InBand(float value, float[] band) =>
            band == null || band.Length < 2 || (value >= band[0] && value <= band[1]);
    }
}
