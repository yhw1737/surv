using System;
using System.Collections.Generic;
using Isle.Core;

namespace Isle.World.Generation
{
    /// <summary>
    /// SYS-DUNG-01 Clockwork Ruin: "rooms on a timer". Extra doorways turn a floor's tree of rooms into loops, then some
    /// doors are split into two gear groups that swap open/shut every period — the route changes, but in either phase
    /// the way from the entrance to the exit stays open and every room can be reached in at least one phase. Loops stay
    /// inside one lock region (never around a locked door or the vault's gate) and never touch the boss room or the
    /// vault, so the lock-and-key guarantee holds. Static pure, seed-deterministic.
    /// </summary>
    public static class ClockworkDoors
    {
        static readonly Vec2Int[] Steps = { new(1, 0), new(0, 1) };

        /// <summary>Joins grid-neighbour rooms that aren't joined, each with <paramref name="chance"/>. Returns how many
        /// doorways were added (appended to <see cref="DungeonFloor.Edges"/> as plain doors).</summary>
        public static int AddLoops(DungeonFloor floor, int seed, float chance)
        {
            var region = Regions(floor);
            var byCell = new Dictionary<Vec2Int, int>();
            foreach (var room in floor.Rooms) byCell[room.Cell] = room.Index;
            var rng = new Random(unchecked((int)IslandGenerator.Hash(seed, 7351, floor.Rooms.Count)));
            var added = 0;
            foreach (var room in floor.Rooms)
            foreach (var step in Steps)
            {
                if (!byCell.TryGetValue(new Vec2Int(room.Cell.X + step.X, room.Cell.Y + step.Y), out var other)) continue;
                if (Joined(floor, room.Index, other) || region[room.Index] != region[other]) continue;
                if (Excluded(floor.Rooms[room.Index].Kind) || Excluded(floor.Rooms[other].Kind)) continue;
                if (rng.NextDouble() >= chance) continue;
                floor.Edges.Add(new DungeonEdge { A = room.Index, B = other, Gate = GateKind.None });
                added++;
            }
            return added;
        }

        /// <summary>Gear groups (0 or 1) for up to <paramref name="share"/> of the plain doors: a door of group g is open
        /// while the phase is g. Doors that would break a phase's guarantees stay plain.</summary>
        public static Dictionary<int, int> Assign(DungeonFloor floor, int seed, float share)
        {
            var gears = new Dictionary<int, int>();
            var candidates = new List<int>();
            for (var e = 0; e < floor.Edges.Count; e++)
            {
                var edge = floor.Edges[e];
                if (edge.Gate != GateKind.None || Excluded(floor.Rooms[edge.A].Kind) || Excluded(floor.Rooms[edge.B].Kind)) continue;
                candidates.Add(e);
            }
            var want = (int)Math.Round(candidates.Count * share);
            var rng = new Random(unchecked((int)IslandGenerator.Hash(seed, 7353, floor.Edges.Count)));
            for (var i = candidates.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }
            foreach (var e in candidates)
            {
                if (gears.Count >= want) break;
                var first = rng.Next(2);
                for (var k = 0; k < 2; k++)
                {
                    var group = (first + k) % 2;
                    gears[e] = group;
                    if (Valid(floor, gears)) break;
                    gears.Remove(e);
                }
            }
            return gears;
        }

        /// <summary>Both phases keep the exit reachable from the entrance, and no room is shut off in both.</summary>
        public static bool Valid(DungeonFloor floor, IReadOnlyDictionary<int, int> gears)
        {
            var phase0 = Reachable(floor, gears, 0, floor.Entrance);
            var phase1 = Reachable(floor, gears, 1, floor.Entrance);
            if (!phase0.Contains(floor.Exit) || !phase1.Contains(floor.Exit)) return false;
            foreach (var room in floor.Rooms)
                if (!phase0.Contains(room.Index) && !phase1.Contains(room.Index)) return false;
            return true;
        }

        /// <summary>Rooms reachable from <paramref name="from"/> in a phase. Locked doors and soft gates count as
        /// passable (the player opens them); a gear door only in its own phase.</summary>
        public static HashSet<int> Reachable(DungeonFloor floor, IReadOnlyDictionary<int, int> gears, int phase, int from)
        {
            var seen = new HashSet<int> { from };
            var queue = new Queue<int>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var room = queue.Dequeue();
                for (var e = 0; e < floor.Edges.Count; e++)
                {
                    var edge = floor.Edges[e];
                    if (edge.A != room && edge.B != room) continue;
                    if (gears.TryGetValue(e, out var group) && group != phase) continue;
                    var next = edge.A == room ? edge.B : edge.A;
                    if (seen.Add(next)) queue.Enqueue(next);
                }
            }
            return seen;
        }

        static bool Excluded(RoomKind kind) => kind is RoomKind.Boss or RoomKind.Vault;

        static bool Joined(DungeonFloor floor, int a, int b)
        {
            foreach (var edge in floor.Edges)
                if ((edge.A == a && edge.B == b) || (edge.A == b && edge.B == a)) return true;
            return false;
        }

        /// <summary>Rooms grouped by what's reachable through plain doors only (locked doors and soft gates split them).</summary>
        static int[] Regions(DungeonFloor floor)
        {
            var region = new int[floor.Rooms.Count];
            for (var i = 0; i < region.Length; i++) region[i] = -1;
            var next = 0;
            for (var start = 0; start < region.Length; start++)
            {
                if (region[start] >= 0) continue;
                var queue = new Queue<int>();
                queue.Enqueue(start);
                region[start] = next;
                while (queue.Count > 0)
                {
                    var room = queue.Dequeue();
                    foreach (var edge in floor.Edges)
                    {
                        if (edge.Gate != GateKind.None || (edge.A != room && edge.B != room)) continue;
                        var other = edge.A == room ? edge.B : edge.A;
                        if (region[other] >= 0) continue;
                        region[other] = next;
                        queue.Enqueue(other);
                    }
                }
                next++;
            }
            return region;
        }
    }
}
