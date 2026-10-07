using System;
using System.Collections.Generic;
using Isle.Core;

namespace Isle.World.Generation
{
    /// <summary>A room layout (SYS-DUNG-01: room templates from JSON). <see cref="Rows"/> are <see cref="DungeonTiles.RoomTiles"/>
    /// strings of that length, top row first: <c>#</c> wall, <c>.</c> floor, <c>S</c> creature spawn, <c>C</c> chest,
    /// <c>K</c> key spot, <c>X</c> feature (stairs, exit, boss spot), <c>F</c> campfire spot, <c>,</c> floor decoration.</summary>
    public sealed class RoomTemplate
    {
        public string Id;
        public string[] Tags;
        public string[] Rows;
    }

    /// <summary>The tiles of one floor plus the markers its rooms placed (tile coordinates, y up).</summary>
    public sealed class DungeonTileMap
    {
        public readonly int Size;
        readonly char[] _tiles;
        public readonly List<(Vec2Int Tile, int Room)> Spawns = new();
        public readonly List<(Vec2Int Tile, int Room)> Chests = new();
        public readonly Dictionary<int, Vec2Int> Features = new();   // room → its X (or the room centre)
        public readonly Dictionary<int, Vec2Int> KeySpots = new();
        public readonly Dictionary<int, Vec2Int> Campfires = new();
        public readonly List<(Vec2Int Tile, char Kind)> Decor = new();

        public DungeonTileMap(int size)
        {
            Size = size;
            _tiles = new char[size * size];
            for (var i = 0; i < _tiles.Length; i++) _tiles[i] = DungeonTiles.Wall;
        }

        public char At(int x, int y) => x < 0 || y < 0 || x >= Size || y >= Size ? DungeonTiles.Wall : _tiles[y * Size + x];
        public void Set(int x, int y, char c) { if (x >= 0 && y >= 0 && x < Size && y < Size) _tiles[y * Size + x] = c; }
        public bool IsFloor(int x, int y) => At(x, y) != DungeonTiles.Wall;

