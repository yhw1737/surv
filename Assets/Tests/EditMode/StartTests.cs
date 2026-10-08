using System.Collections.Generic;
using System.IO;
using System.Linq;
using Isle.Data;
using Isle.Gameplay.Character;
using Isle.Modding.Defs;
using NUnit.Framework;
using UnityEngine;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-START-01: random passengers balance their traits to about zero points, never doubling up a group,
    /// with skills 0–3 plus a background's +5; every trait effect is one the game reads.</summary>
    public sealed class StartTests
    {
        [SetUp]
        public void SetUp()
        {
            DefRegistry.Clear();
            Assert.IsEmpty(DefinitionBootstrap.Load(Path.Combine(Application.streamingAssetsPath, "definitions")));
        }

        [TearDown]
        public void TearDown() => DefRegistry.Clear();

        [Test]
        public void Roll_1000Passengers_BalanceAndNoClashes()
        {
            var scenario = StartDirector.Scenario;
            Assert.IsNotNull(scenario);
            var backgrounds = 0;
            var dressedUp = 0;
            for (var seed = 1; seed <= 1000; seed++)
            {
                var survivor = StartDirector.Roll(new System.Random(seed));
                Assert.That(survivor.Points, Is.InRange(-1, 0), $"seed {seed}: points {survivor.Points} (outfit included)");
                Assert.That(survivor.Traits.Count, Is.InRange(scenario.TraitsMin, scenario.TraitsMax), $"seed {seed}: trait count");
                for (var i = 0; i < survivor.Traits.Count; i++)
                    Assert.IsTrue(SurvivorGenerator.Compatible(survivor.Traits[i], survivor.Traits.Where((_, j) => j != i)), $"seed {seed}: {survivor.Traits[i].Id} clashes");
                Assert.LessOrEqual(survivor.Traits.Count(t => t.Background), 1);
                Assert.IsNotNull(survivor.Outfit, $"seed {seed}: no outfit");
                Assert.LessOrEqual(survivor.Outfit.Cost, 2, "clothes never take more than 2 points");
                Assert.Contains(survivor.Name, scenario.Names);
                var boosted = survivor.Background?.Skills?.Select(s => s.Skill).ToHashSet() ?? new HashSet<Isle.Core.Ids.NamespacedId>();
                foreach (var (skill, level) in survivor.Skills)
                    Assert.That(level, Is.InRange(boosted.Contains(skill) ? 5 : 0, scenario.SkillMax + (boosted.Contains(skill) ? 5 : 0)), $"seed {seed}: {skill} {level}");
                if (survivor.Background != null) backgrounds++;
                if (survivor.Outfit.Cost > 0) dressedUp++;
            }
            Assert.That(backgrounds, Is.InRange(550, 850), "background chance 0.7");
            Assert.Greater(dressedUp, 50, "better clothes do show up");
        }

        [Test]
        public void Exclusions_SoldierIsNeverFrail()
        {
            var soldier = DefRegistry.Get<TraitDef>(Isle.Core.Ids.NamespacedId.Parse("isle:bg_soldier"));
            var fragile = DefRegistry.Get<TraitDef>(Isle.Core.Ids.NamespacedId.Parse("isle:fragile"));
            Assert.IsFalse(SurvivorGenerator.Compatible(fragile, new[] { soldier }));
            Assert.IsFalse(SurvivorGenerator.Compatible(soldier, new[] { fragile }), "checked both ways");
        }

        [Test]
        public void Roll_SameSeed_SamePassenger()
        {
            var a = StartDirector.Roll(new System.Random(77));
            var b = StartDirector.Roll(new System.Random(77));
            Assert.AreEqual(a.Name, b.Name);
            CollectionAssert.AreEqual(a.Traits.Select(t => t.Id), b.Traits.Select(t => t.Id));
            CollectionAssert.AreEqual(a.Skills, b.Skills);
        }

        /// <summary>Every effect a trait carries is read somewhere — a typo would silently do nothing.</summary>
        [Test]
        public void Traits_OnlyUseEffectsTheGameReads()
        {
            var read = new HashSet<string>
            {
                "carry_capacity_mult", "move_speed_mult", "stamina_max_mult", "stamina_regen_mult", "xp_mult", "hunger_drain_mult",
                "thirst_drain_mult", "hypothermia_threshold_shift", "damage_resist", "gather_speed_mult", "damage_taken_mult", "aim_sway_mult",
                "buff_duration_mult", "melee_power_mult", "ranged_power_mult", "sprint_cost_mult",
            };
            var traits = DefRegistry.All<TraitDef>();
            Assert.That(traits.Count(t => !t.Background), Is.GreaterThanOrEqualTo(30));
            foreach (var trait in traits)
            foreach (var effect in trait.Effects ?? System.Array.Empty<BuffEffect>())
                Assert.Contains(effect.Type, read.ToList(), $"{trait.Id}: {effect.Type}");
        }

        [Test]
        public void Belongings_AreDistinct_AndWithinTheRoll()
        {
            var roll = StartDirector.Scenario.Belongings;
            for (var seed = 1; seed <= 200; seed++)
            {
                var picked = StartDirector.Belongings(roll, new System.Random(seed));
                Assert.That(picked.Count, Is.InRange(roll.Min, roll.Max));
                Assert.AreEqual(picked.Count, picked.Select(p => p.Item.Id).Distinct().Count());
            }
        }

        [Test]
        public void Scenario_Outfits_AreRealWearables_OnePerSlot()
        {
            foreach (var outfit in StartDirector.Scenario.Outfits)
            {
                var slots = outfit.Items.Select(id => DefRegistry.Get<ItemDef>(id).EquipSlot).ToList();
                Assert.IsTrue(slots.All(s => !string.IsNullOrEmpty(s)), outfit.Name);
                Assert.AreEqual(slots.Count, slots.Distinct().Count(), $"{outfit.Name}: two items in one slot");
            }
        }
    }
}
