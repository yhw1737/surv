using System;
using System.Collections.Generic;
using Isle.Core;

namespace Isle.World.Generation
{
    public enum RoomKind { Entrance, Combat, Rest, Key, Vault, Stairs, Boss }

    public enum GateKind { None, Lock, Soft }

    public sealed class DungeonRoom
    {
        public int Index;
        public Vec2Int Cell;
        public RoomKind Kind;
    }

    public sealed class DungeonEdge
    {
        public int A, B;
        public GateKind Gate;
        public int LockId = -1;
    }

    public sealed class DungeonLock
    {
        public int Id;
        public int Edge;
        public int KeyRoom;
    }

    /// <summary>One generated floor: rooms on the grid, the doors between them, and which doors are locked or soft-gated.</summary>
    public sealed class DungeonFloor
    {
        public readonly List<DungeonRoom> Rooms = new();
        public readonly List<DungeonEdge> Edges = new();
        public readonly List<DungeonLock> Locks = new();
        public int Entrance;
        public int Exit;
    }

    /// <summary>
    /// SYS-DUNG-01 §Floor layout. Static pure, seed-deterministic. A self-avoiding walk carves the main path entrance →
    /// exit (stairs, or the boss on the last floor); side branches hang off it. The lock-and-key pass locks
    /// <see cref="LocksFor"/> doors on the main path and puts each key on a branch that leaves the path before its lock,
    /// so a floor is always solvable. One rest room, one soft-gated vault, the rest combat rooms.
    /// </summary>
    public static class DungeonGenerator
    {
        // SYS-DUNG-01 decided values (developer Q&A, 2026-10-06/07).
        public const int GridCells = 6;
        public const int MinRooms = 8;
        public const int MaxRooms = 12;

        public static int LocksFor(int danger) => danger >= 3 ? 2 : 1;

        static readonly Vec2Int[] Steps = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

        public static DungeonFloor Generate(int seed, int floorIndex, int floors, int danger)
        {
            for (var attempt = 0; attempt < 200; attempt++)
            {
                var rng = new Random(unchecked((int)IslandGenerator.Hash(seed, floorIndex * 131 + danger, attempt)));
                var floor = TryGenerate(rng, floorIndex == floors - 1, LocksFor(danger));
                if (floor != null) return floor;
            }
            throw new InvalidOperationException($"dungeon floor could not be laid out (seed {seed}, floor {floorIndex})");
        }

        static DungeonFloor TryGenerate(Random rng, bool lastFloor, int locks)
        {
            var target = rng.Next(MinRooms, MaxRooms + 1);
            // Branches needed besides the main path: one key room per lock, the rest room, the vault.
            var special = locks + 2;
            var mainLength = Math.Max(3 + locks, Math.Min(target - special, target / 2 + 2));
            if (mainLength + special > target) return null;

            var floor = new DungeonFloor();
            var occupied = new Dictionary<Vec2Int, int>();

            // Main path.
            var cell = new Vec2Int(rng.Next(GridCells), rng.Next(GridCells));
            var main = new List<int> { AddRoom(floor, occupied, cell, RoomKind.Combat) };
            while (main.Count < mainLength)
            {
                var options = FreeNeighbours(occupied, cell);
                if (options.Count == 0) return null;
                var next = options[rng.Next(options.Count)];
                var index = AddRoom(floor, occupied, next, RoomKind.Combat);
                AddEdge(floor, main[^1], index, GateKind.None);
                main.Add(index);
                cell = next;
            }
            floor.Entrance = main[0];
            floor.Exit = main[^1];
            floor.Rooms[floor.Entrance].Kind = RoomKind.Entrance;
            floor.Rooms[floor.Exit].Kind = lastFloor ? RoomKind.Boss : RoomKind.Stairs;

            // Locks on the main path (never the first door, so the entrance room is free), spread along it.
            var lockEdges = new List<int>();
            var candidates = new List<int>();
            for (var k = 1; k < main.Count - 1; k++) candidates.Add(k); // edge k joins main[k] → main[k+1]
            if (candidates.Count < locks) return null;
            Shuffle(candidates, rng);
            lockEdges.AddRange(candidates.GetRange(0, locks));
            lockEdges.Sort();
            for (var i = 0; i < lockEdges.Count; i++)
            {
                var k = lockEdges[i];
                var edge = FindEdge(floor, main[k], main[k + 1]);
                floor.Edges[edge].Gate = GateKind.Lock;
                floor.Edges[edge].LockId = i;

                // Its key hangs off a main room at or before the lock.
                var keyRoom = -1;
                var order = new List<int>();
                for (var j = 0; j <= k; j++) order.Add(j);
                Shuffle(order, rng);
                foreach (var j in order)
                {
                    keyRoom = Branch(floor, occupied, main[j], RoomKind.Key, GateKind.None, rng);
                    if (keyRoom >= 0) break;
                }
                if (keyRoom < 0) return null;
                floor.Locks.Add(new DungeonLock { Id = i, Edge = edge, KeyRoom = keyRoom });
            }

            // Rest room: on the boss floor, just before the boss; elsewhere anywhere off the main path.
            var restFrom = lastFloor ? new List<int> { main[^2] } : new List<int>(main);
            if (!lastFloor) Shuffle(restFrom, rng);
            var rest = -1;
            foreach (var from in restFrom)
            {
                rest = Branch(floor, occupied, from, RoomKind.Rest, GateKind.None, rng);
                if (rest >= 0) break;
            }
            if (rest < 0) return null;

            // The vault, behind a soft gate, off any room.
            if (!BranchAnywhere(floor, occupied, RoomKind.Vault, GateKind.Soft, rng)) return null;

            // Fill with combat rooms.
            while (floor.Rooms.Count < target)
                if (!BranchAnywhere(floor, occupied, RoomKind.Combat, GateKind.None, rng)) break;
            return floor.Rooms.Count >= MinRooms ? floor : null;
        }

