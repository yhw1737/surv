using System.Collections.Generic;
using System.Linq;
using Isle.Core;
using Isle.Data;
using Isle.Gameplay.Building;
using Isle.Gameplay.Crafting;
using Isle.Gameplay.Inventory;
using Isle.Modding.Defs;
using UnityEngine;

namespace Isle.UI.Prototype
{
    /// <summary>SYS-CRAFT-01 §Enchanting window, opened with E at an enchanting table: the gear you carry or wear that
    /// can take an enchant (slots used / slots from its quality), and for the one picked, every enchant it accepts —
    /// what it does, the level it needs, the catalyst, the odds — with a button that asks the server to try.</summary>
    public sealed partial class PrototypeHud
    {
        bool _enchantOpen;
        EnchantingTable _enchantTable;
        string _enchantSlot;
        int _enchantContainer = -1;
        Vec2Int _enchantAt;
        Vector2 _enchantScroll;

        void OnEnchantOpened(object table)
        {
            _enchantTable = table as EnchantingTable;
            _enchantOpen = _enchantTable != null;
        }

        void TickEnchantWindow()
        {
            if (!_enchantOpen) return;
            if (_enchantTable == null || _player == null
                || Vector2.Distance(_player.transform.position, _enchantTable.transform.position) > Isle.Gameplay.Character.PlayerInteraction.ReachTiles + 0.5f)
                _enchantOpen = false;
        }

        readonly struct EnchantCandidate
        {
            public readonly ItemDef Item;
            public readonly ItemWear Wear;
            public readonly string Slot;
            public readonly int Container;
            public readonly Vec2Int At;

            public EnchantCandidate(ItemDef item, ItemWear wear, string slot, int container, Vec2Int at)
            {
                Item = item;
                Wear = wear;
                Slot = slot;
                Container = container;
                At = at;
            }

            public int Slots => QualityCalculator.EnchantSlots(Wear?.Quality);
            public int Used => Wear?.Enchants.Count ?? 0;
        }

        List<EnchantCandidate> EnchantCandidates()
        {
            var list = new List<EnchantCandidate>();
            foreach (var slot in EquipSlots.All)
                if (_inventory.Slots.Get(slot) is { Durability: > 0 } item && QualityCalculator.EnchantSlots(_inventory.Slots.WearOf(slot)?.Quality) > 0)
                    list.Add(new EnchantCandidate(item, _inventory.Slots.WearOf(slot), slot, -1, default));
            var containers = _inventory.Containers();
            for (var c = 0; c < containers.Count; c++)
                foreach (var placed in containers[c].Placements)
                    if (placed.Item.Durability is > 0 && QualityCalculator.EnchantSlots(placed.Wear?.Quality) > 0)
                        list.Add(new EnchantCandidate(placed.Item, placed.Wear, null, c, placed.Position));
            return list;
        }

