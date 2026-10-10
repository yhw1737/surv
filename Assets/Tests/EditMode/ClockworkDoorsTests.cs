using System.Collections.Generic;
using System.Linq;
using Isle.World.Generation;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>T-203 Clockwork Ruin: gear doors change the route every period but never trap or shut anyone out.</summary>
    public sealed class ClockworkDoorsTests
    {
        const float LoopChance = 0.6f, Share = 0.4f;

        [Test]
        public void ThousandSeeds_BothPhasesReachTheExit_NoRoomShutForever()
        {
            var withGears = 0;
            var routeChanges = 0;
            for (var seed = 1; seed <= 1000; seed++)
            {
                var floor = DungeonGenerator.Generate(seed, seed % 3, 3, 3);
                ClockworkDoors.AddLoops(floor, seed, LoopChance);
                var gears = ClockworkDoors.Assign(floor, seed, Share);
                Assert.IsTrue(ClockworkDoors.Valid(floor, gears), $"seed {seed}");
                if (gears.Count > 0) withGears++;
                // Some gear door lies on a route the other phase doesn't use: the way through really changes.
                if (gears.Values.Contains(0) && gears.Values.Contains(1)) routeChanges++;
            }
            Assert.Greater(withGears, 900, "most floors should have gear doors");
            Assert.Greater(routeChanges, 500, "both gear groups should be in use on most floors");
        }

        [Test]
        public void Loops_NeverBypassALockOrTheVaultGate_NorTouchTheBossRoom()
        {
            for (var seed = 1; seed <= 500; seed++)
            {
                var floor = DungeonGenerator.Generate(seed, 2, 3, 3);
                var before = floor.Edges.Count;
                ClockworkDoors.AddLoops(floor, seed, 1f);
                foreach (var edge in floor.Edges.Skip(before))
                {
                    Assert.AreEqual(GateKind.None, edge.Gate);
                    Assert.IsFalse(floor.Rooms[edge.A].Kind is RoomKind.Boss or RoomKind.Vault, $"seed {seed}");
                    Assert.IsFalse(floor.Rooms[edge.B].Kind is RoomKind.Boss or RoomKind.Vault, $"seed {seed}");
                }
                // Every lock still separates its key's side from the rooms past it: with all locked doors and gates
                // shut, the exit is unreachable from the entrance whenever the main path had a lock before it.
                var shut = new Dictionary<int, int>();
                for (var e = 0; e < floor.Edges.Count; e++) if (floor.Edges[e].Gate != GateKind.None) shut[e] = -1;
                var open = ClockworkDoors.Reachable(floor, shut, 0, floor.Entrance);
                if (floor.Locks.Count > 0) Assert.IsFalse(open.Contains(floor.Exit), $"seed {seed}: a loop went around a lock");
            }
        }

        [Test]
        public void SameSeed_SameDoors()
        {
            var a = DungeonGenerator.Generate(42, 1, 3, 3);
            var b = DungeonGenerator.Generate(42, 1, 3, 3);
            ClockworkDoors.AddLoops(a, 42, LoopChance);
            ClockworkDoors.AddLoops(b, 42, LoopChance);
            CollectionAssert.AreEqual(ClockworkDoors.Assign(a, 42, Share), ClockworkDoors.Assign(b, 42, Share));
        }
    }
}
