using System.Collections.Generic;
using System.Linq;
using Isle.Data;
using Isle.Gameplay.Combat;
using Isle.World.Generation;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-DUNG-01 §Tidal Grotto (T-202): tides follow the clock twice a day, about half the rooms flood
    /// (never the entrance, rest or boss room), one low-tide cache per floor, and the Hermit Colossus's shell phase.</summary>
    public sealed class TidalGrottoTests
    {
        // --- Tides -------------------------------------------------------------------------------------------------

        [Test]
        public void Tide_IsHighAroundSixAndEighteen_LowAroundMidnightAndNoon()
        {
            Assert.IsTrue(Tides.IsHigh(6f, 12f, 6f));
            Assert.IsTrue(Tides.IsHigh(18f, 12f, 6f));
            Assert.IsFalse(Tides.IsHigh(0f, 12f, 6f));
            Assert.IsFalse(Tides.IsHigh(12f, 12f, 6f));
        }

        [Test]
        public void Tide_FloodsTwicePerDay()
        {
            var rises = 0;
            var was = Tides.IsHigh(0f, 12f, 6f);
            for (var minute = 1; minute <= 24 * 60; minute++)
            {
                var now = Tides.IsHigh(minute / 60f, 12f, 6f);
                if (now && !was) rises++;
                was = now;
            }
            Assert.AreEqual(2, rises);
        }

        // --- Flooded rooms -----------------------------------------------------------------------------------------

        static IEnumerable<int> Seeds(int n) => Enumerable.Range(1, n).Select(i => i * 104729 + 7);

        [Test]
        public void Flooding_HalfTheRooms_NeverEntranceRestOrBoss()
        {
            foreach (var seed in Seeds(300))
            {
                var floor = DungeonGenerator.Generate(seed, 0, 1, 1);
                var flooded = Tides.FloodedRooms(floor, 0.5f, seed);
                var eligible = floor.Rooms.Count(r => r.Kind is not (RoomKind.Entrance or RoomKind.Rest or RoomKind.Boss));
                Assert.AreEqual(eligible / 2, flooded.Count, $"seed {seed}");
                foreach (var room in flooded)
                    Assert.IsFalse(floor.Rooms[room].Kind is RoomKind.Entrance or RoomKind.Rest or RoomKind.Boss, $"seed {seed}: room {room} is {floor.Rooms[room].Kind}");
            }
        }

        [Test]
        public void Flooding_SameSeed_SameRooms()
        {
            var floor = DungeonGenerator.Generate(99, 0, 1, 1);
            CollectionAssert.AreEquivalent(Tides.FloodedRooms(floor, 0.5f, 99), Tides.FloodedRooms(floor, 0.5f, 99));
        }

        [Test]
        public void Cache_IsInAFloodedRoom()
        {
            foreach (var seed in Seeds(100))
            {
                var floor = DungeonGenerator.Generate(seed, 0, 1, 1);
                var flooded = Tides.FloodedRooms(floor, 0.5f, seed);
                Assert.IsTrue(flooded.Contains(Tides.CacheRoom(flooded, seed)), $"seed {seed}");
            }
        }

        // --- Boss shell --------------------------------------------------------------------------------------------

        static readonly BossShellSpec Shell = new() { BelowHealth = 0.5f, Seconds = 6f, EverySeconds = 15f, DamageMult = 0.2f };

        [Test]
        public void Shell_NeverAboveTheThreshold()
        {
            var shell = new BossShell();
            for (var t = 0f; t < 60f; t += 0.5f) shell.Tick(t, 0.8f, Shell);
            Assert.IsFalse(shell.IsHidden(60f));
            Assert.AreEqual(1f, shell.DamageMult(60f, Shell), 1e-6f);
        }

        [Test]
        public void Shell_HidesOnCrossing_ForItsSeconds_ThenAgainOnSchedule()
        {
            var shell = new BossShell();
            shell.Tick(10f, 0.45f, Shell);
            Assert.IsTrue(shell.IsHidden(10f));
            Assert.AreEqual(0.2f, shell.DamageMult(12f, Shell), 1e-6f);
            shell.Tick(16.5f, 0.45f, Shell);
            Assert.IsFalse(shell.IsHidden(16.5f), "out after 6 s");
            shell.Tick(24.9f, 0.45f, Shell);
            Assert.IsFalse(shell.IsHidden(24.9f));
            shell.Tick(25f, 0.45f, Shell);
            Assert.IsTrue(shell.IsHidden(25f), "hides again 15 s after the last one began");
        }

        [Test]
        public void Shell_StunBreaksIt()
        {
            var shell = new BossShell();
            shell.Tick(0f, 0.3f, Shell);
            Assert.IsTrue(shell.IsHidden(1f));
            shell.Break(2f);
            Assert.IsFalse(shell.IsHidden(2f));
            shell.Tick(3f, 0.3f, Shell);
            Assert.IsFalse(shell.IsHidden(3f), "a broken shell stays broken until the next scheduled hide");
        }
    }
}