        static bool BranchAnywhere(DungeonFloor floor, Dictionary<Vec2Int, int> occupied, RoomKind kind, GateKind gate, Random rng)
        {
            var from = new List<int>();
            foreach (var room in floor.Rooms)
                if (room.Kind is RoomKind.Combat or RoomKind.Entrance or RoomKind.Rest or RoomKind.Key) from.Add(room.Index);
            Shuffle(from, rng);
            foreach (var index in from)
                if (Branch(floor, occupied, index, kind, gate, rng) >= 0) return true;
            return false;
        }

        static int Branch(DungeonFloor floor, Dictionary<Vec2Int, int> occupied, int from, RoomKind kind, GateKind gate, Random rng)
        {
            var options = FreeNeighbours(occupied, floor.Rooms[from].Cell);
            if (options.Count == 0) return -1;
            var index = AddRoom(floor, occupied, options[rng.Next(options.Count)], kind);
            AddEdge(floor, from, index, gate);
            return index;
        }

        static int AddRoom(DungeonFloor floor, Dictionary<Vec2Int, int> occupied, Vec2Int cell, RoomKind kind)
        {
            var index = floor.Rooms.Count;
            floor.Rooms.Add(new DungeonRoom { Index = index, Cell = cell, Kind = kind });
            occupied[cell] = index;
            return index;
        }

        static void AddEdge(DungeonFloor floor, int a, int b, GateKind gate) => floor.Edges.Add(new DungeonEdge { A = a, B = b, Gate = gate });

        static int FindEdge(DungeonFloor floor, int a, int b)
        {
            for (var i = 0; i < floor.Edges.Count; i++)
                if ((floor.Edges[i].A == a && floor.Edges[i].B == b) || (floor.Edges[i].A == b && floor.Edges[i].B == a)) return i;
            return -1;
        }

        static List<Vec2Int> FreeNeighbours(Dictionary<Vec2Int, int> occupied, Vec2Int cell)
        {
            var list = new List<Vec2Int>();
            foreach (var step in Steps)
            {
                var n = new Vec2Int(cell.X + step.X, cell.Y + step.Y);
                if (n.X < 0 || n.Y < 0 || n.X >= GridCells || n.Y >= GridCells || occupied.ContainsKey(n)) continue;
                list.Add(n);
            }
            return list;
        }

        static void Shuffle<T>(List<T> list, Random rng)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>Rooms reachable from the entrance through open doors: unlocked doors, locks in
        /// <paramref name="opened"/>, and soft gates when <paramref name="softGatesOpen"/>.</summary>
        public static HashSet<int> Reachable(DungeonFloor floor, ISet<int> opened, bool softGatesOpen)
        {
            var seen = new HashSet<int> { floor.Entrance };
            var queue = new Queue<int>();
            queue.Enqueue(floor.Entrance);
            while (queue.Count > 0)
            {
                var room = queue.Dequeue();
                foreach (var edge in floor.Edges)
                {
                    if (edge.A != room && edge.B != room) continue;
                    var open = edge.Gate switch
                    {
                        GateKind.Lock => opened != null && opened.Contains(edge.LockId),
                        GateKind.Soft => softGatesOpen,
                        _ => true,
                    };
                    if (!open) continue;
                    var other = edge.A == room ? edge.B : edge.A;
                    if (seen.Add(other)) queue.Enqueue(other);
                }
            }
            return seen;
        }
    }
}