        void DrawEnchantWindow()
        {
            var st = UiTheme.Styles;
            var enchants = DefRegistry.All<EnchantDef>().OrderBy(e => e.EnchantingSkillRequired).ToList();
            var skill = enchants.FirstOrDefault()?.Skill ?? default;
            var level = skill.IsValid ? _player.LevelOf(skill) : 0;
            var rect = Window(660f, 430f, 0f, Lang.Get("@ui.enchant_title"), skill.IsValid ? $"{SkillName(skill)} Lv {level}" : null, ref _enchantOpen);
            var candidates = EnchantCandidates();
            var list = new Rect(rect.x + 12f, rect.y + WindowHeaderPx + 8f, 250f, rect.height - WindowHeaderPx - 20f);
            GUI.Box(list, GUIContent.none, st.Inset);
            if (candidates.Count == 0)
            {
                GUI.Label(new Rect(list.x + 10f, list.y + 10f, list.width - 20f, 40f), Lang.Get("@ui.enchant_none"), st.Muted);
                return;
            }
            var picked = candidates.FirstOrDefault(c => c.Slot != null ? c.Slot == _enchantSlot : c.Container == _enchantContainer && c.At.Equals(_enchantAt));
            if (picked.Item == null)
            {
                picked = candidates[0];
                (_enchantSlot, _enchantContainer, _enchantAt) = (picked.Slot, picked.Container, picked.At);
            }
            const float row = 46f;
            var view = new Rect(0, 0, list.width - 20f, candidates.Count * (row + 4f));
            _enchantScroll = GUI.BeginScrollView(new Rect(list.x + 4f, list.y + 4f, list.width - 6f, list.height - 8f), _enchantScroll, view, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);
            var y = 0f;
            foreach (var candidate in candidates)
            {
                var r = new Rect(0, y, view.width, row);
                var selected = candidate.Slot != null ? candidate.Slot == _enchantSlot : candidate.Slot == null && _enchantSlot == null && candidate.Container == _enchantContainer && candidate.At.Equals(_enchantAt);
                if (GUI.Button(r, GUIContent.none, selected ? st.ButtonOn : st.Button))
                    (_enchantSlot, _enchantContainer, _enchantAt) = (candidate.Slot, candidate.Container, candidate.At);
                DrawIcon(new Rect(r.x + 4f, r.y + 5f, 36f, 36f), Isle.UI.Art.ItemIcons.Texture(candidate.Item));
                GUI.Label(new Rect(r.x + 46f, r.y + 4f, r.width - 50f, 20f), Lang.Get(candidate.Item.Name), st.Text);
                GUI.Label(new Rect(r.x + 46f, r.y + 23f, r.width - 50f, 18f), string.Format(Lang.Get("@ui.enchant_slots"), candidate.Used, candidate.Slots), st.Small);
                y += row + 4f;
            }
            GUI.EndScrollView();

            // The picked item's enchants.
            var right = new Rect(list.xMax + 12f, list.y, rect.xMax - list.xMax - 24f, list.height);
            GUI.Box(right, GUIContent.none, st.Inset);
            var stock = CraftingCalculator.StockOf(_inventory.Containers());
            var applied = picked.Wear?.Enchants ?? new List<Isle.Core.Ids.NamespacedId>();
            if (applied.Count > 0)
                GUI.Label(new Rect(right.x + 10f, right.y + 6f, right.width - 20f, 20f),
                    $"{Lang.Get("@ui.enchants")}: {string.Join(", ", applied.GroupBy(id => id).Select(g => Lang.Get(DefRegistry.Get<EnchantDef>(g.Key).Name) + (g.Count() > 1 ? $" ×{g.Count()}" : "")))}", st.Small);
            var ey = right.y + 30f;
            foreach (var enchant in enchants.Where(e => EnchantCalculator.Applies(e, picked.Item.Tags)))
            {
                var box = new Rect(right.x + 8f, ey, right.width - 16f, 78f);
                GUI.Box(box, GUIContent.none, st.Slot);
                var refusal = EnchantCalculator.Check(enchant, picked.Item.Tags, picked.Slots, applied, level);
                var catalyst = enchant.Catalyst ?? System.Array.Empty<IngredientRef>();
                var hasCatalyst = CraftingCalculator.HasIngredients(catalyst, stock);
                GUI.Label(new Rect(box.x + 8f, box.y + 4f, box.width - 120f, 20f), $"{Lang.Get(enchant.Name)}  —  {Lang.Get("@enchant_desc." + enchant.Id.Name)}", st.Text);
                var needs = string.Join(", ", catalyst.Select(c => $"{(DefRegistry.TryGet<ItemDef>(c.Item, out var d) ? Lang.Get(d.Name) : c.Item.Value)} {CraftingCalculator.CountMatching(c, stock)}/{c.Count}"));
                GUI.Label(new Rect(box.x + 8f, box.y + 26f, box.width - 120f, 18f), needs, hasCatalyst ? st.Small : st.Muted);
                var levelText = $"Lv {enchant.EnchantingSkillRequired} · {string.Format(Lang.Get("@ui.enchant_chance"), Mathf.RoundToInt(EnchantCalculator.SuccessRate(level) * 100f))}";
                GUI.Label(new Rect(box.x + 8f, box.y + 46f, box.width - 120f, 18f), levelText, refusal == EnchantRefusal.SkillTooLow ? st.Muted : st.Small);
                var reason = refusal switch
                {
                    EnchantRefusal.NoFreeSlot => Lang.Get("@ui.enchant_no_slot"),
                    EnchantRefusal.MaxStack => Lang.Get("@ui.enchant_max"),
                    EnchantRefusal.SkillTooLow => Lang.Get("@ui.skill_too_low"),
                    _ => null,
                };
                GUI.enabled = refusal == EnchantRefusal.None && hasCatalyst;
                if (GUI.Button(new Rect(box.xMax - 108f, box.y + 20f, 100f, 34f), reason ?? Lang.Get("@ui.enchant_apply"), st.Button))
                {
                    if (picked.Slot != null) _player.RequestEnchantEquipped(picked.Slot, enchant.Id.Value);
                    else _player.RequestEnchantPlaced(picked.Container, picked.At, enchant.Id.Value);
                }
                GUI.enabled = true;
                ey += 84f;
            }
        }
    }
}
