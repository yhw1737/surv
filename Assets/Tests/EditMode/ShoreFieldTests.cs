using Isle.World.Island;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>The smooth shoreline: collision follows the drawn water's edge, not the tile grid.</summary>
    public sealed class ShoreFieldTests
    {
        static float Field(System.Func<int, int, bool> isWater, float u, float v) =>
            ShoreField.At((x, y) => ShoreField.AtTile(isWater, x, y), u, v);

        [Test]
        public void StraightShore_EdgeIsBetweenTheTiles()
        {
            // Water for x < 0, land for x >= 0: tile edge at u = -0.5 (tile centres are integers).
            bool Water(int x, int y) => x < 0;
            Assert.That(Field(Water, -0.5f, 3f), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.IsFalse(ShoreField.IsWater(Field(Water, -0.4f, 3f)), "just on the land side");
            Assert.IsTrue(ShoreField.IsWater(Field(Water, -0.6f, 3f)), "just on the water side");
        }

        [Test]
        public void StairShore_CornerIsRounded()
        {
            // A diagonal staircase: water where x + y < 0. The square land corner at the step pokes out on the
            // tile grid; on the smooth field the shore runs between, so the corner tip is water and the notch is land.
            bool Water(int x, int y) => x + y < 0;
            // Land tile (0,0)'s outer corner (-0.5,-0.5) sits on the grid line; the smooth line cuts it off.
            Assert.IsTrue(ShoreField.IsWater(Field(Water, -0.45f, -0.45f)), "corner tip of a land tile");
            // Water tile (-1,0)'s inner notch toward land (−0.55, 0.45) lies on the land side of the smooth line.
            Assert.IsFalse(ShoreField.IsWater(Field(Water, -0.55f, 0.45f)), "notch of a water tile");
        }

        [Test]
        public void OpenWaterAndOpenLand_AreUnambiguous()
        {
            Assert.IsTrue(ShoreField.IsWater(Field((x, y) => true, 0.3f, 0.7f)));
            Assert.IsFalse(ShoreField.IsWater(Field((x, y) => false, 0.3f, 0.7f)));
        }

        [Test]
        public void SingleWaterTile_IsTooSmallToDraw_AndToBlock()
        {
            Assert.That(ShoreField.AtTile((x, y) => x == 0 && y == 0, 0, 0), Is.EqualTo(36f / 256f).Within(1e-5f));
        }
    }
}
