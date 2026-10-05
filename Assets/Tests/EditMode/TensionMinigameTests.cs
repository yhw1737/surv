using Isle.Gameplay.Fishing;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-FISH-01 §Tension minigame, including verification 4 (halfWindow) and 5 (solo ×0.4 drain).</summary>
    public sealed class TensionMinigameTests
    {
        const float Tolerance = 0.001f;

        [TestCase(0, 0.22f)]
        [TestCase(25, 0.33f)]
        [TestCase(50, 0.44f)]
        public void HalfWindow_ByLevel_MatchesSpecTable(int level, float expected)
        {
            Assert.AreEqual(expected, TensionMinigame.HalfWindow(0.22f, level), Tolerance);
        }

        [Test]
        public void DrainMult_SoloAtOrAboveCoopThreshold_Is0_4()
        {
            Assert.AreEqual(0.4f, TensionMinigame.DrainMult(weightKg: 4f, coopThresholdKg: 3f, anglers: 1), Tolerance);
            Assert.AreEqual(1f, TensionMinigame.DrainMult(weightKg: 2f, coopThresholdKg: 3f, anglers: 1), Tolerance);
            Assert.AreEqual(1f, TensionMinigame.DrainMult(weightKg: 4f, coopThresholdKg: 3f, anglers: 2), Tolerance);
        }

        static TensionMinigame Calm(float stamina = 100f) =>
            new(pattern: "", tensionWindow: 0.22f, fishingLevel: 0, fishStamina: stamina, drainMult: 1f);

        [Test]
        public void Step_Reeling_RaisesTensionAt0_55()
        {
            var game = Calm();
            game.Step(0.1f, reeling: true);
            Assert.AreEqual(0.555f, game.Tension, Tolerance);
        }

        [Test]
        public void Step_Slack_LowersTensionAt0_40()
        {
            var game = Calm();
            game.Step(0.1f, reeling: false);
            Assert.AreEqual(0.46f, game.Tension, Tolerance);
        }

        [Test]
        public void Step_InsideBand_DrainsStaminaAt18()
        {
            var game = Calm();
            game.Step(0.05f, reeling: false);
            Assert.AreEqual(100f - 0.9f, game.FishStamina, 0.01f);
        }

        [Test]
        public void Step_StaminaGone_Succeeds()
        {
            var game = Calm(stamina: 1f);
            for (var i = 0; i < 10 && game.Result == FightResult.Ongoing; i++) game.Step(0.05f, reeling: i % 2 == 0);
            Assert.AreEqual(FightResult.Caught, game.Result);
        }

        [Test]
        public void Step_HeldAboveBand_LineBreaks()
        {
            var game = Calm();
            for (var i = 0; i < 200 && game.Result == FightResult.Ongoing; i++) game.Step(0.05f, reeling: true);
            Assert.AreEqual(FightResult.LineBroke, game.Result);
        }

        [Test]
        public void Step_SlackBelowBand_HookSlips()
        {
            var game = Calm();
            for (var i = 0; i < 200 && game.Result == FightResult.Ongoing; i++) game.Step(0.05f, reeling: false);
            Assert.AreEqual(FightResult.HookSlipped, game.Result);
        }

        [Test]
        public void Step_StraightPattern_SpikesAfter1_5Seconds()
        {
            var game = new TensionMinigame("straight", 0.22f, 0, 1000f, 1f);
            // Alternate reel/slack to hold roughly level, then compare against a calm fish over the same inputs.
            var calm = Calm(1000f);
            for (var i = 0; i < 31; i++)
            {
                game.Step(0.05f, reeling: i % 2 == 0);
                calm.Step(0.05f, reeling: i % 2 == 0);
            }
            Assert.AreEqual(calm.Tension + 0.25f, game.Tension, 0.02f);
        }
    }
}
