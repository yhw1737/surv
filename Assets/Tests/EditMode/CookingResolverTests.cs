using System.Linq;
using Isle.Core.Ids;
using Isle.Data;
using Isle.Gameplay.Cooking;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-COOK-01 §Verification, cases 1–4, 6, 7 and 9 (5 and 8 are in DishFactoryTests and
    /// SatietyTrackerTests). Method numbers are the spec's own table.</summary>
    public sealed class CookingResolverTests
    {
        const float Tolerance = 0.001f;

        static readonly NamespacedId Steady = NamespacedId.Parse("isle:steady_hand");
        static readonly NamespacedId Cold = NamespacedId.Parse("isle:cold_resist");
        static readonly NamespacedId Poison = NamespacedId.Parse("isle:food_poisoning");
        static readonly NamespacedId Endurance = NamespacedId.Parse("isle:endurance");
        static readonly NamespacedId Hydrated = NamespacedId.Parse("isle:hydrated");

        static ItemDef Food(string id, float hunger, float thirst, params string[] tags) => new()
        {
            Id = NamespacedId.Parse(id),
            Tags = tags,
            Nutrition = new NutritionSpec { Hunger = hunger, Thirst = thirst },
        };

        static CookMethodDef Method(string id, float hunger, float thirst, float preservation, params TagReaction[] reactions) => new()
        {
            Id = NamespacedId.Parse(id),
            Input = new CookInput { MinItems = 1, MaxItems = 5 },
            Modifiers = new CookModifiers { Hunger = hunger, Thirst = thirst, Preservation = preservation, BuffDuration = 1f },
            TagReactions = reactions,
        };

        static TagReaction Grant(NamespacedId buff, float power, params string[] when) => new() { When = when, GrantBuff = buff, Power = power };

        static readonly CookMethodDef Grill = Method("isle:grill", 1.30f, 0.70f, 0.60f,
            Grant(Steady, 1.0f, "fish"), Grant(Endurance, 0.8f, "meat"), Grant(Poison, 2.0f, "spoiled"));
        static readonly CookMethodDef Boil = Method("isle:boil", 1.00f, 1.80f, 0.50f);
        // Stew's two-buff rule is def data (max_buffs 2 from 3 groups), not a hardcoded method id.
        static readonly CookMethodDef Stew = new()
        {
            Id = NamespacedId.Parse("isle:stew"),
            Input = new CookInput { MinItems = 3, MaxItems = 5 },
            Modifiers = new CookModifiers { Hunger = 1.35f, Thirst = 1.30f, Preservation = 0.80f, BuffDuration = 1.4f },
            TagReactions = new[] { Grant(Endurance, 1.0f, "meat"), Grant(Hydrated, 0.9f, "vegetable"), Grant(Steady, 0.8f, "grain") },
            MaxBuffs = 2,
            MaxBuffsMinGroups = 3,
        };

        [Test]
        public void Resolve_Case1_RawMeatGrill_ScalesHungerAndThirstAtMostOneBuff()
        {
            var result = CookingResolver.Resolve(Grill, new[] { Food("isle:raw_meat", 10f, 2f, "meat", "raw") }, cookingLevel: 1, failureRoll: 1f);
            Assert.AreEqual(13f, result.Hunger, Tolerance);
            Assert.AreEqual(1.4f, result.Thirst, Tolerance);
            Assert.LessOrEqual(result.Buffs.Count, 1);
        }

        [Test]
        public void Resolve_Case2_OilyFishBoil_ThirstX1_8NoColdResist()
        {
            var result = CookingResolver.Resolve(Boil, new[] { Food("isle:mackerel", 8f, 4f, "fish", "oily") }, 1, 1f);
            Assert.AreEqual(7.2f, result.Thirst, Tolerance);
            Assert.IsFalse(result.Buffs.Any(b => b.Buff == Cold));
        }

        [Test]
        public void Resolve_Case3_StewThreeGroups_TwoBuffs()
        {
            var ingredients = new[] { Food("isle:raw_meat", 10, 0, "meat"), Food("isle:carrot", 3, 2, "vegetable"), Food("isle:barley", 4, 0, "grain") };
            Assert.AreEqual(2, CookingResolver.Resolve(Stew, ingredients, 1, 1f).Buffs.Count);
        }

        [Test]
        public void Resolve_Case4_StewTwoGroups_OneBuff()
        {
            var ingredients = new[] { Food("isle:raw_meat", 10, 0, "meat"), Food("isle:carrot", 3, 2, "vegetable") };
            var result = CookingResolver.Resolve(Stew, ingredients, 1, 1f);
            Assert.AreEqual(1, result.Buffs.Count);
            Assert.AreEqual(Endurance, result.Buffs[0].Buff, "excess drops by lowest power");
        }

        [Test]
        public void Resolve_Case6_Lv40_CareTagAndDurationX1_5()
        {
            var result = CookingResolver.Resolve(Grill, new[] { Food("isle:raw_meat", 10, 0, "meat") }, 40, 1f);
            Assert.IsTrue(result.CareTag);
            Assert.AreEqual(1.5f, result.BuffDurationMult, Tolerance);
        }

        [Test]
        public void Resolve_Case7_Lv39_NoCareTag()
        {
            var result = CookingResolver.Resolve(Grill, new[] { Food("isle:raw_meat", 10, 0, "meat") }, 39, 1f);
            Assert.IsFalse(result.CareTag);
            Assert.AreEqual(1f, result.BuffDurationMult, Tolerance);
        }

        [Test]
        public void Resolve_Case9_SpoiledIngredient_FoodPoisoning()
        {
            var result = CookingResolver.Resolve(Grill, new[] { Food("isle:rotten_food", 2, 0, "food", "spoiled") }, 1, 1f);
            Assert.IsTrue(result.Buffs.Any(b => b.Buff == Poison));
        }

        [Test]
        public void Resolve_TagHierarchy_ChildTagMatchesParentReaction()
        {
            var result = CookingResolver.Resolve(Grill, new[] { Food("isle:sardine", 4, 0, "fish/saltwater") }, 1, 1f);
            Assert.IsTrue(result.Buffs.Any(b => b.Buff == Steady));
        }

        [Test]
        public void Resolve_FailureRollBelowRate_Fails()
        {
            var method = Method("isle:grill", 1.3f, 0.7f, 0.6f);
            var failing = new CookMethodDef { Id = method.Id, Input = method.Input, Modifiers = method.Modifiers, Failure = new CookFailure { BaseRate = 0.2f, SkillReduction = 0.003f } };
            Assert.IsTrue(CookingResolver.Resolve(failing, new[] { Food("isle:raw_meat", 10, 0, "meat") }, 10, failureRoll: 0.1f).Failed);
            Assert.IsFalse(CookingResolver.Resolve(failing, new[] { Food("isle:raw_meat", 10, 0, "meat") }, 10, failureRoll: 0.2f).Failed);
        }

        [Test]
        public void CanCook_CountOutsideMinMax_False()
        {
            var method = new CookMethodDef { Input = new CookInput { MinItems = 1, MaxItems = 2 } };
            Assert.IsFalse(CookingResolver.CountAllowed(method, 0));
            Assert.IsTrue(CookingResolver.CountAllowed(method, 2));
            Assert.IsFalse(CookingResolver.CountAllowed(method, 3));
        }
    }
}
