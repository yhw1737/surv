using Isle.Gameplay.Hunting;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-HUNT-01 §Spoilage and scent, §Carrying: a carcass spoils over a day (faster in heat), its yield
    /// drops and then rots, it draws predators from a scent radius, and its weight decides how it's moved.</summary>
    public sealed class CarcassTests
    {
        [Test]
        public void Spoilage_ADayAt15C_IsAboutOne()
        {
            var s = CarcassCalculator.Spoil(0f, minutes: 24 * 60, ambientTemp: 15f);
            Assert.AreEqual(1f, s, 0.01f);
        }

        [Test]
        public void Spoilage_TwiceAsFastAt30C()
        {
            Assert.AreEqual(2f, CarcassCalculator.TempFactor(30f), 1e-5f);
            Assert.AreEqual(1f, CarcassCalculator.TempFactor(5f), 1e-5f, "cold doesn't slow it below the base rate");
            Assert.AreEqual(2f * CarcassCalculator.Spoil(0f, 60, 15f), CarcassCalculator.Spoil(0f, 60, 30f), 1e-5f);
        }

        [Test]
        public void Yield_FallsPastHalf_RotsPast08()
        {
            Assert.AreEqual(1f, CarcassCalculator.YieldMult(0.49f), 1e-5f);
            Assert.AreEqual(0.6f, CarcassCalculator.YieldMult(0.5f), 1e-5f);
            Assert.IsFalse(CarcassCalculator.IsRotten(0.79f));
            Assert.IsTrue(CarcassCalculator.IsRotten(0.8f));
        }

        [Test]
        public void Scent_62kgBoar_Is13_4Tiles() =>
            Assert.AreEqual(13.44f, CarcassCalculator.ScentRadiusTiles(62f), 0.01f);

        [Test]
        public void Carry_ByWeight()
        {
            Assert.AreEqual(CarryClass.Small, CarcassCalculator.ClassFor(4.9f));
            Assert.AreEqual(CarryClass.Medium, CarcassCalculator.ClassFor(5f));
            Assert.AreEqual(CarryClass.Medium, CarcassCalculator.ClassFor(15f));
            Assert.AreEqual(CarryClass.WorldOnly, CarcassCalculator.ClassFor(15.1f));
        }

        [Test]
        public void Haul_Speeds_SoloDragAndCoopCarry()
        {
            Assert.AreEqual(0.4f, CarcassCalculator.DragSpeedMult, 1e-5f);
            Assert.AreEqual(0.8f, CarcassCalculator.CoopSpeedMult, 1e-5f);
            Assert.AreEqual(2, CarcassCalculator.CoopMinPlayers);
        }
    
        // --- Content -----------------------------------------------------------------------------------------------

        static void Load()
        {
            Isle.Modding.Defs.DefRegistry.Clear();
            Assert.IsEmpty(Isle.Modding.Defs.DefinitionBootstrap.Load(System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, "definitions")));
        }

        static int Count(System.Collections.Generic.List<(Isle.Core.Ids.NamespacedId Item, int Count)> cuts, string id) =>
            System.Linq.Enumerable.Sum(System.Linq.Enumerable.Where(cuts, c => c.Item.Value == "isle:" + id), c => c.Count);

        [Test]
        public void Cuts_GoingOff_GiveLess_Rotten_GivesOnlyRottenMeat()
        {
            Load();
            try
            {
                var deer = Isle.Modding.Defs.DefRegistry.Get<Isle.Data.CreatureDef>(Isle.Core.Ids.NamespacedId.Parse("isle:deer")).Butcher;
                var fresh = CreatureDirector.Cuts(deer, 50f, 1f, 30, 1f, 0.95f, spoilage: 0f);
                var going = CreatureDirector.Cuts(deer, 50f, 1f, 30, 1f, 0.95f, spoilage: 0.6f);
                var rotten = CreatureDirector.Cuts(deer, 50f, 1f, 30, 1f, 0.95f, spoilage: 0.9f);
                Assert.Less(Count(going, "raw_meat"), Count(fresh, "raw_meat"));
                Assert.AreEqual(0, Count(rotten, "raw_meat"));
                Assert.AreEqual(0, Count(rotten, "hide"));
                Assert.Greater(Count(rotten, "rotten_food"), 0);
            }
            finally { Isle.Modding.Defs.DefRegistry.Clear(); }
        }

        [Test]
        public void CarcassItems_OnlyForCarryableAnimals_SizedByWeight()
        {
            Load();
            try
            {
                Isle.Data.ItemDef Item(string c) =>
                    Isle.Modding.Defs.DefRegistry.TryGet<Isle.Data.ItemDef>(Isle.Modding.Defs.CarcassItems.IdFor(Isle.Core.Ids.NamespacedId.Parse("isle:" + c)), out var i) ? i : null;
                Assert.AreEqual(2, Item("rabbit").Grid.H, "rabbit: 1×2");
                Assert.AreEqual(1, Item("rabbit").Grid.W);
                Assert.AreEqual(3, Item("fox").Grid.H, "fox: 2×3");
                Assert.IsNull(Item("boar"), "a boar is never carried in a bag");
                Assert.IsNull(Item("deer"));
            }
            finally { Isle.Modding.Defs.DefRegistry.Clear(); }
        }
    }
}
