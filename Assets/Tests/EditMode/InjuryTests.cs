using Isle.Gameplay.Character;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-MED-01: wounds, bleeding, healing, limbs, tending.</summary>
    public sealed class InjuryTests
    {
        [Test]
        public void Slash20_MakesACut_MaxHealth80() // verification 1
        {
            var body = new Injuries();
            var wound = body.AddHit("slash", 20f, 0.05f);
            Assert.AreEqual(WoundKind.Cut, wound.Kind);
            Assert.AreEqual(BodyPart.Torso, wound.Part);
            Assert.AreEqual(80f, body.MaxHealth(100f), 1e-4f);
        }

        [Test]
        public void UntendedCut20_Bleeds016() // verification 2
        {
            var body = new Injuries();
            body.AddHit("slash", 20f, 0.05f);
            Assert.AreEqual(0.16f, body.BleedPerSecond, 1e-5f);
        }

        [Test]
        public void Tended_StopsBleeding_HealsFaster() // verification 3
        {
            var body = new Injuries();
            var wound = body.AddHit("slash", 20f, 0.05f);
            body.Tend(0.5f, 0f);
            Assert.AreEqual(0f, body.BleedPerSecond);
            body.Heal(10f);
            Assert.AreEqual(20f - 0.09f * 10f, wound.Severity, 1e-4f);
        }

        [Test]
        public void Untended_HealsSlowly()
        {
            var body = new Injuries();
            var wound = body.AddHit("blunt", 10f, 0.05f);
            body.Heal(100f);
            Assert.AreEqual(7f, wound.Severity, 1e-4f);
        }

        [TestCase(0.05f, BodyPart.Torso)]
        [TestCase(0.45f, BodyPart.Head)]
        [TestCase(0.55f, BodyPart.LeftArm)]
        [TestCase(0.70f, BodyPart.RightArm)]
        [TestCase(0.80f, BodyPart.LeftLeg)]
        [TestCase(0.95f, BodyPart.RightLeg)]
        public void PartFor_Roll_FollowsTheTable(float roll, BodyPart part) => Assert.AreEqual(part, Injuries.PartFor(roll));

        [Test]
        public void BothLegsAt30_MoveSpeed06() // verification 4
        {
            var body = new Injuries();
            body.AddHit("blunt", 30f, 0.80f);
            body.AddHit("blunt", 30f, 0.95f);
            Assert.AreEqual(0.6f, body.LegMult, 1e-5f);
            Assert.AreEqual(1f, body.ArmMult, 1e-5f);
        }

        [Test]
        public void Tend_NoWounds_ReturnsNull() => Assert.IsNull(new Injuries().Tend(0.8f, 0f)); // verification 6

        [Test]
        public void Tend_PicksTheBleedingOneFirst()
        {
            var body = new Injuries();
            body.AddHit("blunt", 25f, 0.05f);
            var cut = body.AddHit("slash", 5f, 0.05f);
            Assert.AreSame(cut, body.Tend(0.5f, 0f));
        }

        [Test]
        public void Toxic_LeavesNoWound() => Assert.IsNull(new Injuries().AddHit("toxic", 10f, 0.1f));

        [Test]
        public void ManyWounds_MergePastTheCap()
        {
            var body = new Injuries();
            for (var i = 0; i < Injuries.MaxWounds + 5; i++) body.AddHit("slash", 1f, 0.05f);
            Assert.AreEqual(Injuries.MaxWounds, body.Wounds.Count);
            Assert.AreEqual(Injuries.MaxWounds + 5f, body.TotalSeverity, 1e-4f);
        }
    
        // ---- infection, fractures, scars, skill, coverage (SYS-MED-01, 2026-10-13)

        [Test]
        public void Infection_UntendedCutAnHourIn_CanTurnInfectedAndGrows()
        {
            var body = new Injuries();
            var cut = body.AddHit("slash", 10f, 0.05f);
            body.Heal(60f, () => 0.1f); // below 0.25: infected
            Assert.IsTrue(cut.Infected);
            var start = cut.Infection;
            body.Heal(100f, () => 1f);
            Assert.Greater(cut.Infection, start, "an untended infection should grow");
            Assert.Greater(body.InfectionDrainPerSecond, 0f);
        }

        [Test]
        public void Infection_WellTended_Fades()
        {
            var body = new Injuries();
            var cut = body.AddHit("slash", 10f, 0.05f);
            body.Heal(60f, () => 0.1f);
            Assert.IsTrue(cut.Infected);
            Assert.AreSame(cut, body.Tend(0.9f, 0f), "an infected wound should take the tending");
            body.Heal(100f, () => 1f);
            Assert.IsFalse(cut.Infected, "a well-tended infection should clear");
        }

        [Test]
        public void Infection_TendedWellEarly_RarelyInfects()
        {
            var body = new Injuries();
            var cut = body.AddHit("slash", 10f, 0.05f);
            body.Tend(1f, 0f);
            body.Heal(60f, () => 0.1f); // chance is 0.25 × (1 − 1) = 0
            Assert.IsFalse(cut.Infected);
        }

        [Test]
        public void Fracture_HeavyBluntOnALeg_SlowsMoreAndHealsSlowlyUntilSplinted()
        {
            var body = new Injuries();
            var wound = body.AddHit("blunt", 15f, BodyPart.LeftLeg, 0.1f);
            Assert.IsTrue(wound.Fracture);
            Assert.AreEqual(1f - Injuries.LimbPenalty * 0.5f * 1f, body.LegMult, 1e-4f, "a 15 fracture counts double: 30 → disabled");
            body.Heal(10f);
            Assert.AreEqual(15f - 0.03f * 0.25f * 10f, wound.Severity, 1e-4f);
            Assert.AreSame(wound, body.Tend(0.5f, 0f, splint: true));
            Assert.IsTrue(wound.Splinted);
        }

        [Test]
        public void Fracture_NotOnTheTorso_NorOnALightHit()
        {
            var body = new Injuries();
            Assert.IsFalse(body.AddHit("blunt", 20f, BodyPart.Torso, 0f).Fracture);
            Assert.IsFalse(body.AddHit("blunt", 10f, BodyPart.LeftArm, 0f).Fracture);
        }

        [Test]
        public void Scar_BadWoundLeftUntended_CostsMaxHealth()
        {
            var body = new Injuries();
            body.AddHit("blunt", 15f, BodyPart.Torso, 1f);
            body.Heal(1000f);
            Assert.AreEqual(0, body.Wounds.Count);
            Assert.AreEqual(1, body.Scars.Count);
            Assert.AreEqual(98f, body.MaxHealth(100f), 1e-4f);
        }

        [Test]
        public void Scar_WellTended_NoScar()
        {
            var body = new Injuries();
            body.AddHit("blunt", 15f, BodyPart.Torso, 1f);
            body.Tend(0.8f, 0f);
            body.Heal(1000f);
            Assert.AreEqual(0, body.Scars.Count);
        }

        [TestCase(0, 0.6f)]
        [TestCase(25, 1.0f)]
        [TestCase(50, 1.4f)]
        public void TendQuality_SkillScalesTheMedicine(int level, float factor) =>
            Assert.AreEqual(0.5f * factor, Injuries.TendQuality(0.5f, level), 1e-5f);

        [Test]
        public void Covers_ArmorProtectsItsParts()
        {
            CollectionAssert.AreEquivalent(new[] { BodyPart.Head }, Injuries.Covers("head"));
            CollectionAssert.Contains(Injuries.Covers("chest"), BodyPart.LeftArm);
            CollectionAssert.Contains(Injuries.Covers("feet"), BodyPart.RightLeg);
            CollectionAssert.IsEmpty(Injuries.Covers("main_hand"));
        }
    }
}
