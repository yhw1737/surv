using System;

namespace Isle.Core
{
    /// <summary>
    /// Integer 2D coordinate with no Unity dependency (Absolute rule: `Data` and other non-Unity
    /// layers must not reference `UnityEngine.Vector2Int`). Used for tile and chunk coordinates.
    /// </summary>
    public readonly struct Vec2Int : IEquatable<Vec2Int>
    {
        public readonly int X;
        public readonly int Y;

        public Vec2Int(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(Vec2Int other) => X == other.X && Y == other.Y;

        public override bool Equals(object obj) => obj is Vec2Int other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 397) ^ Y;
            }
        }

        public static bool operator ==(Vec2Int a, Vec2Int b) => a.Equals(b);

        public static bool operator !=(Vec2Int a, Vec2Int b) => !a.Equals(b);

        public static Vec2Int operator +(Vec2Int a, Vec2Int b) => new(a.X + b.X, a.Y + b.Y);

        public override string ToString() => $"({X}, {Y})";
    }
}
