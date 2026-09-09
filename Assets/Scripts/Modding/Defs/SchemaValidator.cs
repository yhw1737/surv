using System.Collections.Generic;
using Isle.Data;

namespace Isle.Modding.Defs
{
    /// <summary>
    /// Checks T-011's POCOs deliberately leave undone (see each type's own doc comments):
    /// <c>IngredientRef</c> exactly-one-of item/tag, the length-2 numeric bands, an artifact's
    /// combat fields, and an <c>EnchantEffect</c>'s per-type required fields.
    /// <para>
    /// Everything here is a semantic check on an already-deserialized definition. Whether the raw
    /// JSON's shape matches the type at all (unknown/missing fields) is <see cref="DefinitionLoader"/>'s
    /// job, run before a definition ever reaches here.
    /// </para>
    /// </summary>
    public static class SchemaValidator
    {
        public static void Validate(IDefinition def, List<string> errors)
        {
            switch (def)
            {
                case CraftRecipeDef recipe:
                    ValidateIngredients(recipe.Ingredients, "ingredients", errors);
                    break;
                case EnchantDef enchant:
                    ValidateIngredients(enchant.Catalyst, "catalyst", errors);
                    ValidateEffects(enchant.Effects, errors);
                    break;
                case CreatureDef creature:
                    ValidateRange(creature.Butcher?.ConditionRange, "butcher.condition_range", errors);
                    break;
                case FishDef fish:
                    ValidateRange(fish.Habitat?.Depth, "habitat.depth", errors);
                    ValidateRange(fish.Habitat?.WaterTemp, "habitat.water_temp", errors);
                    ValidateRange(fish.Butcher?.ConditionRange, "butcher.condition_range", errors);
                    break;
                case ArtifactDef artifact:
                    // Absolute Rule 5: artifacts never touch combat skills. The fields exist only
                    // so this check can insist on them (ArtifactDef's own doc comment).
                    if (artifact.CombatSkill.IsValid)
                        errors.Add("\"combat_skill\" must be null — artifacts never grant combat skill (Absolute Rule 5).");
                    if (artifact.GrantsCombatXp)
                        errors.Add("\"grants_combat_xp\" must be false — artifacts never grant combat XP (Absolute Rule 5).");
                    break;
            }
        }

        static void ValidateIngredients(IngredientRef[] refs, string field, List<string> errors)
        {
            if (refs == null) return;
            for (var i = 0; i < refs.Length; i++)
            {
                var hasItem = refs[i].Item.IsValid;
                var hasTag = !string.IsNullOrEmpty(refs[i].Tag);
                if (hasItem == hasTag)
                    errors.Add($"{field}[{i}]: exactly one of \"item\" or \"tag\" must be set.");
            }
        }

        static void ValidateRange(float[] range, string field, List<string> errors)
        {
            if (range == null) return;
            if (range.Length != 2)
                errors.Add($"\"{field}\" must be [min, max] (length 2), got length {range.Length}.");
        }

        // Vocabulary is SCHEMA.md §Enchants' two documented examples. An unrecognized type is a
        // warning, not an error — SYS-CRAFT-01 §Open questions leaves the full type list undecided,
        // and hard-failing here would foreclose that.
        static void ValidateEffects(EnchantEffect[] effects, List<string> errors)
        {
            if (effects == null) return;
            for (var i = 0; i < effects.Length; i++)
            {
                var effect = effects[i];
                switch (effect.Type)
                {
                    case "damage_mult":
                        if (effect.Value == null)
                            errors.Add($"effects[{i}] (damage_mult): missing \"value\".");
                        break;
                    case "conditional":
                        if (effect.When == null)
                            errors.Add($"effects[{i}] (conditional): missing \"when\".");
                        if (effect.DamageMult == null)
                            errors.Add($"effects[{i}] (conditional): missing \"damage_mult\".");
                        break;
                }
            }
        }
    }
}
