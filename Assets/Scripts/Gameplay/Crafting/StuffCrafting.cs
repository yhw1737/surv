using System.Collections.Generic;
using System.Linq;
using Isle.Core.Ids;
using Isle.Data;
using Isle.Modding.Defs;

namespace Isle.Gameplay.Crafting
{
    /// <summary>
    /// SYS-CRAFT-02 §Materials: the rules for crafting (and repairing) a template from a chosen material — which
    /// materials it accepts, how many units, where (a material's station overrides the recipe's), at what level, and
    /// which generated item comes out. Shared by the server and the craft window so both agree.
    /// </summary>
    public static class StuffCrafting
    {
        public static ItemDef Template(CraftRecipeDef recipe) =>
            recipe?.Stuff != null && recipe.Output != null && DefRegistry.TryGet<ItemDef>(recipe.Output.Item, out var t) ? t : null;

        /// <summary>Materials this recipe can use: the recipe's categories, or else the template's.</summary>
        public static IReadOnlyList<MaterialDef> MaterialsFor(CraftRecipeDef recipe)
        {
            var categories = recipe?.Stuff?.Categories ?? Template(recipe)?.Stuff?.Categories;
            if (categories == null) return new List<MaterialDef>();
            var spec = new StuffSpec { Categories = categories };
            return DefRegistry.All<MaterialDef>().Where(m => StuffVariants.Accepts(spec, m))
                .OrderBy(m => m.Tier).ThenBy(m => m.Id.Value).ToList();
        }

        public static int CountFor(CraftRecipeDef recipe) =>
            recipe?.Stuff == null ? 0 : recipe.Stuff.Count > 0 ? recipe.Stuff.Count : System.Math.Max(1, Template(recipe)?.Stuff?.Amount ?? 1);

        public static NamespacedId StationFor(CraftRecipeDef recipe, MaterialDef material) =>
            material != null && material.Station.IsValid ? material.Station : recipe.Station;

        /// <summary>The crafting level this needs: the recipe's own, raised to the material's.</summary>
        public static int LevelFor(CraftRecipeDef recipe, MaterialDef material)
        {
            var own = recipe.Skills?.Where(s => s.Primary).Select(s => s.Level).DefaultIfEmpty(1).Max() ?? 1;
            return System.Math.Max(own, material?.CraftLevel ?? 1);
        }

        /// <summary>The generated item a recipe makes from <paramref name="material"/>; null if it doesn't accept it.</summary>
        public static ItemDef OutputFor(CraftRecipeDef recipe, MaterialDef material)
        {
            if (recipe?.Stuff == null || material == null || !MaterialsFor(recipe).Contains(material)) return null;
            return DefRegistry.TryGet<ItemDef>(StuffVariants.IdFor(recipe.Output.Item, material.Id), out var item) ? item : null;
        }

        /// <summary>The recipe that makes <paramref name="item"/> (its template's recipe for a generated variant).</summary>
        public static CraftRecipeDef RecipeFor(ItemDef item)
        {
            if (item == null) return null;
            var made = item.StuffTemplate.IsValid ? item.StuffTemplate : item.Id;
            return DefRegistry.All<CraftRecipeDef>().FirstOrDefault(r => r.Output != null && r.Output.Item == made);
        }

        /// <summary>Everything a repair of <paramref name="item"/> takes: half the recipe's fixed inputs and half its
        /// material (rounded up).</summary>
        public static IngredientRef[] RepairCost(ItemDef item, CraftRecipeDef recipe)
        {
            var cost = RepairCalculator.Cost(recipe.Ingredients).ToList();
            if (recipe.Stuff != null && item.Material.IsValid && DefRegistry.TryGet<MaterialDef>(item.Material, out var material))
                cost.AddRange(RepairCalculator.Cost(new[] { new IngredientRef { Item = material.Item, Count = CountFor(recipe) } }));
            return cost.ToArray();
        }

        /// <summary>The skill a recipe's level requirement is on (its primary skill, else its XP skill).</summary>
        public static NamespacedId PrimarySkill(CraftRecipeDef recipe) =>
            recipe.Skills?.FirstOrDefault(s => s.Primary)?.Skill ?? recipe.Xp?.Skill ?? NamespacedId.Parse("isle:crafting");

        /// <summary>A recipe's inputs with the material's units added (a template recipe), or as-is.</summary>
        public static IngredientRef[] Needs(CraftRecipeDef recipe, MaterialDef material)
        {
            var needs = recipe.Ingredients ?? System.Array.Empty<IngredientRef>();
            return recipe.Stuff != null && material != null
                ? needs.Append(new IngredientRef { Item = material.Item, Count = CountFor(recipe) }).ToArray()
                : needs;
        }

        public static MaterialDef MaterialOf(ItemDef item) =>
            item != null && item.Material.IsValid && DefRegistry.TryGet<MaterialDef>(item.Material, out var m) ? m : null;
    }
}
