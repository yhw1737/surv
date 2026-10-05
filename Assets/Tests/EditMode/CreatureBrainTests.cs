using Isle.Gameplay.Combat;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-COMBAT-01 §Creature AI: presets decide which transitions a creature takes;
    /// the vision radius itself comes from the creature def, not from here.</summary>
    public sealed class CreatureBrainTests
    {
        const string Skittish = "isle:skittish";
        const string Charger = "isle:territorial_charger";

        [Test]
        public void Next_SkittishInSight_Flees()
        {
            Assert.AreEqual(CreatureState.Flee, CreatureBrain.Next(CreatureState.Idle, Skittish, distanceTiles: 8f, visionTiles: 8f));
        }

        [Test]
        public void Next_SkittishOutOfSight_StaysIdle()
        {
            Assert.AreEqual(CreatureState.Idle, CreatureBrain.Next(CreatureState.Idle, Skittish, distanceTiles: 9f, visionTiles: 8f));
        }

        [Test]
        public void Next_SkittishFleeing_KeepsFleeingUntilTwiceVision()
        {
            Assert.AreEqual(CreatureState.Flee, CreatureBrain.Next(CreatureState.Flee, Skittish, distanceTiles: 12f, visionTiles: 8f));
            Assert.AreEqual(CreatureState.Idle, CreatureBrain.Next(CreatureState.Flee, Skittish, distanceTiles: 17f, visionTiles: 8f));
        }

        [Test]
        public void Next_ChargerInSight_Engages()
        {
            Assert.AreEqual(CreatureState.Engage, CreatureBrain.Next(CreatureState.Idle, Charger, distanceTiles: 6f, visionTiles: 6f));
        }

        [Test]
        public void Next_ChargerOutOfSight_StaysIdle()
        {
            Assert.AreEqual(CreatureState.Idle, CreatureBrain.Next(CreatureState.Idle, Charger, distanceTiles: 7f, visionTiles: 6f));
        }

        [Test]
        public void Next_ChargerEngaged_GivesUpBeyondTwiceVision()
        {
            Assert.AreEqual(CreatureState.Engage, CreatureBrain.Next(CreatureState.Engage, Charger, distanceTiles: 11f, visionTiles: 6f));
            Assert.AreEqual(CreatureState.Idle, CreatureBrain.Next(CreatureState.Engage, Charger, distanceTiles: 13f, visionTiles: 6f));
        }

        // Deer (SYS-COMBAT-01 table: "alerts, then flees on noise"). The noise system isn't built, so the
        // prototype's version alerts on sight, then flees once the alert has run for alertSeconds. [invented]
        const string AlertThenFlee = "isle:alert_then_flee";

        [Test]
        public void Next_AlertPresetInSight_Alerts()
        {
            Assert.AreEqual(CreatureState.Alert, CreatureBrain.Next(CreatureState.Idle, AlertThenFlee, distanceTiles: 8f, visionTiles: 8f, alertElapsedSeconds: 0f, alertSeconds: 2f));
        }

        [Test]
        public void Next_AlertBeforeAlertSecondsRun_StaysAlert()
        {
            Assert.AreEqual(CreatureState.Alert, CreatureBrain.Next(CreatureState.Alert, AlertThenFlee, distanceTiles: 8f, visionTiles: 8f, alertElapsedSeconds: 1f, alertSeconds: 2f));
        }

        [Test]
        public void Next_AlertAfterAlertSecondsRun_Flees()
        {
            Assert.AreEqual(CreatureState.Flee, CreatureBrain.Next(CreatureState.Alert, AlertThenFlee, distanceTiles: 8f, visionTiles: 8f, alertElapsedSeconds: 2f, alertSeconds: 2f));
        }

        [Test]
        public void Next_AlertWhenPlayerLeavesSight_ReturnsToIdle()
        {
            Assert.AreEqual(CreatureState.Idle, CreatureBrain.Next(CreatureState.Alert, AlertThenFlee, distanceTiles: 9f, visionTiles: 8f, alertElapsedSeconds: 1f, alertSeconds: 2f));
        }

        // Crocodile (SYS-COMBAT-01 table: "ambush from water"): same transitions as a charger, but it lies still
        // while idle — the director reads IsStationary for that.
        [Test]
        public void Next_AmbusherInSight_Engages()
        {
            Assert.AreEqual(CreatureState.Engage, CreatureBrain.Next(CreatureState.Idle, CreatureBrain.Ambusher, distanceTiles: 3f, visionTiles: 3f));
        }

        [Test]
        public void Next_AmbusherOutOfSight_StaysIdle()
        {
            Assert.AreEqual(CreatureState.Idle, CreatureBrain.Next(CreatureState.Idle, CreatureBrain.Ambusher, distanceTiles: 3.5f, visionTiles: 3f));
        }

        [Test]
        public void IsStationary_OnlyAmbusher_ReturnsTrue()
        {
            Assert.IsTrue(CreatureBrain.IsStationary(CreatureBrain.Ambusher));
            Assert.IsFalse(CreatureBrain.IsStationary(CreatureBrain.TerritorialCharger));
            Assert.IsFalse(CreatureBrain.IsStationary(CreatureBrain.Skittish));
        }

        // Activity windows come from the def's spawn.time; outside them the creature sleeps.
        [TestCase(new[] { "day" }, "day", true)]
        [TestCase(new[] { "day" }, "night", false)]
        [TestCase(new[] { "day", "dusk" }, "dusk", true)]
        [TestCase(new[] { "any" }, "night", true)]
        public void IsAwake_ActiveTimes_MatchPhase(string[] times, string phase, bool expected)
        {
            Assert.AreEqual(expected, CreatureBrain.IsAwake(times, phase));
        }

        [Test]
        public void IsAwake_NoTimesListed_AlwaysAwake()
        {
            Assert.IsTrue(CreatureBrain.IsAwake(null, "night"));
            Assert.IsTrue(CreatureBrain.IsAwake(new string[0], "night"));
        }

        [Test]
        public void Next_UnknownPreset_StaysIdle()
        {
            Assert.AreEqual(CreatureState.Idle, CreatureBrain.Next(CreatureState.Idle, "isle:not_a_preset", distanceTiles: 1f, visionTiles: 6f));
        }
    }
}
