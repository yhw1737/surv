using Isle.Gameplay.Character;
using NUnit.Framework;
using static Isle.Gameplay.Character.VitalsCalculator;

namespace Isle.Tests.EditMode
{
    /// <summary>
    /// SYS-SURV-01 §Verification, all 6 cases, plus T-051's wet-penalty decay and fire-bonus
    /// falloff formulas. The real ambientTemp/weather/campfire-proximity inputs that would drive
    /// those two, and the six-water-source drink interaction T-052 will build, aren't exercised
    /// here — no weather or world-object system exists yet to produce them (§Open questions).
    /// </summary>
    public sealed class VitalsCalculatorTests
    {
        const float Tolerance = 0.001f;

        [Test]
        public void HungerAndThirstDrain_Case1_IdleBaseline_Returns1_05And2_1PerHour()
        {
            Assert.AreEqual(1.05f, HungerDrainPerHour(Activity.Idle, temperature: 37f), Tolerance);
            Assert.AreEqual(2.1f, ThirstDrainPerHour(Activity.Idle, ambientTemp: 20f, minutesSinceSaltyFood: 999f), Tolerance);
        }

        [Test]
        public void HungerAndThirstDrain_Case2_Sprinting30C_Returns2_7And8_64PerHour()
        {
            Assert.AreEqual(2.7f, HungerDrainPerHour(Activity.Sprinting, temperature: 37f), Tolerance);
            Assert.AreEqual(8.64f, ThirstDrainPerHour(Activity.Sprinting, ambientTemp: 30f, minutesSinceSaltyFood: 999f), Tolerance);
        }

        [Test]
        public void ZeroGaugeHpDrain_Case3_HungerZero60RealSeconds_Returns24Hp()
        {
            var drainPerSecond = ZeroGaugeHpDrainPerSecond(hunger: 0f, thirst: 100f);
            Assert.AreEqual(-24f, drainPerSecond * 60f, Tolerance);
        }

        [Test]
        public void ZeroGaugeHpDrain_Case4_ThirstZero60RealSeconds_Returns60Hp()
        {
            var drainPerSecond = ZeroGaugeHpDrainPerSecond(hunger: 100f, thirst: 0f);
            Assert.AreEqual(-60f, drainPerSecond * 60f, Tolerance);
        }

        [Test]
        public void DrinkThirstDelta_Case5_Seawater_ReturnsMinus15()
        {
            Assert.AreEqual(-15f, DrinkThirstDelta(WaterSource.Seawater), Tolerance);
        }

        [Test]
        public void StaminaRegen_Case6_HungerTwenty_Returns9PerSecond()
        {
            var regen = StaminaRegenPerSecondAt(overweightFactor: 1f, hunger: 20f, thirst: 100f);
            Assert.AreEqual(9f, regen, Tolerance);
        }

        // Supporting cases beyond the 6-row table — same spec, other rows of its own tables.

        [TestCase(35f, false)]
        [TestCase(34.99f, true)]
        public void ColdMultiplier_BelowThreshold_Applies1_5(float temperature, bool expectCold)
        {
            Assert.AreEqual(expectCold ? 1.5f : 1.0f, ColdMultiplier(temperature), Tolerance);
        }

        [TestCase(28f, false)]
        [TestCase(28.01f, true)]
        public void HeatMultiplier_AboveThreshold_Applies1_6(float ambientTemp, bool expectHot)
        {
            Assert.AreEqual(expectHot ? 1.6f : 1.0f, HeatMultiplier(ambientTemp), Tolerance);
        }

        [TestCase(0f, true)]
        [TestCase(30f, true)]
        [TestCase(30.01f, false)]
        public void SaltMultiplier_Within30Minutes_Applies1_4(float minutesSince, bool expectSalty)
        {
            Assert.AreEqual(expectSalty ? 1.4f : 1.0f, SaltMultiplier(minutesSince), Tolerance);
        }

