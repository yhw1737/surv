using Isle.Gameplay.Combat;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-COMBAT-01 §Melee: 3-hit combo, block, parry, and the skill unlocks.</summary>
    public sealed class MeleeCombatTests
    {
        // --- Combo ---------------------------------------------------------------------------------------------

        [Test]
        public void Combo_Lv0_IsThreeHits_Lv20_IsFour()
        {
            Assert.AreEqual(3, MeleeCombo.Length(0));
            Assert.AreEqual(3, MeleeCombo.Length(19));
            Assert.AreEqual(4, MeleeCombo.Length(20));
        }

        [Test]
        public void Combo_ThirdHit_HitsHarder()
        {
            Assert.AreEqual(1f, MeleeCombo.PowerMult(1, 3), 1e-5f);
            Assert.AreEqual(1f, MeleeCombo.PowerMult(2, 3), 1e-5f);
            Assert.AreEqual(1.4f, MeleeCombo.PowerMult(3, 3), 1e-5f);
        }

        [Test]
        public void Combo_Stamina_Is8_8_14_ForAnEightCostWeapon()
        {
            Assert.AreEqual(8f, MeleeCombo.StaminaCost(8f, 1, 3, 0), 1e-4f);
            Assert.AreEqual(8f, MeleeCombo.StaminaCost(8f, 2, 3, 0), 1e-4f);
            Assert.AreEqual(14f, MeleeCombo.StaminaCost(8f, 3, 3, 0), 1e-4f);
        }

        [Test]
        public void Combo_Lv45_StaminaMinus30Percent()
        {
            Assert.AreEqual(5.6f, MeleeCombo.StaminaCost(8f, 1, 4, 45), 1e-4f);
            Assert.AreEqual(9.8f, MeleeCombo.StaminaCost(8f, 4, 4, 45), 1e-4f);
        }

        [Test]
        public void Combo_PressesWithinWindow_Advance_ThenWrap()
        {
            var combo = new MeleeCombo();
            Assert.AreEqual(1, combo.Advance(readyAt: 0f, now: 0f, length: 3));
            Assert.AreEqual(2, combo.Advance(readyAt: 0.5f, now: 1.0f, length: 3));
            Assert.AreEqual(3, combo.Advance(readyAt: 1.5f, now: 2.6f, length: 3));
            Assert.AreEqual(1, combo.Advance(readyAt: 3.0f, now: 3.1f, length: 3)); // after the finisher it starts over
        }

        [Test]
        public void Combo_PressAfterWindow_Resets()
        {
            var combo = new MeleeCombo();
            combo.Advance(0f, 0f, 3);
            // Ready again at 0.5 s; the window is 1.2 s from then, so 1.8 s is too late.
            Assert.AreEqual(1, combo.Advance(readyAt: 0.5f, now: 1.8f, length: 3));
        }

        [Test]
        public void Combo_Reset_StartsOver()
        {
            var combo = new MeleeCombo();
            combo.Advance(0f, 0f, 3);
            combo.Reset();
            Assert.AreEqual(1, combo.Advance(0.5f, 0.6f, 3));
        }

        // --- Block / parry -------------------------------------------------------------------------------------

        [Test]
        public void Defense_NotBlocking_TakesFullDamage()
        {
            var r = MeleeDefense.Resolve(blocking: false, sinceBlockStart: 5f, parryWindow: 0.25f, angleToAttackerDeg: 0f, damage: 10f, stamina: 100f);
            Assert.AreEqual(BlockOutcome.None, r.Outcome);
            Assert.AreEqual(10f, r.DamageThrough, 1e-4f);
            Assert.AreEqual(0f, r.StaminaCost, 1e-4f);
        }

        [Test]
        public void Defense_BlockPressedJustBeforeImpact_Parries_NoDamage_NoStamina()
        {
            var r = MeleeDefense.Resolve(true, 0.2f, 0.25f, 10f, 10f, 100f);
            Assert.AreEqual(BlockOutcome.Parried, r.Outcome);
            Assert.AreEqual(0f, r.DamageThrough, 1e-4f);
            Assert.AreEqual(0f, r.StaminaCost, 1e-4f);
        }

        [Test]
        public void Defense_BlockHeldLonger_Blocks_CostsStamina()
        {
            var r = MeleeDefense.Resolve(true, 1f, 0.25f, 10f, 10f, 100f);
            Assert.AreEqual(BlockOutcome.Blocked, r.Outcome);
            Assert.AreEqual(0f, r.DamageThrough, 1e-4f);
            Assert.Greater(r.StaminaCost, 0f);
        }

        [Test]
        public void Defense_HitFromBehind_IsNotBlocked()
        {
            // Frontal 90° = within 45° either side of where the guard faces.
            Assert.AreEqual(BlockOutcome.Blocked, MeleeDefense.Resolve(true, 1f, 0.25f, 44f, 10f, 100f).Outcome);
            Assert.AreEqual(BlockOutcome.None, MeleeDefense.Resolve(true, 1f, 0.25f, 46f, 10f, 100f).Outcome);
            Assert.AreEqual(BlockOutcome.None, MeleeDefense.Resolve(true, 0.1f, 0.25f, 120f, 10f, 100f).Outcome);
        }

        [Test]
        public void Defense_OutOfStamina_GuardBreaks_RestGoesThrough()
        {
            var full = MeleeDefense.Resolve(true, 1f, 0.25f, 0f, 10f, 100f);
            var r = MeleeDefense.Resolve(true, 1f, 0.25f, 0f, 10f, full.StaminaCost * 0.5f);
            Assert.AreEqual(BlockOutcome.GuardBroken, r.Outcome);
            Assert.AreEqual(full.StaminaCost * 0.5f, r.StaminaCost, 1e-4f);
            Assert.AreEqual(5f, r.DamageThrough, 1e-3f);
        }

        [Test]
        public void Defense_ParryWindow_Lv10_Widens()
        {
            Assert.AreEqual(0.25f, MeleeDefense.ParryWindow(9), 1e-5f);
            Assert.AreEqual(0.35f, MeleeDefense.ParryWindow(10), 1e-5f);
        }

        [Test]
        public void Defense_ParryStagger_IsOneSecond() => Assert.AreEqual(1f, MeleeDefense.ParryStaggerSeconds, 1e-5f);

        // --- Situational / execute ------------------------------------------------------------------------------

        [Test]
        public void Situational_StaggeredTarget_Is1_2()
        {
            Assert.AreEqual(1.2f, MeleeCombo.SituationalMult(staggered: true), 1e-5f);
            Assert.AreEqual(1f, MeleeCombo.SituationalMult(staggered: false), 1e-5f);
        }

        [Test]
        public void Execute_OnlyFromLv35_BelowTwentyPercent()
        {
            Assert.IsFalse(MeleeCombo.Executes(34, 0.1f));
            Assert.IsTrue(MeleeCombo.Executes(35, 0.19f));
            Assert.IsFalse(MeleeCombo.Executes(35, 0.2f));
        }
    }
}
