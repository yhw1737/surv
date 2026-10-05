using Isle.Core.Ids;
using Isle.Data;
using Isle.Gameplay.Cooking;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>A cook's result as an item: SYS-COOK-01 verification 5 (dry: preservation ×4.0, grid 1×1), naming,
    /// and the same combination always being the same item so dishes stack.</summary>
    public sealed class DishFactoryTests
    {
        static readonly ItemDef Meat = new()
        {
            Id = NamespacedId.Parse("isle:raw_meat"), Name = "@item.raw_meat", Tags = new[] { "food", "meat", "raw" },
            Grid = new GridSize { W = 2, H = 2 }, Weight = 0.5f,
            Nutrition = new NutritionSpec { Hunger = 6f },
            Spoilage = new SpoilageSpec { BaseHours = 24f, Result = NamespacedId.Parse("isle:rotten_food") },
        };

        static readonly CookMethodDef Dry = new()
        {
            Id = NamespacedId.Parse("isle:dry"), Naming = "@pattern.dried",
            Input = new CookInput { MinItems = 1, MaxItems = 4 },
            Modifiers = new CookModifiers { Hunger = 0.7f, Thirst = 0.3f, Preservation = 4.0f },
            ResultGrid = new GridSize { W = 1, H = 1 },
        };

        [SetUp]
        public void SetUp() => DishFactory.Clear();

        [Test]
        public void Create_Case5_RawMeatDry_PreservationX4AndGrid1x1()
        {
            var result = CookingResolver.Resolve(Dry, new[] { Meat }, 1, 1f);
            Assert.AreEqual(4.0f, result.Preservation, 1e-4f);
            var dish = DishFactory.Create(Dry, new[] { Meat }, result);
            Assert.AreEqual(1, dish.Grid.W);
            Assert.AreEqual(1, dish.Grid.H);
        }

        [Test]
        public void Create_Name_IsPatternPlusMainIngredient()
        {
            var dish = DishFactory.Create(Dry, new[] { Meat }, CookingResolver.Resolve(Dry, new[] { Meat }, 1, 1f));
            Assert.AreEqual("@pattern.dried|@item.raw_meat", dish.Name);
        }

        static readonly ItemDef Berries = new()
        {
            Id = NamespacedId.Parse("isle:berries"), Name = "@item.berries", Tags = new[] { "food", "fruit" },
            Grid = new GridSize { W = 1, H = 1 }, Weight = 0.1f, Nutrition = new NutritionSpec { Hunger = 8f },
        };

        [Test]
        public void Create_Mixed_MainIsHeaviestOthersListed()
        {
            // Berries carry more hunger than the meat but the meat is the dish: main ingredient = heaviest.
            var dish = DishFactory.Create(Dry, new[] { Berries, Meat }, CookingResolver.Resolve(Dry, new[] { Berries, Meat }, 1, 1f));
            Assert.AreEqual("@pattern.dried_with|@item.raw_meat|@item.berries", dish.Name);
        }

        [Test]
        public void IngredientsOf_ListsWhatWentIn()
        {
            var dish = DishFactory.Create(Dry, new[] { Berries, Meat }, CookingResolver.Resolve(Dry, new[] { Berries, Meat }, 1, 1f));
            CollectionAssert.AreEquivalent(new[] { "isle:berries", "isle:raw_meat" }, DishFactory.IngredientsOf(dish));
        }

        [Test]
        public void Create_Spoilage_ScalesBaseHoursByPreservationOverRaw()
        {
            var dish = DishFactory.Create(Dry, new[] { Meat }, CookingResolver.Resolve(Dry, new[] { Meat }, 1, 1f));
            Assert.AreEqual(24f * 4.0f / DishFactory.RawPreservation, dish.Spoilage.BaseHours, 1e-3f);
            Assert.IsFalse(System.Array.Exists(dish.Tags, t => t == "raw"), "a cooked dish isn't raw");
        }

        [Test]
        public void Create_SameCombination_ReturnsSameItem()
        {
            var a = DishFactory.Create(Dry, new[] { Meat }, CookingResolver.Resolve(Dry, new[] { Meat }, 1, 1f));
            var b = DishFactory.Create(Dry, new[] { Meat }, CookingResolver.Resolve(Dry, new[] { Meat }, 1, 1f));
            Assert.AreSame(a, b);
            Assert.IsTrue(a.Id.IsValid);
        }

        [Test]
        public void KeyOf_RoundTripsThroughRebuild()
        {
            var dish = DishFactory.Create(Dry, new[] { Meat }, CookingResolver.Resolve(Dry, new[] { Meat }, 1, 1f));
            var key = DishFactory.KeyOf(dish);
            DishFactory.Clear();
            var rebuilt = DishFactory.Rebuild(key, id => id == Dry.Id ? Dry : null, id => id == Meat.Id ? Meat : null);
            Assert.AreEqual(dish.Id, rebuilt.Id);
            Assert.AreEqual(dish.Nutrition.Hunger, rebuilt.Nutrition.Hunger, 1e-4f);
        }
    }
}
