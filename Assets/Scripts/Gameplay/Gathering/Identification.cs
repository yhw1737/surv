using System;
using Isle.Core.Ids;
using Isle.Data;

namespace Isle.Gameplay.Gathering
{
    /// <summary>SYS-FORAGE-01 §Identification: whether someone with the given skill levels can tell what an item is.
    /// Static pure; the viewer's levels come in as a function so this stays free of player types.</summary>
    public static class Identification
    {
        public static bool Known(ItemDef item, Func<NamespacedId, int> levelOf) =>
            item?.Identify == null || !item.Identify.Skill.IsValid || levelOf(item.Identify.Skill) >= item.Identify.Level;
    }
}
