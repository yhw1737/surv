using System.Collections.Generic;
using System.IO;
using Isle.Data;

namespace Isle.Modding.Defs
{
    /// <summary>
    /// SYS-CORE-01 §Load pipeline steps 4/5/7/9/10 for Isle's own base content — one content root,
    /// no mods. Multi-mod orchestration (scan, dependency order, patches — steps 1–3/6) is T-130's
    /// <c>ModLoader</c>/<c>LoadOrderResolver</c>/<c>PatchApplier</c>, not this; <see cref="Load"/> is
    /// what that pipeline will eventually call per mod, not a replacement for it.
    /// <para>
    /// The eleven types are named explicitly rather than looped over via reflection, matching
    /// <see cref="ReferenceResolver"/>'s style — there's no common base beyond <see cref="IDefinition"/>
    /// to loop over generically without one.
    /// </para>
    /// </summary>
    public static class DefinitionBootstrap
    {
        public static IReadOnlyList<LoadError> Load(string contentRoot)
        {
            var (defs, errors) = LoadAll(contentRoot);

            DefRegistry.Register(defs.Items);
            DefRegistry.Register(defs.Creatures);
            DefRegistry.Register(defs.Fish);
            DefRegistry.Register(defs.CookMethods);
            DefRegistry.Register(defs.Recipes);
            DefRegistry.Register(defs.Weapons);
            DefRegistry.Register(defs.Artifacts);
            DefRegistry.Register(defs.Enchants);
            DefRegistry.Register(defs.Crops);
            DefRegistry.Register(defs.Skills);
            DefRegistry.Register(defs.Buffs);
            DefRegistry.Freeze();

            return errors;
        }

        /// <summary>SYS-CORE-01 §Hot reload: same load + resolve, applied in place via <see cref="DefRegistry.Reload{T}"/>
        /// instead of <see cref="DefRegistry.Register{T}"/>/<see cref="DefRegistry.Freeze"/>.</summary>
        public static IReadOnlyList<LoadError> Reload(string contentRoot)
        {
            var (defs, errors) = LoadAll(contentRoot);

            DefRegistry.Reload(defs.Items);
            DefRegistry.Reload(defs.Creatures);
            DefRegistry.Reload(defs.Fish);
            DefRegistry.Reload(defs.CookMethods);
            DefRegistry.Reload(defs.Recipes);
            DefRegistry.Reload(defs.Weapons);
            DefRegistry.Reload(defs.Artifacts);
            DefRegistry.Reload(defs.Enchants);
            DefRegistry.Reload(defs.Crops);
            DefRegistry.Reload(defs.Skills);
            DefRegistry.Reload(defs.Buffs);

            return errors;
        }

        static (Loaded, List<LoadError>) LoadAll(string contentRoot)
        {
            var defs = new Loaded
            {
                Items = DefinitionLoader.LoadAll<ItemDef>(Path.Combine(contentRoot, "items")),
                Creatures = DefinitionLoader.LoadAll<CreatureDef>(Path.Combine(contentRoot, "creatures")),
                Fish = DefinitionLoader.LoadAll<FishDef>(Path.Combine(contentRoot, "fish")),
                CookMethods = DefinitionLoader.LoadAll<CookMethodDef>(Path.Combine(contentRoot, "cook_methods")),
                Recipes = DefinitionLoader.LoadAll<CraftRecipeDef>(Path.Combine(contentRoot, "recipes")),
                Weapons = DefinitionLoader.LoadAll<WeaponDef>(Path.Combine(contentRoot, "weapons")),
                Artifacts = DefinitionLoader.LoadAll<ArtifactDef>(Path.Combine(contentRoot, "artifacts")),
                Enchants = DefinitionLoader.LoadAll<EnchantDef>(Path.Combine(contentRoot, "enchants")),
                Crops = DefinitionLoader.LoadAll<CropDef>(Path.Combine(contentRoot, "crops")),
                Skills = DefinitionLoader.LoadAll<SkillDef>(Path.Combine(contentRoot, "skills")),
                Buffs = DefinitionLoader.LoadAll<BuffDef>(Path.Combine(contentRoot, "buffs")),
            };

            ReferenceResolver.ResolveItemRefs(defs.Items, defs.Recipes, defs.Enchants, defs.Creatures, defs.Fish, defs.Crops, defs.CookMethods);

            var errors = new List<LoadError>();
            foreach (ILoadResult result in new ILoadResult[]
                     {
                         defs.Items, defs.Creatures, defs.Fish, defs.CookMethods, defs.Recipes,
                         defs.Weapons, defs.Artifacts, defs.Enchants, defs.Crops, defs.Skills, defs.Buffs
                     })
                errors.AddRange(result.Errors);

            return (defs, errors);
        }

        struct Loaded
        {
            public LoadResult<ItemDef> Items;
            public LoadResult<CreatureDef> Creatures;
            public LoadResult<FishDef> Fish;
            public LoadResult<CookMethodDef> CookMethods;
            public LoadResult<CraftRecipeDef> Recipes;
            public LoadResult<WeaponDef> Weapons;
            public LoadResult<ArtifactDef> Artifacts;
            public LoadResult<EnchantDef> Enchants;
            public LoadResult<CropDef> Crops;
            public LoadResult<SkillDef> Skills;
            public LoadResult<BuffDef> Buffs;
        }
    }
}
