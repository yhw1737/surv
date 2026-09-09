using Isle.Core.Util;
using Isle.Data;
using UnityEngine;

namespace Isle.UI
{
    /// <summary>
    /// ART_PIPELINE §Placeholders: an item icon falls back to "solid rounded rectangle, tag-derived
    /// colour" when <see cref="ItemDef.Icon"/> is null — every item today, since no icon art exists
    /// yet, and every mod, which never ships art for someone else's game.
    /// <para>
    /// Only items are wired up. Creatures, weapons, tiles and world objects are also in
    /// ART_PIPELINE's shape table, but none of them has an owning presentation class yet to attach
    /// a placeholder to — Phase 3/4/8 haven't started. <see cref="PlaceholderVisuals"/>'s shape
    /// primitives are ready for them when they do.
    /// </para>
    /// </summary>
    public static class PlaceholderIcons
    {
        const int IconSizePx = 32; // ART_PIPELINE §Style: one tile = 32px; an icon is one tile.

        public static Sprite ItemIcon(ItemDef def)
        {
            var color = PlaceholderVisuals.ColorForTags(def.Tags);
            var texture = PlaceholderVisuals.RoundedRect(IconSizePx, IconSizePx, color);
            return PlaceholderVisuals.AsSprite(texture, IconSizePx);
        }
    }
}
