using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Isle.Core.Ids;
using Isle.Data;

namespace Isle.Modding.Defs
{
    /// <summary>
    /// SYS-CRAFT-02 §Materials: expands every template item (<c>stuff</c> set) into one real item per material it accepts
    /// — "isle:sword" × "isle:iron" → "isle:sword__iron" — with the material's factors applied, plus a matching weapon
    /// for weapon templates. Runs at load, before references are checked, so the variants are ordinary defs everywhere
    /// else (inventory, saves, recipes' outputs, repair). Pure data; no Unity.
    /// </summary>
    public static class StuffVariants
    {
        public const string Separator = "__";

        /// <summary>The variant id for a template made of a material.</summary>
        public static NamespacedId IdFor(NamespacedId template, NamespacedId material) =>
            NamespacedId.Parse($"{template.Namespace}:{template.Name}{Separator}{material.Name}");

        public static bool Accepts(StuffSpec stuff, MaterialDef material) =>
            stuff?.Categories != null && material.Categories != null && stuff.Categories.Intersect(material.Categories).Any();

        public static void Expand(LoadResult<ItemDef> items, LoadResult<WeaponDef> weapons, IReadOnlyList<MaterialDef> materials)
        {
            if (materials == null || materials.Count == 0) return;
            var weaponById = weapons.Definitions.ToDictionary(w => w.Id);
            foreach (var template in items.Definitions.Where(i => i.Stuff != null).ToList())
            foreach (var material in materials.Where(m => Accepts(template.Stuff, m)))
            {
                var id = IdFor(template.Id, material.Id);
                WeaponDef weaponVariant = null;
                if (template.Weapon.IsValid && weaponById.TryGetValue(template.Weapon, out var weapon))
                {
                    weaponVariant = WeaponVariant(weapon, material, id);
                    weapons.Definitions.Add(weaponVariant);
                    weapons.SourcePaths.Add("(generated)");
                }
                items.Definitions.Add(ItemVariant(template, material, id, weaponVariant));
                items.SourcePaths.Add("(generated)");
            }
        }

        static int HalfUp(float value) => (int)Math.Floor(value + 0.5f);

        static float PowerFactor(MaterialDef material, string type) =>
            type == "blunt" ? material.PowerBlunt : material.PowerSharp;

        static ItemDef ItemVariant(ItemDef template, MaterialDef material, NamespacedId id, WeaponDef weapon)
        {
            var variant = Copy(template);
            var amount = Math.Max(1, template.Stuff.Amount);
            Set(variant, nameof(ItemDef.Id), id);
            Set(variant, nameof(ItemDef.Name), $"@pattern.made_of|{material.Name}|{template.Name}");
            Set(variant, nameof(ItemDef.Tags), (template.Tags ?? Array.Empty<string>())
                .Concat(material.Categories.Select(c => "made_of/" + c)).Append("made_of/" + material.Id.Name).Distinct().ToArray());
            Set(variant, nameof(ItemDef.Weight), template.Weight + material.Mass * amount);
            if (template.Durability is > 0) Set(variant, nameof(ItemDef.Durability), (int?)Math.Max(1, HalfUp(template.Durability.Value * material.Durability)));
            Set(variant, nameof(ItemDef.Tier), material.Tier);
            if (template.ToolPower > 0f) Set(variant, nameof(ItemDef.ToolPower), material.ToolSpeed);
            if (template.AmmoPower > 0f) Set(variant, nameof(ItemDef.AmmoPower), template.AmmoPower * material.PowerSharp);
            if (template.Armor > 0f)
            {
                var a = template.Armor;
                Set(variant, nameof(ItemDef.Armor), Round1(a * material.ArmorSharp));
                Set(variant, nameof(ItemDef.ArmorTypes), new Dictionary<string, float>
                {
                    ["slash"] = Round1(a * material.ArmorSharp),
                    ["pierce"] = Round1(a * material.ArmorSharp),
                    ["blunt"] = Round1(a * material.ArmorBlunt),
                    ["heat"] = Round1(a * material.ArmorHeat),
                });
            }
            if (template.Warmth > 0f) Set(variant, nameof(ItemDef.Warmth), Round1(template.Warmth * material.Warmth));
            if (template.Wear != null) Set(variant, nameof(ItemDef.Wear), new WearSpec { Style = template.Wear.Style, Color = material.Color ?? template.Wear.Color });
            if (template.Hold != null)
                Set(variant, nameof(ItemDef.Hold), new HoldSpec { Style = template.Hold.Style, Length = template.Hold.Length, Color = template.Hold.Color, Tip = material.Color ?? template.Hold.Tip });
            if (template.IconStyle != null)
                Set(variant, nameof(ItemDef.IconStyle), new IconSpec { Shape = template.IconStyle.Shape, Color = template.IconStyle.Color, Accent = material.Color ?? template.IconStyle.Accent });
            if (weapon != null) Set(variant, nameof(ItemDef.Weapon), weapon.Id);
            Set(variant, nameof(ItemDef.Stuff), null);
            Set(variant, nameof(ItemDef.StuffTemplate), template.Id);
            Set(variant, nameof(ItemDef.Material), material.Id);
            return variant;
        }

        static WeaponDef WeaponVariant(WeaponDef template, MaterialDef material, NamespacedId id)
        {
            var variant = Copy(template);
            var primary = template.DamageType ?? template.Attacks?.FirstOrDefault()?.Type ?? "blunt";
            var primaryFactor = PowerFactor(material, primary);
            Set(variant, nameof(WeaponDef.Id), id);
            Set(variant, nameof(WeaponDef.Name), $"@pattern.made_of|{material.Name}|{template.Name}");
            Set(variant, nameof(WeaponDef.BasePower), (float)HalfUp(template.BasePower * primaryFactor));
            if (template.Durability > 0) Set(variant, nameof(WeaponDef.Durability), Math.Max(1, HalfUp(template.Durability * material.Durability)));
            if (template.Attacks != null && primaryFactor > 0f)
                Set(variant, nameof(WeaponDef.Attacks), template.Attacks.Select(a =>
                {
                    // Each step keeps its share of the weapon's power, rescaled for how this material hits that way.
                    var step = Copy(a);
                    Set(step, nameof(AttackSpec.PowerMult), a.PowerMult * PowerFactor(material, a.Type) / primaryFactor);
                    return step;
                }).ToArray());
            return variant;
        }

        static float Round1(float value) => (float)Math.Round(value, 1);

        // Defs are init-only to code; the loader's own reload path writes them by reflection the same way.
        static T Copy<T>(T source) where T : class, new()
        {
            var copy = new T();
            foreach (var property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                if (property.CanRead && property.SetMethod != null)
                    property.SetValue(copy, property.GetValue(source));
            return copy;
        }

        static void Set(object target, string property, object value) =>
            target.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance)!.SetValue(target, value);
    }
}
