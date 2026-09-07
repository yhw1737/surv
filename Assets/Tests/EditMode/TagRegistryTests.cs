using System;
using Isle.Core.Tags;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-CORE-01 §Tags and verification cases 2 and 3.</summary>
    public sealed class TagRegistryTests
    {
        TagRegistry _tags;

        [SetUp]
        public void SetUp() => _tags = new TagRegistry();

        // --- SYS-CORE-01 verification cases 2 and 3 --------------------------------------------

        [Test]
        public void HasTag_ChildImpliesParent_IsTrue()
        {
            var flat = _tags.Flatten(new[] { "fish/saltwater" });
            Assert.IsTrue(_tags.HasTag(flat, "fish"));
            Assert.IsTrue(_tags.HasTag(flat, "fish/saltwater"));
        }

        [Test]
        public void HasTag_ParentDoesNotImplyChild_IsFalse()
        {
            var flat = _tags.Flatten(new[] { "fish" });
            Assert.IsFalse(_tags.HasTag(flat, "fish/saltwater"));
        }

        // --- Flattening --------------------------------------------------------------------------

        [Test]
        public void Flatten_DepthThree_IncludesEveryAncestor()
        {
            var flat = _tags.Flatten(new[] { "station/pot/iron" });
            Assert.AreEqual(3, flat.Count);
            Assert.IsTrue(_tags.HasTag(flat, "station/pot/iron"));
            Assert.IsTrue(_tags.HasTag(flat, "station/pot"));
            Assert.IsTrue(_tags.HasTag(flat, "station"));
        }

        [Test]
        public void Flatten_MultipleTags_UnionsThem()
        {
            var flat = _tags.Flatten(new[] { "fish/deep", "oily", "raw" });
            foreach (var tag in new[] { "fish/deep", "fish", "oily", "raw" })
                Assert.IsTrue(_tags.HasTag(flat, tag), tag);
            Assert.AreEqual(4, flat.Count);
        }

        [Test]
        public void Flatten_SharedAncestor_IsNotDuplicated()
        {
            var flat = _tags.Flatten(new[] { "fish/saltwater", "fish/deep" });
            Assert.AreEqual(3, flat.Count, "fish must appear once");
        }

        [Test]
        public void Flatten_UndeclaredTag_IsInternedNotRejected()
        {
            // SCHEMA.md's own item examples carry tags no tag file declares (oily, protein).
            var flat = _tags.Flatten(new[] { "protein" });
            Assert.IsTrue(_tags.HasTag(flat, "protein"));
        }

        [Test]
        public void Flatten_Null_ReturnsEmpty()
        {
            Assert.AreEqual(0, _tags.Flatten(null).Count);
        }

        [Test]
        public void HasTag_UnknownTag_IsFalseAndDoesNotIntern()
        {
            var flat = _tags.Flatten(new[] { "fish" });
            var before = _tags.Count;
            Assert.IsFalse(_tags.HasTag(flat, "weapon/blade"));
            Assert.AreEqual(before, _tags.Count, "asking must not create");
        }

        // --- Interning ----------------------------------------------------------------------------

        [Test]
        public void Intern_SameTagTwice_ReturnsSameId()
        {
            Assert.AreEqual(_tags.Intern("fish/reef"), _tags.Intern("fish/reef"));
        }

        [Test]
        public void Intern_Child_AlsoInternsAncestors()
        {
            _tags.Intern("station/forge");
            Assert.IsTrue(_tags.TryGetId("station", out _));
            Assert.AreEqual(2, _tags.Count);
        }

        [Test]
        public void SelfAndAncestors_OrderedNearestFirst()
        {
            var id = _tags.Intern("station/pot/iron");
            var chain = _tags.SelfAndAncestors(id);
            Assert.AreEqual(3, chain.Count);
            Assert.AreEqual("station/pot/iron", _tags.PathOf(chain[0]));
            Assert.AreEqual("station/pot", _tags.PathOf(chain[1]));
            Assert.AreEqual("station", _tags.PathOf(chain[2]));
        }

        [Test]
        public void Intern_NamespacedRoot_KeepsPrefixOnEveryAncestor()
        {
            // SCHEMA.md §Tags declares tags as "coolmod:deep_sea" with parent "isle:fish/saltwater".
            var id = _tags.Intern("isle:fish/saltwater");
            var chain = _tags.SelfAndAncestors(id);
            Assert.AreEqual(2, chain.Count);
            Assert.AreEqual("isle:fish", _tags.PathOf(chain[1]));
        }

        [Test]
        public void Intern_NamespacedAndBareRoots_AreDistinct()
        {
            // Reconciling the two spellings is a load-time concern (T-014), not this layer's.
            Assert.AreNotEqual(_tags.Intern("isle:fish"), _tags.Intern("fish"));
        }

        // --- Rejections ------------------------------------------------------------------------------

        [TestCase(null, TestName = "TryIntern_Null_Fails")]
        [TestCase("", TestName = "TryIntern_Empty_Fails")]
        [TestCase("a/b/c/d", TestName = "TryIntern_DepthFour_Fails")]
        [TestCase("fish/", TestName = "TryIntern_TrailingSeparator_Fails")]
        [TestCase("/fish", TestName = "TryIntern_LeadingSeparator_Fails")]
        [TestCase("fish//deep", TestName = "TryIntern_DoubleSeparator_Fails")]
        [TestCase("Fish", TestName = "TryIntern_Uppercase_Fails")]
        [TestCase("fish-deep", TestName = "TryIntern_Hyphen_Fails")]
        [TestCase("fish saltwater", TestName = "TryIntern_Space_Fails")]
        [TestCase("isle:core:fish", TestName = "TryIntern_TwoColonsInRoot_Fails")]
        [TestCase("fish/isle:deep", TestName = "TryIntern_ColonOutsideRoot_Fails")]
        [TestCase(":fish", TestName = "TryIntern_EmptyNamespace_Fails")]
        public void TryIntern_Invalid_FailsWithMessage(string path)
        {
            Assert.IsFalse(_tags.TryIntern(path, out var id, out var error));
            Assert.AreEqual(-1, id);
            Assert.IsNotEmpty(error);
            Assert.AreEqual(0, _tags.Count, "a rejected tag must leave nothing behind");
        }

        [Test]
        public void Intern_DepthFour_Throws()
        {
            Assert.Throws<FormatException>(() => _tags.Intern("a/b/c/d"));
        }

        [Test]
        public void TryIntern_DepthFour_ErrorNamesTheLimit()
        {
            _tags.TryIntern("a/b/c/d", out _, out var error);
            StringAssert.Contains("maximum is 3", error);
        }

        [Test]
        public void Intern_DepthThree_IsAllowed()
        {
            Assert.DoesNotThrow(() => _tags.Intern("a/b/c"));
        }
    }
}
