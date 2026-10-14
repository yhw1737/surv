using Isle.UI.Prototype;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>T-306: the intro cutscene's shots, captions, skipping and the title card's fades.</summary>
    public sealed class CutsceneTimelineTests
    {
        [TestCase(0f, IntroShot.Sailing)]
        [TestCase(3.4f, IntroShot.Sailing)]
        [TestCase(3.5f, IntroShot.Storm)]
        [TestCase(7.5f, IntroShot.Sinking)]
        [TestCase(9.5f, IntroShot.Dark)]
        [TestCase(12f, IntroShot.Waking)]
        [TestCase(14f, IntroShot.Done)]
        public void Shot_AtTime_IsInOrder(float t, IntroShot expected) => Assert.AreEqual(expected, CutsceneTimeline.Shot(t));

        [Test]
        public void Caption_OnePerShot_SinkingAndDarkShare()
        {
            Assert.AreEqual(0, CutsceneTimeline.CaptionIndex(1f, 4));
            Assert.AreEqual(1, CutsceneTimeline.CaptionIndex(5f, 4));
            Assert.AreEqual(2, CutsceneTimeline.CaptionIndex(8f, 4));
            Assert.AreEqual(2, CutsceneTimeline.CaptionIndex(10f, 4));
            Assert.AreEqual(3, CutsceneTimeline.CaptionIndex(12f, 4));
            Assert.AreEqual(-1, CutsceneTimeline.CaptionIndex(12f, 3), "a scenario with fewer captions shows none there");
        }

        [Test]
        public void Skip_JumpsToWaking_ThenEnds()
        {
            Assert.AreEqual(IntroShot.Waking, CutsceneTimeline.Shot(CutsceneTimeline.SkipFrom(2f)));
            Assert.AreEqual(IntroShot.Done, CutsceneTimeline.Shot(CutsceneTimeline.SkipFrom(CutsceneTimeline.SkipFrom(2f))));
        }

        [Test]
        public void Waking_FadesFromBlack()
        {
            Assert.AreEqual(1f, CutsceneTimeline.WakeDarkness(CutsceneTimeline.WakingAt), 1e-4f);
            Assert.AreEqual(0f, CutsceneTimeline.WakeDarkness(CutsceneTimeline.StandUpAt), 1e-4f, "awake before standing up");
        }

        [Test]
        public void Title_FadesInHoldsAndOut()
        {
            Assert.AreEqual(0f, CutsceneTimeline.TitleAlpha(0f), 1e-4f);
            Assert.AreEqual(1f, CutsceneTimeline.TitleAlpha(CutsceneTimeline.TitleFadeIn + 0.5f), 1e-4f);
            Assert.AreEqual(0f, CutsceneTimeline.TitleAlpha(CutsceneTimeline.TitleLength), 1e-4f);
        }

        [Test]
        public void Lightning_FlashesOnlyAtStrikes()
        {
            Assert.AreEqual(0f, CutsceneTimeline.Flash(1f));
            Assert.Greater(CutsceneTimeline.Flash(CutsceneTimeline.LightningAt[0] + 0.05f), 0.5f);
        }
    }
}
