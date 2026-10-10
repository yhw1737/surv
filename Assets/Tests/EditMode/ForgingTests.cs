using Isle.Gameplay.Crafting;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-CRAFT-01 §Forging minigame.</summary>
    public sealed class ForgingTests
    {
        [Test]
        public void Window_Lv0_IsTheSpecBand()
        {
            var forge = new ForgingMinigame(4, 0);
            Assert.That(forge.WindowLow, Is.EqualTo(0.55f).Within(1e-5f));
            Assert.That(forge.WindowHigh, Is.EqualTo(0.75f).Within(1e-5f));
        }

        [Test]
        public void Window_Lv50_IsTwiceAsWide()
        {
            var forge = new ForgingMinigame(4, 50);
            Assert.That(forge.HalfWindow, Is.EqualTo(0.20f).Within(1e-5f));
            Assert.That(forge.WindowLow, Is.EqualTo(0.45f).Within(1e-5f));
        }

        [TestCase(1, 3)]
        [TestCase(5, 5)]
        [TestCase(12, 8)]
        public void Required_ClampedTo3To8(int asked, int required) =>
            Assert.That(new ForgingMinigame(asked, 10).Required, Is.EqualTo(required));

        [Test]
        public void Bellows_RaiseHeat_ItFallsOnItsOwn()
        {
            var forge = new ForgingMinigame(4, 0);
            forge.Step(1f, bellows: true);
            Assert.That(forge.Heat, Is.EqualTo(ForgingMinigame.BellowsRate - ForgingMinigame.DecayRate).Within(1e-5f));
            var heat = forge.Heat;
            forge.Step(0.5f, bellows: false);
            Assert.That(forge.Heat, Is.EqualTo(heat - ForgingMinigame.DecayRate * 0.5f).Within(1e-5f));
        }

        [Test]
        public void StrikeInWindow_Succeeds_OutsideFails_ScoreIsShare()
        {
            var forge = new ForgingMinigame(4, 0);
            Assert.IsTrue(forge.Strike(), "cold strike is taken");      // heat 0: a miss
            HeatTo(forge, 0.65f);
            forge.Step(ForgingMinigame.StrikeCooldown, false);
            HeatTo(forge, 0.65f);
            Assert.IsTrue(forge.Strike());                               // in the band
            forge.Step(ForgingMinigame.StrikeCooldown, false);
            HeatTo(forge, 0.65f);
            Assert.IsTrue(forge.Strike());
            forge.Step(ForgingMinigame.StrikeCooldown, false);
            HeatTo(forge, 0.95f);
            Assert.IsTrue(forge.Strike());                               // too hot: a miss
            Assert.IsTrue(forge.Done);
            Assert.AreEqual(2, forge.Successes);
            Assert.That(forge.Score, Is.EqualTo(0.5f).Within(1e-5f));
            CollectionAssert.AreEqual(new[] { false, true, true, false }, forge.Results);
            Assert.IsFalse(forge.Strike(), "no strikes past the required count");
        }

        [Test]
        public void Strike_TooSoon_IsIgnored()
        {
            var forge = new ForgingMinigame(4, 0);
            Assert.IsTrue(forge.Strike());
            Assert.IsFalse(forge.Strike());
            Assert.AreEqual(1, forge.Strikes);
        }

        [Test]
        public void Coop_HalvesDecay_AndAdds008()
        {
            var forge = new ForgingMinigame(3, 0, coop: true);
            forge.Step(1f, bellows: true);
            Assert.That(forge.Heat, Is.EqualTo(ForgingMinigame.BellowsRate - ForgingMinigame.DecayRate * 0.5f).Within(1e-5f));
            for (var i = 0; i < 3; i++)
            {
                forge.Strike();
                forge.Step(ForgingMinigame.StrikeCooldown, false);
            }
            Assert.That(forge.Score, Is.EqualTo(ForgingMinigame.CoopScoreBonus + forge.Successes / 3f).Within(1e-5f));
        }

        /// <summary>Pumps the bellows (or waits) until the heat reaches <paramref name="target"/>.</summary>
        static void HeatTo(ForgingMinigame forge, float target)
        {
            for (var i = 0; i < 2000 && System.Math.Abs(forge.Heat - target) > 0.01f; i++)
                forge.Step(0.005f, forge.Heat < target);
        }
    }
}
