using Isle.Core.Ids;
using Isle.Data;
using Isle.Gameplay.Crafting;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SCHEMA §Craft recipes: ingredients match by item id or by tag, and one stock unit
    /// can only satisfy one requirement.</summary>
    public sealed class CraftingCalculatorTests
    {
        static readonly NamespacedId Wood = NamespacedId.Parse("isle:wood");
        static readonly NamespacedId Stone = NamespacedId.Parse("isle:stone");
        static readonly NamespacedId Oak = NamespacedId.Parse("isle:oak_log");

        static IngredientRef ByItem(NamespacedId item, int count) => new IngredientRef { Item = item, Count = count };
        static IngredientRef ByTag(string tag, int count) => new IngredientRef { Tag = tag, Count = count };

        [Test]
        public void HasIngredients_EmptyRequirements_ReturnsTrue()
        {
            Assert.IsTrue(CraftingCalculator.HasIngredients(new IngredientRef[0], new Stock[0]));
        }

        [Test]
        public void HasIngredients_TagSumsAcrossStacks_ReturnsTrue()
        {
            var stock = new[] { new Stock(Wood, new[] { "wood" }, 1), new Stock(Oak, new[] { "wood" }, 1) };
            Assert.IsTrue(CraftingCalculator.HasIngredients(new[] { ByTag("wood", 2) }, stock));
        }

        [Test]
        public void HasIngredients_OneShort_ReturnsFalse()
        {
            var stock = new[] { new Stock(Wood, new[] { "wood" }, 2) };
            Assert.IsFalse(CraftingCalculator.HasIngredients(new[] { ByItem(Wood, 3) }, stock));
        }

        [Test]
        public void HasIngredients_MissingOneRequirement_ReturnsFalse()
        {
            var stock = new[] { new Stock(Wood, new[] { "wood" }, 2) };
            Assert.IsFalse(CraftingCalculator.HasIngredients(new[] { ByTag("wood", 2), ByItem(Stone, 1) }, stock));
        }

        [Test]
        public void HasIngredients_OneUnitCannotCountTwice_ReturnsFalse()
        {
            // One wood satisfies "item isle:wood" OR "tag wood", not both.
            var stock = new[] { new Stock(Wood, new[] { "wood" }, 1) };
            Assert.IsFalse(CraftingCalculator.HasIngredients(new[] { ByItem(Wood, 1), ByTag("wood", 1) }, stock));
        }
    }
}
