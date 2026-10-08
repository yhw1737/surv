using System.Collections.Generic;
using System.Linq;
using Isle.Core.Ids;
using Isle.Data;
using Isle.Gameplay.Character;
using Isle.Gameplay.Cooking;
using Isle.Gameplay.Crafting;
using Isle.Modding.Defs;
using Isle.World.Objects;
using UnityEngine;

namespace Isle.UI.Prototype
{
    /// <summary>
    /// The cooking (K) and crafting (C) windows in the shared <see cref="UiTheme"/>: a list of methods/recipes on the
    /// left with why each is or isn't available, the selection's details on the right, and — for cooking — the pot,
    /// the pantry and a preview of what the dish will do before it's cooked. Everything shown is computed with the
    /// same rules the server applies (<see cref="CookingResolver"/>, <see cref="CraftingCalculator"/>).
    /// </summary>
    public sealed partial class PrototypeHud
    {
        const float WindowHeaderPx = 40f;
        const float ListWidthPx = 230f;
        const float ListRowPx = 50f;
        const float TilePx = 64f;
        string _craftRecipe;
        bool _repairTab;
        Vector2 _repairScroll;
        Vector2 _cookListScroll, _craftListScroll, _pantryScroll;

        Rect Window(float width, float height, float xBias, string title, string subtitle, ref bool open)
        {
            var st = UiTheme.Styles;
            var rect = new Rect(Mathf.Clamp((Screen.width - width) * 0.5f + xBias, Margin, Screen.width - width - Margin),
                Mathf.Max(Margin + 60f, (Screen.height - height) * 0.45f), width, height);
            if (Event.current.type == EventType.Layout) _panels.Add(rect);
            GUI.Box(rect, GUIContent.none, st.Window);
            var header = new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, WindowHeaderPx - 6f);
            GUI.Box(header, GUIContent.none, st.Header);
            GUI.Label(new Rect(header.x + 12f, header.y + 5f, 300f, 24f), title, st.Title);
            if (!string.IsNullOrEmpty(subtitle))
                GUI.Label(new Rect(header.xMax - 260f, header.y + 8f, 210f, 20f), subtitle, new GUIStyle(st.Muted) { alignment = TextAnchor.UpperRight });
            if (GUI.Button(new Rect(header.xMax - 32f, header.y + 6f, 24f, 22f), "×", st.Close)) open = false;
            return rect;
        }

        static void DrawIcon(Rect rect, Texture2D texture, bool dim = false)
        {
            if (texture == null) return;
            var old = GUI.color;
            GUI.color = dim ? new Color(0.55f, 0.55f, 0.55f, 0.8f) : Color.white;
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
            GUI.color = old;
        }

        static Color ColourFor(string key)
        {
            // A stable, pleasant hue per id — the method/recipe "icon" until art exists.
            var hash = (uint)(key ?? "").GetHashCode();
            return Color.HSVToRGB(hash % 360 / 360f, 0.45f, 0.82f);
        }

        static Color ItemColour(ItemDef item) => Isle.Core.Util.PlaceholderVisuals.ColorForTags(item?.Tags);

        string StationName(NamespacedId station) =>
            DefRegistry.TryGet<WorldObjectDef>(station, out var def) ? Lang.Get(def.Name) : station.Value;

        // ---------------------------------------------------------------- cooking

