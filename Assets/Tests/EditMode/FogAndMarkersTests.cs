using Isle.Core;
using Isle.World.Island;
using NUnit.Framework;
using UnityEngine;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-MAP-01 verification 1–4: fog reveal and persistence, marker cap and removal.</summary>
    public sealed class FogAndMarkersTests
    {
        [Test]
        public void Reveal_Radius16_ExploresNearNotFar()
        {
            var fog = new FogGrid(sizeTiles: 1152, cellTiles: 4);
            fog.Reveal(new Vec2Int(500, 500), radiusTiles: 16);
            Assert.IsTrue(fog.IsExplored(new Vec2Int(500, 500)));
            Assert.IsTrue(fog.IsExplored(new Vec2Int(510, 500)));
            Assert.IsFalse(fog.IsExplored(new Vec2Int(524, 500)));
        }

        [Test]
        public void Reveal_ReportsOnlyNewCells()
        {
            var fog = new FogGrid(1152, 4);
            Assert.Greater(fog.Reveal(new Vec2Int(100, 100), 16).Count, 0);
            Assert.AreEqual(0, fog.Reveal(new Vec2Int(100, 100), 16).Count);
        }

        [Test]
        public void Serialize_RoundTrip_Identical()
        {
            var fog = new FogGrid(1152, 4);
            fog.Reveal(new Vec2Int(300, 700), 16);
            var back = new FogGrid(1152, 4);
            back.Deserialize(fog.Serialize());
            Assert.IsTrue(back.IsExplored(new Vec2Int(300, 700)));
            Assert.IsFalse(back.IsExplored(new Vec2Int(10, 10)));
        }

        [Test]
        public void Deserialize_Garbage_LeavesFogHidden()
        {
            var fog = new FogGrid(1152, 4);
            fog.Deserialize("not base64!");
            Assert.IsFalse(fog.IsExplored(new Vec2Int(0, 0)));
        }

        [Test]
        public void Markers_Add33rd_DropsOldest()
        {
            var markers = new MapMarkers(capacity: 32);
            for (var i = 0; i < 33; i++) markers.Add(new Vector2(i, 0));
            Assert.AreEqual(32, markers.All.Count);
            Assert.AreEqual(1f, markers.All[0].Position.x, 1e-4f);
        }

        [Test]
        public void Markers_RemoveNear_RemovesOnlyWithinRadius()
        {
            var markers = new MapMarkers(32);
            markers.Add(new Vector2(10, 10));
            Assert.IsFalse(markers.RemoveNear(new Vector2(40, 10), radius: 12f));
            Assert.IsTrue(markers.RemoveNear(new Vector2(15, 10), radius: 12f));
            Assert.AreEqual(0, markers.All.Count);
        }

        [Test]
        public void Markers_CycleColours()
        {
            var markers = new MapMarkers(32);
            markers.Add(Vector2.zero);
            markers.Add(Vector2.one);
            Assert.AreNotEqual(markers.All[0].Colour, markers.All[1].Colour);
        }
    }
}
