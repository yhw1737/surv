using Isle.Gameplay.Skills;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-SKILL-01 §XP curve table.</summary>
    public sealed class XpCurveTests
    {
        [TestCase(1, 80)]
        [TestCase(5, 1051)]
        [TestCase(10, 3185)]
        [TestCase(15, 6093)]
        [TestCase(20, 9655)]
        [TestCase(49, 40495)]
        public void XpToNext_MatchesSpecTable(int level, int expected)
        {
            Assert.AreEqual(expected, XpCurve.XpToNext(level));
        }

        [TestCase(5, 1522L)]
        [TestCase(10, 10699L)]
        [TestCase(15, 32160L)]
        [TestCase(50, 783538L)]
        public void TotalXpTo_MatchesSpecTable(int level, long expected)
        {
            Assert.AreEqual(expected, XpCurve.TotalXpTo(level));
        }
    }
}