        public HashSet<Vec2Int> FloodFill(Vec2Int start)
        {
            var seen = new HashSet<Vec2Int>();
            if (!IsFloor(start.X, start.Y)) return seen;
            var stack = new Stack<Vec2Int>();
            stack.Push(start);
            seen.Add(start);
            while (stack.Count > 0)
            {
                var p = stack.Pop();
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    var n = new Vec2Int(p.X + dx, p.Y + dy);
                    if (!IsFloor(n.X, n.Y) || !seen.Add(n)) continue;
                    stack.Push(n);
                }
            }
            return seen;
        }
    }

    /// <summary>
    /// SYS-DUNG-01: stamps room templates into a floor's tiles (rotated/mirrored per seed), opens a 3-tile doorway
    /// for every connection and clears a short corridor in from it so a template can never seal a door. Static pure.
    /// </summary>
    public static class DungeonTiles
    {
        /// <summary>Decided: one room cell is 20×20 tiles.</summary>
        public const int RoomTiles = 20;
        public const char Wall = '#';
        public const char Floor = '.';
        const int DoorHalfWidth = 1;
        const int CorridorDepth = 4;

        public static Vec2Int RoomCentre(Vec2Int cell) => new(cell.X * RoomTiles + RoomTiles / 2, cell.Y * RoomTiles + RoomTiles / 2);

        /// <summary>The doorway tile on <paramref name="b"/>'s side of the wall it shares with <paramref name="a"/>.</summary>
        public static Vec2Int DoorwayCentre(Vec2Int a, Vec2Int b)
        {
            if (a.Y == b.Y) return new Vec2Int(Math.Max(a.X, b.X) * RoomTiles, a.Y * RoomTiles + RoomTiles / 2);
            return new Vec2Int(a.X * RoomTiles + RoomTiles / 2, Math.Max(a.Y, b.Y) * RoomTiles);
        }

        /// <summary>The doorway's tiles (both sides of the wall, full width) — where a lock or a soft gate stands.</summary>
        public static List<Vec2Int> DoorwayTiles(Vec2Int a, Vec2Int b)
        {
            var list = new List<Vec2Int>();
            var c = DoorwayCentre(a, b);
            for (var w = -DoorHalfWidth; w <= DoorHalfWidth; w++)
            for (var d = -1; d <= 0; d++)
                list.Add(a.Y == b.Y ? new Vec2Int(c.X + d, c.Y + w) : new Vec2Int(c.X + w, c.Y + d));
            return list;
        }

        public static DungeonTileMap Build(DungeonFloor floor, IReadOnlyList<RoomTemplate> templates, int seed)
        {
            var map = new DungeonTileMap(DungeonGenerator.GridCells * RoomTiles);
            var rng = new Random(unchecked((int)IslandGenerator.Hash(seed, 9001, floor.Rooms.Count)));
            foreach (var room in floor.Rooms)
            {
                var template = Pick(templates, room.Kind, rng);
                var turns = rng.Next(4);
                var mirror = rng.Next(2) == 1;
                Stamp(map, room, template, turns, mirror);
            }
            foreach (var edge in floor.Edges) Carve(map, floor.Rooms[edge.A].Cell, floor.Rooms[edge.B].Cell);
            return map;
        }

        static RoomTemplate Pick(IReadOnlyList<RoomTemplate> templates, RoomKind kind, Random rng)
        {
            var tag = kind.ToString().ToLowerInvariant();
            var matches = new List<RoomTemplate>();
            foreach (var t in templates) if (t.Tags != null && Array.IndexOf(t.Tags, tag) >= 0 && Valid(t)) matches.Add(t);
            if (matches.Count == 0)
                foreach (var t in templates) if (t.Tags != null && Array.IndexOf(t.Tags, "combat") >= 0 && Valid(t)) matches.Add(t);
            if (matches.Count == 0) matches.AddRange(FallbackTemplates());
            return matches[rng.Next(matches.Count)];
        }

        /// <summary>A template is usable if it is the right size and its centre is floor (everything is reachable from there).</summary>
        public static bool Valid(RoomTemplate t)
        {
            if (t?.Rows == null || t.Rows.Length != RoomTiles) return false;
            foreach (var row in t.Rows) if (row == null || row.Length != RoomTiles) return false;
            return t.Rows[RoomTiles / 2][RoomTiles / 2] != Wall;
        }

        static void Stamp(DungeonTileMap map, DungeonRoom room, RoomTemplate template, int turns, bool mirror)
        {
            var ox = room.Cell.X * RoomTiles;
            var oy = room.Cell.Y * RoomTiles;
            Vec2Int? feature = null;
            for (var row = 0; row < RoomTiles; row++)
            for (var col = 0; col < RoomTiles; col++)
            {
                var c = template.Rows[row][col];
                // Template rows are written top-down; tile y grows upward.
                var (x, y) = Transform(col, RoomTiles - 1 - row, turns, mirror);
                var tile = new Vec2Int(ox + x, oy + y);
                map.Set(tile.X, tile.Y, c == Wall ? Wall : Floor);
                switch (c)
                {
                    case 'S': map.Spawns.Add((tile, room.Index)); break;
                    case 'C': map.Chests.Add((tile, room.Index)); break;
                    case 'K': map.KeySpots[room.Index] = tile; break;
                    case 'X': feature = tile; break;
                    case 'F': map.Campfires[room.Index] = tile; break;
                    case ',': map.Decor.Add((tile, c)); break;
                }
            }
            map.Features[room.Index] = feature ?? RoomCentre(room.Cell);
            if (!map.KeySpots.ContainsKey(room.Index)) map.KeySpots[room.Index] = map.Features[room.Index];
            if (!map.Campfires.ContainsKey(room.Index)) map.Campfires[room.Index] = map.Features[room.Index];
        }

        static (int, int) Transform(int x, int y, int turns, bool mirror)
        {
            const int max = RoomTiles - 1;
            if (mirror) x = max - x;
            for (var i = 0; i < turns; i++) (x, y) = (max - y, x);
            return (x, y);
        }

        static void Carve(DungeonTileMap map, Vec2Int a, Vec2Int b)
        {
            var c = DoorwayCentre(a, b);
            var horizontal = a.Y == b.Y;
            for (var w = -DoorHalfWidth; w <= DoorHalfWidth; w++)
            for (var d = -CorridorDepth; d < CorridorDepth; d++)
            {
                var tile = horizontal ? new Vec2Int(c.X + d, c.Y + w) : new Vec2Int(c.X + w, c.Y + d);
                map.Set(tile.X, tile.Y, Floor);
            }
            // Join each corridor end to its room's centre, so even a template with an interior wall ring stays open.
            CarveLine(map, c, RoomCentre(a));
            CarveLine(map, c, RoomCentre(b));
        }

        static void CarveLine(DungeonTileMap map, Vec2Int from, Vec2Int to)
        {
            // Stop at the first floor tile already connected to the room centre's open area.
            var x = from.X;
            var y = from.Y;
            while (x != to.X || y != to.Y)
            {
                if (x != to.X) x += Math.Sign(to.X - x);
                else y += Math.Sign(to.Y - y);
                if (map.At(x, y) == Wall) map.Set(x, y, Floor);
            }
        }

        /// <summary>Built-in templates — used by tests and whenever the JSON set has none for a kind.</summary>
        public static IReadOnlyList<RoomTemplate> FallbackTemplates()
        {
            string[] Room(Func<int, int, char> inner)
            {
                var rows = new string[RoomTiles];
                for (var r = 0; r < RoomTiles; r++)
                {
                    var chars = new char[RoomTiles];
                    for (var c = 0; c < RoomTiles; c++)
                        chars[c] = r == 0 || c == 0 || r == RoomTiles - 1 || c == RoomTiles - 1 ? Wall : inner(c, r);
                    rows[r] = new string(chars);
                }
                return rows;
            }
            return new[]
            {
                new RoomTemplate { Id = "fallback_plain", Tags = new[] { "combat", "entrance", "stairs", "boss", "rest", "key", "vault" }, Rows = Room((c, r) => (c, r) == (10, 10) ? 'X' : (c is 5 or 14) && (r is 5 or 14) ? 'S' : Floor) },
            };
        }
    }
}
