using System;
using System.Linq;
using System.Reflection;
using Isle.Core.Ids;
using Isle.Data;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>
    /// Architectural guards on the <c>Isle.Data</c> POCOs (T-011). There is no logic to test in a
    /// definition — the risk is that one of ARCHITECTURE's invariants quietly stops holding as
    /// types get added, so these check the shape of the assembly rather than any behaviour.
    /// </summary>
    public sealed class DataDefinitionTests
    {
        static readonly Assembly Data = typeof(ItemDef).Assembly;

        /// <summary>The eleven types documented in <c>docs/modding/SCHEMA.md</c> §Definition types.</summary>
        static readonly string[] SchemaTypes =
        {
            nameof(ItemDef), nameof(CreatureDef), nameof(FishDef), nameof(CookMethodDef),
            nameof(CraftRecipeDef), nameof(WeaponDef), nameof(ArtifactDef), nameof(EnchantDef),
            nameof(CropDef), nameof(SkillDef), nameof(BuffDef)
        };

        static Type[] Definitions() =>
            Data.GetTypes().Where(t => typeof(IDefinition).IsAssignableFrom(t) && !t.IsInterface).ToArray();

        [Test]
        public void IsleData_References_ExcludeUnity()
        {
            var unity = Data.GetReferencedAssemblies()
                .Select(a => a.Name)
                .Where(n => n.StartsWith("UnityEngine", StringComparison.Ordinal)
                         || n.StartsWith("UnityEditor", StringComparison.Ordinal))
                .ToArray();

            Assert.IsEmpty(unity,
                "Isle.Data must stay engine-free (ARCHITECTURE §Assembly definitions). Referenced: "
                + string.Join(", ", unity));
        }

        [Test]
        public void Definitions_MatchSchema_OneTypePerDocumentedKind()
        {
            var actual = Definitions().Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal);
            CollectionAssert.AreEqual(SchemaTypes.OrderBy(n => n, StringComparer.Ordinal), actual);
        }

        [Test]
        public void Definitions_AreSealed_AndParameterlessConstructible()
        {
            foreach (var type in Definitions())
            {
                Assert.IsTrue(type.IsSealed, $"{type.Name} must be sealed.");
                Assert.IsNotNull(type.GetConstructor(Type.EmptyTypes),
                    $"{type.Name} needs a public parameterless constructor for System.Text.Json (T-012).");
            }
        }

        /// <summary>
        /// A def is held by reference from every stack that uses it, so one runtime write would
        /// retune the whole world. ARCHITECTURE §Never: "Runtime mutation of definitions".
        /// </summary>
        [Test]
        public void DataProperties_AreInitOnly_NeverSettable()
        {
            var settable =
                from type in Data.GetTypes()
                where type.IsPublic && !type.IsEnum && !type.IsInterface
                from prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                let setter = prop.SetMethod
                where setter != null && !IsInitOnly(setter)
                select $"{type.Name}.{prop.Name}";

            Assert.IsEmpty(settable.ToArray(),
                "Definition properties must be init-only. Settable: " + string.Join(", ", settable));
        }

        /// <summary>
        /// Every definition must be labellable. <c>IDefinition.Name</c> makes it a compile error
        /// to add a type without one; this pins that the key is a <c>@</c> reference and not
        /// player-facing text (GLOSSARY §Display names).
        /// </summary>
        [Test]
        public void Definitions_CarryALanguageKey_NotLiteralText()
        {
            IDefinition boar = new CreatureDef
            {
                Id = NamespacedId.Parse("isle:boar"),
                Name = "@creature.boar"
            };

            Assert.AreEqual("@creature.boar", boar.Name);
            StringAssert.StartsWith("@", boar.Name);
        }

        /// <summary>
        /// Downstream formula tests build def fixtures inline, which only compiles while
        /// <c>IsExternalInit</c> is reachable from outside <c>Isle.Data</c>. This test failing to
        /// <b>compile</b> is the signal; the assertions are incidental.
        /// </summary>
        [Test]
        public void Definition_ObjectInitializer_WorksFromAnotherAssembly()
        {
            var item = new ItemDef
            {
                Id = NamespacedId.Parse("isle:raw_meat"),
                Weight = 1.2f,
                Grid = new GridSize { W = 2, H = 1 },
                Nutrition = new NutritionSpec { Hunger = 12f, Thirst = 4f, SanitationRisk = 0.35f }
            };

            Assert.AreEqual("isle:raw_meat", item.Id.Value);
            Assert.AreEqual(2, item.Grid.W);
            Assert.AreEqual(12f, item.Nutrition.Hunger);
        }

        static bool IsInitOnly(MethodInfo setter) =>
            setter.ReturnParameter.GetRequiredCustomModifiers()
                .Any(m => m.Name == "IsExternalInit");
    }
}
