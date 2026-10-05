using Isle.Gameplay.Fishing;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-FISH-01 prototype revision: cast → wait → bite → hook within the window, or the fish gets away.</summary>
    public sealed class FishingCastTests
    {
        [Test]
        public void Step_BeforeBiteTime_StillWaiting()
        {
            var cast = new FishingCast(biteAfterSeconds: 5f, hookWindowSeconds: 1f);
            cast.Step(4.9f, clicked: false);
            Assert.AreEqual(CastState.Waiting, cast.State);
        }

        [Test]
        public void Step_AtBiteTime_Bites()
        {
            var cast = new FishingCast(5f, 1f);
            cast.Step(5f, false);
            Assert.AreEqual(CastState.Bite, cast.State);
        }

        [Test]
        public void Step_ClickDuringBite_Hooks()
        {
            var cast = new FishingCast(5f, 1f);
            cast.Step(5f, false);
            cast.Step(0.5f, clicked: true);
            Assert.AreEqual(CastState.Hooked, cast.State);
        }

        [Test]
        public void Step_ClickBeforeBite_Ignored()
        {
            var cast = new FishingCast(5f, 1f);
            cast.Step(1f, clicked: true);
            Assert.AreEqual(CastState.Waiting, cast.State);
        }

        [Test]
        public void Step_WindowPassesWithoutClick_Missed()
        {
            var cast = new FishingCast(5f, 1f);
            cast.Step(5f, false);
            cast.Step(1.01f, false);
            Assert.AreEqual(CastState.Missed, cast.State);
        }

        [Test]
        public void GatherSeconds_ScalesWithLevelAndFloorsAtOne()
        {
            Assert.AreEqual(10f, Isle.World.Objects.GatherCalculator.GatherSeconds(10f, level: 1), 1e-4f);
            Assert.AreEqual(3f, Isle.World.Objects.GatherCalculator.GatherSeconds(10f, level: 50), 1e-4f);
            Assert.AreEqual(1f, Isle.World.Objects.GatherCalculator.GatherSeconds(2f, level: 50), 1e-4f);
        }
    }
}
