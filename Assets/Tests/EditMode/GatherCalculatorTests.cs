using Isle.World.Objects;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-WORLD-03 §Gathering: respawn is in-game time, converted to real seconds.</summary>
    public sealed class GatherCalculatorTests
    {
        const float Tolerance = 0.001f;

        [Test]
        public void RespawnRealSeconds_SixtyInGameMinutes_Returns50()
        {
            Assert.AreEqual(50f, GatherCalculator.RespawnRealSeconds(60f), Tolerance);
        }

        [Test]
        public void RespawnRealSeconds_ZeroMinutes_ReturnsZero()
        {
            Assert.AreEqual(0f, GatherCalculator.RespawnRealSeconds(0f), Tolerance);
        }
    }
}
