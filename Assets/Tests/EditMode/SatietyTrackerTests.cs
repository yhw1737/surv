using Isle.Gameplay.Cooking;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-COOK-01 §Satiety fatigue, verification 8: the fifth identical dish gives 0.40×.</summary>
    public sealed class SatietyTrackerTests
    {
        [Test]
        public void Eat_SameDishFiveTimes_FifthIs0_40()
        {
            var tracker = new SatietyTracker();
            float last = 0f;
            for (var i = 0; i < 5; i++) last = tracker.Eat("isle:grill|meat", nowMinutes: 0);
            Assert.AreEqual(0.40f, last, 0.001f);
        }

        [Test]
        public void Eat_SecondTime_Is0_85()
        {
            var tracker = new SatietyTracker();
            tracker.Eat("isle:grill|meat", 0);
            Assert.AreEqual(0.85f, tracker.Eat("isle:grill|meat", 0), 0.001f);
        }

        [Test]
        public void Eat_DifferentDishes_EachStartsFull()
        {
            var tracker = new SatietyTracker();
            tracker.Eat("isle:grill|meat", 0);
            Assert.AreEqual(1f, tracker.Eat("isle:grill|fish", 0), 0.001f);
        }

        [Test]
        public void Eat_AfterTwoDays_CountDecaysByOne()
        {
            var tracker = new SatietyTracker();
            tracker.Eat("isle:grill|meat", 0);
            tracker.Eat("isle:grill|meat", 0);
            // count 2 → after 2 days decays to 1, so this eat is the 2nd again: 0.85.
            Assert.AreEqual(0.85f, tracker.Eat("isle:grill|meat", 2 * 1440), 0.001f);
        }
    }
}
