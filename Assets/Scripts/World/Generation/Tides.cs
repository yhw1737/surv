using System;
using System.Collections.Generic;

namespace Isle.World.Generation
{
    /// <summary>
    /// SYS-DUNG-01 §Tidal Grotto: the water level follows the in-game clock, and the same rooms of a floor flood at
    /// every high tide. Static pure, seed-deterministic.
    /// </summary>
    public static class Tides
    {
        /// <summary>−1 (lowest) … 1 (a high-tide peak at <paramref name="highAtHour"/>, then every <paramref name="cycleHours"/>).</summary>
        public static float Level(float hourOfDay, float cycleHours, float highAtHour) =>
            (float)Math.Cos(2.0 * Math.PI * (hourOfDay - highAtHour) / cycleHours);

        /// <summary>The water is up for the half of the cycle around each peak.</summary>
        public static bool IsHigh(float hourOfDay, float cycleHours, float highAtHour) => Level(hourOfDay, cycleHours, highAtHour) >= 0f;

        /// <summary>Rooms that flood at high tide: <paramref name="share"/> of the rooms that may (never the entrance, rest
        /// or boss room — there is always somewhere dry to stand).</summary>
        public static HashSet<int> FloodedRooms(DungeonFloor floor, float share, int seed) => PickRooms(floor, share, seed, 7331);

        /// <summary>A seed-fixed ⌊eligible × share⌋ of a floor's rooms, never the entrance, rest or boss room. Each
        /// mechanic passes its own <paramref name="salt"/> so they pick independently (tides 7331, miasma 7349).</summary>
        public static HashSet<int> PickRooms(DungeonFloor floor, float share, int seed, int salt)
        {
            var eligible = new List<int>();
            foreach (var room in floor.Rooms)
                if (room.Kind is not (RoomKind.Entrance or RoomKind.Rest or RoomKind.Boss)) eligible.Add(room.Index);
            var rng = new Random(unchecked((int)IslandGenerator.Hash(seed, salt, floor.Rooms.Count)));
            for (var i = eligible.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (eligible[i], eligible[j]) = (eligible[j], eligible[i]);
            }
            var count = (int)Math.Floor(eligible.Count * share);
            return new HashSet<int>(eligible.GetRange(0, Math.Min(count, eligible.Count)));
        }

        /// <summary>The flooded room holding the floor's low-tide cache; -1 when nothing floods.</summary>
        public static int CacheRoom(HashSet<int> flooded, int seed)
        {
            if (flooded.Count == 0) return -1;
            var sorted = new List<int>(flooded);
            sorted.Sort();
            return sorted[(int)(IslandGenerator.Hash(seed, 7333, sorted.Count) % (uint)sorted.Count)];
        }
    }
}
