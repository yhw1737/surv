using System.Collections.Generic;
using Isle.Gameplay.Building;
using NUnit.Framework;
using UnityEngine;

namespace Isle.Tests.EditMode
{
    /// <summary>Placement rules for structures, and the rain catcher's fill (SYS-SURV-01 §Water sources: rain catcher).</summary>
    public sealed class BuildingCalculatorTests
    {
        static bool Everywhere(Vector2 _) => true;
        static readonly List<Vector2> None = new();

        [Test]
        public void CanPlace_InReachOnLandClear_ReturnsTrue()
        {
            Assert.IsTrue(BuildingCalculator.CanPlace(Vector2.zero, new Vector2(1.5f, 0f), Everywhere, None));
        }

        [Test]
        public void CanPlace_BeyondReach_ReturnsFalse()
        {
            Assert.IsFalse(BuildingCalculator.CanPlace(Vector2.zero, new Vector2(2.5f, 0f), Everywhere, None));
        }

        [Test]
        public void CanPlace_OnWater_ReturnsFalse()
        {
            Assert.IsFalse(BuildingCalculator.CanPlace(Vector2.zero, Vector2.right, _ => false, None));
        }

        [Test]
        public void CanPlace_TooCloseToAnotherStructure_ReturnsFalse()
        {
            Assert.IsFalse(BuildingCalculator.CanPlace(Vector2.zero, Vector2.right, Everywhere, new List<Vector2> { new(1.5f, 0f) }));
        }

        [Test]
        public void CanPlace_OnThePlayer_ReturnsFalse()
        {
            Assert.IsFalse(BuildingCalculator.CanPlace(Vector2.zero, new Vector2(0.2f, 0f), Everywhere, None));
        }

        [Test]
        public void Fill_Raining_AddsRateTimesSecondsClampedToCapacity()
        {
            Assert.AreEqual(1.5f, BuildingCalculator.Fill(1f, capacity: 4f, perSecond: 0.1f, seconds: 5f, raining: true), 1e-4f);
            Assert.AreEqual(4f, BuildingCalculator.Fill(3.9f, capacity: 4f, perSecond: 0.1f, seconds: 5f, raining: true), 1e-4f);
        }

        [Test]
        public void Fill_Dry_Unchanged()
        {
            Assert.AreEqual(1f, BuildingCalculator.Fill(1f, capacity: 4f, perSecond: 0.1f, seconds: 5f, raining: false), 1e-4f);
        }
    }
}
