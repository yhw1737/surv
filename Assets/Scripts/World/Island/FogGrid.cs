using System;
using System.Collections.Generic;
using Isle.Core;

namespace Isle.World.Island
{
    /// <summary>
    /// SYS-MAP-01 §Fog of war: which parts of the map a player has seen, at a coarse cell resolution. Reveals are
    /// permanent. Pure, no Unity refs; the presentation layer paints a texture from <see cref="Reveal"/>'s result.
    /// </summary>
    public sealed class FogGrid
    {
        readonly bool[] _explored;

        public FogGrid(int sizeTiles, int cellTiles)
        {
            CellTiles = cellTiles;
            Cells = (sizeTiles + cellTiles - 1) / cellTiles;
            _explored = new bool[Cells * Cells];
        }

        public int CellTiles { get; }
        public int Cells { get; }

        public bool IsExplored(Vec2Int tile) => IsCellExplored(tile.X / CellTiles, tile.Y / CellTiles);

        public bool IsCellExplored(int cx, int cy) => cx >= 0 && cy >= 0 && cx < Cells && cy < Cells && _explored[cy * Cells + cx];

        /// <summary>Explores every cell whose centre is within <paramref name="radiusTiles"/> of <paramref name="tile"/>;
        /// returns the cells that were new, for the texture to repaint.</summary>
        public List<(int X, int Y)> Reveal(Vec2Int tile, float radiusTiles)
        {
            var changed = new List<(int, int)>();
            var reach = (int)Math.Ceiling(radiusTiles / CellTiles) + 1;
            var ccx = tile.X / CellTiles;
            var ccy = tile.Y / CellTiles;
            for (var cy = ccy - reach; cy <= ccy + reach; cy++)
            for (var cx = ccx - reach; cx <= ccx + reach; cx++)
            {
                if (cx < 0 || cy < 0 || cx >= Cells || cy >= Cells || _explored[cy * Cells + cx]) continue;
                var dx = (cx + 0.5f) * CellTiles - tile.X;
                var dy = (cy + 0.5f) * CellTiles - tile.Y;
                if (dx * dx + dy * dy > radiusTiles * radiusTiles) continue;
                _explored[cy * Cells + cx] = true;
                changed.Add((cx, cy));
            }
            return changed;
        }

        /// <summary>Bit-packed, base64 — about 55 KB for a 576×576 grid.</summary>
        public string Serialize()
        {
            var bytes = new byte[(_explored.Length + 7) / 8];
            for (var i = 0; i < _explored.Length; i++)
                if (_explored[i]) bytes[i / 8] |= (byte)(1 << (i % 8));
            return Convert.ToBase64String(bytes);
        }

        /// <summary>Loads a saved grid; unreadable text leaves everything hidden.</summary>
        public void Deserialize(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            byte[] bytes;
            try { bytes = Convert.FromBase64String(text); }
            catch (FormatException) { return; }
            // A save from a grid half as fine (4-tile cells before 2026-10-06): each old cell covers 2×2 new ones.
            var half = Cells / 2;
            if (Cells % 2 == 0 && bytes.Length == (half * half + 7) / 8 && bytes.Length * 8 < _explored.Length)
            {
                for (var y = 0; y < Cells; y++)
                for (var x = 0; x < Cells; x++)
                {
                    var old = (y / 2) * half + x / 2;
                    _explored[y * Cells + x] = (bytes[old / 8] & (1 << (old % 8))) != 0;
                }
                return;
            }
            for (var i = 0; i < _explored.Length && i / 8 < bytes.Length; i++)
                _explored[i] = (bytes[i / 8] & (1 << (i % 8))) != 0;
        }
    }
}
