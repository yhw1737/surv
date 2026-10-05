using Isle.Gameplay.Combat;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-COMBAT-01 §Power and §Damage, verification table, ±0.05.</summary>
    public sealed class PowerCalculatorTests
    {
        const float Tolerance = 0.05f;

        [Test]
        public void FinalPower_Case1_Lv0Common_Returns15()
        {
            Assert.AreEqual(15.0f, PowerCalculator.FinalPower(30f, 0, qualityMult: 1f), Tolerance);
        }

        [Test]
        public void FinalPower_Case2_Lv50Common_Returns30()
        {
            Assert.AreEqual(30.0f, PowerCalculator.FinalPower(30f, 50, qualityMult: 1f), Tolerance);
        }

        [Test]
        public void FinalPower_Case3_Lv50Superior_Returns39()
        {
            Assert.AreEqual(39.0f, PowerCalculator.FinalPower(30f, 50, qualityMult: 1.30f), Tolerance);
        }

        [Test]
        public void FinalPower_Case4_Lv25Master_Returns33_75()
        {
            Assert.AreEqual(33.75f, PowerCalculator.FinalPower(30f, 25, qualityMult: 1.50f), Tolerance);
        }

        [Test]
        public void FinalPower_Case5_Lv50SuperiorBackstab_Returns52_65()
        {
            Assert.AreEqual(52.65f, PowerCalculator.FinalPower(30f, 50, qualityMult: 1.30f, situationalMult: 1.35f), Tolerance);
        }

        [Test]
        public void Damage_Case6_40VsArmor60_Returns20()
        {
            Assert.AreEqual(20.0f, DamageResolver.Damage(40f, totalArmor: 60f), Tolerance);
        }

        [Test]
        public void Damage_Case7_40VsArmor300_Returns10_CapAt75Percent()
        {
            Assert.AreEqual(10.0f, DamageResolver.Damage(40f, totalArmor: 300f), Tolerance);
        }
    }
}
