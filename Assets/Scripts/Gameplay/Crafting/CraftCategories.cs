using Isle.Data;

namespace Isle.Gameplay.Crafting
{
    /// <summary>
    /// T-151 playtest: the craft window's tabs. A recipe's tab comes from what it makes — its output's tags and blocks,
    /// never its id — so a modded recipe lands in the right tab by itself. Static pure.
    /// </summary>
    public static class CraftCategories
    {
        public const string All = "all";
        public const string Tool = "tool";
        public const string Weapon = "weapon";
        public const string Gear = "gear";
        public const string Build = "build";
        public const string Material = "material";
        public const string Brew = "brew";
        public const string Medical = "medical";

        /// <summary>The tabs, in the order the window shows them.</summary>
        public static readonly string[] Tabs = { All, Tool, Weapon, Gear, Build, Material, Brew, Medical };

        public static string Of(ItemDef output)
        {
            if (output == null) return Material;
            if (output.Medicine != null) return Medical;
            if (Has(output, "brew")) return Brew;
            if (output.Places.IsValid) return Build;
            if (HasPrefix(output, "tool")) return Tool;
            if (HasPrefix(output, "weapon") || HasPrefix(output, "ammo")) return Weapon;
            if (output.Wear != null || output.Armor > 0f || output.BagGrid != null || output.BagCapacityKg > 0f || !string.IsNullOrEmpty(output.EquipSlot)) return Gear;
            return Material;
        }

        public static bool InTab(string tab, ItemDef output) => tab == All || Of(output) == tab;

        /// <summary>The search box: every word typed must appear in the name or the id (case ignored).</summary>
        public static bool MatchesSearch(string query, string name, string id)
        {
            if (string.IsNullOrWhiteSpace(query)) return true;
            var haystack = ((name ?? "") + " " + (id ?? "")).ToLowerInvariant();
            foreach (var word in query.ToLowerInvariant().Split(' ', System.StringSplitOptions.RemoveEmptyEntries))
                if (!haystack.Contains(word)) return false;
            return true;
        }

        static bool Has(ItemDef item, string tag) => item.Tags != null && System.Array.IndexOf(item.Tags, tag) >= 0;

        static bool HasPrefix(ItemDef item, string tag)
        {
            if (item.Tags == null) return false;
            foreach (var t in item.Tags)
                if (t == tag || t.StartsWith(tag + "/")) return true;
            return false;
        }
    }
}
