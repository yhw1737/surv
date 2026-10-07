using System.Collections.Generic;
using System.Linq;
using Isle.Core;
using Isle.Data;
using Isle.Modding.Defs;
using Isle.World.Generation;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-DUNG-01 §Floor layout and verification 1–2: connected floors, keys before their locks, the exit
    /// reachable, deterministic per seed, and the decided sizes (6×6 grid, 8–12 rooms, one rest room, locks 1/1/2).</summary>
    public sealed class DungeonGeneratorTests
    {
        static IEnumerable<int> Seeds(int n) => Enumerable.Range(1, n).Select(i => i * 7919 + 13);

        [Test]
        public void Floors_AreConnected_AndTheExitIsReachable_With1000Seeds()
        {
            foreach (var seed in Seeds(1000))
            {
                var floor = DungeonGenerator.Generate(seed, floorIndex: 0, floors: 3, danger: 3);
                var reachable = DungeonGenerator.Reachable(floor, opened: floor.Locks.Select(l => l.Id).ToHashSet(), softGatesOpen: true);
                Assert.AreEqual(floor.Rooms.Count, reachable.Count, $"seed {seed}: not every room is connected");
                Assert.IsTrue(reachable.Contains(floor.Exit), $"seed {seed}: exit unreachable");
            }
        }

        [Test]
        public void EveryKey_IsReachableBeforeItsLock_With1000Seeds()
        {
            foreach (var seed in Seeds(1000))
            {
                var floor = DungeonGenerator.Generate(seed, 0, 3, danger: 3);
                // Walk the floor the way a player would: open whatever lock's key is already reachable, repeat.
                var opened = new HashSet<int>();
                while (true)
                {
                    var reachable = DungeonGenerator.Reachable(floor, opened, softGatesOpen: false);
                    var newly = floor.Locks.Where(l => !opened.Contains(l.Id) && reachable.Contains(l.KeyRoom)).Select(l => l.Id).ToList();
                    if (newly.Count == 0) break;
                    foreach (var id in newly) opened.Add(id);
                }
                Assert.AreEqual(floor.Locks.Count, opened.Count, $"seed {seed}: a key is behind its own lock");
                Assert.IsTrue(DungeonGenerator.Reachable(floor, opened, softGatesOpen: false).Contains(floor.Exit), $"seed {seed}: exit needs a soft gate");
            }
        }

        [Test]
        public void SameSeed_SameFloor()
        {
            var a = DungeonGenerator.Generate(424242, 1, 3, 2);
            var b = DungeonGenerator.Generate(424242, 1, 3, 2);
            Assert.AreEqual(a.Rooms.Select(r => (r.Cell.X, r.Cell.Y, r.Kind)), b.Rooms.Select(r => (r.Cell.X, r.Cell.Y, r.Kind)));
            Assert.AreEqual(a.Edges.Select(e => (e.A, e.B, e.Gate)), b.Edges.Select(e => (e.A, e.B, e.Gate)));
        }

        [Test]
        public void Sizes_FollowTheDecidedValues()
        {
            foreach (var seed in Seeds(300))
            {
                var floor = DungeonGenerator.Generate(seed, 0, 1, danger: 1);
                Assert.That(floor.Rooms.Count, Is.InRange(DungeonGenerator.MinRooms, DungeonGenerator.MaxRooms));
                Assert.IsTrue(floor.Rooms.All(r => r.Cell.X >= 0 && r.Cell.Y >= 0 && r.Cell.X < DungeonGenerator.GridCells && r.Cell.Y < DungeonGenerator.GridCells));
                Assert.AreEqual(1, floor.Rooms.Count(r => r.Kind == RoomKind.Rest), $"seed {seed}: rest rooms");
                Assert.AreEqual(1, floor.Rooms.Count(r => r.Kind == RoomKind.Entrance));
                Assert.AreEqual(1, floor.Rooms.Count(r => r.Kind == RoomKind.Vault));
                Assert.AreEqual(1, floor.Edges.Count(e => e.Gate == GateKind.Soft), "one soft-gated vault per floor");
            }
        }

        [Test]
        public void Locks_ByDanger_Are1_1_2()
        {
            Assert.AreEqual(1, DungeonGenerator.LocksFor(1));
            Assert.AreEqual(1, DungeonGenerator.LocksFor(2));
            Assert.AreEqual(2, DungeonGenerator.LocksFor(3));
            Assert.AreEqual(2, DungeonGenerator.LocksFor(4));
            foreach (var seed in Seeds(200))
                Assert.AreEqual(2, DungeonGenerator.Generate(seed, 0, 3, danger: 3).Locks.Count, $"seed {seed}");
        }

        [Test]
        public void LastFloor_EndsInABossRoom_OthersInStairs()
        {
            Assert.AreEqual(RoomKind.Stairs, DungeonGenerator.Generate(5, 0, 3, 2).Rooms.First(r => r.Index == DungeonGenerator.Generate(5, 0, 3, 2).Exit).Kind);
            var last = DungeonGenerator.Generate(5, 2, 3, 2);
            Assert.AreEqual(RoomKind.Boss, last.Rooms.First(r => r.Index == last.Exit).Kind);
        }

        // --- Tile build -------------------------------------------------------------------------------------------

        [Test]
        public void Tiles_DoorwaysOpenOnlyWhereRoomsConnect()
        {
            var floor = DungeonGenerator.Generate(77, 0, 2, 2);
            var tiles = DungeonTiles.Build(floor, DungeonTiles.FallbackTemplates(), seed: 77);
            Assert.AreEqual(DungeonGenerator.GridCells * DungeonTiles.RoomTiles, tiles.Size);
            foreach (var edge in floor.Edges)
            {
                var door = DungeonTiles.DoorwayCentre(floor.Rooms[edge.A].Cell, floor.Rooms[edge.B].Cell);
                Assert.AreNotEqual(DungeonTiles.Wall, tiles.At(door.X, door.Y), $"edge {edge.A}-{edge.B} has no doorway");
            }
            // A cell with no room is solid wall.
            var empty = Enumerable.Range(0, DungeonGenerator.GridCells * DungeonGenerator.GridCells)
                .Select(i => new Vec2Int(i % DungeonGenerator.GridCells, i / DungeonGenerator.GridCells))
                .FirstOrDefault(c => floor.Rooms.All(r => !r.Cell.Equals(c)));
            var centre = new Vec2Int(empty.X * DungeonTiles.RoomTiles + DungeonTiles.RoomTiles / 2, empty.Y * DungeonTiles.RoomTiles + DungeonTiles.RoomTiles / 2);
            Assert.AreEqual(DungeonTiles.Wall, tiles.At(centre.X, centre.Y));
        }

        [Test]
        public void Tiles_EveryRoomCentreIsReachableFromTheEntrance()
        {
            foreach (var seed in Seeds(50))
            {
                var floor = DungeonGenerator.Generate(seed, 0, 3, 3);
                var tiles = DungeonTiles.Build(floor, DungeonTiles.FallbackTemplates(), seed);
                var start = DungeonTiles.RoomCentre(floor.Rooms[floor.Entrance].Cell);
                var seen = tiles.FloodFill(start);
                foreach (var room in floor.Rooms)
                {
                    var c = DungeonTiles.RoomCentre(room.Cell);
                    Assert.IsTrue(seen.Contains(c), $"seed {seed}: room {room.Index} ({room.Kind}) centre unreachable through tiles");
                }
            }
        }

        // --- Content ----------------------------------------------------------------------------------------------

        [Test]
        public void Content_RoomTemplates_AreValid_AndCoverEveryRoomKind()
        {
            DefRegistry.Clear();
            try
            {
                var errors = DefinitionBootstrap.Load(System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, "definitions"));
                Assert.IsEmpty(errors, "definition load errors");
                var templates = DefRegistry.All<RoomTemplateDef>().ToList();
                Assert.IsNotEmpty(templates);
                foreach (var t in templates)
                    Assert.IsTrue(DungeonTiles.Valid(new RoomTemplate { Id = t.Id.Value, Tags = t.Tags, Rows = t.Rows }), $"{t.Id}: not 20×20 or its centre is wall");
                foreach (var kind in System.Enum.GetValues(typeof(RoomKind)).Cast<RoomKind>())
                {
                    var tag = kind.ToString().ToLowerInvariant();
                    Assert.IsTrue(templates.Any(t => t.Tags != null && t.Tags.Contains(tag)), $"no room template tagged '{tag}'");
                }
                Assert.AreEqual(4, DefRegistry.All<DungeonDef>().Count());
            }
            finally { DefRegistry.Clear(); }
        }

        [Test]
        public void Content_RealTemplates_KeepEveryRoomReachable()
        {
            DefRegistry.Clear();
            try
            {
                DefinitionBootstrap.Load(System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, "definitions"));
                var templates = DefRegistry.All<RoomTemplateDef>().Select(t => new RoomTemplate { Id = t.Id.Value, Tags = t.Tags, Rows = t.Rows }).ToList();
                foreach (var seed in Seeds(100))
                {
                    var floor = DungeonGenerator.Generate(seed, 0, 3, 3);
                    var tiles = DungeonTiles.Build(floor, templates, seed);
                    var seen = tiles.FloodFill(DungeonTiles.RoomCentre(floor.Rooms[floor.Entrance].Cell));
                    foreach (var room in floor.Rooms)
                    {
                        Assert.IsTrue(seen.Contains(DungeonTiles.RoomCentre(room.Cell)), $"seed {seed}: room {room.Index} ({room.Kind}) unreachable");
                        Assert.IsTrue(seen.Contains(tiles.Features[room.Index]), $"seed {seed}: room {room.Index} feature unreachable");
                        Assert.IsTrue(seen.Contains(tiles.KeySpots[room.Index]), $"seed {seed}: room {room.Index} key spot unreachable");
                    }
                    foreach (var (spawn, _) in tiles.Spawns) Assert.IsTrue(seen.Contains(spawn), $"seed {seed}: spawn {spawn.X},{spawn.Y} walled in");
                }
            }
            finally { DefRegistry.Clear(); }
        }
    }
}