        [TestCase(27.99f, -0.5f)]
        [TestCase(28f, 0f)]
        [TestCase(44f, 0f)]
        [TestCase(44.01f, -0.5f)]
        public void TemperatureHpDrainPerSecond_OutsideBands_Applies0_5(float temperature, float expected)
        {
            Assert.AreEqual(expected, TemperatureHpDrainPerSecond(temperature), Tolerance);
        }

        [Test]
        public void TargetTemperature_CombinesAmbientClothingFireWet()
        {
            Assert.AreEqual(37f, TargetTemperature(ambientTemp: 30f, clothingBonus: 5f, fireBonus: 8f, wetPenalty: 6f), Tolerance);
        }

        [Test]
        public void ApproachTemperature_ClampsToRatePerInGameMinute()
        {
            // 2.0/in-game-minute rate: 1 minute towards a target 10 away moves only 2.0.
            Assert.AreEqual(34f, ApproachTemperature(current: 32f, target: 50f, inGameMinutes: 1f), Tolerance);
            // Already within one step's reach: lands exactly on target, no overshoot.
            Assert.AreEqual(37f, ApproachTemperature(current: 36f, target: 37f, inGameMinutes: 1f), Tolerance);
        }

        [Test]
        public void StaminaRegen_LowHungerOrThirst_HalvesRegenAndCapsMax()
        {
            Assert.AreEqual(9f, StaminaRegenPerSecondAt(1f, hunger: 24.99f, thirst: 100f), Tolerance);
            Assert.AreEqual(9f, StaminaRegenPerSecondAt(1f, hunger: 100f, thirst: 24.99f), Tolerance);
            Assert.AreEqual(0.7f, MaxStaminaFactor(hunger: 20f, thirst: 100f), Tolerance);
            Assert.AreEqual(1.0f, MaxStaminaFactor(hunger: 25f, thirst: 100f), Tolerance);
        }

        [TestCase(WaterSource.Seawater, -15f)]
        [TestCase(WaterSource.StandingWater, 25f)]
        [TestCase(WaterSource.Stream, 35f)]
        [TestCase(WaterSource.RainCatcher, 40f)]
        [TestCase(WaterSource.BoiledWater, 45f)]
        [TestCase(WaterSource.Coconut, 30f)]
        public void DrinkThirstDelta_AllSixSources_MatchSpecTable(WaterSource source, float expected)
        {
            Assert.AreEqual(expected, DrinkThirstDelta(source), Tolerance);
        }

        // T-051: wet penalty decay and fire bonus falloff. Weather/campfire proximity still don't
        // exist to drive these (§Open questions), but both formulas the spec text itself gives are
        // pure and verifiable now.

        [Test]
        public void DecayWetPenalty_TenInGameMinutes_HalvesTowardZero()
        {
            // -6 magnitude drying over 20 in-game minutes: 10 minutes in, half gone.
            Assert.AreEqual(3f, DecayWetPenalty(current: 6f, inGameMinutes: 10f), Tolerance);
        }

        [Test]
        public void DecayWetPenalty_FullDuration_ReachesZeroWithoutOvershoot()
        {
            Assert.AreEqual(0f, DecayWetPenalty(current: 6f, inGameMinutes: 20f), Tolerance);
            Assert.AreEqual(0f, DecayWetPenalty(current: 6f, inGameMinutes: 999f), Tolerance);
        }

        [Test]
        public void DecayWetPenalty_AlreadyDry_StaysZero()
        {
            Assert.AreEqual(0f, DecayWetPenalty(current: 0f, inGameMinutes: 5f), Tolerance);
        }

        [TestCase(0f, 8f)]
        [TestCase(2.5f, 4f)]
        [TestCase(5f, 0f)]
        [TestCase(8f, 0f)]
        public void FireBonusAtDistance_LinearFalloffWithinFiveTiles(float distanceTiles, float expected)
        {
            Assert.AreEqual(expected, FireBonusAtDistance(distanceTiles), Tolerance);
        }
    }
}
