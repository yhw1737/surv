using System;
using System.Collections.Generic;
using Isle.Core.Ids;
using Isle.Data;
using Isle.Gameplay.Inventory;
using Isle.Modding.Defs;

namespace Isle.Gameplay.Crafting
{
    /// <summary>Why an enchant can't go on an item right now.</summary>
    public enum EnchantRefusal { None, NotApplicable, NoFreeSlot, MaxStack, SkillTooLow }

    /// <summary>
    /// SYS-CRAFT-01 §Enchanting: the enchanter fills the slots the smith's quality tier made. Success is
    /// <c>0.60 + 0.008 × enchanting level</c> (certain at Lv 50); a failure costs only the catalyst, and within its slots an
    /// item is never destroyed. Effects stack by multiplying. Static pure (the <see cref="EnchantEffects"/> helpers below
    /// look applied enchants up in the registry).
    /// </summary>
    public static class EnchantCalculator
    {
        public const float BaseRate = 0.60f;
        public const float SkillRate = 0.008f;

        public static float SuccessRate(int enchantingLevel) => Math.Clamp(BaseRate + SkillRate * enchantingLevel, 0f, 1f);

        /// <summary>An item takes an enchant when one of its tags is an applicable tag or sits under it
        /// (<c>tool</c> covers <c>tool/axe</c>).</summary>
        public static bool Applies(EnchantDef enchant, string[] itemTags)
        {
            if (enchant?.ApplicableTags == null || itemTags == null) return false;
            foreach (var tag in itemTags)
            foreach (var wanted in enchant.ApplicableTags)
                if (tag == wanted || tag.StartsWith(wanted + "/", StringComparison.Ordinal)) return true;
            return false;
        }

        public static EnchantRefusal Check(EnchantDef enchant, string[] itemTags, int slots, IReadOnlyList<NamespacedId> applied, int enchantingLevel)
        {
            if (!Applies(enchant, itemTags)) return EnchantRefusal.NotApplicable;
            if (applied.Count >= slots) return EnchantRefusal.NoFreeSlot;
            var same = 0;
            foreach (var id in applied) if (id == enchant.Id) same++;
            if (enchant.MaxStack > 0 && same >= enchant.MaxStack) return EnchantRefusal.MaxStack;
            if (enchantingLevel < enchant.EnchantingSkillRequired) return EnchantRefusal.SkillTooLow;
            return EnchantRefusal.None;
        }

        /// <summary>Product of every <paramref name="type"/> effect's value over the applied enchants (1 with none).</summary>
        public static float Mult(IEnumerable<EnchantDef> applied, string type)
        {
            var mult = 1f;
            foreach (var enchant in applied)
            foreach (var effect in enchant?.Effects ?? Array.Empty<EnchantEffect>())
                if (effect.Type == type && effect.Value is { } value) mult *= value;
            return mult;
        }

        /// <summary>Chance a use or hit doesn't wear the item: 1 − Π(1 − chance) over <c>wear_skip_chance</c> effects.</summary>
        public static float SkipChance(IEnumerable<EnchantDef> applied)
        {
            var keep = 1f;
            foreach (var enchant in applied)
            foreach (var effect in enchant?.Effects ?? Array.Empty<EnchantEffect>())
                if (effect.Type == WearSkipChance && effect.Value is { } value) keep *= 1f - Math.Clamp(value, 0f, 1f);
            return 1f - keep;
        }

        // Effect types the game reads (SCHEMA §Enchants).
        public const string DamageMult = "damage_mult";
        public const string ArmorMult = "armor_mult";
        public const string ToolSpeedMult = "tool_speed_mult";
        public const string WearSkipChance = "wear_skip_chance";
    }

    /// <summary>The enchants on an item's wear record, resolved through the registry.</summary>
    public static class EnchantEffects
    {
        public static IEnumerable<EnchantDef> Of(ItemWear wear)
        {
            if (wear == null) yield break;
            foreach (var id in wear.Enchants)
                if (DefRegistry.TryGet<EnchantDef>(id, out var def)) yield return def;
        }

        public static float Mult(ItemWear wear, string type) => wear == null || wear.Enchants.Count == 0 ? 1f : EnchantCalculator.Mult(Of(wear), type);

        /// <summary>Whether this use is spared, for a roll in 0..1.</summary>
        public static bool SkipsWear(ItemWear wear, float roll) => wear != null && wear.Enchants.Count > 0 && roll < EnchantCalculator.SkipChance(Of(wear));
    }
}
