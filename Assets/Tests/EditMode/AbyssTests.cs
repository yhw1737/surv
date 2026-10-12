using System.Linq;
using Isle.Core.Ids;
using Isle.Data;
using Isle.Modding.Defs;
using Isle.World.Generation;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>T-204 the Abyss: SYS-DUNG-01's decided curse and pressure values, and the dungeon's data.</summary>
    public sealed class AbyssTests
    {
        [TestCase(4, 0, 4)]
        [TestCase(4, 3, 1)]
        [TestCase(4, 4, 0)]
        [TestCase(4, 6, 0)]
        public void Missing_IsRequiredMinusPlaced(int required, int placed, int missing) =>
            Assert.AreEqual(missing, AbyssCalculator.Missing(placed, required));

        [Test]
        public void AllFourMissing_DoublesEnemies_Leaves40PercentLight()
        {
            Assert.AreEqual(2f, AbyssCalculator.EnemyMult(4, 0.25f), 1e-5f);
            Assert.AreEqual(0.4f, AbyssCalculator.LightMult(4, 0.15f), 1e-5f);
        }

        [Test]
        public void NoneMissing_NoCurse()
        {
            Assert.AreEqual(1f, AbyssCalculator.EnemyMult(0, 0.25f));
            Assert.AreEqual(1f, AbyssCalculator.LightMult(0, 0.15f));
        }

        [TestCase(0, 0.85f)]
        [TestCase(1, 0.70f)]
        [TestCase(2, 0.55f)]
        public void Pressure_15PercentPerFloor(int floorIndex, float mult) =>
            Assert.AreEqual(mult, AbyssCalculator.StaminaRegenMult(floorIndex, 0.15f), 1e-5f);

        [Test]
        public void CursedCount_RoundsByTheRoll()
        {
            Assert.AreEqual(2, AbyssCalculator.CursedCount(1, 2f, 0.9));
            Assert.AreEqual(1, AbyssCalculator.CursedCount(1, 1.25f, 0.5));
            Assert.AreEqual(2, AbyssCalculator.CursedCount(1, 1.25f, 0.1));
            Assert.AreEqual(4, AbyssCalculator.CursedCount(3, 1.5f, 0.6));
            Assert.AreEqual(5, AbyssCalculator.CursedCount(3, 1.5f, 0.4));
        }

        [Test]
        public void AbyssData_SealsWithTheFourBossSigils_UnderTheRuin()
        {
            DefRegistry.Clear();
            try
            {
                Assert.IsEmpty(DefinitionBootstrap.Load(System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, "definitions")));
                var abyss = DefRegistry.Get<DungeonDef>(NamespacedId.Parse("isle:abyss"));
                Assert.AreEqual(3, abyss.Floors);
                Assert.AreEqual("isle:clockwork_ruin", abyss.Entrance.Near.Value);
                CollectionAssert.AreEquivalent(new[] { "isle:tide_sigil", "isle:root_sigil", "isle:mire_sigil", "isle:ruin_sigil" }, abyss.Seal.Sigils.Select(s => s.Value));
                // Each sigil is what one of the other dungeons' bosses drops.
                foreach (var sigil in abyss.Seal.Sigils)
                    Assert.IsTrue(DefRegistry.All<CreatureDef>().Any(c => c.Boss?.Drops?.Any(d => d.Item == sigil) == true), sigil.Value);
                Assert.AreEqual(0.25f, abyss.Seal.EnemiesPerMissing);
                Assert.AreEqual(0.15f, abyss.Seal.LightPerMissing);
                Assert.AreEqual(0.15f, abyss.Pressure.StaminaRegenPerFloor);
            }
            finally { DefRegistry.Clear(); }
        }
    }
}
