using System.Collections;
using System.Linq;
using FishNet;
using Isle.Core.Ids;
using Isle.Data;
using Isle.Gameplay.Building;
using Isle.Gameplay.Character;
using Isle.Gameplay.Combat;
using Isle.Gameplay.Hunting;
using Isle.Gameplay.Inventory;
using Isle.World.Objects;
using Isle.Modding.Defs;
using Isle.Networking;
using Isle.World.Island;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Isle.Tests.PlayMode
{
    /// <summary>
    /// Loads the real <c>SampleScene</c>, starts the host (server + local client, SYS-NET-01 §Transports),
    /// and runs the prototype for a few seconds. Unity fails the test on any unexpected error log, so this
    /// catches runtime exceptions in the spawn path and the AI loop that the EditMode tests can't reach.
    /// </summary>
    public sealed class SceneHostSmokeTests
    {
        static readonly string ScratchSave = System.IO.Path.Combine(Application.temporaryCachePath, "isle_test_save.json");

        [SetUp]
        public void SetUp()
        {
            // Never touch the real save: a leftover one would also change what the test starts with.
            // The scene opens on the main menu now (T-153); this test drives the game itself, so skip it.
            Isle.UI.Prototype.GameSession.SkipMenu = true;
            Isle.UI.Prototype.SaveGame.FilePathOverride = ScratchSave;
            if (System.IO.File.Exists(ScratchSave)) System.IO.File.Delete(ScratchSave);

            // The runtime bootstrap only runs once per domain, but earlier tests' TearDown clears the
            // registry, so reload it here. In a real launch DefinitionBootstrapRunner does this before any scene.
            DefRegistry.Clear();
            var errors = DefinitionBootstrap.Load(System.IO.Path.Combine(Application.streamingAssetsPath, "definitions"));
            Assert.IsEmpty(errors, "definition load errors");
        }

        [TearDown]
        public void TearDown()
        {
            // The loaded scene outlives the test; without this its SaveGame would write on editor quit — to the real
            // path, once the override is cleared.
            foreach (var save in Object.FindObjectsByType<Isle.UI.Prototype.SaveGame>(FindObjectsSortMode.None))
                Object.DestroyImmediate(save);
            if (System.IO.File.Exists(ScratchSave)) System.IO.File.Delete(ScratchSave);
            Isle.UI.Prototype.SaveGame.FilePathOverride = null;
            Isle.UI.Prototype.GameSession.SkipMenu = false;
            LootPiles.Clear();
            StructureFactory.Clear();
            DefRegistry.Clear();
            Isle.Gameplay.World.Difficulty.Current = Isle.Gameplay.World.DifficultyLevel.Normal;
        }

        [UnityTest]
        public IEnumerator SampleScene_HostRuns_WithoutErrors()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");

            yield return WaitUntil(() => IslandWorld.Instance != null, 10f, "IslandWorld never installed");
            yield return WaitUntil(() => InstanceFinder.IsServerStarted && InstanceFinder.IsClientStarted, 15f, "host never started");
            yield return WaitUntil(() => FindLocalPlayer() != null, 15f, "local player never spawned");

            for (var i = 0; i < 180; i++) yield return null;

            // SYS-START-01: a fresh island starts on the beach with wreckage around. The random passenger's traits and
            // skills would skew every number below, so the rest of the run plays a blank survivor.
            Assert.IsNotNull(FindLocalPlayer().Survivor, "no survivor applied on a fresh island");
            Assert.GreaterOrEqual(LootPiles.All.Count(p => p.Shape == "wreckage"), 3, "no wreckage on the beach");
            LootPiles.Clear();
            StartDirector.Become(FindLocalPlayer(), new Survivor { Name = "Test" }, setSkills: false);
            foreach (var skill in DefRegistry.All<SkillDef>()) FindLocalPlayer().Skills.Restore(skill.Id, 0);
            FindLocalPlayer().GetComponent<InventoryNetwork>().Slots.Clear(); // the start clothes would add warmth and armor to every check

            Assert.Greater(CreatureDirector.Instance.Creatures.Count, 0, "no creatures spawned in the live scene");
            Assert.IsNotNull(FindLocalPlayer().GetComponent<Vitals>());

            // SYS-MAP-01: the spawn is revealed within a moment, the far side of the map isn't.
            var map = Isle.UI.Prototype.MapState.Instance;
            Assert.IsNotNull(map, "MapState not installed");
            Assert.IsTrue(map.IsExplored(FindLocalPlayer().transform.position), "spawn never revealed");
            Assert.IsFalse(map.IsExplored(FindLocalPlayer().transform.position + new Vector3(400f, 400f)), "fog revealed too far");

            // T-303: the debug screen reads every system without an error line, on the surface.
            AssertDebugScreen("surface", "Tile:", "Biome:");

            // Same session: FishNet can't re-initialise SampleScene's placed Campfire on a second load, so the
            // gameplay flow runs here rather than in its own test.
            yield return GatherCraftDrinkHunt();
            yield return EnterClearAndLeaveADungeon();
            yield return RootwoodHollow();
            yield return DrownedTemple();
            yield return ClockworkRuin();
            yield return Abyss();
            yield return Enchanting();
            yield return Stew();
            yield return DifficultyTiers();
            yield return BrewAndCast();
            yield return InventoryMenu();
            yield return Forage();
            yield return WoundsAndTending();
            yield return StationsAndToolbar();
            yield return Sleep();
        }

        /// <summary>T-304: resting by a fire at night lies down and runs the night by on the clock over a few seconds, then
        /// wakes at 06:00; a hit while asleep wakes you where the night stood.</summary>
        IEnumerator Sleep()
        {
            var player = FindLocalPlayer();
            var (shore, _) = CoastSpot(IslandWorld.Instance);
            Teleport(player, shore);
            ClearCreaturesNear(player.transform.position);
            var clock = Isle.World.Time.WorldTime.Instance.Clock;
            var fire = StructureFactory.Build(DefRegistry.Get<WorldObjectDef>(NamespacedId.Parse("isle:campfire")), shore + Vector2.left * 0.5f);
            fire.GetComponent<Isle.Gameplay.World.CampfireInteraction>().Fuel = 2000f;
            fire.IsActive = true;
            clock.SetTotalMinutes(clock.TotalMinutes / 1440 * 1440 + 22 * 60);
            player.RequestRest();
            yield return WaitUntil(() => player.Sleeping, 2f, "resting didn't lie down");
            var asleepAt = clock.TotalMinutes;
            yield return new WaitForSeconds(PlayerInteraction.SleepFadeSeconds + PlayerInteraction.SleepPassSeconds * 0.5f);
            Assert.Greater(clock.TotalMinutes, asleepAt + 60, "the night isn't running by");
            Assert.IsTrue(player.Sleeping, "woke up too soon");
            yield return WaitUntil(() => !player.Sleeping, PlayerInteraction.SleepTotalSeconds + 2f, "never woke up");
            Assert.AreEqual(Isle.World.Time.WorldClock.RestWakeMinute, clock.MinuteOfDay, "didn't wake at the wake hour");

            // A hit wakes you.
            clock.SetTotalMinutes(clock.TotalMinutes / 1440 * 1440 + 23 * 60);
            yield return null;
            fire.GetComponent<Isle.Gameplay.World.CampfireInteraction>().Fuel = 2000f; // the jump burned the hours skipped
            fire.IsActive = true;
            player.RequestRest();
            yield return WaitUntil(() => player.Sleeping, 2f, "second rest didn't lie down");
            yield return new WaitForSeconds(PlayerInteraction.SleepFadeSeconds + 0.5f);
            player.ReceiveCreatureStrike(shore + Vector2.left, 5f);
            yield return WaitUntil(() => !player.Sleeping, 2f, "a hit didn't wake the sleeper");
            Assert.AreNotEqual(Isle.World.Time.WorldClock.RestWakeMinute, clock.MinuteOfDay, "a woken sleeper still slept the night through");
            player.GetComponent<Vitals>().ResetOnRespawn();
            StructureFactory.Remove(fire);
        }

        /// <summary>T-151 playtest: E at a workbench opens the craft window, at an unlit campfire lights it and at a lit one
        /// opens the cook window; the toolbar's buttons open and close what their keys do.</summary>
        IEnumerator StationsAndToolbar()
        {
            var player = FindLocalPlayer();
            var hud = Isle.UI.Prototype.PrototypeHud.Instance;
            var (shore, _) = CoastSpot(IslandWorld.Instance);
            Teleport(player, shore);
            ClearCreaturesNear(player.transform.position);
            foreach (var tool in new[] { "craft", "cook", "status" }) if (hud.IsToolOpen(tool)) hud.ToggleTool(tool);

            // Closer than the sea next to the shore spot, so E picks the station, not a drink.
            var bench = StructureFactory.Build(DefRegistry.Get<WorldObjectDef>(NamespacedId.Parse("isle:workbench")), shore + Vector2.left * 0.5f);
            player.RequestInteract();
            yield return WaitUntil(() => hud.IsToolOpen("craft"), 2f, "E at a workbench didn't open crafting");
            hud.ToggleTool("craft");
            StructureFactory.Remove(bench);
            yield return null;

            var fire = StructureFactory.Build(DefRegistry.Get<WorldObjectDef>(NamespacedId.Parse("isle:campfire")), shore + Vector2.left * 0.5f);
            var burner = fire.GetComponent<Isle.Gameplay.World.CampfireInteraction>();
            var screen = Isle.UI.Inventory.InventoryScreen.Instance;
            Assert.IsFalse(fire.IsActive, "a built campfire starts lit");
            // E opens the fire beside the bags (it doesn't light it).
            var inventory = player.GetComponent<InventoryNetwork>();
            inventory.Bag.Clear();
            player.RequestInteract();
            yield return WaitUntil(() => screen.Open && screen.Campfire == burner, 2f, "E at a campfire didn't open it");
            Assert.IsFalse(fire.IsActive, "E lit the fire");
            // No fuel, no fire.
            player.RequestLightFire();
            yield return new WaitForSeconds(0.4f);
            Assert.IsFalse(fire.IsActive, "a campfire lit without fuel");
            // Wood onto the fire's grid, then light: one piece burns, the other waits.
            Give(inventory, "isle:wood", 2);
            player.RequestPutFuel(0, inventory.Bag.Placements.First(p => p.Item.Id.Value == "isle:wood").Position, 2);
            yield return WaitUntil(() => burner.Grid.Placements.Sum(p => p.Count) == 2, 2f, "wood never reached the fire");
            Assert.AreEqual(0, CountOf(inventory, "isle:wood"));
            player.RequestLightFire();
            yield return WaitUntil(() => fire.IsActive, 2f, "the light button didn't light it");
            Assert.That(burner.Fuel, Is.InRange(110f, 120f), "one wood isn't two hours of fuel");
            Assert.AreEqual(1, burner.Grid.Placements.Sum(p => p.Count), "lighting didn't take one piece");
            // The burning piece runs out: the next one goes on; then nothing's left and it goes out.
            burner.Fuel = 1f;
            yield return WaitUntil(() => burner.Grid.Placements.Count == 0, 4f, "the next piece never went on");
            Assert.IsTrue(fire.IsActive, "it went out with fuel still in the grid");
            burner.Fuel = 1f;
            yield return WaitUntil(() => !fire.IsActive, 4f, "a fire burned on with no fuel left");
            // The cook-here button and putting it out.
            burner.Fuel = 100f;
            player.RequestLightFire();
            yield return WaitUntil(() => fire.IsActive, 2f, "relight failed");
            player.RequestExtinguish();
            yield return WaitUntil(() => !fire.IsActive, 2f, "put out failed");
            screen.Close();
            StructureFactory.Remove(fire);

            hud.ToggleTool("status");
            Assert.IsTrue(hud.IsToolOpen("status"), "the status button didn't open the status window");
            hud.ToggleTool("status");
            Assert.IsFalse(hud.IsToolOpen("status"));
        }

        /// <summary>SYS-MED-01: a slash lands on a body part as a bleeding cut that caps health; a herbal poultice made at
        /// the apothecary tends it (no more bleeding, quality 0.55); hemlock "tends" at 0 and poisons; with no wound left
        /// to tend, nothing is spent.</summary>
        IEnumerator WoundsAndTending()
        {
            var player = FindLocalPlayer();
            var vitals = player.GetComponent<Vitals>();
            var inventory = player.GetComponent<InventoryNetwork>();
            var (shore, _) = CoastSpot(IslandWorld.Instance);
            Teleport(player, shore);
            ClearCreaturesNear(player.transform.position);
            inventory.Bag.Clear();
            inventory.Slots.Clear();
            vitals.ResetOnRespawn();
            Vitals.WoundRoll = () => 0.85f; // left leg
            Vitals.TendRollOverride = () => 0f;
            player.ReceiveCreatureStrike(shore + Vector2.left, 20f, Isle.Gameplay.Combat.DamageTypes.Slash);
            Assert.AreEqual(1, vitals.Injuries.Wounds.Count, "a slash left no wound");
            var cut = vitals.Injuries.Wounds[0];
            Assert.AreEqual(BodyPart.LeftLeg, cut.Part);
            Assert.AreEqual(WoundKind.Cut, cut.Kind);
            Assert.IsTrue(cut.Bleeds);
            Assert.AreEqual(100f - cut.Severity, vitals.MaxHealth, 0.01f, "the wound doesn't cap health");
            var before = vitals.Health;
            yield return new WaitForSeconds(1.5f);
            Assert.Less(vitals.Health, before, "an untended cut doesn't bleed");

            // A poultice from the apothecary tends it.
            var table = StructureFactory.Build(DefRegistry.Get<WorldObjectDef>(NamespacedId.Parse("isle:apothecary_table")), shore + Vector2.left * 1.2f);
            player.Skills.Restore(NamespacedId.Parse("isle:medicine"), Isle.Gameplay.Skills.XpCurve.TotalXpTo(25)); // ×1.0
            Give(inventory, "isle:healing_herb", 2);
            Give(inventory, "isle:cloth", 1);
            player.RequestCraft("isle:med_herbal_poultice");
            yield return WaitUntil(() => CountOf(inventory, "isle:herbal_poultice") == 1, 10f, "no poultice made");
            player.RequestTend("isle:herbal_poultice");
            yield return WaitUntil(() => cut.Tended, 2f, "the poultice tended nothing");
            Assert.AreEqual(0.55f, cut.Quality, 1e-3f, "tend quality");
            Assert.IsFalse(vitals.Injuries.Bleeding, "still bleeding after tending");
            Assert.AreEqual(0, CountOf(inventory, "isle:herbal_poultice"));

            // Nothing left to tend: the item stays.
            Give(inventory, "isle:herbal_poultice", 1);
            player.RequestTend("isle:herbal_poultice");
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(1, CountOf(inventory, "isle:herbal_poultice"), "medicine spent with no wound to tend");

            // Hemlock: quality 0 and poisoned.
            player.ReceiveCreatureStrike(shore + Vector2.left, 10f, Isle.Gameplay.Combat.DamageTypes.Pierce);
            Give(inventory, "isle:hemlock", 1);
            player.RequestTend("isle:hemlock");
            yield return WaitUntil(() => vitals.ActiveBuffs().Any(b => b.Id.Value == "isle:toxin"), 2f, "hemlock didn't poison");

            // Armor by part: a helmet stands in front of the head, not the torso.
            vitals.ResetOnRespawn();
            var helmet = DefRegistry.All<ItemDef>().First(i => i.EquipSlot == "head" && i.Armor > 0f);
            Assert.IsTrue(inventory.Slots.TryEquip("head", helmet));
            Vitals.WoundRoll = () => 0.05f; // torso
            vitals.Restore(100f, 100f, 100f, 100f, Vitals.ComfortableTemperature, 0f);
            player.ReceiveCreatureStrike(shore + Vector2.left, 20f, Isle.Gameplay.Combat.DamageTypes.Blunt);
            var torsoLoss = 100f - vitals.Health;
            vitals.ResetOnRespawn();
            Vitals.WoundRoll = () => 0.45f; // head
            player.ReceiveCreatureStrike(shore + Vector2.left, 20f, Isle.Gameplay.Combat.DamageTypes.Blunt);
            var headLoss = 100f - vitals.Health;
            Assert.Less(headLoss, torsoLoss, "the helmet didn't protect the head more than the bare torso");
            inventory.Slots.Clear();

            // A heavy blunt hit on a leg breaks it; a splint sets it.
            vitals.ResetOnRespawn();
            Vitals.WoundRoll = () => 0.85f;
            Vitals.FractureRoll = () => 0f;
            player.ReceiveCreatureStrike(shore + Vector2.left, 25f, Isle.Gameplay.Combat.DamageTypes.Blunt);
            var broken = vitals.Injuries.Wounds.Single();
            Assert.IsTrue(broken.Fracture, "no fracture");
            Give(inventory, "isle:splint", 1);
            player.RequestTend("isle:splint");
            yield return WaitUntil(() => broken.Splinted, 2f, "the splint didn't set the leg");
            Vitals.FractureRoll = null;
            Vitals.WoundRoll = null;
            Vitals.TendRollOverride = null;
            vitals.ResetOnRespawn();
            StructureFactory.Remove(table);
        }

        /// <summary>T-151 / SYS-FORAGE-01: every berry bush and mushroom patch grows on the island; a death cap eaten raw
        /// poisons for its full time and drains health; at Gathering 0 a sunberry is an "unknown red berry", at 10 it's
        /// itself.</summary>
        IEnumerator Forage()
        {
            var player = FindLocalPlayer();
            var vitals = player.GetComponent<Vitals>();
            var inventory = player.GetComponent<InventoryNetwork>();
            var world = IslandWorld.Instance;
            foreach (var node in new[] { "sunberry_bush", "duskberry_bush", "frostberry_bush", "ghostberry_bush", "brown_cap_patch", "fools_cap_patch", "honey_cap_patch", "death_cap_patch" })
                Assert.IsTrue(world.Nodes.Any(n => n.Def.Id.Value == "isle:" + node), $"no {node} on the island");

            var gathering = NamespacedId.Parse("isle:gathering");
            player.Skills.Restore(gathering, 0);
            var sunberry = DefRegistry.Get<ItemDef>(NamespacedId.Parse("isle:sunberry"));
            Assert.AreEqual(Isle.UI.Prototype.Lang.Get("@item.unknown_red_berry"), Isle.UI.Prototype.ItemText.Name(sunberry), "a novice can tell a sunberry");
            player.Skills.Restore(gathering, Isle.Gameplay.Skills.XpCurve.TotalXpTo(10));
            Assert.AreEqual(Isle.UI.Prototype.Lang.Get("@item.sunberry"), Isle.UI.Prototype.ItemText.Name(sunberry), "Gathering 10 still can't tell");

            vitals.ResetOnRespawn(); // no heal-over-time left from an earlier step to hide the poison
            inventory.Bag.Clear();
            Give(inventory, "isle:death_cap", 1);
            player.RequestUse("isle:death_cap");
            yield return WaitUntil(() => vitals.ActiveBuffs().Any(b => b.Id.Value == "isle:toxin"), 3f, "a death cap eaten raw didn't poison");
            var expires = vitals.Buffs.ExpiresAt(NamespacedId.Parse("isle:toxin")) ?? 0;
            Assert.GreaterOrEqual(expires - Vitals.NowMinutes, 115, "raw eating cut the poison short");
            var before = vitals.Health;
            yield return new WaitForSeconds(2.5f);
            Assert.Less(vitals.Health, before - 0.5f, "the poison doesn't drain health");
            foreach (var id in vitals.ActiveBuffs().Select(b => b.Id).ToList()) vitals.Buffs.Clear(id);
            vitals.Restore(100f, 100f, 100f, 100f, Vitals.ComfortableTemperature, 0f);
        }

        /// <summary>T-151 playtest round 1: right-clicking a bag item opens a menu of what it can do — berries can be eaten,
        /// moved to the backpack or dropped; "drop" puts them on the ground.</summary>
        IEnumerator InventoryMenu()
        {
            var player = FindLocalPlayer();
            var inventory = player.GetComponent<InventoryNetwork>();
            var (shore, _) = CoastSpot(IslandWorld.Instance);
            Teleport(player, shore);
            ClearCreaturesNear(player.transform.position);
            ClearCreaturesNear(player.transform.position);
            inventory.Bag.Clear();
            inventory.Slots.Clear();
            Give(inventory, "isle:straw_backpack", 1);
            player.RequestEquipItem("isle:straw_backpack");
            yield return WaitUntil(() => inventory.Slots.BagFor("back") != null, 3f, "backpack never worn");
            Give(inventory, "isle:berries", 3);
            var screen = Isle.UI.Inventory.InventoryScreen.Instance;
            screen.Show();
            for (var i = 0; i < 5; i++) yield return null;
            var berries = inventory.Bag.Placements.First(p => p.Item.Id.Value == "isle:berries");
            var labels = screen.OpenMenuFor(berries.Item);
            Assert.Contains(Isle.UI.Prototype.Lang.Get("@ui.ctx_eat"), labels, "no eat option for berries");
            Assert.Contains(Isle.UI.Prototype.Lang.Get("@ui.ctx_drop"), labels, "no drop option");
            Assert.IsTrue(labels.Any(l => l == string.Format(Isle.UI.Prototype.Lang.Get("@ui.ctx_move_to"), Isle.UI.Prototype.Lang.Get(inventory.Slots.Get("back").Name))),
                $"no move-to-backpack option: {string.Join(" | ", labels)}");

            var piles = LootPiles.All.Count;
            Assert.IsTrue(screen.PressMenu(Isle.UI.Prototype.Lang.Get("@ui.ctx_drop")));
            yield return WaitUntil(() => LootPiles.All.Count == piles + 1, 3f, "drop from the menu made no pile");
            Assert.AreEqual(0, CountOf(inventory, "isle:berries"), "berries still in the bag");
            screen.Close();
            LootPiles.Clear();
        }

        /// <summary>T-107 SYS-BREW-01 and T-117 SYS-MAGIC-01: a health tonic brewed at a stand heals 30 at once; a mending
        /// draught heals over time; a mana tonic restores 50; an apprentice wand spends 8 mana per bolt and hurts what it
        /// hits; with too little mana nothing fires.</summary>
        IEnumerator BrewAndCast()
        {
            var player = FindLocalPlayer();
            var vitals = player.GetComponent<Vitals>();
            var inventory = player.GetComponent<InventoryNetwork>();
            var (shore, _) = CoastSpot(IslandWorld.Instance);
            Teleport(player, shore);
            ClearCreaturesNear(player.transform.position);
            inventory.Bag.Clear();
            inventory.Slots.Clear();
            vitals.ResetOnRespawn(); // no wounds from earlier steps capping the heals below
            player.Skills.Restore(NamespacedId.Parse("isle:enchanting"), Isle.Gameplay.Skills.XpCurve.TotalXpTo(50));
            var stand = StructureFactory.Build(DefRegistry.Get<WorldObjectDef>(NamespacedId.Parse("isle:brewing_stand")), shore + Vector2.left * 1.2f);

            // Brew and drink a health tonic: +30 at once.
            Give(inventory, "isle:bitter_herb", 2);
            Give(inventory, "isle:berries", 2);
            player.RequestCraft("isle:brew_health_tonic");
            yield return WaitUntil(() => CountOf(inventory, "isle:health_tonic") == 1, 10f, "the tonic was never brewed");
            vitals.Restore(50f, 100f, 100f, 100f, Vitals.ComfortableTemperature, 0f);
            player.RequestUse("isle:health_tonic");
            yield return WaitUntil(() => CountOf(inventory, "isle:health_tonic") == 0, 2f, "the tonic wasn't drunk");
            Assert.AreEqual(80f, vitals.Health, 0.5f, "a health tonic heals 30 at once");

            // A mending draught heals over time.
            vitals.Restore(20f, 100f, 100f, 100f, Vitals.ComfortableTemperature, 0f);
            Give(inventory, "isle:mending_draught", 1);
            player.RequestUse("isle:mending_draught");
            yield return WaitUntil(() => vitals.EffectsRunning > 0, 2f, "the draught started nothing");
            Assert.Less(vitals.Health, 30f, "a draught healed at once");
            yield return new WaitForSeconds(2f);
            Assert.That(vitals.Health, Is.InRange(22f, 30f), "about 2 HP a second");

            // A mana tonic: +50.
            vitals.SetMana(10f);
            Give(inventory, "isle:mana_tonic", 1);
            player.RequestUse("isle:mana_tonic");
            yield return WaitUntil(() => vitals.Mana >= 59f, 2f, "the mana tonic restored nothing");
            Assert.Less(vitals.Mana, 62f, "the mana tonic restored too much");

            // An apprentice wand: a bolt costs 8 mana and hurts what it hits.
            var wand = DefRegistry.Get<ItemDef>(NamespacedId.Parse("isle:apprentice_wand"));
            Assert.IsTrue(inventory.Slots.TryEquip("main_hand", wand, ItemWear.Fresh(wand)));
            var rabbitDef = DefRegistry.Get<CreatureDef>(NamespacedId.Parse("isle:rabbit"));
            var target = CreatureDirector.Instance.SpawnAt(rabbitDef, shore + Vector2.left * 3f, false);
            var targetHealth = target.Health;
            vitals.SetMana(20f);
            player.RequestAttack(target.Position - shore);
            yield return WaitUntil(() => vitals.Mana < 14f, 2f, "the wand cast nothing");
            Assert.AreEqual(12f, vitals.Mana, 0.6f, "a bolt costs 8 mana");
            Assert.AreEqual(Isle.Gameplay.Combat.PowerCalculator.FinalPower(12f, player.LevelOf(NamespacedId.Parse("isle:magic"))), player.LastSpellDamage, 1e-3f, "bolt damage");
            yield return WaitUntil(() => target.Dead || target.Health < targetHealth, 3f, "the bolt never hit");

            // Too little mana: nothing fires.
            yield return new WaitForSeconds(0.8f);
            vitals.SetMana(5f);
            player.RequestAttack(Vector2.left);
            yield return new WaitForSeconds(0.4f);
            Assert.GreaterOrEqual(vitals.Mana, 5f, "a cast went through without the mana");
            inventory.Slots.Clear();
            StructureFactory.Remove(stand);
        }

        /// <summary>T-028 SYS-DIFF-01: Hard creature strikes land ×1.25 of Normal; Unlimited can't be hurt, doesn't get
        /// hungry and takes items from the palette, which is refused anywhere else.</summary>
        IEnumerator DifficultyTiers()
        {
            var player = FindLocalPlayer();
            var vitals = player.GetComponent<Vitals>();
            var inventory = player.GetComponent<InventoryNetwork>();
            var clock = Isle.World.Time.WorldTime.Instance.Clock;
            Teleport(player, (Vector2)IslandWorld.Instance.ShoreStart(Isle.World.Chunks.Biome.Coast) + Vector2.up * 2f);
            ClearCreaturesNear(player.transform.position);
            inventory.Slots.Clear(); // no armor in the way
            float Hit(Isle.Gameplay.World.DifficultyLevel tier)
            {
                Isle.Gameplay.World.Difficulty.Current = tier;
                vitals.Injuries.Clear(); // a wound from the last hit would cap health below 100
                vitals.Restore(100f, 100f, 100f, 100f, Vitals.ComfortableTemperature, 0f);
                player.ReceiveCreatureStrike((Vector2)player.transform.position + Vector2.left, 10f);
                return 100f - vitals.Health;
            }
            var normal = Hit(Isle.Gameplay.World.DifficultyLevel.Normal);
            var hard = Hit(Isle.Gameplay.World.DifficultyLevel.Hard);
            var easy = Hit(Isle.Gameplay.World.DifficultyLevel.Easy);
            Assert.Greater(normal, 0f, "a Normal strike did nothing");
            Assert.AreEqual(normal * 1.25f, hard, 0.05f, "Hard isn't ×1.25");
            Assert.AreEqual(normal * 0.5f, easy, 0.05f, "Easy isn't ×0.5");
            vitals.Restore(100f, 100f, 100f, 100f, Vitals.ComfortableTemperature, 0f);
            yield return new WaitForSeconds(1.2f); // hit statuses (bleed) run out

            // Unlimited: no damage, no hunger after a day, the palette gives.
            Assert.AreEqual(0f, Hit(Isle.Gameplay.World.DifficultyLevel.Unlimited), "Unlimited took damage");
            clock.SetTotalMinutes(clock.TotalMinutes + 24 * 60);
            yield return new WaitForSeconds(1.5f);
            Assert.AreEqual(100f, vitals.Hunger, 0.01f, "Unlimited got hungry");
            Assert.AreEqual(100f, vitals.Health, 0.01f, "Unlimited lost health");
            var wood = CountOf(inventory, "isle:wood");
            player.RequestCreativeTake("isle:wood", 5);
            yield return WaitUntil(() => CountOf(inventory, "isle:wood") == wood + 5, 2f, "the palette gave nothing");

            Isle.Gameplay.World.Difficulty.Current = Isle.Gameplay.World.DifficultyLevel.Normal;
            player.RequestCreativeTake("isle:wood", 5);
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(wood + 5, CountOf(inventory, "isle:wood"), "the palette worked outside Unlimited");
        }

        /// <summary>T-102 SYS-COOK-01: wild grain and onion grow on the island; at a cauldron a meat + onion + grain stew
        /// (three ingredient groups) grants two buffs.</summary>
        IEnumerator Stew()
        {
            var player = FindLocalPlayer();
            var inventory = player.GetComponent<InventoryNetwork>();
            var world = IslandWorld.Instance;
            Assert.IsTrue(world.Nodes.Any(n => n.Def.Id.Value == "isle:wild_grain_patch"), "no wild grain on the island");
            Assert.IsTrue(world.Nodes.Any(n => n.Def.Id.Value == "isle:wild_onion_patch"), "no wild onion on the island");
            Teleport(player, (Vector2)world.ShoreStart(Isle.World.Chunks.Biome.Coast) + Vector2.up);
            var cauldron = StructureFactory.Build(DefRegistry.Get<WorldObjectDef>(NamespacedId.Parse("isle:cauldron")), (Vector2)player.transform.position + Vector2.right);
            player.Skills.Restore(NamespacedId.Parse("isle:cooking"), Isle.Gameplay.Skills.XpCurve.TotalXpTo(50));
            inventory.Bag.Clear();
            Give(inventory, "isle:raw_meat", 1);
            Give(inventory, "isle:wild_onion", 1);
            Give(inventory, "isle:wild_grain", 1);
            player.RequestCook("isle:stew", new[] { "isle:raw_meat", "isle:wild_onion", "isle:wild_grain" });
            yield return WaitUntil(() => player.Cooking != null, 2f, "the stew didn't start");
            yield return WaitUntil(() => Dish(inventory) != null, 15f, "the stew never finished"); // (4 + 2×3) × 2 × 0.5 = 10 s
            var stew = Dish(inventory);
            Assert.AreEqual(2, stew.Buffs?.Length ?? 0, "a three-group stew didn't grant two buffs");
            Assert.AreEqual(0, CountOf(inventory, "isle:wild_grain") + CountOf(inventory, "isle:wild_onion") + CountOf(inventory, "isle:raw_meat"), "ingredients not used");
            StructureFactory.Remove(cauldron);
        }

        /// <summary>T-106 SYS-CRAFT-01 §Enchanting: at a table, a Fine sword takes two enchants (its quality's slots),
        /// spends a catalyst each time, and refuses a third; a failure costs only the catalyst; enchants hold the damage
        /// multiplier and survive in the wear record.</summary>
        IEnumerator Enchanting()
        {
            var player = FindLocalPlayer();
            var inventory = player.GetComponent<InventoryNetwork>();
            Teleport(player, (Vector2)IslandWorld.Instance.ShoreStart(Isle.World.Chunks.Biome.Coast) + Vector2.zero);
            var table = StructureFactory.Build(DefRegistry.Get<WorldObjectDef>(NamespacedId.Parse("isle:enchanting_table")), (Vector2)player.transform.position + Vector2.right);
            Assert.IsTrue(table.TryGetComponent<EnchantingTable>(out _), "the table isn't an enchanting station");
            player.Skills.Restore(NamespacedId.Parse("isle:enchanting"), Isle.Gameplay.Skills.XpCurve.TotalXpTo(50));
            inventory.Bag.Clear();
            var sword = DefRegistry.Get<ItemDef>(NamespacedId.Parse("isle:sword__copper"));
            Assert.IsTrue(InventoryOps.TryGive(inventory.Containers(), sword, 1, new ItemWear(150, 150) { Quality = Isle.Gameplay.Crafting.QualityTier.Fine }));
            Give(inventory, "isle:tide_pearl", 4);
            var placed = inventory.Bag.Placements.First(p => p.Item == sword);
            var wear = placed.Wear;
            var pearls = CountOf(inventory, "isle:tide_pearl"); // the Grotto's cache may have left some in a backpack

            // A failure: the pearl is spent, the sword untouched.
            Isle.Gameplay.Character.PlayerInteraction.EnchantRollOverride = () => 1.5f; // above any success rate
            player.RequestEnchantPlaced(0, placed.Position, "isle:keen");
            yield return WaitUntil(() => CountOf(inventory, "isle:tide_pearl") == pearls - 1, 2f, "a failed enchant didn't spend the catalyst");
            Assert.AreEqual(0, wear.Enchants.Count, "a failed enchant changed the item");

            // Two successes fill a Fine sword's two slots; a third is refused and costs nothing.
            Isle.Gameplay.Character.PlayerInteraction.EnchantRollOverride = () => 0f;
            player.RequestEnchantPlaced(0, placed.Position, "isle:keen");
            yield return WaitUntil(() => wear.Enchants.Count == 1, 2f, "enchant never set");
            player.RequestEnchantPlaced(0, placed.Position, "isle:keen");
            yield return WaitUntil(() => wear.Enchants.Count == 2, 2f, "second enchant never set");
            player.RequestEnchantPlaced(0, placed.Position, "isle:keen");
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(2, wear.Enchants.Count, "a third enchant went past the slots");
            Assert.AreEqual(pearls - 3, CountOf(inventory, "isle:tide_pearl"), "a refused enchant spent a pearl");
            Assert.AreEqual(1.12f * 1.12f, Isle.Gameplay.Crafting.EnchantEffects.Mult(wear, Isle.Gameplay.Crafting.EnchantCalculator.DamageMult), 1e-4f);
            Isle.Gameplay.Character.PlayerInteraction.EnchantRollOverride = null;
            StructureFactory.Remove(table);
        }

        /// <summary>T-204 the Abyss: beside the Ruin, it opens with no sigils under four curses (double the enemies, 40%
        /// light, near-black); sigils carried in are set into the seal and lift curses; every floor down presses on
        /// stamina; a light gets you through the dark shaft fast; the Warden has HP 2000.</summary>
        IEnumerator Abyss()
        {
            var player = FindLocalPlayer();
            var inventory = player.GetComponent<InventoryNetwork>();
            var dungeons = Isle.Gameplay.Dungeons.DungeonDirector.Instance;
            var site = dungeons.Sites.FirstOrDefault(s => s.Def.Id.Value == "isle:abyss");
            Assert.IsNotNull(site, "no Abyss on the island");
            var ruin = dungeons.Sites.First(s => s.Def.Id.Value == "isle:clockwork_ruin");
            Assert.That(Vector2.Distance(site.Entrance, ruin.Entrance), Is.InRange(5.5f, 12.5f), "the Abyss isn't beside the Ruin");

            // In with no sigils: the gate opens, four curses.
            foreach (var sigil in site.Def.Seal.Sigils)
                while (InventoryOps.Count(inventory.Containers(), sigil) > 0) InventoryOps.TakeOne(inventory.Containers(), sigil);
            Teleport(player, site.Entrance + Vector2.down);
            player.RequestInteract();
            yield return WaitUntil(() => dungeons.IsUnderground(player.transform.position), 3f, "the sealed Abyss didn't open without sigils");
            var floor = dungeons.FloorAt(player.transform.position);
            Assert.AreEqual(4, site.Curses);
            Assert.AreEqual(0.4f, dungeons.LightMultAt(player.transform.position), 1e-4f, "light under four curses");
            Assert.AreEqual(0.08f, dungeons.DarknessAt(player.transform.position), 1e-4f, "the Abyss isn't dark");
            Assert.AreEqual(0.85f, dungeons.StaminaRegenMultAt(player.transform.position), 1e-4f, "floor 1 pressure");
            AssertDebugScreen("underground", "Underground: isle:abyss floor 1/3", "curses 4");
            var marks = floor.Tiles.Spawns.Count(s => floor.Layout.Rooms[s.Item2].Kind is not (Isle.World.Generation.RoomKind.Entrance
                or Isle.World.Generation.RoomKind.Rest or Isle.World.Generation.RoomKind.Vault or Isle.World.Generation.RoomKind.Boss));
            var dwellers = CreatureDirector.Instance.Creatures.Count(c => c.InDungeon && dungeons.FloorAt(c.Position) == floor && c.Def.Boss == null);
            Assert.AreEqual(marks * 2, dwellers, "four curses didn't double the enemies");

            // A light gets anyone through the dark shaft at the specialist's pace.
            var torch = DefRegistry.Get<ItemDef>(NamespacedId.Parse("isle:torch"));
            inventory.Slots.Unequip("off_hand", out _);
            Assert.IsTrue(inventory.Slots.TryEquip("off_hand", torch));
            var shaft = floor.Barriers.Single(b => b.LockId == -1);
            var edge = floor.Layout.Edges[shaft.Edge];
            var a = floor.Layout.Rooms[edge.A].Cell;
            var b2 = floor.Layout.Rooms[edge.B].Cell;
            var door = Isle.World.Generation.DungeonTiles.DoorwayCentre(a, b2);
            Teleport(player, floor.TileToWorld(a.Y == b2.Y ? new Isle.Core.Vec2Int(door.X + 1, door.Y) : new Isle.Core.Vec2Int(door.X, door.Y + 1)));
            foreach (var creature in CreatureDirector.Instance.Creatures.Where(c => c.InDungeon && dungeons.FloorAt(c.Position) == floor).ToList())
                CreatureDirector.Instance.Damage(creature, 100000f, new System.Collections.Generic.List<(NamespacedId, int)>());
            player.RequestInteract();
            yield return WaitUntil(() => shaft.Open, 6f, "a torch didn't speed through the dark shaft");
            inventory.Slots.Unequip("off_hand", out _);

            // Back up; two sigils carried in are set into the seal.
            Teleport(player, site.Entrance + Vector2.down);
            Give(inventory, "isle:tide_sigil", 1);
            Give(inventory, "isle:root_sigil", 1);
            player.RequestInteract();
            yield return WaitUntil(() => site.SigilsPlaced.Count == 2, 3f, "carried sigils weren't set into the seal");
            Assert.AreEqual(2, site.Curses);
            Assert.AreEqual(0, CountOf(inventory, "isle:tide_sigil") + CountOf(inventory, "isle:root_sigil"), "set sigils weren't used up");

            // Floor 3: heavier pressure; the Warden waits.
            var deepest = dungeons.EnsureFloor(site, site.Def.Floors - 1);
            var deep = deepest.TileToWorld(deepest.Tiles.Features[deepest.Layout.Entrance]);
            Assert.AreEqual(0.55f, dungeons.StaminaRegenMultAt(deep), 1e-4f, "floor 3 pressure");
            Assert.IsNotNull(site.Boss, "the Abyss Warden never spawned");
            Assert.AreEqual(2000f, site.Boss.MaxHealth, 0.01f, "Abyss Warden HP");
            Teleport(player, site.Entrance + Vector2.down * 2f);
        }

        /// <summary>T-203 Clockwork Ruin: gear doors swap every period (never shutting on anyone); the Sentinel charges,
        /// overheats after three charges and takes ×1.5 damage meanwhile, and calls beetles below half.</summary>
        IEnumerator ClockworkRuin()
        {
            var player = FindLocalPlayer();
            var world = IslandWorld.Instance;
            var vitals = player.GetComponent<Vitals>();
            var dungeons = Isle.Gameplay.Dungeons.DungeonDirector.Instance;
            var site = dungeons.Sites.FirstOrDefault(s => s.Def.Id.Value == "isle:clockwork_ruin");
            Assert.IsNotNull(site, "no Clockwork Ruin on the island");
            Isle.Gameplay.Dungeons.DungeonDirector.Floor floor = null;
            for (var i = 0; i < site.Def.Floors && (floor == null || !floor.Barriers.Any(b => b.Gear >= 0)); i++) floor = dungeons.EnsureFloor(site, i);
            var gears = floor.Barriers.Where(b => b.Gear >= 0).ToList();
            Assert.Greater(gears.Count, 0, "no gear doors on any floor");
            Teleport(player, site.Entrance + Vector2.down * 2f);

            // Phase 0, then phase 1: each door is open exactly in its own phase, and blocks while shut.
            foreach (var phase in new[] { 0, 1 })
            {
                floor.CreatedAt = Time.time - phase * site.Def.Clockwork.PeriodSeconds - 1f;
                yield return null;
                yield return null;
                Assert.AreEqual(phase, dungeons.GearPhase(floor));
                foreach (var door in gears)
                {
                    Assert.AreEqual(door.Gear == phase, door.Open, $"gear door (group {door.Gear}) in phase {phase}");
                    Assert.AreEqual(door.Open, world.IsWalkable(door.Position), "a gear door's walkability doesn't match it");
                }
            }

            // Standing in a door that's due to shut holds it open.
            var held = gears.FirstOrDefault(d => d.Open);
            if (held != null)
            {
                Teleport(player, held.Position);
                floor.CreatedAt = Time.time - (1 - held.Gear) * site.Def.Clockwork.PeriodSeconds - 1f;
                yield return null;
                yield return null;
                Assert.IsTrue(held.Open, "a gear door shut on the player");
                Teleport(player, site.Entrance + Vector2.down * 2f);
                yield return null;
                yield return null;
                Assert.IsFalse(held.Open, "the door never shut once the player stepped out");
            }

            // The Sentinel: HP 1300; three charges, then it overheats.
            var last = dungeons.EnsureFloor(site, site.Def.Floors - 1);
            var boss = site.Boss;
            Assert.IsNotNull(boss, "Sentinel never spawned");
            Assert.AreEqual(1300f, boss.MaxHealth, 0.01f, "Sentinel HP");
            foreach (var creature in CreatureDirector.Instance.Creatures.Where(c => c != boss && c.InDungeon && dungeons.FloorAt(c.Position) == last).ToList())
                CreatureDirector.Instance.Damage(creature, 100000f, new System.Collections.Generic.List<(NamespacedId, int)>());
            vitals.Restore(100f, 100f, 100f, 100f, Vitals.ComfortableTemperature, 0f); // fed, watered, warm: only what's tested hurts
            Teleport(player, boss.Position + Vector2.down * 3.5f);
            yield return WaitUntil(() => boss.IsOverheated || vitals.Health <= 0f, 20f, "the Sentinel never overheated");
            Assert.IsTrue(boss.IsOverheated, "the player fell before the Sentinel overheated");
            Teleport(player, site.Entrance + Vector2.down * 2f);

            // Hitting it while it vents does ×1.5.
            var before = boss.Health;
            CreatureDirector.Instance.Damage(boss, 100f, new System.Collections.Generic.List<(NamespacedId, int)>());
            var hot = before - boss.Health;
            boss.OverheatUntil = float.NegativeInfinity;
            before = boss.Health;
            CreatureDirector.Instance.Damage(boss, 100f, new System.Collections.Generic.List<(NamespacedId, int)>());
            var cool = before - boss.Health;
            Assert.AreEqual(1.5f, hot / cool, 0.01f, "overheated damage multiplier");

            // Below half it calls clockwork beetles.
            var beetle = DefRegistry.Get<CreatureDef>(NamespacedId.Parse("isle:clockwork_beetle"));
            Teleport(player, boss.Position + Vector2.down * 8f);
            boss.Health = boss.MaxHealth * 0.4f;
            yield return WaitUntil(() => CreatureDirector.Instance.Creatures.Count(c => c.Def == beetle && Vector2.Distance(c.Position, boss.Position) < 6f) >= 2, 4f, "the Sentinel never called beetles");
            Teleport(player, site.Entrance + Vector2.down * 2f);
            vitals.Restore(100f, 100f, 100f, 100f, Vitals.ComfortableTemperature, 0f); // fed, watered, warm: only what's tested hurts
        }

        /// <summary>T-203 Drowned Temple: poison fog hurts and poisons; an antidote shrugs it off; the Mire Mother has
        /// HP 1100 and calls bog broods below 60%; bitter herbs grow in the marsh.</summary>
        IEnumerator DrownedTemple()
        {
            var player = FindLocalPlayer();
            var world = IslandWorld.Instance;
            var vitals = player.GetComponent<Vitals>();
            var dungeons = Isle.Gameplay.Dungeons.DungeonDirector.Instance;
            Assert.IsTrue(world.Nodes.Any(n => n.Def.Id.Value == "isle:bitter_herb_patch"), "no bitter herbs on the island");
            var site = dungeons.Sites.FirstOrDefault(s => s.Def.Id.Value == "isle:drowned_temple");
            Assert.IsNotNull(site, "no Drowned Temple on the island");
            var floor = dungeons.EnsureFloor(site, 0);
            Assert.Greater(floor.MiasmaTiles.Count, 0, "no poison fog on the floor");
            var fog = floor.TileToWorld(floor.MiasmaTiles.First(t => world.IsWalkable(floor.TileToWorld(t))));
            Assert.IsTrue(dungeons.IsMiasma(fog));

            // Only the fog should hurt here: clear the floor's creatures out of the way first.
            foreach (var creature in CreatureDirector.Instance.Creatures.Where(c => c.InDungeon && dungeons.FloorAt(c.Position) == floor).ToList())
                CreatureDirector.Instance.Damage(creature, 100000f, new System.Collections.Generic.List<(NamespacedId, int)>());
            vitals.Restore(100f, 100f, 100f, 100f, Vitals.ComfortableTemperature, 0f); // fed, watered, warm: only what's tested hurts
            Teleport(player, fog);
            yield return new WaitForSeconds(2.5f);
            Assert.Less(vitals.Health, 98f, "poison fog did no harm");
            Assert.Greater(vitals.Status.Stacks(Isle.Gameplay.Combat.DamageTypes.Toxic), 0, "poison fog didn't poison");

            // An antidote (full toxic resistance) shrugs it off.
            var clock = Isle.World.Time.WorldTime.Instance.Clock;
            vitals.Status.Clear();
            vitals.Buffs.Grant(DefRegistry.Get<BuffDef>(NamespacedId.Parse("isle:antidote")), clock.TotalMinutes);
            vitals.Restore(100f, 100f, 100f, 100f, Vitals.ComfortableTemperature, 0f); // fed, watered, warm: only what's tested hurts
            yield return new WaitForSeconds(2.5f);
            Assert.AreEqual(100f, vitals.Health, 0.5f, "the antidote didn't keep the fog out");
            Teleport(player, site.Entrance + Vector2.down * 2f);

            // The Mire Mother.
            dungeons.EnsureFloor(site, site.Def.Floors - 1);
            var boss = site.Boss;
            Assert.IsNotNull(boss, "Mire Mother never spawned");
            Assert.AreEqual(1100f, boss.MaxHealth, 0.01f, "Mire Mother HP");
            Teleport(player, boss.Position + Vector2.down * 6f);
            var brood = DefRegistry.Get<CreatureDef>(NamespacedId.Parse("isle:bog_brood"));
            boss.Health = boss.MaxHealth * 0.5f;
            yield return WaitUntil(() => CreatureDirector.Instance.Creatures.Count(c => c.Def == brood && Vector2.Distance(c.Position, boss.Position) < 6f) >= 3, 4f, "the Mire Mother never called her brood");
            Teleport(player, site.Entrance + Vector2.down * 2f);
        }

        /// <summary>T-203 Rootwood Hollow: cut roots grow back; a torch burns them for good but calls root sprites; the
        /// Elder Heartwood calls sprites of its own below half health.</summary>
        IEnumerator RootwoodHollow()
        {
            var player = FindLocalPlayer();
            var world = IslandWorld.Instance;
            var dungeons = Isle.Gameplay.Dungeons.DungeonDirector.Instance;
            var site = dungeons.Sites.FirstOrDefault(s => s.Def.Id.Value == "isle:rootwood_hollow");
            Assert.IsNotNull(site, "no Rootwood Hollow on the island");
            var floor = dungeons.EnsureFloor(site, 0);
            var gate = floor.Barriers.Single(b => b.LockId < 0);
            player.Skills.Restore(site.Def.Gate.Skill, Isle.Gameplay.Skills.XpCurve.TotalXpTo(50));
            var edge = floor.Layout.Edges[gate.Edge];
            var a = floor.Layout.Rooms[edge.A].Cell;
            var b2 = floor.Layout.Rooms[edge.B].Cell;
            var door = Isle.World.Generation.DungeonTiles.DoorwayCentre(a, b2);
            var stand = floor.TileToWorld(a.Y == b2.Y ? new Isle.Core.Vec2Int(door.X + 1, door.Y) : new Isle.Core.Vec2Int(door.X, door.Y + 1));
            Teleport(player, stand);
            player.RequestInteract();
            yield return WaitUntil(() => gate.Open, 8f, "root wall never cut");
            Assert.Greater(gate.RegrowAt, Time.time, "cut roots aren't set to grow back");
            Assert.AreEqual(300f, gate.RegrowAt - Time.time, 10f, "roots don't regrow in 5 minutes");

            // Grown back once the time is up (someone standing in it is waited for).
            gate.RegrowAt = Time.time;
            yield return null;
            Assert.IsTrue(gate.Open, "roots grew back on the player standing in them");
            Teleport(player, stand + (stand - gate.Position).normalized * 3f);
            yield return WaitUntil(() => !gate.Open, 2f, "roots never grew back");

            // A torch burns them away for good and wakes root sprites.
            var inventory = player.GetComponent<InventoryNetwork>();
            var torch = DefRegistry.Get<ItemDef>(NamespacedId.Parse("isle:torch"));
            inventory.Slots.Unequip("off_hand", out _);
            Assert.IsTrue(inventory.Slots.TryEquip("off_hand", torch), "couldn't hold a torch");
            var sprite = DefRegistry.Get<CreatureDef>(NamespacedId.Parse("isle:root_sprite"));
            var spritesBefore = CreatureDirector.Instance.Creatures.Count(c => c.Def == sprite);
            Teleport(player, stand);
            player.RequestInteract();
            yield return WaitUntil(() => gate.Open, 2f, "the torch didn't burn the roots");
            Assert.IsTrue(gate.Burned, "burned roots not marked");
            Assert.AreEqual(spritesBefore + 2, CreatureDirector.Instance.Creatures.Count(c => c.Def == sprite), "burning didn't call 2 root sprites");
            gate.RegrowAt = Time.time;
            yield return null;
            Assert.IsTrue(gate.Open, "burned roots grew back");
            inventory.Slots.Unequip("off_hand", out _);

            // The Elder Heartwood: HP 800; below half it calls root sprites.
            var last = dungeons.EnsureFloor(site, site.Def.Floors - 1);
            var boss = site.Boss;
            Assert.IsNotNull(boss, "Elder Heartwood never spawned");
            Assert.AreEqual(800f, boss.MaxHealth, 0.01f, "Elder Heartwood HP");
            Teleport(player, boss.Position + Vector2.down * 6f);
            boss.Health = boss.MaxHealth * 0.4f;
            var near = 0;
            yield return WaitUntil(() => (near = CreatureDirector.Instance.Creatures.Count(c => c.Def == sprite && Vector2.Distance(c.Position, boss.Position) < 6f)) >= 2, 4f, "the Heartwood never called root sprites");
            Assert.LessOrEqual(near, boss.Def.Boss.Summon.MaxAlive, "summons past the cap");
            Assert.IsNotNull(last);
        }

        /// <summary>Drives the real ServerRpc paths on the host: harvest a tree and a rock, craft a spear from
        /// them, drink seawater, and kill a rabbit with fists. Nodes are picked away from water so the interact
        /// key's drink-first priority doesn't take over.</summary>
        IEnumerator GatherCraftDrinkHunt()
        {
            var player = FindLocalPlayer();
            var inventory = player.GetComponent<Isle.Gameplay.Inventory.InventoryNetwork>();
            var vitals = player.GetComponent<Vitals>();
            var world = IslandWorld.Instance;
            inventory.Bag.Clear();

            // Harvests take time now (SYS-WORLD-03); a master gatherer keeps the test short (tree 10 s → 3 s).
            player.Skills.Restore(NamespacedId.Parse("isle:gathering"), Isle.Gameplay.Skills.XpCurve.TotalXpTo(50));
            yield return HarvestNearest(player, world, "isle:tree");
            // Standing trees block their trunk; a felled one doesn't.
            var standingTree = world.Nodes.First(n => n.Def.Id.Value == "isle:tree" && n.UsesLeft > 0);
            Assert.IsFalse(world.IsWalkable(standingTree.Position), "walked through a tree trunk");
            var felled = world.Nodes.First(n => n.Def.Id.Value == "isle:tree" && n.UsesLeft <= 0);
            Assert.IsTrue(world.IsWalkable(felled.Position), "a felled tree still blocks");
            yield return HarvestNearest(player, world, "isle:rock");
            Assert.AreEqual(4, CountOf(inventory, "isle:wood"), "tree harvest");
            Assert.Greater(player.Skills.TotalXp(NamespacedId.Parse("isle:gathering")), 0d, "harvesting earned no gathering XP");
            Assert.AreEqual(2, CountOf(inventory, "isle:stone"), "rock harvest");

            player.RequestCraft("isle:craft_spear", "isle:stone");
            // T-151 playtest: crafting takes time now — under way, nothing made or spent yet.
            yield return WaitUntil(() => player.Crafting != null, 2f, "crafting never started");
            Assert.AreEqual(0, CountOf(inventory, "isle:spear__stone"), "crafted instantly");
            Assert.AreEqual(4, CountOf(inventory, "isle:wood"), "inputs spent before the craft finished");
            yield return WaitUntil(() => CountOf(inventory, "isle:spear__stone") == 1, 12f, "spear never crafted");
            Assert.AreEqual(2, CountOf(inventory, "isle:wood"), "wood not consumed");
            // SYS-CRAFT-01 §Quality: crafted gear carries its tier, and its durability is scaled by it.
            var spear = inventory.Bag.Placements.First(p => p.Item.Id.Value == "isle:spear__stone");
            Assert.IsNotNull(spear.Wear?.Quality, "crafted spear has no quality");
            Assert.AreEqual(Isle.Gameplay.Crafting.QualityCalculator.MaxDurability(spear.Item.Durability.Value, spear.Wear.Quality.Value), spear.Wear.Max,
                "durability not scaled by quality");
            // SYS-CRAFT-01 §Crafter mark: it carries its maker's name.
            Assert.AreEqual(player.Survivor.Name, spear.Wear.Crafter, "crafted spear has no crafter mark");

            // SYS-CRAFT-01 §Forging: a copper knife at an anvil is hammered out — bellows into the window, strike, three
            // times — and only then made, from the inputs still held.
            player.Skills.Restore(NamespacedId.Parse("isle:crafting"), Isle.Gameplay.Skills.XpCurve.TotalXpTo(10));
            ClearCreaturesNear(player.transform.position);
            var anvil = Isle.Gameplay.Building.StructureFactory.Build(DefRegistry.Get<WorldObjectDef>(NamespacedId.Parse("isle:anvil")),
                (Vector2)player.transform.position + Vector2.right * 1f);
            ClearLeftovers(inventory);
            Give(inventory, "isle:copper_ingot", 2);
            Give(inventory, "isle:wood", 2);
            player.RequestCraft("isle:craft_knife", "isle:copper");
            yield return WaitUntil(() => player.Forge != null, 2f, "forging never started");
            Assert.AreEqual(0, CountOf(inventory, "isle:knife__copper"), "forged gear made before the hammering");
            var forge = player.Forge;
            for (var strike = 0; strike < forge.Required; strike++)
            {
                player.RequestForgeBellows(true);
                yield return WaitUntil(() => forge.Heat >= Isle.Gameplay.Crafting.ForgingMinigame.BandCentre, 6f, "bellows never heated the metal");
                player.RequestForgeBellows(false);
                player.RequestForgeStrike();
                yield return WaitUntil(() => forge.Strikes > strike, 2f, "the hammer never fell");
                yield return new WaitForSeconds(Isle.Gameplay.Crafting.ForgingMinigame.StrikeCooldown);
            }
            yield return WaitUntil(() => CountOf(inventory, "isle:knife__copper") == 1, 3f, "forged knife never made");
            Assert.IsNull(player.Forge, "forging didn't end");
            Assert.Greater(forge.Successes, 0, "no strike landed in the window");
            var knife = inventory.Bag.Placements.First(p => p.Item.Id.Value == "isle:knife__copper");
            Assert.IsNotNull(knife.Wear?.Quality, "forged knife has no quality");
            Isle.Gameplay.Building.StructureFactory.Remove(anvil);

            // Drinking is from terrain water now: stand on the shore and E drinks the sea.
            Teleport(player, CoastSpot(world).Shore);
            var thirstBefore = vitals.Thirst;
            player.RequestInteract();
            yield return WaitUntil(() => vitals.Thirst < thirstBefore - 10f, 3f, "seawater didn't lower thirst");

            // Rabbit weight rolls 1.5–5 kg, so a heavy one survives a single punch: keep after it.
            var rabbit = CreatureDirector.Instance.Creatures.First(c => c.Def.Id.Value == "isle:rabbit");
            for (var attempt = 0; attempt < 5 && !rabbit.Dead; attempt++)
            {
                Teleport(player, rabbit.Position + Vector2.right * 0.5f);
                player.RequestAttack();
                yield return new WaitForSeconds(0.8f);
            }
            // SYS-HUNT-01: the kill leaves a carcass; butchering it (E, takes a moment) gives the meat — at least one piece.
            Assert.IsTrue(rabbit.Dead, "rabbit never died");
            Assert.Contains(rabbit, CreatureDirector.Instance.Carcasses.ToList(), "no carcass left");
            Assert.AreEqual(0, CountOf(inventory, "isle:raw_meat"), "the kill itself handed out meat");
            // SYS-HUNT-01 §Carrying: a rabbit is light — G puts it in the bag; dropping it lays the carcass back down.
            ClearLeftovers(inventory); // a rabbit weighs up to 5 kg
            Teleport(player, rabbit.Position + Vector2.right * 0.3f);
            player.RequestHaul();
            yield return WaitUntil(() => CountOf(inventory, "isle:carcass__rabbit") == 1, 2f, "rabbit carcass never picked up");
            Assert.IsFalse(CreatureDirector.Instance.Carcasses.Contains(rabbit), "picked-up carcass still on the ground");
            // G with nothing in reach puts the bagged carcass down in front of you; G again picks it back up.
            player.RequestHaul();
            yield return WaitUntil(() => CountOf(inventory, "isle:carcass__rabbit") == 0, 2f, "G never put the bagged carcass down");
            Assert.IsTrue(CreatureDirector.Instance.Carcasses.Any(c => c.Def.Id.Value == "isle:rabbit"), "put-down carcass isn't on the ground");
            player.RequestHaul();
            yield return WaitUntil(() => CountOf(inventory, "isle:carcass__rabbit") == 1, 2f, "put-down carcass never picked up again");
            var carried = inventory.Containers().SelectMany(c => c.Placements).First(pl => pl.Item.Id.Value == "isle:carcass__rabbit");
            Assert.AreEqual(rabbit.Weight, carried.Wear.Carcass.WeightKg, 1e-4f, "carried body lost its weight");
            var bagIndex = inventory.Containers().IndexOf(inventory.Containers().First(c => c.Placements.Contains(carried)));
            var carcassDropped = false;
            inventory.RequestDrop(bagIndex, carried.Position, 1, ok => carcassDropped = ok);
            yield return WaitUntil(() => carcassDropped, 2f, "carcass never dropped");
            rabbit = CreatureDirector.Instance.Carcasses.OrderBy(c => Vector2.Distance(c.Position, player.transform.position)).First();
            Assert.AreEqual("isle:rabbit", rabbit.Def.Id.Value, "dropping the bag item didn't lay a carcass down");
            player.RequestInteract();
            yield return WaitUntil(() => player.Butchering != null, 2f, "butchering never started");
            yield return WaitUntil(() => CountOf(inventory, "isle:raw_meat") > 0, 5f, "butchering gave no meat");
            Assert.IsFalse(CreatureDirector.Instance.Carcasses.Contains(rabbit), "carcass still there after butchering");

            // A heavy carcass is dragged (×0.4 speed, follows behind), and left a day it rots: only rotten meat comes off.
            var boarDef = DefRegistry.Get<CreatureDef>(NamespacedId.Parse("isle:boar"));
            var boar = CreatureDirector.Instance.PutCarcass(new Isle.Gameplay.Hunting.CarcassState { Def = boarDef, WeightKg = 62f }, (Vector2)player.transform.position + Vector2.right * 0.5f);
            player.RequestHaul();
            yield return WaitUntil(() => player.Hauling == boar, 2f, "boar never dragged");
            Assert.AreEqual(Isle.Gameplay.Hunting.CarcassCalculator.DragSpeedMult, player.HaulSpeed, 1e-5f);
            Teleport(player, (Vector2)player.transform.position + Vector2.up * 3f);
            yield return WaitUntil(() => Vector2.Distance(boar.Position, player.transform.position) < 1.2f, 2f, "dragged carcass didn't follow");
            player.RequestHaul();
            yield return WaitUntil(() => player.Hauling == null, 2f, "boar never put down");
            boar.ScavengersDrawn = int.MaxValue; // the scent's predators are checked in EditMode; here they'd only interrupt
            var dayClock = Isle.World.Time.WorldTime.Instance.Clock;
            // Half an hour at a time, so it stops in the rotten band instead of rotting away entirely (heat speeds it up).
            for (var step = 0; step < 80 && boar.Spoilage < Isle.Gameplay.Hunting.CarcassCalculator.SpoilRotten; step++)
            {
                dayClock.SetTotalMinutes(dayClock.TotalMinutes + 30);
                yield return null;
                yield return null;
            }
            Assert.That(boar.Spoilage, Is.InRange(Isle.Gameplay.Hunting.CarcassCalculator.SpoilRotten, Isle.Gameplay.Hunting.CarcassCalculator.SpoilGone), "carcass never spoiled");
            var rottenBefore = CountOf(inventory, "isle:rotten_food");
            Teleport(player, boar.Position + Vector2.right * 0.4f);
            player.RequestInteract();
            yield return WaitUntil(() => !CreatureDirector.Instance.Carcasses.Contains(boar), 30f, "rotten boar never butchered");
            Assert.Greater(CountOf(inventory, "isle:rotten_food"), rottenBefore, "a rotten carcass gave no rotten meat");
            Assert.AreEqual(0, CountOf(inventory, "isle:boar_hide"), "a rotten carcass still gave hide");

            player.RequestEquipItem("isle:spear__stone");
            yield return WaitUntil(() => inventory.Slots.Get("main_hand")?.Id.Value == "isle:spear__stone", 3f, "spear never wielded");
            Assert.AreEqual(0, CountOf(inventory, "isle:spear__stone"), "wielded spear still in bag");

            // SYS-COMBAT-01 §Melee: raise the guard toward a strike from the right — a fresh guard parries, a held one
            // blocks for stamina, and a hit from behind gets through. Then three swings in rhythm make a full combo.
            var guardAt = (Vector2)player.transform.position;
            player.RequestBlock(true, Vector2.right);
            yield return WaitUntil(() => player.Blocking, 2f, "guard never went up");
            // Read just before the strike: vitals ticks (cold, hunger) may shave health while the guard goes up.
            var guardHealth = vitals.Health;
            Assert.AreEqual(BlockOutcome.Parried, player.ReceiveCreatureStrike(guardAt + Vector2.right, 10f), "fresh guard didn't parry");
            Assert.AreEqual(guardHealth, vitals.Health, 1e-3f, "parry let damage through");
            yield return new WaitForSeconds(0.5f);
            guardHealth = vitals.Health;
            var guardStamina = vitals.Stamina;
            Assert.AreEqual(BlockOutcome.Blocked, player.ReceiveCreatureStrike(guardAt + Vector2.right, 10f), "held guard didn't block");
            Assert.Less(vitals.Stamina, guardStamina, "block cost no stamina");
            Assert.AreEqual(guardHealth, vitals.Health, 1e-3f, "block let damage through");
            Assert.AreEqual(BlockOutcome.None, player.ReceiveCreatureStrike(guardAt + Vector2.left, 10f), "guard covered the back");
            Assert.Less(vitals.Health, guardHealth, "hit from behind did no damage");
            player.RequestBlock(false, Vector2.right);
            yield return WaitUntil(() => !player.Blocking, 2f, "guard never came down");

            Teleport(player, CoastSpot(world).Shore); // open ground, nothing to hit
            vitals.ResetOnRespawn();
            for (var swing = 1; swing <= 3; swing++)
            {
                player.RequestAttack();
                var expected = swing;
                yield return WaitUntil(() => player.ComboStep == expected, 3f, $"combo step {expected} never landed");
                yield return new WaitForSeconds(1.3f); // spear: 0.8 swings/s
            }
            Assert.AreEqual(3, player.ComboLength);

            // SYS-INV-01 grid inventory: the Tab screen is bound to this player, and a stack moves from the base carry
            // into a worn backpack through the server (InventoryNetwork.RequestMove).
            Assert.IsNotNull(Isle.UI.Inventory.InventoryScreen.Instance, "inventory screen not installed");
            Give(inventory, "isle:straw_backpack", 1);
            player.RequestEquipItem("isle:straw_backpack");
            yield return WaitUntil(() => inventory.Slots.BagFor("back") != null, 3f, "backpack never worn");
            Give(inventory, "isle:fiber", 3);
            var fiber = inventory.Bag.Placements.First(p => p.Item.Id.Value == "isle:fiber");
            bool? moved = null;
            inventory.RequestMove(0, fiber.Position, 1, new Isle.Core.Vec2Int(0, 0), false, fiber.Count, ok => moved = ok);
            yield return WaitUntil(() => moved.HasValue, 3f, "move never acknowledged");
            Assert.IsTrue(moved.Value, "server refused the bag → backpack move");
            Assert.IsTrue(inventory.Slots.BagFor("back").Placements.Any(p => p.Item.Id.Value == "isle:fiber"), "fiber not in the backpack");

            // Dropping: the stack leaves the backpack and lands in a pile at the player's feet.
            var inPack = inventory.Slots.BagFor("back").Placements.First(p => p.Item.Id.Value == "isle:fiber");
            var pilesBefore = LootPiles.All.Count;
            bool? dropped = null;
            inventory.RequestDrop(1, inPack.Position, inPack.Count, ok => dropped = ok);
            yield return WaitUntil(() => dropped.HasValue, 3f, "drop never acknowledged");
            Assert.IsTrue(dropped.Value, "server refused the drop");
            Assert.IsFalse(inventory.Slots.BagFor("back").Placements.Any(p => p.Item.Id.Value == "isle:fiber"), "dropped fiber still in the backpack");
            Assert.AreEqual(pilesBefore + 1, LootPiles.All.Count, "no pile for the dropped fiber");
            player.RequestInteract(); // pick it back up so the later loot-pile checks start clean
            yield return WaitUntil(() => LootPiles.All.Count == pilesBefore, 3f, "dropped pile never picked up");

            // Lit directly: walking up and pressing E could hit a tree first if one stands within reach.
            var fire = Object.FindObjectsByType<WorldObjectInstance>(FindObjectsSortMode.None).First(f => f.HasTag("station/campfire"));
            fire.GetComponent<Isle.Gameplay.World.CampfireInteraction>().Fuel = 480f; // SYS-SURV-01 §Fire: a fire burns fuel
            fire.IsActive = true;
            Teleport(player, fire.transform.position + Vector3.down);
            var rawBefore = CountOf(inventory, "isle:raw_meat");
            // SYS-COOK-01: grill one raw meat at the lit campfire → a generated dish; eating it grants the grill's
            // meat reaction (endurance) and its cooked nutrition.
            player.RequestCook("isle:grill", new[] { "isle:raw_meat" });
            // SYS-COOK-01 §Cook time: it takes a few seconds now, standing still.
            yield return WaitUntil(() => player.Cooking != null || Dish(inventory) != null, 2f, "cooking didn't start");
            Assert.IsNull(Dish(inventory), "cooked instantly");
            yield return WaitUntil(() => Dish(inventory) != null, 12f, "meat never cooked");
            Assert.AreEqual(rawBefore - 1, CountOf(inventory, "isle:raw_meat"));
            var dish = Dish(inventory);
            Assert.Greater(dish.Nutrition.Hunger, 0f);

            player.RequestUse(dish.Id.Value);
            yield return WaitUntil(() => Dish(inventory) == null, 3f, "dish never eaten");
            Assert.IsTrue(vitals.ActiveBuffs().Any(b => b.Id.Value == "isle:endurance"), "grilled meat granted no endurance");

            // Building: place a crate, store wood in it, take it back. Kits are given directly — the craft path is
            // already covered above.
            Give(inventory, "isle:crate_kit", 1);
            var crateAt = (Vector2)player.transform.position + Vector2.right * 1.5f;
            Assert.IsTrue(player.CanPlaceAt(crateAt), "crate spot rejected");
            player.RequestPlace("isle:crate_kit", crateAt);
            yield return WaitUntil(() => StructureFactory.NearestStorage(player.transform.position, 2f) != null, 3f, "crate never placed");
            var crate = StructureFactory.NearestStorage(player.transform.position, 2f);
            Assert.AreEqual(0, CountOf(inventory, "isle:crate_kit"), "kit not consumed");

            Give(inventory, "isle:wood", 5);
            var woodHeld = CountOf(inventory, "isle:wood");
            player.RequestStore("isle:wood");
            yield return WaitUntil(() => crate.Contents.Placements.Any(pl => pl.Item.Id.Value == "isle:wood"), 3f, "wood never stored");
            Assert.AreEqual(0, CountOf(inventory, "isle:wood"));
            player.RequestTake("isle:wood");
            yield return WaitUntil(() => CountOf(inventory, "isle:wood") == woodHeld, 3f, "wood never taken back");

            // Clothing (SYS-CRAFT-02): a wolf-fur parka — warmth and per-type armor come from the template × the material.
            var parka = DefRegistry.Get<ItemDef>(NamespacedId.Parse("isle:parka__wolf_fur"));
            Assert.AreEqual(6f * 2.5f, parka.Warmth, 0.01f, "parka warmth = 6 × wolf fur 2.5");
            Give(inventory, parka.Id.Value, 1);
            player.RequestEquipItem(parka.Id.Value);
            yield return WaitUntil(() => Mathf.Approximately(vitals.ClothingBonus, parka.Warmth), 3f, "parka warmth never applied");

            // Armor: the hit is blunt, so the parka's blunt armor applies (SYS-COMBAT-01 §Damage curve).
            // SYS-MED-01: armor covers its parts — the parka covers the torso, so the hit lands there.
            var healthBefore = vitals.Health;
            Vitals.WoundRoll = () => 0.05f;
            vitals.TakeDamage(10f);
            Vitals.WoundRoll = null;
            var expectedHit = Isle.Gameplay.Combat.DamageTypes.Damage(10f, "blunt", 1f, parka.ArmorTypes["blunt"]);
            Assert.AreEqual(expectedHit, healthBefore - vitals.Health, 0.3f, "armor not applied");
            // SYS-CRAFT-02: the hit wore the cloak (checked now: a wild animal may land a blow during the harvests below).
            Assert.AreEqual(59, inventory.Slots.WearOf("chest").Current, "hit didn't wear the cloak");

            // Backpack: equipping one adds a second container that items can go into.
            Give(inventory, "isle:straw_backpack", 1);
            player.RequestEquipItem("isle:straw_backpack");
            yield return WaitUntil(() => inventory.Containers().Count == 2, 3f, "backpack never opened a grid");

            // Tool: a hatchet in hand adds the tree's tool_bonus (2 → 4 wood).
            Give(inventory, "isle:hatchet__stone", 1);
            player.RequestEquipItem("isle:hatchet__stone");
            yield return WaitUntil(() => inventory.Slots.Get("main_hand")?.Id.Value == "isle:hatchet__stone", 3f, "hatchet never equipped");
            var woodBefore = CountOf(inventory, "isle:wood");
            yield return HarvestNearest(player, world, "isle:tree");
            Assert.AreEqual(woodBefore + 6, CountOf(inventory, "isle:wood"), "hatchet bonus not applied");

            // SYS-CRAFT-02: the harvest wore the hatchet; a broken hatchet is no tool; a repair costs half the recipe and
            // lowers the maximum to ×0.92.
            Assert.AreEqual(59, inventory.Slots.WearOf("main_hand").Current, "harvest didn't wear the hatchet");
            inventory.Slots.Wear("main_hand", 59);
            Assert.IsNull(inventory.Slots.Working("main_hand"), "broken hatchet still works");
            ClearLeftovers(inventory); // room for the next harvest
            woodBefore = CountOf(inventory, "isle:wood");
            yield return HarvestNearest(player, world, "isle:tree");
            Assert.AreEqual(woodBefore + 4, CountOf(inventory, "isle:wood"), "broken hatchet still gave its bonus");
            Give(inventory, "isle:stone", 1);
            Give(inventory, "isle:fiber", 1);
            player.RequestRepairEquipped("main_hand");
            yield return WaitUntil(() => inventory.Slots.WearOf("main_hand").Max == 55, 3f, "hatchet never repaired");
            Assert.AreEqual(55, inventory.Slots.WearOf("main_hand").Current);

            // Tiered veins: an iron vein refuses a stone pickaxe and yields to a copper one.
            Give(inventory, "isle:pickaxe__stone", 1);
            player.RequestEquipItem("isle:pickaxe__stone");
            yield return WaitUntil(() => inventory.Slots.Get("main_hand")?.Id.Value == "isle:pickaxe__stone", 3f, "pickaxe never equipped");
            var vein = world.Nodes.FirstOrDefault(n => n.Def.Id.Value == "isle:iron_vein" && n.IsHarvestable && world.NearestWater(n.Position, 3f) == null);
            Assert.IsNotNull(vein, "no iron vein on the island");
            Teleport(player, vein.Position);
            player.RequestInteract();
            yield return new WaitForSeconds(0.5f);
            Assert.IsNull(player.Gathering, "stone pickaxe started on an iron vein");
            // The better pickaxe only sits in the bag: working the vein takes it out on its own.
            Give(inventory, "isle:pickaxe__copper", 1);
            yield return HarvestNearest(player, world, "isle:iron_vein");
            Assert.AreEqual("isle:pickaxe__copper", inventory.Slots.Get("main_hand")?.Id.Value, "best pickaxe wasn't taken out");
            Assert.AreEqual(1, CountOf(inventory, "isle:pickaxe__stone"), "the stone pickaxe didn't go back in the bag");
            Assert.AreEqual(3, CountOf(inventory, "isle:iron_ore"), "iron vein: 2 + 1 tool bonus");

            // Smelting at a furnace.
            player.Skills.Restore(NamespacedId.Parse("isle:crafting"), Isle.Gameplay.Skills.XpCurve.TotalXpTo(20));
            // T-305: a 14 kg kit outweighs the pockets and the straw backpack — it's set up straight from the ground.
            LootPiles.Drop(player.transform.position, new[] { (DefRegistry.Get<ItemDef>(NamespacedId.Parse("isle:furnace_kit")), 1) });
            var furnaceAt = (Vector2)player.transform.position + Vector2.right * 1.5f;
            if (!player.CanPlaceAt(furnaceAt)) furnaceAt = (Vector2)player.transform.position + Vector2.left * 1.5f;
            player.RequestPlace("isle:furnace_kit", furnaceAt);
            yield return WaitUntil(() => StructureFactory.Built.Any(b => b != null && b.Def != null && b.Def.Id.Value == "isle:furnace"), 3f, "furnace never placed");
            Assert.IsFalse(LootPiles.All.Any(p => p.Items.Any(e => e.Item.Id.Value == "isle:furnace_kit")), "the kit stayed on the ground");
            Give(inventory, "isle:coal", 1);
            player.RequestCraft("isle:craft_iron_ingot");
            yield return WaitUntil(() => CountOf(inventory, "isle:iron_ingot") == 1, 12f, "no iron ingot smelted");
            Assert.AreEqual(1, CountOf(inventory, "isle:iron_ore"), "smelting took 2 ore");

            // Ranged: equip a bow, draw for a full charge, loose. Sway makes the hit itself random at level 0, so
            // the check is that an arrow was spent and a projectile flew and expired.
            Give(inventory, "isle:short_bow", 1);
            Give(inventory, "isle:arrow__stone", 3);
            Assert.AreEqual(3, CountOf(inventory, "isle:arrow__stone"), "test setup: arrows didn't fit");
            player.RequestEquipItem("isle:short_bow");
            yield return WaitUntil(() => player.HoldsRangedWeapon(), 3f, "bow never equipped");
            player.RequestBeginDraw();
            yield return new WaitForSeconds(0.9f);
            player.RequestLoose((Vector2)player.transform.position + Vector2.left * 5f);
            yield return WaitUntil(() => CountOf(inventory, "isle:arrow__stone") == 2, 3f, "no arrow spent");
            // In flight — unless it already struck something close (after the day skip above, animals roam nearby).
            Assert.LessOrEqual(Isle.Gameplay.Combat.Projectiles.Instance.InFlight, 1, "more than one projectile from one shot");
            yield return WaitUntil(() => Isle.Gameplay.Combat.Projectiles.Instance.InFlight == 0, 3f, "arrow never landed");

            // Farming: plant seeds, let a full growth period pass on the world clock, harvest.
            Give(inventory, "isle:berry_seeds", 1);
            var plotAt = (Vector2)player.transform.position + Vector2.down * 1.5f;
            Assert.IsTrue(player.CanPlaceAt(plotAt), "plot spot rejected");
            player.RequestPlace("isle:berry_seeds", plotAt);
            yield return WaitUntil(() => StructureFactory.Built.Any(b => b != null && b.GetComponent<CropPlot>() != null), 3f, "seeds never planted");
            var plot = StructureFactory.Built.First(b => b != null && b.GetComponent<CropPlot>() != null).GetComponent<CropPlot>();
            Assert.IsFalse(CropCalculator.IsRipe(plot.Growth), "crop ripe at planting");
            var clock = Isle.World.Time.WorldTime.Instance.Clock;
            clock.SetTotalMinutes(clock.TotalMinutes + 1440);
            Assert.IsTrue(CropCalculator.IsRipe(plot.Growth), "crop not ripe after its growth days");
            var berriesBefore = CountOf(inventory, "isle:berries");
            plot.Interact(player.gameObject);
            Assert.AreEqual(berriesBefore + 6, CountOf(inventory, "isle:berries"), "harvest didn't give output.base_count");

            // Rod fishing: a fight starts, runs on the server, and ends. No mouse in batchmode, so nobody reels and the
            // hook slips — the point is the session lifecycle, not the catch.
            // No rod, no fishing; a rod in the bag comes out on its own when casting.
            var (shore, sea) = CoastSpot(world);
            Teleport(player, shore);
            player.RequestCast(sea);
            yield return new WaitForSeconds(0.5f);
            Assert.IsNull(player.Cast, "cast without a rod");
            Give(inventory, "isle:fishing_rod", 1);
            // Cast → bite → hook (SYS-FISH-01 prototype revision). Below Fishing 5 the rod fishes as a handline:
            // hooking lands the fish with no fight.
            var fishBefore = CountOf(inventory, "isle:raw_fish");
            yield return CastAndHook(player, sea);
            Assert.IsNull(player.Fight, "rod worked below Fishing 5");
            Assert.AreEqual("isle:fishing_rod", inventory.Slots.Get("main_hand")?.Id.Value, "rod wasn't taken out to cast");
            yield return WaitUntil(() => CountOf(inventory, "isle:raw_fish") > fishBefore, 2f, "handline hook landed nothing");

            player.Skills.Restore(NamespacedId.Parse("isle:fishing"), Isle.Gameplay.Skills.XpCurve.TotalXpTo(5));
            yield return CastAndHook(player, sea);
            yield return WaitUntil(() => player.Fight != null, 3f, "no fight started");
            Assert.Greater(player.FightWeightKg, 0f);
            yield return WaitUntil(() => player.Fight == null, 8f, "fight never ended");

            // Death: everything drops into one pile, the body can't move, and respawn brings it back.
            var death = player.GetComponent<DeathHandler>();
            var deathSpot = (Vector2)player.transform.position;
            vitals.TakeDamage(1000f);
            yield return WaitUntil(() => death.IsDead, 3f, "player never died");
            Assert.AreEqual(0, inventory.Bag.Placements.Count, "bag not emptied on death");
            Assert.IsNull(inventory.Slots.Get("main_hand"), "hand not emptied on death");
            var pile = LootPiles.Nearest(deathSpot, 1f);
            Assert.IsNotNull(pile, "no loot pile at the death spot");
            Assert.IsTrue(pile.Items.Any(i => i.Item.Id.Value == "isle:spear__stone"), "equipped spear not in the pile");
            Assert.IsTrue(player.GetComponent<PlayerMovement>().Frozen, "dead player can still move");

            yield return WaitUntil(() => !death.IsDead, DeathHandler.RespawnDelaySeconds + 3f, "never respawned");
            Assert.AreEqual(VitalsCalculator.GaugeMax, vitals.Health, 0.01f);
            Assert.IsFalse(player.GetComponent<PlayerMovement>().Frozen);

            // SYS-INV-02: what doesn't fit by weight stays on the ground; the gear comes back first.
            var pileBefore = pile.Items.Count;
            Teleport(player, deathSpot);
            player.RequestInteract();
            yield return WaitUntil(() => LootPiles.Nearest(deathSpot, 1f) is not { } left || left.Items.Count < pileBefore, 3f, "pile never picked up");
            // The repaired hatchet came back through unequip → bag → death pile with its wear — into the bag, or straight
            // back into the empty main hand (recovering a body re-equips gear first).
            var hatchetWear = inventory.Containers().SelectMany(c => c.Placements).Where(pl => pl.Item.Id.Value == "isle:hatchet__stone").Select(pl => pl.Wear)
                .Append(inventory.Slots.Get("main_hand")?.Id.Value == "isle:hatchet__stone" ? inventory.Slots.WearOf("main_hand") : null);
            Assert.IsTrue(hatchetWear.Any(w => w != null && w.Max == 55), "hatchet wear lost through the death pile");
            // Recovering a body re-equips gear into empty slots first, so the spear may be back in hand instead of the bag.
            var spearBack = CountOf(inventory, "isle:spear__stone") + (inventory.Slots.Get("main_hand")?.Id.Value == "isle:spear__stone" ? 1 : 0);
            Assert.AreEqual(1, spearBack, "spear not recovered from the pile");
        }

        /// <summary>SYS-DUNG-01 (T-201): walk into an entrance, take each lock's key and see the door open, clear the
        /// vault's soft gate as a specialist, then leave by the entrance stairs.</summary>
        IEnumerator EnterClearAndLeaveADungeon()
        {
            var player = FindLocalPlayer();
            var world = IslandWorld.Instance;
            var dungeons = Isle.Gameplay.Dungeons.DungeonDirector.Instance;
            Assert.IsNotNull(dungeons, "DungeonDirector not installed");
            yield return WaitUntil(() => dungeons.Sites.Count > 0, 5f, "no dungeon entrance placed");
            var site = dungeons.Sites.FirstOrDefault(s => s.Def.Id.Value == "isle:tidal_grotto") ?? dungeons.Sites[0];
            Assert.IsTrue(world.IsWalkable(site.Entrance), "entrance placed off land");

            Teleport(player, site.Entrance + Vector2.down);
            player.RequestInteract();
            yield return WaitUntil(() => dungeons.IsUnderground(player.transform.position), 3f, "never went down");
            var floor = dungeons.FloorAt(player.transform.position);
            Assert.IsTrue(world.IsWalkable(player.transform.position), "landed on a wall");

            foreach (var barrier in floor.Barriers.Where(b => b.LockId >= 0))
            {
                Assert.IsFalse(world.IsWalkable(barrier.Position), "a locked door can be walked through");
                var key = floor.Keys.First(k => k.LockId == barrier.LockId);
                Teleport(player, key.Position);
                player.RequestInteract();
                yield return WaitUntil(() => barrier.Open, 3f, "taking the key never opened its door");
                Assert.IsTrue(world.IsWalkable(barrier.Position), "opened door still blocks");
            }

            var gate = floor.Barriers.Single(b => b.LockId < 0);
            var skill = site.Def.Gate.Skill;
            player.Skills.Restore(skill, Isle.Gameplay.Skills.XpCurve.TotalXpTo(50));
            var edge = floor.Layout.Edges[gate.Edge];
            var a = floor.Layout.Rooms[edge.A].Cell;
            var b2 = floor.Layout.Rooms[edge.B].Cell;
            var door = Isle.World.Generation.DungeonTiles.DoorwayCentre(a, b2);
            var stand = a.Y == b2.Y ? new Isle.Core.Vec2Int(door.X + 1, door.Y) : new Isle.Core.Vec2Int(door.X, door.Y + 1);
            Teleport(player, floor.TileToWorld(stand));
            player.RequestInteract();
            yield return WaitUntil(() => gate.Open, 8f, "specialist never cleared the soft gate");

            // T-202 (Tidal Grotto only): high tide floods water tiles and slows swimmers; low tide opens the cache.
            if (site.Def.Tides != null)
            {
                var clock = Isle.World.Time.WorldTime.Instance.Clock;
                var day = clock.TotalMinutes / Isle.World.Time.WorldClock.MinutesPerDay;
                clock.SetTotalMinutes(day * Isle.World.Time.WorldClock.MinutesPerDay + 6 * 60);
                yield return WaitUntil(() => site.TideHigh, 2f, "tide never came in at 06:00");
                var water = floor.TileToWorld(floor.WaterTiles.First());
                Assert.IsTrue(dungeons.IsWater(water), "high tide left a flooded room dry");
                Assert.AreEqual(site.Def.Tides.SwimSpeed, Isle.Networking.PlayerMovement.TerrainSpeed(water), 1e-4f, "swimming isn't slowed");
                Assert.IsFalse(floor.Cache != null && floor.Cache.View.activeSelf, "cache visible under water");

                clock.SetTotalMinutes(day * Isle.World.Time.WorldClock.MinutesPerDay + 12 * 60);
                yield return WaitUntil(() => !site.TideHigh, 2f, "tide never went out at 12:00");
                Assert.IsFalse(dungeons.IsWater(water));
                Assert.IsNotNull(floor.Cache, "no low-tide cache on the floor");
                Teleport(player, floor.Cache.Position);
                player.RequestInteract();
                yield return WaitUntil(() => floor.Cache.Taken, 3f, "low-tide cache never opened");
                Assert.That(CountOf(player.GetComponent<InventoryNetwork>(), "isle:tide_pearl") + LootPiles.All.Sum(pile => pile.Items.Where(i => i.Item.Id.Value == "isle:tide_pearl").Sum(i => i.Count)), Is.InRange(1, 2), "cache didn't hold 1–2 tide pearls");
            }

            // The boss holds the last floor's way out until it falls; its drops land where it died.
            if (site.Def.Boss.IsValid && site.Def.Floors == 1)
            {
                var boss = site.Boss;
                Assert.IsNotNull(boss, "boss never spawned");
                Assert.AreEqual(600f, boss.MaxHealth, 0.01f, "Hermit Colossus HP");
                var exits = floor.Portals.Count(p => p.Kind == Isle.Gameplay.Dungeons.DungeonDirector.DoorKind.Exit);
                var bossAt = boss.Position;
                CreatureDirector.Instance.Damage(boss, 100000f, new System.Collections.Generic.List<(NamespacedId, int)>());
                yield return WaitUntil(() => site.BossDefeated, 2f, "boss death never registered");
                Assert.AreEqual(exits + 1, floor.Portals.Count(p => p.Kind == Isle.Gameplay.Dungeons.DungeonDirector.DoorKind.Exit), "no way out opened in the boss room");
                var pile = LootPiles.Nearest(bossAt, 1.5f);
                Assert.IsNotNull(pile, "boss dropped nothing");
                Assert.IsTrue(pile.Items.Any(i => i.Item.Id.Value == "isle:tide_sigil"), "no sigil in the boss drop");
                Assert.AreEqual(3, pile.Items.Where(i => i.Item.Id.Value == "isle:tide_pearl").Sum(i => i.Count), "boss pearls");
            }

            var up = floor.Portals.First(p => p.Kind == Isle.Gameplay.Dungeons.DungeonDirector.DoorKind.Exit);
            Teleport(player, up.Position);
            player.RequestInteract();
            yield return WaitUntil(() => !dungeons.IsUnderground(player.transform.position), 3f, "never came back up");
            Assert.Less(Vector2.Distance(player.transform.position, site.Entrance), 4f, "didn't surface at the entrance");
        }

        IEnumerator HarvestNearest(PlayerInteraction player, IslandWorld world, string defId)
        {
            var node = world.Nodes.First(n => n.Def.Id.Value == defId && n.IsHarvestable &&
                world.NearestWater(n.Position, 3f) == null &&
                !world.Nodes.Any(o => o != n && o.IsHarvestable && Vector2.Distance(o.Position, n.Position) < 2.5f));
            Teleport(player, node.Position);
            var usesBefore = node.UsesLeft;
            player.RequestInteract();
            yield return WaitUntil(() => node.UsesLeft < usesBefore, 15f, defId + " was never harvested");
        }

        static ItemDef Dish(InventoryNetwork inventory) =>
            inventory.Containers().SelectMany(c => c.Placements).Select(p => p.Item).FirstOrDefault(i => i.Tags != null && i.Tags.Contains("dish"));

        /// <summary>SYS-INV-02: pockets hold 12 kg, and this run piles up materials from step to step — clear loose ones
        /// (no wear, not gear) before a step that needs room. Later steps count from what they hold, not from zero.</summary>
        static void ClearLeftovers(InventoryNetwork inventory, string keep = null)
        {
            foreach (var c in inventory.Containers())
                foreach (var p in c.Placements.Where(p => p.Wear == null && string.IsNullOrEmpty(p.Item.EquipSlot) && p.Item.Id.Value != keep).ToList())
                    c.Remove(p);
        }

        static void Give(InventoryNetwork inventory, string itemId, int count)
        {
            var item = DefRegistry.Get<ItemDef>(NamespacedId.Parse(itemId));
            if (InventoryOps.TryGive(inventory.Containers(), item, count)) return;
            // Out of room: drop the harvest leftovers (wood, stone, fiber, rot) that earlier steps piled up.
            foreach (var c in inventory.Containers())
                foreach (var p in c.Placements.Where(p => p.Item.Id.Value is "isle:wood" or "isle:stone" or "isle:fiber" or "isle:rotten_food" && p.Item.Id.Value != itemId).ToList())
                    c.Remove(p);
            if (InventoryOps.TryGive(inventory.Containers(), item, count)) return;
            // Still none (a heavy kit): the spare tools earlier steps left in the bags go too.
            foreach (var c in inventory.Containers())
                foreach (var p in c.Placements.Where(p => p.Wear != null && p.Wear.Carcass == null && p.Item.Id.Value != itemId).ToList())
                    c.Remove(p);
            Assert.IsTrue(InventoryOps.TryGive(inventory.Containers(), item, count),
                $"test setup: no room for {itemId}: {string.Join(" | ", inventory.Containers().Select(c => $"{c.TotalWeightKg():0.#}/{c.CapacityKg} " + string.Join(",", c.Placements.Select(p => $"{p.Item.Id.Value}×{p.Count}"))))}");
        }

        /// <summary>A dry, node-free land tile right next to the sea, and that sea tile.</summary>
        static (Vector2 Shore, Vector2 Sea) CoastSpot(IslandWorld world)
        {
            var size = Isle.World.Generation.IslandGenerator.Size;
            foreach (var direction in new[] { new Isle.Core.Vec2Int(1, 0), new Isle.Core.Vec2Int(-1, 0), new Isle.Core.Vec2Int(0, 1), new Isle.Core.Vec2Int(0, -1) })
                for (var offset = -20; offset <= 20; offset += 4)
                {
                    var tile = new Isle.Core.Vec2Int(size / 2 + (direction.Y != 0 ? offset : 0), size / 2 + (direction.X != 0 ? offset : 0));
                    // Out from the middle until the sea (trees and ponds on the way don't stop the search).
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

        static IEnumerator CastAndHook(PlayerInteraction player, Vector2 water)
        {
            player.RequestCast(water);
            yield return WaitUntil(() => player.Cast != null, 3f, "cast never landed");
            yield return WaitUntil(() => player.Cast == null || player.Cast.State == Isle.Gameplay.Fishing.CastState.Bite, 14f, "nothing ever bit");
            player.RequestHook();
            yield return WaitUntil(() => player.Cast == null, 3f, "hook never resolved");
        }

        static void Teleport(PlayerInteraction player, Vector2 position) =>
            player.transform.position = new Vector3(position.x, position.y, player.transform.position.z);

        static int CountOf(Isle.Gameplay.Inventory.InventoryNetwork inventory, string itemId) =>
            InventoryOps.Count(inventory.Containers(), NamespacedId.Parse(itemId));

        static PlayerInteraction FindLocalPlayer()
        {
            foreach (var player in Object.FindObjectsByType<PlayerInteraction>(FindObjectsSortMode.None))
                if (player.IsOwner) return player;
            return null;
        }

        /// <summary>T-303: the debug screen text builds with no section failing (IMGUI drawing needs graphics), and carries
        /// <paramref name="expected"/>.</summary>
        static void AssertDebugScreen(string where, params string[] expected)
        {
            var overlay = Isle.UI.Prototype.DebugOverlay.Instance;
            Assert.IsNotNull(overlay, "DebugOverlay not installed");
            var (left, right) = overlay.Lines();
            var all = left.Concat(right).ToList();
            var failed = all.Where(l => l.StartsWith("[")).ToList();
            Assert.IsEmpty(failed, $"debug screen sections failed ({where}): {string.Join(" | ", failed)}");
            foreach (var text in expected)
                Assert.IsTrue(all.Any(l => l.Contains(text)), $"debug screen ({where}) lacks '{text}':\n{string.Join("\n", all)}");
            Assert.IsTrue(all.Any(l => l.StartsWith("HP ")), $"debug screen ({where}) shows no vitals");
            Assert.IsTrue(all.Any(l => l.StartsWith("Net: host")), $"debug screen ({where}) shows no network role");
        }

        /// <summary>Removes every creature near <paramref name="at"/> — steps that measure health exactly can't have a wild
        /// boar landing a bleeding blow in the middle (SYS-MED-01 wounds make a stray hit matter).</summary>
        static void ClearCreaturesNear(Vector2 at, float radius = 25f)
        {
            foreach (var creature in CreatureDirector.Instance.Creatures.Where(c => !c.Dead && Vector2.Distance(c.Position, at) <= radius).ToList())
                CreatureDirector.Instance.Damage(creature, 1e6f, new System.Collections.Generic.List<(NamespacedId, int)>());
        }

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
