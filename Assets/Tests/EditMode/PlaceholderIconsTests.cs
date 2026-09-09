using Isle.Core.Ids;
using Isle.Data;
using Isle.UI;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>ART_PIPELINE §Placeholders, T-017: item icon fallback.</summary>
    public sealed class PlaceholderIconsTests
    {
        [Test]
        public void ItemIcon_ReturnsOneTileSprite()
        {
            var def = new ItemDef { Id = NamespacedId.Parse("isle:raw_meat"), Name = "@item.raw_meat", Tags = new[] { "meat" } };

            var sprite = PlaceholderIcons.ItemIcon(def);

            Assert.AreEqual(32, sprite.rect.width);
            Assert.AreEqual(32, sprite.rect.height);
        }

        [Test]
        public void ItemIcon_NoTags_StillReturnsSprite()
        {
            var def = new ItemDef { Id = NamespacedId.Parse("isle:mystery_item"), Name = "@item.mystery", Tags = null };

            Assert.DoesNotThrow(() => PlaceholderIcons.ItemIcon(def));
        }
    }
}
