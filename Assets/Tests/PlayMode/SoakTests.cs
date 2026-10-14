using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Isle.Core.Ids;
using Isle.Data;
using Isle.Gameplay.Building;
using Isle.Gameplay.Character;
using Isle.Gameplay.Hunting;
using Isle.Gameplay.Inventory;
using Isle.Modding.Defs;
using Isle.UI.Prototype;
using Isle.World.Island;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Isle.Tests.PlayMode
{
    /// <summary>
    /// T-151 solo bug pass, automated: a fresh game from the menu, then a few minutes of a restless survivor — walking to
    /// random spots all over the island, harvesting, hunting, eating, dropping into a dungeon and back, days passing,
    /// starving to death and getting back up. Every error and exception logged on the way is collected and reported at the
    /// end, together with the things that should stay bounded: creatures, carcasses, loot piles, managed memory.
    /// </summary>
    public sealed class SoakTests
    {
        static readonly string ScratchSave = System.IO.Path.Combine(Application.temporaryCachePath, "isle_soak_save.json");
        const int Rounds = 90;

        readonly List<string> _errors = new();

        void OnLog(string message, string stack, LogType type)
        {
            if (type is LogType.Error or LogType.Exception or LogType.Assert)
                _errors.Add($"{type}: {message}\n{stack.Split('\n').FirstOrDefault()}");
        }

        [SetUp]
        public void SetUp()
        {
            SaveGame.FilePathOverride = ScratchSave;
            if (System.IO.File.Exists(ScratchSave)) System.IO.File.Delete(ScratchSave);
            DefRegistry.Clear();
            Assert.IsEmpty(DefinitionBootstrap.Load(System.IO.Path.Combine(Application.streamingAssetsPath, "definitions")));
            _errors.Clear();
            Application.logMessageReceived += OnLog;
            LogAssert.ignoreFailingMessages = true; // collected and asserted at the end, so one error doesn't stop the run
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= OnLog;
            LogAssert.ignoreFailingMessages = false;
            Isle.Networking.IsleNetworkManager.Find()?.StopHost();
            foreach (var save in Object.FindObjectsByType<SaveGame>(FindObjectsSortMode.None)) Object.DestroyImmediate(save);
            if (System.IO.File.Exists(ScratchSave)) System.IO.File.Delete(ScratchSave);
            SaveGame.FilePathOverride = null;
            SaveGame.Slot = 1;
            LootPiles.Clear();
            StructureFactory.Clear();
            DefRegistry.Clear();
            Isle.Gameplay.World.Difficulty.Current = Isle.Gameplay.World.DifficultyLevel.Normal;
        }

        [UnityTest, Timeout(900000)] // minutes of play: past the runner's 3-minute default
        public IEnumerator RestlessSurvivor_MinutesOfPlay_NoErrorsAndNothingRunsAway()
        {
            System.GC.Collect();
            var editorBaseline = System.GC.GetTotalMemory(true); // the editor and test runner's own heap, before the game
            GameSession.SkipMenu = false;
            // A host left running by an earlier test would keep the new scene from spawning a player.
            Isle.Networking.IsleNetworkManager.Find()?.StopHost();
            yield return WaitUntil(() => !FishNet.InstanceFinder.IsServerStarted && !FishNet.InstanceFinder.IsClientStarted, 5f, "the previous host never stopped");
            yield return SceneManager.LoadSceneAsync("SampleScene");
            // An earlier test in the same run can leave the session "in game", which skips the menu; go back to it the
            // way a player would.
            yield return WaitUntil(() => Object.FindFirstObjectByType<MainMenu>() != null || PlayerInteraction.Local != null, 15f, "neither menu nor game came up");
            if (Object.FindFirstObjectByType<MainMenu>() == null)
            {
                GameSession.BackToMenu();
                yield return WaitUntil(() => Object.FindFirstObjectByType<MainMenu>() != null, 10f, "main menu never appeared");
            }
            GameSession.StartNew(4242);
            yield return WaitUntil(() => PlayerInteraction.Local != null && PlayerInteraction.Local.Survivor != null, 15f, "player never started");
            for (var i = 0; i < 120; i++) yield return null;

            var player = PlayerInteraction.Local;
            var inventory = player.GetComponent<InventoryNetwork>();
            var death = player.GetComponent<DeathHandler>();
            var world = IslandWorld.Instance;
            var clock = Isle.World.Time.WorldTime.Instance.Clock;
            var creatures = CreatureDirector.Instance;
            var dungeons = Isle.Gameplay.Dungeons.DungeonDirector.Instance;
            var rng = new System.Random(7);

            var surfaceCreatures = creatures.Creatures.Count(c => !c.Dead && !c.InDungeon);
            System.GC.Collect();
            var memoryBefore = System.GC.GetTotalMemory(true);
            var deaths = 0;
            var harvests = 0;
            var kills = 0;
            var visits = 0;
            var missed = new List<string>();
            var attempts = 0;
            int casts = 0, catches = 0, crafts = 0, cooks = 0, rests = 0;
            var toolGated = 0;
            var notices = new List<string>();
            System.Action<string> onNotice = n => notices.Add(n);
            Isle.Gameplay.Feedback.GameFeed.Notice += onNotice;

            for (var round = 0; round < Rounds; round++)
            {
                if (death.IsDead)
                {
                    deaths++;
                    yield return WaitUntil(() => !death.IsDead, DeathHandler.RespawnDelaySeconds + 5f, "never respawned");
                    continue;
                }

                // Halfway: a fatal hit, to run death, the drop and the respawn in the middle of everything else.
                if (round == Rounds / 2)
                {
                    player.GetComponent<Vitals>().TakeDamage(10000f);
                    yield return WaitUntil(() => death.IsDead, 3f, "a fatal hit didn't kill");
                    continue;
                }

                switch (round % 6)
                {
                    case 3: // camp on the coast: fish, craft, cook, sleep — in turn
                    {
                        var (shore, sea) = CoastSpot(world);
                        Teleport(player, shore);
                        for (var f = 0; f < 10; f++) yield return null;
                        switch (round / 6 % 4)
                        {
                            case 0:
                                if (InventoryOps.Count(inventory.Containers(), NamespacedId.Parse("isle:fishing_rod")) == 0 &&
                                    inventory.Slots.Get("main_hand")?.Id.Value != "isle:fishing_rod")
                                    InventoryOps.TryGive(inventory.Containers(), DefRegistry.Get<ItemDef>(NamespacedId.Parse("isle:fishing_rod")), 1);
                                var fishBefore = InventoryOps.Count(inventory.Containers(), NamespacedId.Parse("isle:raw_fish"));
                                player.RequestCast(sea);
                                var castUntil = Time.time + 3f;
                                while (player.Cast == null && Time.time < castUntil) yield return null;
                                if (player.Cast == null) break;
                                casts++;
                                var biteUntil = Time.time + 16f;
                                while (player.Cast != null && player.Cast.State != Isle.Gameplay.Fishing.CastState.Bite && Time.time < biteUntil) yield return null;
                                player.RequestHook();
                                for (var f = 0; f < 60; f++) yield return null;
                                if (InventoryOps.Count(inventory.Containers(), NamespacedId.Parse("isle:raw_fish")) > fishBefore) catches++;
                                break;
                            case 1:
                                InventoryOps.TryGive(inventory.Containers(), DefRegistry.Get<ItemDef>(NamespacedId.Parse("isle:wood")), 2);
                                InventoryOps.TryGive(inventory.Containers(), DefRegistry.Get<ItemDef>(NamespacedId.Parse("isle:stone")), 1);
                                var spears = InventoryOps.Count(inventory.Containers(), NamespacedId.Parse("isle:spear__stone"));
                                player.RequestCraft("isle:craft_spear", "isle:stone");
                                var craftUntil = Time.time + 12f; // crafting takes time (T-151 playtest)
                                while (InventoryOps.Count(inventory.Containers(), NamespacedId.Parse("isle:spear__stone")) == spears && Time.time < craftUntil) yield return null;
                                if (InventoryOps.Count(inventory.Containers(), NamespacedId.Parse("isle:spear__stone")) > spears) crafts++;
                                break;
                            case 2:
                                var fire = Campfire(shore);
                                InventoryOps.TryGive(inventory.Containers(), DefRegistry.Get<ItemDef>(NamespacedId.Parse("isle:raw_meat")), 1);
                                var dishes = inventory.Containers().SelectMany(c => c.Placements).Count(p => p.Item.Tags.Contains("dish"));
                                player.RequestCook("isle:grill", new[] { "isle:raw_meat" });
                                var cookUntil = Time.time + 15f;
                                while (inventory.Containers().SelectMany(c => c.Placements).Count(p => p.Item.Tags.Contains("dish")) == dishes && Time.time < cookUntil) yield return null;
                                if (inventory.Containers().SelectMany(c => c.Placements).Count(p => p.Item.Tags.Contains("dish")) > dishes) cooks++;
                                break;
                            case 3:
                                // Night first, then the fire: a clock jump burns the fuel for the hours skipped.
                                clock.SetTotalMinutes(clock.TotalMinutes / 1440 * 1440 + 22 * 60);
                                for (var f = 0; f < 5; f++) yield return null;
                                Campfire(shore);
                                for (var f = 0; f < 3; f++) yield return null;
                                notices.Clear();
                                player.RequestRest();
                                var restUntil = Time.time + 2f;
                                while (notices.Count == 0 && Time.time < restUntil) yield return null;
                                if (notices.Contains("@ui.rested")) rests++;
                                else missed.Add($"rest: [{string.Join(",", notices)}] phase={clock.Phase} minute={clock.MinuteOfDay} fire={Isle.World.Objects.WorldObjectRegistry.NearestDistanceTiles(player.transform.position, "station/campfire"):0.00} dead={death.IsDead}");
                                break;
                        }
                        break;
                    }
                    case 0: // somewhere new: a random harvestable node anywhere on the island
                    {
                        var nodes = world.Nodes.Where(n => n.IsHarvestable).ToList();
                        var node = nodes[rng.Next(nodes.Count)];
                        Teleport(player, node.Position + Vector2.down * 0.6f);
                        for (var f = 0; f < 10; f++) yield return null;
                        notices.Clear();
                        var before = node.UsesLeft;
                        player.RequestInteract();
                        attempts++;
                        // Gathering takes its time (a tree bare-handed is 10 s base); wait while it's under way.
                        var started = Time.time;
                        while (node.UsesLeft == before && !death.IsDead && Time.time < started + 30f &&
                               (player.Gathering != null || Time.time < started + 1f)) yield return null;
                        if (node.UsesLeft < before) harvests++;
                        else if (notices.Contains("@ui.needs_better_tool")) toolGated++;
                        else missed.Add($"{node.Def.Id.Value} gathering={player.Gathering?.Def.Id.Value ?? "-"} notices=[{string.Join(",", notices)}] water={world.NearestWater(player.transform.position, PlayerInteraction.ReachTiles) != null} carcass={creatures.NearestCarcass(player.transform.position, PlayerInteraction.ReachTiles) != null} pile={LootPiles.Nearest(player.transform.position, PlayerInteraction.ReachTiles) != null}");
                        notices.Clear();
                        break;
                    }
                    case 1: // hunt whatever's near, or anything awake
                    case 4:
                    {
                        var prey = creatures.Creatures.Where(c => !c.Dead && !c.InDungeon && c.Def.Boss == null)
                            .OrderBy(c => Vector2.Distance(c.Position, player.transform.position)).Take(20).ToList();
                        if (prey.Count == 0) break;
                        var target = prey[rng.Next(prey.Count)];
                        for (var swing = 0; swing < 12 && !target.Dead && !death.IsDead; swing++)
                        {
                            Teleport(player, target.Position + Vector2.left * 0.8f);
                            player.RequestAttack(Vector2.right);
                            for (var f = 0; f < 15; f++) yield return null;
                        }
                        if (target.Dead) kills++;
                        break;
                    }
                    case 2: // eat or drink whatever's in the bag
                    {
                        var food = inventory.Containers().SelectMany(c => c.Placements).Select(p => p.Item).FirstOrDefault(i => i.Nutrition != null);
                        if (food != null) player.RequestUse(food.Id.Value);
                        clock.SetTotalMinutes(clock.TotalMinutes + 4 * 60); // time passes: hunger, spoilage, night, weather
                        for (var f = 0; f < 30; f++) yield return null;
                        break;
                    }
                    case 5: // a dungeon: in, look around, out
                    {
                        if (round % 12 != 5) break;
                        var site = dungeons.Sites[rng.Next(dungeons.Sites.Count)];
                        if (site.Def.Seal != null) break;
                        Teleport(player, site.Entrance + Vector2.down);
                        player.RequestInteract();
                        var until = Time.time + 4f;
                        while (Time.time < until && !dungeons.IsUnderground(player.transform.position)) yield return null;
                        if (!dungeons.IsUnderground(player.transform.position)) break;
                        visits++;
                        for (var f = 0; f < 60; f++) yield return null;
                        var floor = dungeons.FloorAt(player.transform.position);
                        var exit = floor.Portals.FirstOrDefault(p => p.Kind != Isle.Gameplay.Dungeons.DungeonDirector.DoorKind.Down);
                        if (exit == null) break;
                        Teleport(player, exit.Position);
                        player.RequestInteract();
                        until = Time.time + 4f;
                        while (Time.time < until && dungeons.IsUnderground(player.transform.position)) yield return null;
                        if (dungeons.IsUnderground(player.transform.position)) Teleport(player, site.Entrance + Vector2.down * 2f);
                        break;
                    }
                }
            }

            Isle.Gameplay.Feedback.GameFeed.Notice -= onNotice;
            // Let anything queued settle, then measure.
            for (var i = 0; i < 60; i++) yield return null;
            System.GC.Collect();
            var memoryAfter = System.GC.GetTotalMemory(true);
            var surfaceAfter = creatures.Creatures.Count(c => !c.Dead && !c.InDungeon);
            var report = $"rounds {Rounds}, harvests {harvests}/{attempts} ({toolGated} need a better tool), kills {kills}, casts {casts} (caught {catches}), crafts {crafts}, cooks {cooks}, rests {rests}, dungeon visits {visits}, deaths {deaths}, " +
                         $"surface creatures {surfaceCreatures} → {surfaceAfter}, all creatures {creatures.Creatures.Count}, carcasses {creatures.Carcasses.Count}, " +
                         $"piles {LootPiles.All.Count}, managed {editorBaseline / 1048576f:0} (editor) → {memoryBefore / 1048576f:0} (game started) → {memoryAfter / 1048576f:0} MB, day {clock.TotalMinutes / 1440 + 1}";
            Debug.Log("[soak] " + report);
            foreach (var miss in missed) Debug.Log("[soak-miss] " + miss);
            System.IO.File.WriteAllText(System.IO.Path.Combine(Application.temporaryCachePath, "isle_soak_report.txt"),
                report + "\nMISSED HARVESTS:\n" + string.Join("\n", missed) + "\nERRORS:\n" + string.Join("\n\n", _errors.Distinct()));

            Assert.IsEmpty(_errors.Distinct().ToList(), $"errors during the soak ({report}):\n{string.Join("\n\n", _errors.Distinct().Take(10))}");
            Assert.GreaterOrEqual(harvests + toolGated, attempts - 2, $"harvests failed for no reason ({report}):\n{string.Join("\n", missed)}");
            Assert.That(surfaceAfter, Is.InRange(surfaceCreatures / 2, surfaceCreatures * 3 / 2), $"surface creatures ran away ({report})");
            Assert.Greater(casts, 0, $"never fished ({report})");
            Assert.Greater(crafts, 0, $"never crafted ({report})");
            Assert.Greater(cooks, 0, $"never cooked ({report})");
            Assert.Greater(rests, 0, $"never slept ({report}):\n{string.Join("\n", missed)}");
            Assert.Greater(deaths, 0, $"the death round never ran ({report})");
            Assert.IsFalse(death.IsDead, "still dead at the end");
            Assert.Less(creatures.Carcasses.Count, 60, $"carcasses pile up ({report})");
            Assert.Less(memoryAfter - memoryBefore, 200L * 1048576, $"managed memory grew ({report})");
        }

        /// <summary>A lit campfire by the camp (built once, relit if the night put it out).</summary>
        static Isle.World.Objects.WorldObjectInstance Campfire(Vector2 shore)
        {
            var at = shore + Vector2.left * 1.2f;
            var fire = Isle.World.Objects.WorldObjectRegistry.All.FirstOrDefault(o => o != null && o.HasTag("station/campfire") && Vector2.Distance(o.transform.position, at) < 0.5f);
            if (fire == null)
                fire = StructureFactory.Build(DefRegistry.Get<WorldObjectDef>(NamespacedId.Parse("isle:campfire")), at).GetComponent<Isle.World.Objects.WorldObjectInstance>();
            fire.GetComponent<Isle.Gameplay.World.CampfireInteraction>().Fuel = 480f;
            fire.IsActive = true;
            return fire;
        }

        /// <summary>A dry, node-free land tile right next to the sea, and that sea tile (as the smoke test finds it).</summary>
        static (Vector2 Shore, Vector2 Sea) CoastSpot(IslandWorld world)
        {
            var size = Isle.World.Generation.IslandGenerator.Size;
            foreach (var direction in new[] { new Isle.Core.Vec2Int(1, 0), new Isle.Core.Vec2Int(-1, 0), new Isle.Core.Vec2Int(0, 1), new Isle.Core.Vec2Int(0, -1) })
                for (var offset = -20; offset <= 20; offset += 4)
                {
                    var tile = new Isle.Core.Vec2Int(size / 2 + (direction.Y != 0 ? offset : 0), size / 2 + (direction.X != 0 ? offset : 0));
                    for (var steps = 0; world.WaterAt(tile)?.Ocean != true && steps < size; steps++)
                        tile = new Isle.Core.Vec2Int(tile.X + direction.X, tile.Y + direction.Y);
                    if (world.WaterAt(tile)?.Ocean != true) continue;
                    var shore = new Isle.Core.Vec2Int(tile.X - direction.X, tile.Y - direction.Y);
                    var shoreWorld = IslandWorld.TileToWorld(shore);
                    if (!world.IsWalkable(shoreWorld) || world.Nodes.Any(n => Vector2.Distance(n.Position, shoreWorld) < 2.5f)) continue;
                    return (shoreWorld, IslandWorld.TileToWorld(tile));
                }
            Assert.Fail("no clear coast spot");
            return default;
        }

        static void Teleport(PlayerInteraction player, Vector2 position) =>
            player.transform.position = new Vector3(position.x, position.y, player.transform.position.z);

        static IEnumerator WaitUntil(System.Func<bool> condition, float timeoutSeconds, string failure)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail(failure);
                yield return null;
            }
        }
    }
}
