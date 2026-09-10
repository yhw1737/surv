using Isle.Core.Ids;
using Isle.Data;
using Isle.Gameplay.Buffs;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-BUFF-01 §Verification.</summary>
    public sealed class BuffSetTests
    {
        static readonly BuffDef Warm = new() { Id = NamespacedId.Parse("isle:warm"), DurationMin = 180 };
        static readonly BuffDef SteadyHand = new() { Id = NamespacedId.Parse("isle:steady_hand"), DurationMin = 180 };
        static readonly BuffDef Permanent = new() { Id = NamespacedId.Parse("isle:permanent"), DurationMin = 0 };

        [Test]
        public void IsActive_BeforeExpiry_ReturnsTrue()
        {
            var buffs = new BuffSet();
            buffs.Grant(Warm, currentTimeMin: 0);

            Assert.IsTrue(buffs.IsActive(Warm.Id, currentTimeMin: 179));
        }

        [Test]
        public void IsActive_AtOrAfterExpiry_ReturnsFalse()
        {
            var buffs = new BuffSet();
            buffs.Grant(Warm, currentTimeMin: 0);

            Assert.IsFalse(buffs.IsActive(Warm.Id, currentTimeMin: 180));
        }

        [Test]
        public void Grant_SameBuffAgainBeforeExpiry_RefreshesDuration()
        {
            var buffs = new BuffSet();
            buffs.Grant(Warm, currentTimeMin: 0);
            buffs.Grant(Warm, currentTimeMin: 100);

            Assert.IsTrue(buffs.IsActive(Warm.Id, currentTimeMin: 279), "refreshed expiry should be 100 + 180 = 280");
            Assert.IsFalse(buffs.IsActive(Warm.Id, currentTimeMin: 280));
        }

        [Test]
        public void Grant_ZeroDuration_NeverExpires()
        {
            var buffs = new BuffSet();
            buffs.Grant(Permanent, currentTimeMin: 0);

            Assert.IsTrue(buffs.IsActive(Permanent.Id, currentTimeMin: 1_000_000));
        }

        [Test]
        public void Clear_RemovesBuffImmediately()
        {
            var buffs = new BuffSet();
            buffs.Grant(Warm, currentTimeMin: 0);
            buffs.Clear(Warm.Id);

            Assert.IsFalse(buffs.IsActive(Warm.Id, currentTimeMin: 1));
        }

        [Test]
        public void ActiveBuffIds_TracksMultipleBuffsIndependently()
        {
            var buffs = new BuffSet();
            buffs.Grant(Warm, currentTimeMin: 0);
            buffs.Grant(SteadyHand, currentTimeMin: 0);

            CollectionAssert.AreEquivalent(new[] { Warm.Id, SteadyHand.Id }, buffs.ActiveBuffIds(currentTimeMin: 1));
        }

        [Test]
        public void IsActive_UngrantedBuff_ReturnsFalse()
        {
            var buffs = new BuffSet();

            Assert.IsFalse(buffs.IsActive(Warm.Id, currentTimeMin: 0));
        }
    }
}
