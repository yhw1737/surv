using System.Collections.Generic;
using Isle.Core;
using Isle.Core.Ids;

namespace Isle.World.Chunks
{
    /// <summary>
    /// SYS-WORLD-01 §Island generation. Fixed set, not moddable — same status as <see
    /// cref="Isle.World.Time.DayPhase"/>. IslandGenerator (T-035) is what actually paints these.
    /// </summary>
    public enum Biome
    {
        Coast,
        Forest,
        Marsh
    }

    /// <summary>
    /// One grid cell of a <see cref="Chunk"/>. SYS-WORLD-01 only names <c>Tile[] tiles</c> in the
    /// chunk layout, not a field list — this is the minimal placeholder IslandGenerator (T-035)
    /// will extend once terrain generation actually needs more than a biome.
    /// </summary>
    public struct Tile
    {
        public Biome Biome;
    }

    /// <summary>
    /// Anything placed in a chunk beyond its tiles (resource nodes, structures, dropped items).
    /// References a definition rather than embedding data (Absolute rule 1 — no hardcoded content).
    /// </summary>
    public sealed class WorldObject
    {
        public NamespacedId DefId;
        public Vec2Int LocalPosition;
    }

    /// <summary>
    /// SYS-WORLD-01 §Chunks. 32×32 tiles, plain C# (no MonoBehaviour, no FishNet) — <see
    /// cref="ChunkManager"/> owns the load/unload lifecycle, <see cref="ChunkSerializer"/> owns
    /// turning this into a SQLite row (JSON text, not BLOB — see ChunkSerializer for why).
    /// </summary>
    public sealed class Chunk
    {
        public const int Size = 32;

        public readonly Vec2Int Coord;
        public readonly Tile[] Tiles;
        public readonly List<WorldObject> Objects;

        /// <summary>In-game minute (<c>WorldClock.TotalMinutes</c>) this chunk was last simulated at.</summary>
        public long LastSimulatedTime;

        public Chunk(Vec2Int coord, Tile[] tiles = null, List<WorldObject> objects = null, long lastSimulatedTime = 0)
        {
            Coord = coord;
            Tiles = tiles ?? new Tile[Size * Size];
            Objects = objects ?? new List<WorldObject>();
            LastSimulatedTime = lastSimulatedTime;
        }

        /// <summary>Which chunk a tile position falls in. Floors toward negative infinity so the
        /// chunk grid is well-defined on both sides of the origin.</summary>
        public static Vec2Int CoordFromTilePosition(Vec2Int tilePosition) =>
            new(FloorDiv(tilePosition.X, Size), FloorDiv(tilePosition.Y, Size));

        static int FloorDiv(int a, int b) => (a >= 0 ? a : a - (b - 1)) / b;
    }
}
