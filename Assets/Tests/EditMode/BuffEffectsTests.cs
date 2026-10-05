using Isle.Core.Ids;
using Isle.Data;
using Isle.Gameplay.Buffs;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>Reading active buffs' effects (SYS-BUFF-01): multipliers multiply, shifts add, absent is neutral.</summary>
    public sealed class BuffEffectsTests
    {
        static BuffDef Buff(string type, float value) => new()
        {
            Id = NamespacedId.Parse("isle:test_" + type),
            Effects = new[] { new BuffEffect { Type = type, Value = value } },
        };

        [Test]
        public void Mult_NoBuffs_IsOne()
        {
            Assert.AreEqual(1f, BuffEffects.Mult(new BuffDef[0], "thirst_drain_mult"), 1e-5f);
        }

        [Test]
        public void Mult_TwoMatching_Multiplies()
        {
            var buffs = new[] { Buff("thirst_drain_mult", 0.7f), Buff("thirst_drain_mult", 0.5f), Buff("aim_sway_mult", 0.1f) };
            Assert.AreEqual(0.35f, BuffEffects.Mult(buffs, "thirst_drain_mult"), 1e-5f);
        }

        [Test]
        public void Sum_Shift_Adds()
        {
            Assert.AreEqual(-5f, BuffEffects.Sum(new[] { Buff("hypothermia_threshold_shift", -5f) }, "hypothermia_threshold_shift"), 1e-5f);
        }
    }
}
