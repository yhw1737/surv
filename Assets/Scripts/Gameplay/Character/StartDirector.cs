using System.Collections.Generic;
using System.Linq;
using Isle.Core.Ids;
using Isle.Data;
using Isle.Gameplay.Feedback;
using Isle.Gameplay.Inventory;
using Isle.Gameplay.Skills;
using Isle.Modding.Defs;
using Isle.World.Chunks;
using Isle.World.Island;
using UnityEngine;

namespace Isle.Gameplay.Character
{
    /// <summary>
    /// SYS-START-01: begins a fresh game for one player from a scenario — the shipwreck. The survivor wakes on a beach,
    /// soaked, in the clothes they had on, with whatever they had on them; wreckage lies along the shore. Server-side.
    /// </summary>
    public static class StartDirector
    {
        public static ScenarioDef Scenario => DefRegistry.All<ScenarioDef>().FirstOrDefault();

        /// <summary>Rolls a passenger from the scenario's pool.</summary>
        public static Survivor Roll(System.Random rng, ScenarioDef scenario = null)
        {
            scenario ??= Scenario;
            if (scenario == null) return new Survivor { Name = "Survivor" };
            var skills = DefRegistry.All<SkillDef>().Select(s => s.Id).OrderBy(id => id.Value).ToList();
            return SurvivorGenerator.Roll(rng, scenario, DefRegistry.All<TraitDef>().OrderBy(t => t.Id.Value).ToList(), skills);
        }

        /// <summary>Who the player is: skills set, traits applied. Used at the start and again when a save loads.</summary>
        public static void Become(PlayerInteraction player, Survivor survivor, bool setSkills)
        {
            player.Survivor = survivor;
            if (player.TryGetComponent<Vitals>(out var vitals)) vitals.SetTraits(survivor.Traits);
            if (!setSkills) return;
            foreach (var (skill, level) in survivor.Skills)
                player.Skills.Restore(skill, level > 0 ? XpCurve.TotalXpTo(level) : 0);
        }

        public static void Begin(PlayerInteraction player, Survivor survivor, ScenarioDef scenario = null)
        {
            scenario ??= Scenario;
            var world = IslandWorld.Instance;
            Become(player, survivor, setSkills: true);
            if (scenario == null || world == null) return;

            var rng = new System.Random(world.Seed ^ 0x5EA5);
            var biome = scenario.StartBiome.IsValid && System.Enum.TryParse<Biome>(scenario.StartBiome.Name, true, out var b) ? b : Biome.Coast;
            var at = world.ShoreStart(biome);
            player.transform.position = new Vector3(at.x, at.y, player.transform.position.z);
            if (player.TryGetComponent<DeathHandler>(out var death)) death.SetLastShelter(at);

            if (player.TryGetComponent<Vitals>(out var vitals) && scenario.StartWet) vitals.SetWet();

            if (player.TryGetComponent<InventoryNetwork>(out var inventory))
            {
                foreach (var id in survivor.Outfit?.Items ?? System.Array.Empty<NamespacedId>())
                    if (DefRegistry.TryGet<ItemDef>(id, out var cloth) && !string.IsNullOrEmpty(cloth.EquipSlot) && inventory.Slots.Get(cloth.EquipSlot) == null)
                        inventory.Slots.TryEquip(cloth.EquipSlot, cloth);
                foreach (var (item, count) in Belongings(scenario.Belongings, rng))
                    if (!InventoryOps.TryGive(inventory.Containers(), item, count))
                        LootPiles.Drop(at, new[] { new LootEntry(item, count) });
            }

            ScatterWreckage(world, scenario.Debris, at, rng);
            GameFeed.RaiseNotice("@ui.start_awake");
        }

        /// <summary>Weighted picks from the pool, each a different item.</summary>
        public static List<(ItemDef Item, int Count)> Belongings(ItemRoll roll, System.Random rng)
        {
            var picked = new List<(ItemDef, int)>();
            if (roll?.Pool == null || roll.Pool.Length == 0) return picked;
            var pool = roll.Pool.Where(p => DefRegistry.TryGet<ItemDef>(p.Item, out _)).ToList();
            var n = rng.Next(roll.Min, roll.Max + 1);
            for (var i = 0; i < n && pool.Count > 0; i++)
            {
                var total = pool.Sum(p => p.Weight);
                var x = rng.NextDouble() * total;
                var choice = pool[^1];
                foreach (var p in pool)
                {
                    x -= p.Weight;
                    if (x > 0) continue;
                    choice = p;
                    break;
                }
                pool.Remove(choice);
                picked.Add((DefRegistry.Get<ItemDef>(choice.Item), Mathf.Max(1, choice.Count)));
            }
            return picked;
        }

        /// <summary>Wreckage piles on dry ground around the start.</summary>
        static void ScatterWreckage(IslandWorld world, DebrisSpec debris, Vector2 around, System.Random rng)
        {
            if (debris?.Contents == null) return;
            var piles = rng.Next(debris.PilesMin, debris.PilesMax + 1);
            for (var i = 0; i < piles; i++)
            for (var attempt = 0; attempt < 30; attempt++)
            {
                var angle = rng.NextDouble() * Mathf.PI * 2f;
                var radius = 2f + rng.NextDouble() * Mathf.Max(0f, debris.RadiusTiles - 2f);
                var spot = around + new Vector2(Mathf.Cos((float)angle), Mathf.Sin((float)angle)) * (float)radius;
                if (!world.IsWalkable(spot) || world.WaterAt(IslandWorld.WorldToTile(spot)) != null) continue;
                var contents = new List<LootEntry>();
                foreach (var c in debris.Contents)
                {
                    var count = rng.Next(c.Min, c.Max + 1);
                    if (count > 0 && DefRegistry.TryGet<ItemDef>(c.Item, out var item)) contents.Add(new LootEntry(item, count));
                }
                if (contents.Count > 0) LootPiles.Drop(spot, contents, "wreckage");
                break;
            }
        }
    }
}
