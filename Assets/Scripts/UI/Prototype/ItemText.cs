using System.Linq;
using Isle.Core.Ids;
using Isle.Data;
using Isle.Gameplay.Character;
using Isle.Gameplay.Cooking;
using Isle.Gameplay.Gathering;
using Isle.Modding.Defs;

namespace Isle.UI.Prototype
{
    /// <summary>
    /// SYS-FORAGE-01 §Identification: the name an item shows to the local player. Below the level its <c>identify</c>
    /// asks, a berry or mushroom shows as its look ("Unknown red berry") and what it does stays hidden; a dish made from
    /// something unidentified is a "Questionable dish". Display only.
    /// </summary>
    public static class ItemText
    {
        static int Level(NamespacedId skill) => PlayerInteraction.Local is { } player ? player.LevelOf(skill) : 0;

        public static bool Known(ItemDef item)
        {
            if (item == null) return true;
            if (DishFactory.IsDish(item))
                return DishFactory.IngredientsOf(item).All(id =>
                    !NamespacedId.TryParse(id, out var parsed, out _) || !DefRegistry.TryGet<ItemDef>(parsed, out var part) || Identification.Known(part, Level));
            return Identification.Known(item, Level);
        }

        public static string Name(ItemDef item)
        {
            if (item == null) return "";
            if (Known(item)) return Lang.Get(item.Name);
            return Lang.Get(DishFactory.IsDish(item) ? "@item.unknown_dish" : item.Identify?.UnknownName ?? "@item.unknown_dish");
        }

        /// <summary>"Gathering Lv 10 to tell what it is" for an item the player can't identify; null otherwise.</summary>
        public static string IdentifyHint(ItemDef item)
        {
            if (item?.Identify == null || Known(item)) return null;
            var skill = DefRegistry.TryGet<SkillDef>(item.Identify.Skill, out var def) ? Lang.Get(def.Name) : item.Identify.Skill.Name;
            return string.Format(Lang.Get("@ui.identify_needed"), skill, item.Identify.Level);
        }
    }
}
