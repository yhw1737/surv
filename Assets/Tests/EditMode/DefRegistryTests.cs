using System.Collections.Generic;
using Isle.Core.Ids;
using Isle.Data;
using Isle.Modding.Defs;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-CORE-01 §Load pipeline steps 9–10.</summary>
    public sealed class DefRegistryTests
    {
        [TearDown]
        public void TearDown() => DefRegistry.Clear();

        static ItemDef Item(string id, params string[] tags) => new()
        {
            Id = NamespacedId.Parse(id),
            Name = "@item.test",
            Tags = tags,
        };

        [Test]
        public void Get_KnownId_ReturnsDef()
        {
            var result = new LoadResult<ItemDef>();
            result.Definitions.Add(Item("isle:raw_meat"));
            DefRegistry.Register(result);

            var def = DefRegistry.Get<ItemDef>(NamespacedId.Parse("isle:raw_meat"));

            Assert.AreEqual("isle:raw_meat", def.Id.Value);
        }

        [Test]
        public void Get_UnknownId_Throws()
        {
            Assert.Throws<KeyNotFoundException>(() => DefRegistry.Get<ItemDef>(NamespacedId.Parse("isle:nope")));
        }

        [Test]
        public void TryGet_UnknownId_ReturnsFalse()
        {
            Assert.IsFalse(DefRegistry.TryGet<ItemDef>(NamespacedId.Parse("isle:nope"), out var def));
            Assert.IsNull(def);
        }

        [Test]
        public void AllWithTag_LiteralTag_ReturnsMatch()
        {
            var result = new LoadResult<ItemDef>();
            result.Definitions.Add(Item("isle:raw_meat", "meat"));
            DefRegistry.Register(result);

            var found = DefRegistry.AllWithTag<ItemDef>("meat");

            Assert.AreEqual(1, found.Count);
            Assert.AreEqual("isle:raw_meat", found[0].Id.Value);
        }

        [Test]
        public void AllWithTag_ImpliedAncestor_ReturnsMatch()
        {
            // "fish/saltwater" implies "fish" (T-010) — DefRegistry is the first thing that queries it.
            var result = new LoadResult<ItemDef>();
            result.Definitions.Add(Item("isle:tuna", "fish/saltwater"));
            DefRegistry.Register(result);

            var found = DefRegistry.AllWithTag<ItemDef>("fish");

            Assert.AreEqual(1, found.Count);
        }

        [Test]
        public void AllWithTag_UnknownTag_ReturnsEmpty()
        {
            Assert.IsEmpty(DefRegistry.AllWithTag<ItemDef>("no_such_tag"));
        }

        [Test]
        public void All_ReturnsEveryRegisteredDef()
        {
            var result = new LoadResult<ItemDef>();
            result.Definitions.Add(Item("isle:a"));
            result.Definitions.Add(Item("isle:b"));
            DefRegistry.Register(result);

            Assert.AreEqual(2, DefRegistry.All<ItemDef>().Count);
        }

        [Test]
        public void Register_TypeWithoutTagsProperty_DoesNotThrow()
        {
            var result = new LoadResult<CraftRecipeDef>();
            result.Definitions.Add(new CraftRecipeDef { Id = NamespacedId.Parse("isle:test_recipe"), Name = "@recipe.test" });

            Assert.DoesNotThrow(() => DefRegistry.Register(result));
            Assert.AreEqual(1, DefRegistry.All<CraftRecipeDef>().Count);
        }

        [Test]
        public void Register_AfterFreeze_Throws()
        {
            DefRegistry.Freeze();

            Assert.Throws<System.InvalidOperationException>(() => DefRegistry.Register(new LoadResult<ItemDef>()));
        }

        // --- Reload (T-015) ---------------------------------------------------------------------

        [Test]
        public void Reload_ExistingId_UpdatesInPlace_SameReference()
        {
            var first = new LoadResult<ItemDef>();
            first.Definitions.Add(Item("isle:raw_meat"));
            DefRegistry.Register(first);
            var before = DefRegistry.Get<ItemDef>(NamespacedId.Parse("isle:raw_meat"));

            var second = new LoadResult<ItemDef>();
            second.Definitions.Add(new ItemDef { Id = NamespacedId.Parse("isle:raw_meat"), Name = "@item.changed" });
            DefRegistry.Reload(second);
            var after = DefRegistry.Get<ItemDef>(NamespacedId.Parse("isle:raw_meat"));

            Assert.AreSame(before, after);
            Assert.AreEqual("@item.changed", after.Name);
        }

        [Test]
        public void Reload_NewId_IsInserted()
        {
            DefRegistry.Register(new LoadResult<ItemDef>());

            var result = new LoadResult<ItemDef>();
            result.Definitions.Add(Item("isle:new_item"));
            DefRegistry.Reload(result);

            Assert.AreEqual("isle:new_item", DefRegistry.Get<ItemDef>(NamespacedId.Parse("isle:new_item")).Id.Value);
        }

        [Test]
        public void Reload_MissingId_IsRemoved()
        {
            var first = new LoadResult<ItemDef>();
            first.Definitions.Add(Item("isle:raw_meat"));
            DefRegistry.Register(first);

            DefRegistry.Reload(new LoadResult<ItemDef>());

            Assert.IsFalse(DefRegistry.TryGet<ItemDef>(NamespacedId.Parse("isle:raw_meat"), out _));
        }

        [Test]
        public void Reload_RebuildsTagIndex()
        {
            var first = new LoadResult<ItemDef>();
            first.Definitions.Add(Item("isle:raw_meat", "meat"));
            DefRegistry.Register(first);

            var second = new LoadResult<ItemDef>();
            second.Definitions.Add(Item("isle:raw_meat", "fish"));
            DefRegistry.Reload(second);

            Assert.IsEmpty(DefRegistry.AllWithTag<ItemDef>("meat"));
            Assert.AreEqual(1, DefRegistry.AllWithTag<ItemDef>("fish").Count);
        }

        [Test]
        public void Reload_AfterFreeze_DoesNotThrow()
        {
            DefRegistry.Freeze();

            Assert.DoesNotThrow(() => DefRegistry.Reload(new LoadResult<ItemDef>()));
        }
    }
}
