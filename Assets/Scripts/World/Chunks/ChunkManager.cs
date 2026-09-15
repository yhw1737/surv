using System;
using System.Collections.Generic;
using Isle.Core;

namespace Isle.World.Chunks
{
    /// <summary>
    /// SYS-WORLD-01 §Chunks. Keeps the union of every player's 3×3 chunk neighborhood loaded and
    /// unloads (stamping <see cref="Chunk.LastSimulatedTime"/> and saving) everything else.
    /// Persistence is injected so this class stays free of SQLite — <c>ChunkSerializer</c> (T-033)
    /// supplies the real <paramref name="loadChunk"/>/<paramref name="saveChunk"/> in production.
    /// </summary>
    public sealed class ChunkManager
    {
        /// <summary>3×3 = center ± 1 chunk.</summary>
        public const int LoadRadius = 1;

        readonly Dictionary<Vec2Int, Chunk> _loaded = new();
        readonly Func<Vec2Int, Chunk> _loadChunk;
        readonly Action<Chunk> _saveChunk;

        public ChunkManager(Func<Vec2Int, Chunk> loadChunk, Action<Chunk> saveChunk)
        {
            _loadChunk = loadChunk ?? throw new ArgumentNullException(nameof(loadChunk));
            _saveChunk = saveChunk ?? throw new ArgumentNullException(nameof(saveChunk));
        }

        public IReadOnlyDictionary<Vec2Int, Chunk> LoadedChunks => _loaded;

        /// <summary>
        /// SYS-WORLD-01 verification #3. Recomputes the active set from current player chunk
        /// coordinates: loads anything newly in range, unloads anything that fell out of every
        /// player's 3×3. Chunks that stay in range are left untouched (not reloaded).
        /// </summary>
        public void UpdateActiveChunks(IReadOnlyList<Vec2Int> playerChunkCoords, long worldTime)
        {
            var wanted = new HashSet<Vec2Int>();
            foreach (var center in playerChunkCoords)
                for (var dx = -LoadRadius; dx <= LoadRadius; dx++)
                    for (var dy = -LoadRadius; dy <= LoadRadius; dy++)
                        wanted.Add(new Vec2Int(center.X + dx, center.Y + dy));

            foreach (var coord in wanted)
            {
                if (!_loaded.ContainsKey(coord))
                    _loaded[coord] = _loadChunk(coord);
            }

            List<Vec2Int> toUnload = null;
            foreach (var coord in _loaded.Keys)
            {
                if (!wanted.Contains(coord))
                    (toUnload ??= new List<Vec2Int>()).Add(coord);
            }

            if (toUnload == null) return;

            foreach (var coord in toUnload)
            {
                var chunk = _loaded[coord];
                chunk.LastSimulatedTime = worldTime;
                _saveChunk(chunk);
                _loaded.Remove(coord);
            }
        }
    }
}
