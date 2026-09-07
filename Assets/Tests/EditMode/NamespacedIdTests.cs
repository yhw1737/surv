using System;
using Isle.Core.Ids;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-CORE-01 §NamespacedId and verification case 1.</summary>
    public sealed class NamespacedIdTests
    {
        // --- SYS-CORE-01 verification case 1 -------------------------------------------------

        [Test]
        public void TryParse_NoNamespace_Fails()
        {
            Assert.IsFalse(NamespacedId.TryParse("raw_meat", out _, out var error));
            StringAssert.Contains("Missing namespace", error);
        }

        [Test]
        public void Parse_NoNamespace_Throws()
        {
            Assert.Throws<FormatException>(() => NamespacedId.Parse("raw_meat"));
        }

        // --- Shape ---------------------------------------------------------------------------

        [Test]
        public void TryParse_Valid_SplitsNamespaceAndName()
        {
            Assert.IsTrue(NamespacedId.TryParse("isle:raw_meat", out var id, out var error));
            Assert.IsNull(error);
            Assert.AreEqual("isle", id.Namespace);
            Assert.AreEqual("raw_meat", id.Name);
            Assert.AreEqual("isle:raw_meat", id.Value);
            Assert.IsTrue(id.IsValid);
        }

        [Test]
        public void TryParse_ModNamespace_IsNotSpecialCased()
        {
            Assert.IsTrue(NamespacedId.TryParse("coolmod:lanternfish", out var id, out _));
            Assert.AreEqual("coolmod", id.Namespace);
            Assert.AreEqual("lanternfish", id.Name);
        }

        [Test]
        public void Default_IsNotValid()
        {
            var id = default(NamespacedId);
            Assert.IsFalse(id.IsValid);
            Assert.IsNull(id.Value);
            Assert.AreEqual("<unset>", id.ToString());
        }

        // --- Rejections ------------------------------------------------------------------------

        [TestCase(null, TestName = "TryParse_Null_Fails")]
        [TestCase("", TestName = "TryParse_Empty_Fails")]
        [TestCase(":raw_meat", TestName = "TryParse_EmptyNamespace_Fails")]
        [TestCase("isle:", TestName = "TryParse_EmptyName_Fails")]
        [TestCase("isle:core:raw_meat", TestName = "TryParse_TwoColons_Fails")]
        [TestCase("Isle:raw_meat", TestName = "TryParse_UppercaseNamespace_Fails")]
        [TestCase("isle:Raw_Meat", TestName = "TryParse_UppercaseName_Fails")]
        [TestCase("isle:raw-meat", TestName = "TryParse_Hyphen_Fails")]
        [TestCase("isle:raw meat", TestName = "TryParse_Space_Fails")]
        [TestCase("isle:raw/meat", TestName = "TryParse_Slash_Fails")]
        public void TryParse_Invalid_FailsWithMessage(string text)
        {
            Assert.IsFalse(NamespacedId.TryParse(text, out var id, out var error));
            Assert.IsFalse(id.IsValid);
            Assert.IsNotEmpty(error, "every rejection must explain itself — SYS-CORE-01 §Load pipeline");
        }

        [Test]
        public void TryParse_InvalidCharacter_ErrorQuotesTheOffender()
        {
            NamespacedId.TryParse("isle:raw-meat", out _, out var error);
            StringAssert.Contains("'-'", error);
        }

        // --- Equality --------------------------------------------------------------------------

        [Test]
        public void Equals_SameText_IsTrue()
        {
            var a = NamespacedId.Parse("isle:raw_meat");
            var b = NamespacedId.Parse("isle:raw_meat");
            Assert.IsTrue(a == b);
            Assert.IsFalse(a != b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void Equals_SameNameDifferentNamespace_IsFalse()
        {
            var isle = NamespacedId.Parse("isle:raw_meat");
            var mod = NamespacedId.Parse("coolmod:raw_meat");
            Assert.IsFalse(isle == mod);
        }

        [Test]
        public void Equals_Default_MatchesOnlyDefault()
        {
            Assert.IsTrue(default(NamespacedId) == default(NamespacedId));
            Assert.IsFalse(default(NamespacedId) == NamespacedId.Parse("isle:raw_meat"));
        }

        [Test]
        public void UsableAsDictionaryKey()
        {
            var map = new System.Collections.Generic.Dictionary<NamespacedId, int>
            {
                [NamespacedId.Parse("isle:raw_meat")] = 1,
                [NamespacedId.Parse("isle:cooked_meat")] = 2,
            };

            Assert.AreEqual(1, map[NamespacedId.Parse("isle:raw_meat")]);
            Assert.IsFalse(map.ContainsKey(NamespacedId.Parse("isle:steel_ingot")));
        }
    }
}
