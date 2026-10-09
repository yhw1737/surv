using System.IO;
using System.Linq;
using FishNet;
using Isle.Core;
using Isle.Core.Ids;
using Isle.Data;
using Isle.Gameplay.Building;
using Isle.Gameplay.Character;
using Isle.Gameplay.Cooking;
using Isle.Gameplay.Feedback;
using Isle.Gameplay.Inventory;
using Isle.Modding.Defs;
using Isle.World.Island;
using Isle.World.Objects;
using Isle.World.Time;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Isle.UI.Prototype
{
    /// <summary>
    /// T-150 prototype: one solo save file. Read before the island is built (so the seed matches), applied
    /// once the host's player has spawned, written every <see cref="AutosaveSeconds"/> and on quit. Runs on
    /// the server only — it reads and writes authoritative state (Absolute Rule 2). <c>F9</c> deletes the save
    /// so the next launch starts a new island.
    /// ponytail: a JSON file in persistentDataPath, not the SQLite world store ADR-001 names — one player, one
    /// island, no chunks streamed yet. Move it into ChunkSerializer's database when chunks go live.
    /// </summary>
    public sealed class SaveGame : MonoBehaviour
    {
        const float AutosaveSeconds = 60f;

        /// <summary>Tolerance for matching a saved campfire to a live one, in tiles.</summary>
        const float FireMatchTiles = 0.5f;

        /// <summary>Tests point this at a scratch file so they never read or overwrite the player's real save.</summary>
        public static string FilePathOverride { get; set; }

        /// <summary>Save slots (developer, 2026-10-06: up to 8 islands kept side by side).</summary>
        public const int MaxSlots = 8;

        /// <summary>The slot the current game reads and writes, 1-based.</summary>
        public static int Slot { get; set; } = 1;

        static string FilePath => SlotPath(Slot);

        /// <summary>One file per slot. With a test override, slot 1 is the override itself and the others sit beside it.</summary>
        public static string SlotPath(int slot)
        {
            if (FilePathOverride != null) return slot == 1 ? FilePathOverride : FilePathOverride + "." + slot;
            return Path.Combine(Application.persistentDataPath, $"isle_save_{slot}.json");
        }

        /// <summary>The single save from before slots existed becomes slot 1 (once; never overwrites a slot).</summary>
        public static void MigrateLegacy()
        {
            if (FilePathOverride != null) return;
            var legacy = Path.Combine(Application.persistentDataPath, "isle_save.json");
            if (File.Exists(legacy) && !File.Exists(SlotPath(1))) File.Move(legacy, SlotPath(1));
        }

        /// <summary>A slot's save and when it was last written, or null when the slot is empty.</summary>
        public static (SaveData Save, System.DateTime Written)? Inspect(int slot)
        {
            var path = SlotPath(slot);
            if (!File.Exists(path)) return null;
            var save = SaveData.FromJson(File.ReadAllText(path));
            return save == null ? null : (save, File.GetLastWriteTime(path));
        }

        /// <summary>The most recently written slot, or 0 when every slot is empty — what Continue resumes.</summary>
        public static int LatestSlot()
        {
            var latest = 0;
            var when = System.DateTime.MinValue;
            for (var slot = 1; slot <= MaxSlots; slot++)
                if (Inspect(slot) is { } info && info.Written > when)
                {
                    when = info.Written;
                    latest = slot;
                }
            return latest;
        }

        SaveData _pending;
        bool _applied;
        float _nextSaveAt;

        /// <summary>Called by the bootstrap before the island exists, so a resumed island uses its own seed.</summary>
        public static SaveData ReadPending()
        {
            if (!File.Exists(FilePath)) return null;
            var save = SaveData.FromJson(File.ReadAllText(FilePath));
            if (save != null) IslandWorld.NextSeed = save.Seed;
            return save;
        }

        public void Begin(SaveData pending) => _pending = pending;

        /// <summary>The save on disk without touching the next island's seed — for the main menu's Continue button.</summary>
        public static SaveData Peek() => Inspect(Slot)?.Save;

        /// <summary>Removes a slot's save (New game over an existing island, or Delete on the load screen).</summary>
        public static void DeleteFile(int slot)
        {
            var path = SlotPath(slot);
            if (File.Exists(path)) File.Delete(path);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f9Key.wasPressedThisFrame)
            {
                DeleteSave();
                return;
            }

            if (!InstanceFinder.IsServerStarted || IslandWorld.Instance == null) return;
            var player = LocalPlayer();
            if (player == null) return;

            if (!_applied && _nextSaveAt != float.PositiveInfinity)
            {
                if (_pending != null) Apply(_pending, player);
                // SYS-START-01: a fresh island — the chosen survivor (or a random one) wakes on the beach.
                else StartDirector.Begin(player, GameSession.TakePendingSurvivor() ?? StartDirector.Roll(new System.Random(IslandWorld.Instance.Seed)));
                _pending = null;
                _applied = true;
                _nextSaveAt = Time.time + AutosaveSeconds;
            }

            if (_applied && Time.time >= _nextSaveAt)
            {
                Write(player);
                _nextSaveAt = Time.time + AutosaveSeconds;
            }
        }

        /// <summary>Deletes the save and stops autosave, so the next launch starts a new island.</summary>
        public void DeleteSave()
        {
            File.Delete(FilePath);
            _applied = false; // stop autosave from writing it straight back
            _nextSaveAt = float.PositiveInfinity;
            GameFeed.RaiseNotice("@ui.save_deleted");
        }

        public void SaveNow()
        {
            if (!_applied || !InstanceFinder.IsServerStarted) return;
            var player = LocalPlayer();
            if (player == null) return;
            Write(player);
            GameFeed.RaiseNotice("@ui.saved");
        }

        void OnApplicationQuit()
        {
            if (!_applied || !InstanceFinder.IsServerStarted) return;
            var player = LocalPlayer();
            if (player != null) Write(player);
        }

        static PlayerInteraction LocalPlayer()
        {
            foreach (var player in PlayerInteraction.All)
                if (player.IsOwner) return player;
            return null;
        }

        void Write(PlayerInteraction player)
        {
            File.WriteAllText(FilePath, Capture(player).ToJson());
        }

        static SaveData Capture(PlayerInteraction player)
        {
            var world = IslandWorld.Instance;
            var save = new SaveData
            {
                Seed = world.Seed,
                TotalMinutes = WorldTime.Instance != null ? WorldTime.Instance.Clock.TotalMinutes : 0,
                PlayerX = player.transform.position.x,
                PlayerY = player.transform.position.y,
            };

            if (player.TryGetComponent<Vitals>(out var vitals))
            {
                save.Health = vitals.Health;
                save.Hunger = vitals.Hunger;
                save.Thirst = vitals.Thirst;
                save.Stamina = vitals.Stamina;
                save.Temperature = vitals.Temperature;
                save.HypothermiaSeverity = vitals.HypothermiaSeverity;
            }
            if (player.TryGetComponent<DeathHandler>(out var death))
            {
                save.ShelterX = death.LastShelter.x;
                save.ShelterY = death.LastShelter.y;
            }
            if (player.TryGetComponent<InventoryNetwork>(out var inventory))
            {
                foreach (var placed in inventory.Bag.Placements)
                    save.Bag.Add(new SavedStack { Item = KeyOf(placed.Item), X = placed.Position.X, Y = placed.Position.Y, Rotated = placed.Rotated, Count = placed.Count, Wear = placed.Wear?.Current ?? 0, WearMax = placed.Wear?.Max ?? 0 }.WithCarcass(placed.Wear));
                foreach (var slot in EquipSlots.All)
                {
                    var item = inventory.Slots.Get(slot);
                    if (item == null) continue;
                    var slotWear = inventory.Slots.WearOf(slot);
                    var equip = new SavedEquip { Slot = slot, Item = KeyOf(item), Wear = slotWear?.Current ?? 0, WearMax = slotWear?.Max ?? 0 };
                    if (inventory.Slots.BagFor(slot) is { } pack)
                        foreach (var placed in pack.Placements)
                            equip.Contents.Add(new SavedStack { Item = KeyOf(placed.Item), X = placed.Position.X, Y = placed.Position.Y, Rotated = placed.Rotated, Count = placed.Count, Wear = placed.Wear?.Current ?? 0, WearMax = placed.Wear?.Max ?? 0 }.WithCarcass(placed.Wear));
                    save.Equipped.Add(equip);
                }
            }

            if (MapState.Instance != null)
            {
                save.Explored = MapState.Instance.Fog.Serialize();
                foreach (var marker in MapState.Instance.Markers.All)
                    save.Markers.Add(new SavedMarker { X = marker.Position.x, Y = marker.Position.y, Colour = marker.Colour });
            }

            foreach (var skill in player.Skills.Skills)
                save.Skills.Add(new SavedSkill { Id = skill.Id.Value, Xp = player.Skills.TotalXp(skill.Id) });
            if (player.Survivor != null)
            {
                save.SurvivorName = player.Survivor.Name;
                save.Traits.AddRange(player.Survivor.Traits.Select(t => t.Id.Value));
            }

            foreach (var node in world.Nodes)
                if (node.Def.Gather != null && node.UsesLeft < node.Def.Gather.Uses)
                    save.Nodes.Add(new SavedNode { X = node.Tile.X, Y = node.Tile.Y, UsesLeft = node.UsesLeft, RespawnInSeconds = Mathf.Max(0f, node.RespawnAt - world.Now) });

            foreach (var fire in WorldObjectRegistry.All)
            {
                if (StructureFactory.Built.Contains(fire)) continue; // saved with its contents below
                save.Fires.Add(new SavedFire { X = fire.transform.position.x, Y = fire.transform.position.y, Lit = fire.IsActive });
            }

            foreach (var built in StructureFactory.Built)
            {
                if (built == null || built.Def == null) continue;
                var saved = new SavedStructure { Def = built.Def.Id.Value, X = built.transform.position.x, Y = built.transform.position.y, Lit = built.IsActive };
                if (built.TryGetComponent<RainCatcher>(out var catcher)) saved.Water = catcher.Water;
                if (built.TryGetComponent<CropPlot>(out var plot) && plot.Crop != null)
                {
                    saved.Crop = plot.Crop.Id.Value;
                    saved.PlantedAt = plot.PlantedAtMinutes;
                }
                if (built.TryGetComponent<StorageBox>(out var box))
                    foreach (var placed in box.Contents.Placements)
                        saved.Contents.Add(new SavedStack { Item = KeyOf(placed.Item), X = placed.Position.X, Y = placed.Position.Y, Rotated = placed.Rotated, Count = placed.Count, Wear = placed.Wear?.Current ?? 0, WearMax = placed.Wear?.Max ?? 0 }.WithCarcass(placed.Wear));
                save.Structures.Add(saved);
            }

            foreach (var pile in LootPiles.All)
            {
                var saved = new SavedPile { X = pile.Position.x, Y = pile.Position.y, Shape = pile.Shape };
                foreach (var entry in pile.Items)
                    saved.Items.Add(new SavedStack { Item = KeyOf(entry.Item), Count = entry.Count, Wear = entry.Wear?.Current ?? 0, WearMax = entry.Wear?.Max ?? 0 }.WithCarcass(entry.Wear));
                save.Piles.Add(saved);
            }
            return save;
        }

        static void Apply(SaveData save, PlayerInteraction player)
        {
            var world = IslandWorld.Instance;
            WorldTime.Instance?.Clock.SetTotalMinutes(save.TotalMinutes);

            // A save from before the island grew (2026-10-05) can point into what is now water: fall back to the spawn.
            var savedAt = new Vector2(save.PlayerX, save.PlayerY);
            if (!world.IsWalkable(savedAt)) savedAt = Vector2.zero;
            player.transform.position = new Vector3(savedAt.x, savedAt.y, player.transform.position.z);
            if (player.TryGetComponent<Vitals>(out var vitals))
                vitals.Restore(save.Health, save.Hunger, save.Thirst, save.Stamina, save.Temperature, save.HypothermiaSeverity);
            if (player.TryGetComponent<DeathHandler>(out var death))
                death.SetLastShelter(world.IsWalkable(new Vector2(save.ShelterX, save.ShelterY)) ? new Vector3(save.ShelterX, save.ShelterY, 0f) : Vector3.zero);

            if (player.TryGetComponent<InventoryNetwork>(out var inventory))
            {
                inventory.Bag.Clear();
                foreach (var stack in save.Bag)
                    if (TryItem(stack.Item, out var item))
                        inventory.Bag.TryPlace(item, new Vec2Int(stack.X, stack.Y), stack.Rotated, stack.Count, WearOf(stack));
                inventory.Slots.Clear();
                foreach (var equip in save.Equipped)
                {
                    if (!TryItem(equip.Item, out var item) || !inventory.Slots.TryEquip(equip.Slot, item, WearOf(equip.Wear, equip.WearMax))) continue;
                    if (inventory.Slots.BagFor(equip.Slot) is not { } pack) continue;
                    foreach (var stack in equip.Contents)
                        if (TryItem(stack.Item, out var inner)) pack.TryPlace(inner, new Vec2Int(stack.X, stack.Y), stack.Rotated, stack.Count, WearOf(stack));
                }
            }

            if (MapState.Instance != null)
            {
                MapState.Instance.Load(save.Explored);
                MapState.Instance.Markers.Clear();
                foreach (var marker in save.Markers) MapState.Instance.Markers.Add(new Vector2(marker.X, marker.Y), marker.Colour);
            }

            // Who this was: name and traits (skills come back just below).
            var survivor = new Survivor { Name = string.IsNullOrEmpty(save.SurvivorName) ? "Survivor" : save.SurvivorName };
            foreach (var id in save.Traits ?? new System.Collections.Generic.List<string>())
                if (NamespacedId.TryParse(id, out var traitId, out _) && DefRegistry.TryGet<TraitDef>(traitId, out var trait)) survivor.Traits.Add(trait);
            StartDirector.Become(player, survivor, setSkills: false);

            foreach (var saved in save.Skills)
                if (NamespacedId.TryParse(saved.Id, out var skillId, out _)) player.Skills.Restore(skillId, saved.Xp);

            foreach (var saved in save.Nodes)
            {
                var node = world.NodeAt(new Vec2Int(saved.X, saved.Y));
                if (node != null) world.RestoreNode(node, saved.UsesLeft, saved.RespawnInSeconds);
            }

            var fires = WorldObjectRegistry.All;
            foreach (var saved in save.Fires)
            {
                var match = fires.FirstOrDefault(f => Vector2.Distance(f.transform.position, new Vector2(saved.X, saved.Y)) < FireMatchTiles);
                if (match != null) match.IsActive = saved.Lit;
            }

            foreach (var saved in save.Structures)
            {
                if (!NamespacedId.TryParse(saved.Def, out var defId, out _) || !DefRegistry.TryGet<WorldObjectDef>(defId, out var def)) continue;
                var at = new Vector2(saved.X, saved.Y);
                // The startup campfire may already stand here (CampfireSpawner runs first) — reuse it, don't double it.
                var built = WorldObjectRegistry.All
                                .FirstOrDefault(w => w.Def == def && Vector2.Distance(w.transform.position, at) < FireMatchTiles)
                            ?? StructureFactory.Build(def, at);
                built.IsActive = saved.Lit;
                if (built.TryGetComponent<RainCatcher>(out var catcher)) catcher.Water = saved.Water;
                if (!string.IsNullOrEmpty(saved.Crop) && !built.TryGetComponent<CropPlot>(out _) &&
                    NamespacedId.TryParse(saved.Crop, out var cropId, out _) && DefRegistry.TryGet<CropDef>(cropId, out var crop))
                    StructureFactory.Plant(built, crop, saved.PlantedAt);
                if (built.TryGetComponent<StorageBox>(out var box))
                    foreach (var stack in saved.Contents)
                        if (TryItem(stack.Item, out var item)) box.Contents.TryPlace(item, new Vec2Int(stack.X, stack.Y), stack.Rotated, stack.Count, WearOf(stack));
            }

            foreach (var saved in save.Piles)
                LootPiles.Drop(new Vector2(saved.X, saved.Y),
                    saved.Items.Select(s => new LootEntry(TryItem(s.Item, out var item) ? item : null, s.Count, WearOf(s))).Where(x => x.Item != null),
                    string.IsNullOrEmpty(saved.Shape) ? null : saved.Shape);

            GameFeed.RaiseNotice("@ui.save_loaded");
        }

        const string DishPrefix = "dish|";

        /// <summary>A registered item saves as its id; a dish (not a def) saves as its recipe key so it can be rebuilt.</summary>
        static string KeyOf(ItemDef item) => DishFactory.IsDish(item) ? DishPrefix + DishFactory.KeyOf(item) : item.Id.Value;

        /// <summary>An item from the save that still resolves — a removed mod's item is skipped, not fatal.</summary>
        /// <summary>SYS-CRAFT-02: a saved wear, or null for none (old saves, items that don't wear).</summary>
        static ItemWear WearOf(int current, int max) => max > 0 ? new ItemWear(current, max) : null;

        /// <summary>A saved stack's record: wear, or for a carried carcass its body (SYS-HUNT-01).</summary>
        static ItemWear WearOf(SavedStack stack)
        {
            if (!string.IsNullOrEmpty(stack.CarcassOf) && TryCreature(stack.CarcassOf, out var def))
                return new ItemWear(1, 1)
                {
                    Carcass = new Isle.Gameplay.Hunting.CarcassState
                    {
                        Def = def, WeightKg = stack.CarcassKg, Condition = stack.CarcassCondition, KillFactor = stack.CarcassKill, Spoilage = stack.CarcassSpoil,
                    },
                };
            return WearOf(stack.Wear, stack.WearMax);
        }

        static bool TryCreature(string key, out CreatureDef def)
        {
            def = null;
            return NamespacedId.TryParse(key, out var id, out _) && DefRegistry.TryGet(id, out def);
        }

        static bool TryItem(string key, out ItemDef item)
        {
            item = null;
            if (string.IsNullOrEmpty(key)) return false;
            if (key.StartsWith(DishPrefix))
            {
                item = DishFactory.Rebuild(key.Substring(DishPrefix.Length),
                    id => DefRegistry.TryGet<CookMethodDef>(id, out var method) ? method : null,
                    id => DefRegistry.TryGet<ItemDef>(id, out var def) ? def : null);
                return item != null;
            }
            return NamespacedId.TryParse(key, out var parsed, out _) && DefRegistry.TryGet(parsed, out item);
        }
    }
}