        void DrawCookWindow()
        {
            var st = UiTheme.Styles;
            var methods = DefRegistry.All<CookMethodDef>().Where(m => !m.EatRaw).ToList();
            var cooking = NamespacedId.Parse("isle:cooking");
            var rect = Window(720f, 470f, -140f, Lang.Get("@ui.cook_title"), $"{SkillName(cooking)} Lv {_player.LevelOf(cooking)}", ref _cookOpen);
            var position = _player.transform.position;

            // Method list.
            var list = new Rect(rect.x + 12f, rect.y + WindowHeaderPx + 8f, ListWidthPx, rect.height - WindowHeaderPx - 20f);
            GUI.Box(list, GUIContent.none, st.Inset);
            GUI.Label(new Rect(list.x + 10f, list.y + 6f, 200f, 18f), Lang.Get("@ui.cook_methods"), st.Muted);
            var view = new Rect(0, 0, list.width - 20f, methods.Count * (ListRowPx + 4f));
            _cookListScroll = GUI.BeginScrollView(new Rect(list.x + 6f, list.y + 26f, list.width - 8f, list.height - 32f), _cookListScroll, view, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);
            var y = 0f;
            foreach (var method in methods)
            {
                var level = method.UnlockSkill?.Level ?? 1;
                var locked = method.UnlockSkill != null && _player.LevelOf(method.UnlockSkill.Skill) < level;
                var stationOk = !method.Station.IsValid || WorldObjectRegistry.IsActiveNear(position, method.Station, PlayerInteraction.ReachTiles);
                string status;
                if (locked) status = UiTheme.Colour(string.Format(Lang.Get("@ui.needs_skill"), SkillName(method.UnlockSkill.Skill), level), UiTheme.Bad);
                else if (!stationOk) status = UiTheme.Colour(string.Format(Lang.Get("@ui.needs_near"), StationName(method.Station)), UiTheme.Bad);
                else status = UiTheme.Colour(string.Format(Lang.Get("@ui.cook_items"), method.Input.MinItems, method.Input.MaxItems), UiTheme.Good);

                var row = new Rect(0, y, view.width, ListRowPx);
                var selected = _cookMethod == method.Id.Value;
                if (GUI.Button(row, GUIContent.none, selected ? st.ButtonOn : st.Button))
                {
                    if (!selected) _cookPicks.Clear();
                    _cookMethod = method.Id.Value;
                }
                DrawIcon(new Rect(row.x + 4f, row.y + 3f, 44f, 44f), Isle.UI.Art.ItemIcons.MethodTexture(method), locked);
                GUI.Label(new Rect(row.x + 48f, row.y + 5f, row.width - 52f, 20f), Lang.Get(method.Name), st.Text);
                GUI.Label(new Rect(row.x + 48f, row.y + 25f, row.width - 52f, 20f), status, st.Small);
                y += ListRowPx + 4f;
            }
            GUI.EndScrollView();

            // Details.
            var right = new Rect(list.xMax + 12f, list.y, rect.xMax - list.xMax - 24f, list.height);
            var chosen = methods.FirstOrDefault(m => m.Id.Value == _cookMethod);
            if (chosen == null)
            {
                GUI.Label(new Rect(right.x + 10f, right.y + 80f, right.width - 20f, 60f), Lang.Get("@ui.cook_pick_method"), new GUIStyle(st.Muted) { alignment = TextAnchor.MiddleCenter });
                return;
            }

            var foods = _inventory.Containers().SelectMany(c => c.Placements).Where(p => p.Item.Nutrition != null)
                .GroupBy(p => p.Item.Id.Value).Select(g => (Item: g.First().Item, Count: g.Sum(p => p.Count))).ToList();
            var max = Mathf.Max(1, chosen.Input.MaxItems);

            // Pot: one slot per allowed ingredient.
            GUI.Label(new Rect(right.x, right.y, 300f, 20f), $"{Lang.Get("@ui.cook_pot")}  {_cookPicks.Count}/{max}", st.Big);
            var slotsY = right.y + 24f;
            for (var i = 0; i < max; i++)
            {
                var slot = new Rect(right.x + i * (TilePx + 8f), slotsY, TilePx, TilePx);
                if (i < _cookPicks.Count)
                {
                    var item = foods.FirstOrDefault(f => f.Item.Id.Value == _cookPicks[i]).Item;
                    if (GUI.Button(slot, GUIContent.none, st.SlotOn))
                    {
                        _cookPicks.RemoveAt(i);
                        break;
                    }
                    DrawIcon(new Rect(slot.x + 12f, slot.y + 2f, 40f, 40f), Isle.UI.Art.ItemIcons.Texture(item));
                    GUI.Label(new Rect(slot.x + 2f, slot.y + 40f, slot.width - 4f, 22f), item != null ? Lang.Get(item.Name) : _cookPicks[i], new GUIStyle(st.Small) { alignment = TextAnchor.UpperCenter, wordWrap = false, clipping = TextClipping.Clip });
                }
                else
                {
                    GUI.Box(slot, GUIContent.none, st.Slot);
                    GUI.Label(slot, "+", new GUIStyle(st.Muted) { alignment = TextAnchor.MiddleCenter, fontSize = 22 });
                }
            }

            // Pantry: what's in the bags that can go in.
            var pantryTop = slotsY + TilePx + 12f;
            GUI.Label(new Rect(right.x, pantryTop, 300f, 20f), Lang.Get("@ui.cook_pantry"), st.Muted);
            var pantry = new Rect(right.x, pantryTop + 20f, right.width, 150f);
            GUI.Box(pantry, GUIContent.none, st.Inset);
            const float tileW = 104f, tileH = 40f;
            var perRow = Mathf.Max(1, (int)((pantry.width - 12f) / (tileW + 6f)));
            var pantryView = new Rect(0, 0, pantry.width - 20f, Mathf.CeilToInt(foods.Count / (float)perRow) * (tileH + 6f));
            _pantryScroll = GUI.BeginScrollView(new Rect(pantry.x + 6f, pantry.y + 6f, pantry.width - 8f, pantry.height - 12f), _pantryScroll, pantryView, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);
            if (foods.Count == 0) GUI.Label(new Rect(0, 50f, pantryView.width, 30f), Lang.Get("@ui.cook_no_food"), new GUIStyle(st.Muted) { alignment = TextAnchor.MiddleCenter });
            for (var i = 0; i < foods.Count; i++)
            {
                var (item, count) = foods[i];
                var available = count - _cookPicks.Count(id => id == item.Id.Value);
                var tile = new Rect(i % perRow * (tileW + 6f), i / perRow * (tileH + 6f), tileW, tileH);
                GUI.enabled = available > 0 && _cookPicks.Count < max;
                if (GUI.Button(tile, GUIContent.none, st.Slot)) _cookPicks.Add(item.Id.Value);
                if (tile.Contains(Event.current.mousePosition)) _hover = item;
                GUI.enabled = true;
                DrawIcon(new Rect(tile.x + 2f, tile.y + 4f, 32f, 32f), Isle.UI.Art.ItemIcons.Texture(item));
                GUI.Label(new Rect(tile.x + 34f, tile.y + 3f, tile.width - 36f, 18f), Lang.Get(item.Name), new GUIStyle(st.Small) { normal = { textColor = UiTheme.Text }, wordWrap = false, clipping = TextClipping.Clip });
                GUI.Label(new Rect(tile.x + 34f, tile.y + 20f, tile.width - 36f, 18f), $"×{available}", st.Small);
            }
            GUI.EndScrollView();

            // Preview: exactly what the server will compute, minus the failure roll.
            var previewTop = pantry.yMax + 10f;
            var preview = new Rect(right.x, previewTop, right.width, right.yMax - previewTop - 44f);
            GUI.Box(preview, GUIContent.none, st.Inset);
            var level2 = _player.LevelOf(cooking);
            var items = _cookPicks.Select(id => foods.FirstOrDefault(f => f.Item.Id.Value == id).Item).Where(i => i != null).ToList();
            var countOk = CookingResolver.CountAllowed(chosen, items.Count);
            if (items.Count > 0)
            {
                var result = CookingResolver.Resolve(chosen, items, level2, failureRoll: 1f);
                var buffs = result.Buffs.Select(b => DefRegistry.TryGet<BuffDef>(b.Buff, out var def) ? Lang.Get(def.Name) : b.Buff.Value).ToList();
                var line1 = $"{Lang.Get("@ui.hunger")} {UiTheme.Colour($"+{result.Hunger:0.#}", UiTheme.Good)}   {Lang.Get("@ui.thirst")} {UiTheme.Colour($"{(result.Thirst >= 0 ? "+" : "")}{result.Thirst:0.#}", result.Thirst >= 0 ? UiTheme.Good : UiTheme.Bad)}";
                if (chosen.Modifiers != null && chosen.Modifiers.Preservation > 1f) line1 += $"   {Lang.Get("@ui.cook_keeps")} ×{chosen.Modifiers.Preservation:0.#}";
                var line2 = buffs.Count > 0 ? $"{Lang.Get("@ui.tt_buffs")}: {UiTheme.Colour(string.Join(", ", buffs), UiTheme.Accent)}" : Lang.Get("@ui.cook_no_buff");
                var fail = chosen.Failure != null ? Mathf.Clamp01(chosen.Failure.BaseRate - level2 * chosen.Failure.SkillReduction) : 0f;
                if (fail > 0f) line2 += $"   {UiTheme.Colour(string.Format(Lang.Get("@ui.cook_fail_chance"), Mathf.RoundToInt(fail * 100f)), UiTheme.Bad)}";
                // The dish as it will come out, then what it does.
                var art = new Rect(preview.x + 6f, preview.y + 4f, preview.height - 8f, preview.height - 8f);
                DrawIcon(art, Isle.UI.Art.ItemIcons.DishPreview(chosen, items));
                var textX = art.xMax + 8f;
                GUI.Label(new Rect(textX, preview.y + 6f, preview.xMax - textX - 8f, 18f), Lang.Get("@ui.cook_preview"), st.Muted);
                GUI.Label(new Rect(textX, preview.y + 24f, preview.xMax - textX - 8f, 20f), line1, st.Text);
                GUI.Label(new Rect(textX, preview.y + 44f, preview.xMax - textX - 8f, 36f), line2, st.Text);
            }
            else GUI.Label(new Rect(preview.x + 10f, preview.y + 20f, preview.width - 20f, 40f), Lang.Get("@ui.cook_add_food"), new GUIStyle(st.Muted) { alignment = TextAnchor.MiddleCenter });

            // Cook.
            var lockedNow = chosen.UnlockSkill != null && _player.LevelOf(chosen.UnlockSkill.Skill) < chosen.UnlockSkill.Level;
            var stationNow = !chosen.Station.IsValid || WorldObjectRegistry.IsActiveNear(position, chosen.Station, PlayerInteraction.ReachTiles);
            string reason = null;
            if (lockedNow) reason = string.Format(Lang.Get("@ui.needs_skill"), SkillName(chosen.UnlockSkill.Skill), chosen.UnlockSkill.Level);
            else if (!stationNow) reason = string.Format(Lang.Get("@ui.needs_near"), StationName(chosen.Station));
            else if (!countOk) reason = string.Format(Lang.Get("@ui.cook_items"), chosen.Input.MinItems, chosen.Input.MaxItems);
            var buttonRect = new Rect(right.xMax - 160f, right.yMax - 36f, 160f, 34f);
            if (reason != null) GUI.Label(new Rect(right.x, buttonRect.y + 8f, right.width - 170f, 20f), UiTheme.Colour(reason, UiTheme.Bad), st.Muted);
            GUI.enabled = reason == null;
            if (GUI.Button(buttonRect, Lang.Get("@ui.cook_now"), st.ButtonOn))
            {
                _player.RequestCook(_cookMethod, _cookPicks);
                _cookPicks.Clear();
            }
            GUI.enabled = true;
        }

        // ---------------------------------------------------------------- crafting

        void DrawCraftWindow()
        {
            var st = UiTheme.Styles;
            var recipes = DefRegistry.All<CraftRecipeDef>();
            var crafting = NamespacedId.Parse("isle:crafting");
            var rect = Window(640f, 470f, -160f, Lang.Get("@ui.craft_title"), $"{SkillName(crafting)} Lv {_player.LevelOf(crafting)}", ref _craftOpen);
            var stock = CraftingCalculator.StockOf(_inventory.Containers());
            var position = _player.transform.position;

            bool StationOk(CraftRecipeDef r) => !r.Station.IsValid || WorldObjectRegistry.IsActiveNear(position, r.Station, PlayerInteraction.ReachTiles);
            bool Ready(CraftRecipeDef r) => StationOk(r) && _player.MeetsSkills(r.Skills) && CraftingCalculator.HasIngredients(r.Ingredients, stock);

            // Tabs: make, or mend (SYS-CRAFT-02).
            var tabY = rect.y + WindowHeaderPx + 6f;
            if (GUI.Button(new Rect(rect.x + 12f, tabY, 120f, 28f), Lang.Get("@ui.craft_tab"), _repairTab ? st.Button : st.ButtonOn)) _repairTab = false;
            if (GUI.Button(new Rect(rect.x + 136f, tabY, 120f, 28f), Lang.Get("@ui.repair_tab"), _repairTab ? st.ButtonOn : st.Button)) _repairTab = true;
            var body = new Rect(rect.x, rect.y + 34f, rect.width, rect.height - 34f);
            if (_repairTab)
            {
                DrawRepairList(body, stock, StationOk);
                return;
            }

            // Recipe list: craftable ones first.
            var ordered = recipes.OrderByDescending(Ready).ThenBy(r => Lang.Get(r.Name)).ToList();
            var list = new Rect(body.x + 12f, body.y + WindowHeaderPx + 8f, ListWidthPx, body.height - WindowHeaderPx - 20f);
            GUI.Box(list, GUIContent.none, st.Inset);
            var view = new Rect(0, 0, list.width - 20f, ordered.Count * (ListRowPx + 4f));
            _craftListScroll = GUI.BeginScrollView(new Rect(list.x + 6f, list.y + 6f, list.width - 8f, list.height - 12f), _craftListScroll, view, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);
            var y = 0f;
            foreach (var recipe in ordered)
            {
                var output = DefRegistry.TryGet<ItemDef>(recipe.Output.Item, out var o) ? o : null;
                var ready = Ready(recipe);
                var row = new Rect(0, y, view.width, ListRowPx);
                var selected = _craftRecipe == recipe.Id.Value;
                if (GUI.Button(row, GUIContent.none, selected ? st.ButtonOn : st.Button)) _craftRecipe = recipe.Id.Value;
                DrawIcon(new Rect(row.x + 4f, row.y + 3f, 44f, 44f), Isle.UI.Art.ItemIcons.Texture(output), !ready);
                GUI.Label(new Rect(row.x + 48f, row.y + 5f, row.width - 52f, 20f), Lang.Get(recipe.Name), st.Text);
                var status = ready ? UiTheme.Colour(Lang.Get("@ui.craft_ready"), UiTheme.Good)
                    : !_player.MeetsSkills(recipe.Skills) ? UiTheme.Colour(Lang.Get("@ui.craft_locked"), UiTheme.Bad)
                    : !StationOk(recipe) ? UiTheme.Colour(string.Format(Lang.Get("@ui.needs_near"), StationName(recipe.Station)), UiTheme.Bad)
                    : UiTheme.Colour(Lang.Get("@ui.craft_missing"), UiTheme.Muted);
                GUI.Label(new Rect(row.x + 48f, row.y + 25f, row.width - 52f, 20f), status, st.Small);
                y += ListRowPx + 4f;
            }
            GUI.EndScrollView();

            var right = new Rect(list.xMax + 14f, list.y, body.xMax - list.xMax - 26f, list.height);
            var chosen = ordered.FirstOrDefault(r => r.Id.Value == _craftRecipe) ?? ordered.FirstOrDefault();
            if (chosen == null) return;
            _craftRecipe = chosen.Id.Value;
            var result = DefRegistry.TryGet<ItemDef>(chosen.Output.Item, out var r0) ? r0 : null;

            GUI.Box(new Rect(right.x, right.y, 56f, 56f), GUIContent.none, st.Slot);
            DrawIcon(new Rect(right.x + 2f, right.y + 2f, 52f, 52f), Isle.UI.Art.ItemIcons.Texture(result));
            GUI.Label(new Rect(right.x + 62f, right.y + 2f, right.width - 62f, 24f), $"{Lang.Get(chosen.Name)}{(chosen.Output.Count > 1 ? $"  ×{chosen.Output.Count}" : "")}", st.Title);
            GUI.Label(new Rect(right.x + 62f, right.y + 28f, right.width - 62f, 20f), ItemSummary(result), st.Muted);
            var hoverRect = new Rect(right.x, right.y, right.width, 52f);
            if (result != null && hoverRect.Contains(Event.current.mousePosition)) _hover = result;

            y = right.y + 66f;
            GUI.Label(new Rect(right.x, y, 200f, 20f), Lang.Get("@ui.ingredients"), st.Big);
            y += 24f;
            foreach (var need in chosen.Ingredients)
            {
                var have = stock.Where(line => CraftingCalculator.Matches(need, line)).Sum(line => line.Count);
                var item = need.Item.IsValid && DefRegistry.TryGet<ItemDef>(need.Item, out var d) ? d : null;
                var name = item != null ? Lang.Get(item.Name) : $"#{need.Tag}";
                var line = new Rect(right.x, y, right.width, 30f);
                GUI.Box(line, GUIContent.none, st.Slot);
                if (item != null) DrawIcon(new Rect(line.x + 3f, line.y + 1f, 28f, 28f), Isle.UI.Art.ItemIcons.Texture(item));
                else UiTheme.Badge(new Rect(line.x + 6f, line.y + 5f, 20f, 20f), Color.gray);
                GUI.Label(new Rect(line.x + 34f, line.y + 6f, line.width - 120f, 20f), name, st.Text);
                GUI.Label(new Rect(line.xMax - 90f, line.y + 6f, 82f, 20f), UiTheme.Colour($"{have} / {need.Count}", have >= need.Count ? UiTheme.Good : UiTheme.Bad), new GUIStyle(st.Text) { alignment = TextAnchor.UpperRight });
                y += 34f;
            }

            y += 6f;
            if (chosen.Station.IsValid)
            {
                GUI.Label(new Rect(right.x, y, right.width, 20f), $"{Lang.Get("@ui.craft_station")}: {UiTheme.Colour(StationName(chosen.Station), StationOk(chosen) ? UiTheme.Good : UiTheme.Bad)}", st.Text);
                y += 22f;
            }
            if (chosen.Skills != null)
                foreach (var need in chosen.Skills.Where(s => s.Level > 1))
                {
                    var ok = _player.LevelOf(need.Skill) >= need.Level;
                    GUI.Label(new Rect(right.x, y, right.width, 20f), $"{SkillName(need.Skill)} Lv {UiTheme.Colour(need.Level.ToString(), ok ? UiTheme.Good : UiTheme.Bad)}", st.Text);
                    y += 22f;
                }

            var buttonRect = new Rect(right.xMax - 160f, right.yMax - 36f, 160f, 34f);
            GUI.enabled = Ready(chosen);
            if (GUI.Button(buttonRect, Lang.Get("@ui.craft_now"), st.ButtonOn)) _player.RequestCraft(chosen.Id.Value);
            GUI.enabled = true;
        }

        /// <summary>SYS-CRAFT-02: every worn item carried or worn, its durability, what mending it costs, and where.</summary>
        void DrawRepairList(Rect body, IReadOnlyList<Stock> stock, System.Func<CraftRecipeDef, bool> stationOk)
        {
            var st = UiTheme.Styles;
            var worn = new List<(ItemDef Item, Isle.Gameplay.Inventory.ItemWear Wear, string Slot, int Container, Isle.Core.Vec2Int At)>();
            foreach (var slot in Isle.Gameplay.Inventory.EquipSlots.All)
                if (_inventory.Slots.Get(slot) is { } item && _inventory.Slots.WearOf(slot) is { NeedsRepair: true } wear)
                    worn.Add((item, wear, slot, -1, default));
            var containers = _inventory.Containers();
            for (var c = 0; c < containers.Count; c++)
                foreach (var placed in containers[c].Placements)
                    if (placed.Wear is { NeedsRepair: true })
                        worn.Add((placed.Item, placed.Wear, null, c, placed.Position));

            var area = new Rect(body.x + 12f, body.y + WindowHeaderPx + 8f, body.width - 24f, body.height - WindowHeaderPx - 20f);
            GUI.Box(area, GUIContent.none, st.Inset);
            if (worn.Count == 0)
            {
                GUI.Label(new Rect(area.x + 14f, area.y + 14f, area.width - 28f, 24f), Lang.Get("@ui.repair_nothing"), st.Muted);
                return;
            }
            const float rowPx = 64f;
            var view = new Rect(0, 0, area.width - 24f, worn.Count * (rowPx + 4f));
            _repairScroll = GUI.BeginScrollView(new Rect(area.x + 6f, area.y + 6f, area.width - 8f, area.height - 12f), _repairScroll, view, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);
            var y = 0f;
            foreach (var (item, wear, slot, container, at) in worn)
            {
                var row = new Rect(0, y, view.width, rowPx);
                GUI.Box(row, GUIContent.none, st.Slot);
                DrawIcon(new Rect(row.x + 6f, row.y + 8f, 48f, 48f), Isle.UI.Art.ItemIcons.Texture(item), wear.Broken);
                GUI.Label(new Rect(row.x + 60f, row.y + 6f, 220f, 20f), Lang.Get(item.Name) + (slot != null ? UiTheme.Colour($"  ({Lang.Get("@ui.repair_equipped")})", UiTheme.Muted) : ""), st.Text);
                DurabilityBar(new Rect(row.x + 60f, row.y + 30f, 150f, 8f), wear.Fraction);
                GUI.Label(new Rect(row.x + 216f, row.y + 24f, 120f, 20f), wear.Broken ? UiTheme.Colour(Lang.Get("@ui.broken"), UiTheme.Bad) : $"{wear.Current} / {wear.Max}", st.Small);

                var recipe = PlayerInteraction.RepairRecipe(item);
                string status;
                var ready = false;
                if (recipe == null) status = UiTheme.Colour(Lang.Get("@ui.repair_none"), UiTheme.Muted);
                else
                {
                    var cost = Isle.Gameplay.Crafting.RepairCalculator.Cost(recipe.Ingredients);
                    var parts = cost.Select(need =>
                    {
                        var have = stock.Where(line => CraftingCalculator.Matches(need, line)).Sum(line => line.Count);
                        var name = need.Item.IsValid && DefRegistry.TryGet<ItemDef>(need.Item, out var d) ? Lang.Get(d.Name) : $"#{need.Tag}";
                        return UiTheme.Colour($"{name} {have}/{need.Count}", have >= need.Count ? UiTheme.Good : UiTheme.Bad);
                    });
                    status = string.Join("  ", parts);
                    if (!stationOk(recipe)) status += "  " + UiTheme.Colour(string.Format(Lang.Get("@ui.needs_near"), StationName(recipe.Station)), UiTheme.Bad);
                    ready = stationOk(recipe) && _player.MeetsSkills(recipe.Skills) && CraftingCalculator.HasIngredients(cost, stock);
                    GUI.Label(new Rect(row.x + 60f, row.y + 42f, row.width - 200f, 20f),
                        $"{Lang.Get("@ui.repair_new_max")} {Isle.Gameplay.Crafting.RepairCalculator.NewMax(wear.Max)}", st.Small);
                }
                GUI.Label(new Rect(row.x + 340f, row.y + 8f, row.width - 470f, 48f), status, st.Small);
                GUI.enabled = ready;
                if (GUI.Button(new Rect(row.xMax - 118f, row.y + 14f, 110f, 34f), Lang.Get("@ui.repair_now"), st.ButtonOn))
                {
                    if (slot != null) _player.RequestRepairEquipped(slot);
                    else _player.RequestRepairPlaced(container, at);
                }
                GUI.enabled = true;
                y += rowPx + 4f;
            }
            GUI.EndScrollView();
        }

        /// <summary>A thin bar, green → amber → red as an item wears down.</summary>
        static void DurabilityBar(Rect rect, float fraction)
        {
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = fraction > 0.5f ? UiTheme.Good : fraction > 0.2f ? UiTheme.Accent : UiTheme.Bad;
            GUI.DrawTexture(new Rect(rect.x + 1f, rect.y + 1f, (rect.width - 2f) * Mathf.Clamp01(fraction), rect.height - 2f), Texture2D.whiteTexture);
            GUI.color = old;
        }

        /// <summary>One line on what an item is for, from its def.</summary>
        static string ItemSummary(ItemDef item)
        {
            if (item == null) return "";
            var parts = new List<string>();
            if (item.Weapon.IsValid && DefRegistry.TryGet<WeaponDef>(item.Weapon, out var weapon)) parts.Add($"{Lang.Get("@ui.tt_power")} {weapon.BasePower:0}");
            if (item.Armor > 0f) parts.Add($"{Lang.Get("@ui.armor")} {item.Armor:0}");
            if (item.Durability is > 0) parts.Add($"{Lang.Get("@ui.durability")} {item.Durability}");
            if (item.Warmth > 0f) parts.Add($"{Lang.Get("@ui.tt_warmth")} +{item.Warmth:0}°");
            if (item.BagGrid is { } bag) parts.Add($"{Lang.Get("@ui.tt_bag")} {bag.W}×{bag.H}");
            if (item.Places.IsValid) parts.Add(Lang.Get("@ui.place"));
            parts.Add($"{item.Weight:0.##} kg");
            return string.Join(" · ", parts);
        }
    }
}
