using System;
using System.Collections.Generic;
using Isle.Gameplay.Inventory;

namespace Isle.Gameplay.Crafting
{
    /// <summary>SYS-CRAFT-01 §Quality tiers, lowest first.</summary>
    public enum QualityTier
    {
        Crude,
        Common,
        Fine,
        Superior,
        Master,
    }

    /// <summary>
    /// SYS-CRAFT-01 §Quality: the crafter's own level, the inputs, the station and the minigame give a score; the score
    /// gives a tier; the tier scales the item's power, durability and enchant slots. Static pure.
    /// </summary>
    public static class QualityCalculator
    {
        // SYS-CRAFT-01 §Quality constants.
        const float ScoreBase = 0.20f;
        const float LevelWeight = 0.45f;
        const float MaterialWeight = 0.15f;
        const float StationWeight = 0.10f;
        const float MinigameWeight = 0.10f;
        const float MaxLevel = 50f;

        /// <summary>Purity of an input that has no quality (raw materials). [invented — spec open question]</summary>
        public const float UnratedPurity = 0.5f;

        /// <summary>Minigame score until the forging minigame (T-091) exists. [invented]</summary>
        public const float NoMinigameScore = 0.5f;

        static readonly float[] Thresholds = { 0.30f, 0.55f, 0.75f, 0.90f };
        static readonly float[] Power = { 0.70f, 1.00f, 1.15f, 1.30f, 1.50f };
        static readonly int[] Slots = { 0, 1, 2, 3, 4 };
        static readonly float[] Durability = { 0.60f, 1.00f, 1.30f, 1.70f, 2.20f };

        public static float Score(int craftingLevel, float materialPurity, float stationTier, float minigameScore) =>
            Math.Clamp(ScoreBase
                       + LevelWeight * (craftingLevel / MaxLevel)
                       + MaterialWeight * Math.Clamp(materialPurity, 0f, 1f)
                       + StationWeight * Math.Clamp(stationTier, 0f, 1f)
                       + MinigameWeight * Math.Clamp(minigameScore, 0f, 1f), 0f, 1f);

        public static QualityTier TierFor(float score)
        {
            var tier = 0;
            while (tier < Thresholds.Length && score >= Thresholds[tier] - 1e-6f) tier++;
            return (QualityTier)tier;
        }

        /// <summary>Weapon power, tool speed and armor protection. An unrated item counts as Common.</summary>
        public static float PowerMult(QualityTier? tier) => Power[(int)(tier ?? QualityTier.Common)];

        public static float DurabilityMult(QualityTier? tier) => Durability[(int)(tier ?? QualityTier.Common)];

        public static int EnchantSlots(QualityTier? tier) => Slots[(int)(tier ?? QualityTier.Common)];

        /// <summary>The power multiplier of a worn item (Common when it has no wear or no quality).</summary>
        public static float PowerOf(ItemWear wear) => PowerMult(wear?.Quality);

        public static int MaxDurability(int baseDurability, QualityTier tier) =>
            Math.Max(1, (int)Math.Floor(baseDurability * DurabilityMult(tier) + 0.5f));

        /// <summary>Crude 0 … Master 1. [invented — spec open question "mean quality of materials"]</summary>
        public static float PurityOf(QualityTier? tier) => tier is { } t ? (int)t / 4f : UnratedPurity;

        /// <summary>The mean purity of the inputs consumed; no inputs reads as unrated.</summary>
        public static float MaterialPurity(IEnumerable<QualityTier?> inputs)
        {
            var sum = 0f;
            var count = 0;
            foreach (var tier in inputs)
            {
                sum += PurityOf(tier);
                count++;
            }
            return count == 0 ? UnratedPurity : sum / count;
        }
    }
}
