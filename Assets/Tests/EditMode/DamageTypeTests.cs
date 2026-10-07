using System.Collections.Generic;
using Isle.Data;
using Isle.Gameplay.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-COMBAT-02 verification: damage types, resistances, pierce bypass, hit shapes, side effects.</summary>
    public sealed class DamageTypeTests
    {
        static readonly Dictionary<string, float> Shell = new() { ["slash"] = 0.5f, ["blunt"] = 1.5f };

        // --- 1. Same power, blunt vs slash on a shelled target ----------------------------------------------------

        [Test]
        public void Damage_BluntBeatsSlashOnAShell_ByTheResistRatio()
        {
            var blunt = DamageTypes.Damage(20f, DamageTypes.Blunt, DamageTypes.ResistOf(Shell, DamageTypes.Blunt), 0f);
            var slash = DamageTypes.Damage(20f, DamageTypes.Slash, DamageTypes.ResistOf(Shell, DamageTypes.Slash), 0f);
            Assert.AreEqual(30f, blunt, 1e-3f);
            Assert.AreEqual(10f, slash, 1e-3f);
            Assert.AreEqual(3f, blunt / slash, 1e-3f);
        }

        [Test]
        public void Resist_Missing_IsOne() => Assert.AreEqual(1f, DamageTypes.ResistOf(null, DamageTypes.Heat), 1e-6f);

        // --- 2. Pierce vs armored target ---------------------------------------------------------------------------

        [Test]
        public void Damage_Pierce_IgnoresFortyPercentOfArmor()
        {
            // 60 armor → 36 effective for pierce → 36/96 = 37.5% reduction; blunt keeps 50%.
            Assert.AreEqual(40f * (1f - 36f / 96f), DamageTypes.Damage(40f, DamageTypes.Pierce, 1f, 60f), 1e-3f);
            Assert.AreEqual(20f, DamageTypes.Damage(40f, DamageTypes.Blunt, 1f, 60f), 1e-3f);
        }

        [Test]
        public void Armor_FlatNumber_CoversPhysicalTypesOnly()
        {
            Assert.AreEqual(30f, DamageTypes.ArmorAgainst(30f, null, DamageTypes.Slash), 1e-6f);
            Assert.AreEqual(0f, DamageTypes.ArmorAgainst(30f, null, DamageTypes.Heat), 1e-6f);
            var byType = new Dictionary<string, float> { ["heat"] = 12f, ["slash"] = 5f };
            Assert.AreEqual(12f, DamageTypes.ArmorAgainst(30f, byType, DamageTypes.Heat), 1e-6f);
            Assert.AreEqual(5f, DamageTypes.ArmorAgainst(30f, byType, DamageTypes.Slash), 1e-6f);
            Assert.AreEqual(30f, DamageTypes.ArmorAgainst(30f, byType, DamageTypes.Blunt), 1e-6f);
        }

        // --- 3. Each shape's hit test ------------------------------------------------------------------------------

        static readonly Vector2 Right = Vector2.right;

        [Test]
        public void Shape_Arc()
        {
            var arc = new AttackSpec { Shape = "arc", Degrees = 90f, Radius = 1.5f };
            Assert.IsTrue(HitShapes.Contains(arc, Vector2.zero, Right, new Vector2(1.4f, 0f), 0.1f));
            Assert.IsFalse(HitShapes.Contains(arc, Vector2.zero, Right, new Vector2(1.8f, 0f), 0.1f), "beyond the radius");
            Assert.IsFalse(HitShapes.Contains(arc, Vector2.zero, Right, new Vector2(0f, 1.2f), 0.05f), "outside the angle");
        }

        [Test]
        public void Shape_Thrust()
        {
            var thrust = new AttackSpec { Shape = "thrust", Length = 2f, Width = 0.4f };
            Assert.IsTrue(HitShapes.Contains(thrust, Vector2.zero, Right, new Vector2(1.9f, 0.1f), 0.05f));
            Assert.IsFalse(HitShapes.Contains(thrust, Vector2.zero, Right, new Vector2(1f, 0.5f), 0.05f), "off the line");
            Assert.IsFalse(HitShapes.Contains(thrust, Vector2.zero, Right, new Vector2(-0.6f, 0f), 0.05f), "behind");
        }

        [Test]
        public void Shape_Smash()
        {
            var smash = new AttackSpec { Shape = "smash", Offset = 1f, Radius = 0.5f };
            Assert.IsTrue(HitShapes.Contains(smash, Vector2.zero, Right, new Vector2(1.3f, 0.2f), 0.05f));
            Assert.IsFalse(HitShapes.Contains(smash, Vector2.zero, Right, new Vector2(0.2f, 0f), 0.05f), "too close: the slam lands ahead");
        }

        [Test]
        public void Shape_Sweep_HitsAllAround()
        {
            var sweep = new AttackSpec { Shape = "sweep", Radius = 1.2f };
            Assert.IsTrue(HitShapes.Contains(sweep, Vector2.zero, Right, new Vector2(-1f, 0f), 0.1f));
            Assert.IsFalse(HitShapes.Contains(sweep, Vector2.zero, Right, new Vector2(0f, 1.5f), 0.1f));
        }

        [Test]
        public void Shape_NoAim_FallsBackToReach()
        {
            var thrust = new AttackSpec { Shape = "thrust", Length = 2f, Width = 0.4f };
            Assert.IsTrue(HitShapes.Contains(thrust, Vector2.zero, Vector2.zero, new Vector2(-1.5f, 0f), 0.1f));
            Assert.AreEqual(2f, HitShapes.Reach(thrust), 1e-6f);
        }

        // --- 4. Old single-block weapon def -------------------------------------------------------------------------

        [Test]
        public void Weapon_WithoutAttacks_IsOneArcOfItsConeAndReach()
        {
            var old = new WeaponDef { Reach = 1.3f, ConeDegrees = 110f };
            var first = WeaponAttacks.For(old, step: 1, length: 3);
            Assert.AreEqual("arc", first.Shape);
            Assert.AreEqual(110f, first.Degrees, 1e-6f);
            Assert.AreEqual(1.3f, first.Radius, 1e-6f);
            Assert.AreEqual(DamageTypes.Blunt, first.Type);
            Assert.AreEqual(1f, first.PowerMult, 1e-6f);
            Assert.AreEqual(MeleeCombo.FinisherPowerMult, WeaponAttacks.For(old, 3, 3).PowerMult, 1e-6f, "finisher keeps ×1.4");
        }

        [Test]
        public void Weapon_Attacks_LastEntryIsTheFinisher()
        {
            var weapon = new WeaponDef
            {
                Attacks = new[]
                {
                    new AttackSpec { Shape = "arc", Type = "slash" },
                    new AttackSpec { Shape = "arc", Type = "slash" },
                    new AttackSpec { Shape = "smash", Type = "blunt", PowerMult = 1.4f },
                },
            };
            Assert.AreEqual("slash", WeaponAttacks.For(weapon, 1, 3).Type);
            Assert.AreEqual("blunt", WeaponAttacks.For(weapon, 3, 3).Type);
            // Lv20's 4-hit combo: steps 1–3 cycle the normal entries, step 4 is the finisher.
            Assert.AreEqual("slash", WeaponAttacks.For(weapon, 3, 4).Type);
            Assert.AreEqual("smash", WeaponAttacks.For(weapon, 4, 4).Shape);
        }

        // --- Side effects (decided values) --------------------------------------------------------------------------

        [Test]
        public void Bleed_IsEightPercentPerSecond_ForFourSeconds()
        {
            var status = new CombatStatus();
            status.OnHit(DamageTypes.Slash, 50f, now: 0f, out _);
            var total = 0f;
            for (var t = 0f; t < 6f; t += 0.1f) total += status.Tick(t + 0.1f, 0.1f);
            Assert.AreEqual(50f * 0.08f * 4f, total, 0.05f);
        }

        [Test]
        public void Poison_StacksToThree()
        {
            var status = new CombatStatus();
            for (var i = 0; i < 5; i++) status.OnHit(DamageTypes.Toxic, 10f, now: 0f, out _);
            Assert.AreEqual(3, status.Stacks(DamageTypes.Toxic));
            Assert.AreEqual(3 * 10f * 0.08f, status.Tick(1f, 1f), 1e-3f);
        }

        [Test]
        public void Bleed_ReappliedRefreshes_DoesNotStack()
        {
            var status = new CombatStatus();
            status.OnHit(DamageTypes.Slash, 10f, 0f, out _);
            status.OnHit(DamageTypes.Slash, 10f, 1f, out _);
            Assert.AreEqual(1, status.Stacks(DamageTypes.Slash));
        }

        [Test]
        public void Stagger_ThreeBluntHitsInTheWindow_Stun()
        {
            var status = new CombatStatus();
            status.OnHit(DamageTypes.Blunt, 10f, 0f, out var a);
            status.OnHit(DamageTypes.Blunt, 10f, 1f, out var b);
            status.OnHit(DamageTypes.Blunt, 10f, 2f, out var c);
            Assert.IsFalse(a || b);
            Assert.IsTrue(c, "third blunt hit within 1.2 s of the last stuns");
            Assert.AreEqual(1f, CombatStatus.StunSeconds, 1e-6f);
        }

        [Test]
        public void Stagger_GapResetsTheCount()
        {
            var status = new CombatStatus();
            status.OnHit(DamageTypes.Blunt, 10f, 0f, out _);
            status.OnHit(DamageTypes.Blunt, 10f, 1f, out _);
            status.OnHit(DamageTypes.Blunt, 10f, 3f, out var stunned); // 2 s gap
            Assert.IsFalse(stunned);
        }
    }
}
