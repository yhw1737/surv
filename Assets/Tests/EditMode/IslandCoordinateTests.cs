using Isle.Core;
using Isle.World.Generation;
using Isle.World.Island;
using NUnit.Framework;
using UnityEngine;

namespace Isle.Tests.EditMode
{
    /// <summary>The island map sprite spans ±Size/2 with one pixel per tile. Node positions, walkability and
    /// the minimap all have to agree on which tile a world point is in.</summary>
    public sealed class IslandCoordinateTests
    {
        [Test]
        public void TileToWorld_ThenBack_ReturnsSameTile()
        {
            foreach (var tile in new[] { new Vec2Int(0, 0), new Vec2Int(191, 192), new Vec2Int(IslandGenerator.Size - 1, 7) })
                Assert.AreEqual(tile, IslandWorld.WorldToTile(IslandWorld.TileToWorld(tile)));
        }

        [Test]
        public void TileToWorld_Tile0_IsHalfTileInsideMapEdge()
        {
            var centre = IslandWorld.TileToWorld(new Vec2Int(0, 0));
            Assert.AreEqual(-IslandGenerator.Size / 2f + 0.5f, centre.x, 1e-5f);
        }

        [Test]
        public void WorldToTile_Origin_IsCentreTile()
        {
            Assert.AreEqual(new Vec2Int(IslandGenerator.Size / 2, IslandGenerator.Size / 2), IslandWorld.WorldToTile(Vector2.zero));
        }
    }
}
