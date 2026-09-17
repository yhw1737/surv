using Isle.World.Time;
using NUnit.Framework;
using static Isle.World.Weather.WeatherCalculator;

namespace Isle.World.Weather.Tests
{
    /// <summary>SYS-WORLD-02 §Verification.</summary>
    public class WeatherCalculatorTests
    {
        const float Tolerance = 0.001f;

        // §Verification cases 1-3: BaseAmbientTemp per phase.
        [Test]
        public void BaseAmbientTemp_Case1_Day_Returns37()
        {
            Assert.AreEqual(37f, BaseAmbientTemp(DayPhase.Day), Tolerance);
        }

        [Test]
        public void BaseAmbientTemp_Case2_DawnAndDusk_Returns31()
        {
            Assert.AreEqual(31f, BaseAmbientTemp(DayPhase.Dawn), Tolerance);
            Assert.AreEqual(31f, BaseAmbientTemp(DayPhase.Dusk), Tolerance);
        }

        [Test]
        public void BaseAmbientTemp_Case3_Night_Returns24()
        {
            Assert.AreEqual(24f, BaseAmbientTemp(DayPhase.Night), Tolerance);
        }

        // §Verification cases 4-5: AmbientTemp with rain penalty.
        [Test]
        public void AmbientTemp_Case4_DayRaining_Returns32()
        {
            Assert.AreEqual(32f, AmbientTemp(DayPhase.Day, isRaining: true), Tolerance);
        }

        [Test]
        public void AmbientTemp_Case5_NightNotRaining_Returns24()
        {
            Assert.AreEqual(24f, AmbientTemp(DayPhase.Night, isRaining: false), Tolerance);
        }

        // §Verification case 6: NextState always alternates.
        [Test]
        public void NextState_Case6_ClearAndRain_Alternates()
        {
            Assert.AreEqual(WeatherState.Rain, NextState(WeatherState.Clear));
            Assert.AreEqual(WeatherState.Clear, NextState(WeatherState.Rain));
        }

        // §Verification case 7: Clear duration range endpoints.
        [Test]
        public void NextDurationMinutes_Case7_ClearEndpoints_Returns180To480()
        {
            Assert.AreEqual(180f, NextDurationMinutes(WeatherState.Clear, t: 0f), Tolerance);
            Assert.AreEqual(480f, NextDurationMinutes(WeatherState.Clear, t: 1f), Tolerance);
        }

        // §Verification case 8: Rain duration range endpoints.
        [Test]
        public void NextDurationMinutes_Case8_RainEndpoints_Returns20To60()
        {
            Assert.AreEqual(20f, NextDurationMinutes(WeatherState.Rain, t: 0f), Tolerance);
            Assert.AreEqual(60f, NextDurationMinutes(WeatherState.Rain, t: 1f), Tolerance);
        }

        // Supporting cases beyond the spec's numbered table: rain's effect on dawn/dusk/night,
        // not just day (case 4 only covers Day).
        [TestCase(DayPhase.Dawn)]
        [TestCase(DayPhase.Dusk)]
        [TestCase(DayPhase.Night)]
        public void AmbientTemp_Raining_SubtractsPenaltyFromEveryPhase(DayPhase phase)
        {
            var expected = BaseAmbientTemp(phase) - PrecipitationTempPenalty;
            Assert.AreEqual(expected, AmbientTemp(phase, isRaining: true), Tolerance);
        }

        [TestCase(0.25f, 255f)]
        [TestCase(0.75f, 405f)]
        public void NextDurationMinutes_Clear_LerpsBetweenEndpoints(float t, float expected)
        {
            Assert.AreEqual(expected, NextDurationMinutes(WeatherState.Clear, t), Tolerance);
        }

        // §Season — cycles Spring/Summer/Autumn/Winter every DaysPerSeason (15) in-game days.
        [TestCase(0L, Season.Spring)]                                         // day 0
        [TestCase(14L * WorldClock.MinutesPerDay, Season.Spring)]            // day 14, still spring
        [TestCase(15L * WorldClock.MinutesPerDay, Season.Summer)]            // day 15, rolls to summer
        [TestCase(30L * WorldClock.MinutesPerDay, Season.Autumn)]            // day 30
        [TestCase(45L * WorldClock.MinutesPerDay, Season.Winter)]            // day 45
        [TestCase(60L * WorldClock.MinutesPerDay, Season.Spring)]            // day 60, year wraps
        public void SeasonAt_CyclesEveryFifteenDays(long totalMinutes, Season expected)
        {
            Assert.AreEqual(expected, SeasonAt(totalMinutes));
        }

        [TestCase(Season.Spring, 0f)]
        [TestCase(Season.Summer, 5f)]
        [TestCase(Season.Autumn, 0f)]
        [TestCase(Season.Winter, -8f)]
        public void SeasonTempOffset_MatchesSpecTable(Season season, float expected)
        {
            Assert.AreEqual(expected, SeasonTempOffset(season), Tolerance);
        }

        [Test]
        public void AmbientTemp_NoSeasonArgument_DefaultsToSpringBaseline()
        {
            // Pre-season callers/tests still get exactly what they got before Season existed.
            Assert.AreEqual(37f, AmbientTemp(DayPhase.Day, isRaining: false), Tolerance);
        }

        [Test]
        public void AmbientTemp_SummerDay_AddsOffsetOnTopOfBase()
        {
            Assert.AreEqual(42f, AmbientTemp(DayPhase.Day, isRaining: false, Season.Summer), Tolerance);
        }

        [Test]
        public void AmbientTemp_WinterNightRaining_StacksAllThreePenalties()
        {
            // 24 (night) - 8 (winter) - 5 (rain) = 11 - coldest reachable combination in the game.
            Assert.AreEqual(11f, AmbientTemp(DayPhase.Night, isRaining: true, Season.Winter), Tolerance);
        }

        // §Season temperature pool — rolled once per map; the range anchors on the existing fixed
        // constants at one endpoint (Summer's old +5, Winter's old -8), per the developer's examples.
        [TestCase(Season.Summer, 0f, 5f)]
        [TestCase(Season.Summer, 1f, 10f)]
        [TestCase(Season.Winter, 0f, -12f)]
        [TestCase(Season.Winter, 1f, -8f)]
        [TestCase(Season.Spring, 0f, 0f)]
        [TestCase(Season.Spring, 1f, 0f)]
        [TestCase(Season.Autumn, 0f, 0f)]
        [TestCase(Season.Autumn, 1f, 0f)]
        public void RollSeasonTempOffset_MatchesRangeEndpoints(Season season, float t, float expected)
        {
            Assert.AreEqual(expected, RollSeasonTempOffset(season, t), Tolerance);
        }

        // §Precipitation — Winter alternates Clear/Snow instead of Clear/Rain.
        [Test]
        public void NextState_Winter_AlternatesClearAndSnow()
        {
            Assert.AreEqual(WeatherState.Snow, NextState(WeatherState.Clear, Season.Winter));
            Assert.AreEqual(WeatherState.Clear, NextState(WeatherState.Snow, Season.Winter));
        }

        [TestCase(Season.Spring)]
        [TestCase(Season.Summer)]
        [TestCase(Season.Autumn)]
        public void NextState_NonWinterSeason_StillRains(Season season)
        {
            Assert.AreEqual(WeatherState.Rain, NextState(WeatherState.Clear, season));
        }

        [Test]
        public void NextState_NoSeasonArgument_DefaultsToSpringRain()
        {
            // Pre-snow callers keep meaning exactly what they meant before Snow existed.
            Assert.AreEqual(WeatherState.Rain, NextState(WeatherState.Clear));
        }

        [Test]
        public void NextDurationMinutes_Snow_SameRangeAsRain()
        {
            Assert.AreEqual(20f, NextDurationMinutes(WeatherState.Snow, t: 0f), Tolerance);
            Assert.AreEqual(60f, NextDurationMinutes(WeatherState.Snow, t: 1f), Tolerance);
        }

        // §Temperature events — cold snap/heat wave, an independent axis from Clear/Rain/Snow.
        [TestCase(TemperatureEvent.None, 0f)]
        [TestCase(TemperatureEvent.HeatWave, 10f)]
        [TestCase(TemperatureEvent.ColdSnap, -10f)]
        public void TemperatureEventOffset_MatchesMagnitude(TemperatureEvent tempEvent, float expected)
        {
            Assert.AreEqual(expected, TemperatureEventOffset(tempEvent), Tolerance);
        }

        [Test]
        public void NextTemperatureEvent_Summer_StartsHeatWave()
        {
            Assert.AreEqual(TemperatureEvent.HeatWave, NextTemperatureEvent(TemperatureEvent.None, Season.Summer));
        }

        [Test]
        public void NextTemperatureEvent_Winter_StartsColdSnap()
        {
            Assert.AreEqual(TemperatureEvent.ColdSnap, NextTemperatureEvent(TemperatureEvent.None, Season.Winter));
        }

        [TestCase(Season.Spring)]
        [TestCase(Season.Autumn)]
        public void NextTemperatureEvent_MildSeason_StaysNone(Season season)
        {
            Assert.AreEqual(TemperatureEvent.None, NextTemperatureEvent(TemperatureEvent.None, season));
        }

        [TestCase(TemperatureEvent.HeatWave)]
        [TestCase(TemperatureEvent.ColdSnap)]
        public void NextTemperatureEvent_ActiveEvent_EndsBackToNone(TemperatureEvent current)
        {
            // An active event always ends into None, never straight into the other event.
            Assert.AreEqual(TemperatureEvent.None, NextTemperatureEvent(current, Season.Summer));
            Assert.AreEqual(TemperatureEvent.None, NextTemperatureEvent(current, Season.Winter));
        }

        [Test]
        public void NextTemperatureEventDurationMinutes_None_Returns720To2160()
        {
            Assert.AreEqual(720f, NextTemperatureEventDurationMinutes(TemperatureEvent.None, t: 0f), Tolerance);
            Assert.AreEqual(2160f, NextTemperatureEventDurationMinutes(TemperatureEvent.None, t: 1f), Tolerance);
        }

        [TestCase(TemperatureEvent.HeatWave)]
        [TestCase(TemperatureEvent.ColdSnap)]
        public void NextTemperatureEventDurationMinutes_ActiveEvent_Returns120To300(TemperatureEvent tempEvent)
        {
            Assert.AreEqual(120f, NextTemperatureEventDurationMinutes(tempEvent, t: 0f), Tolerance);
            Assert.AreEqual(300f, NextTemperatureEventDurationMinutes(tempEvent, t: 1f), Tolerance);
        }

        // §Location's controller-facing overload — takes the resolved per-map season offset and the
        // live WeatherState/TemperatureEvent directly, instead of deriving them internally.
        [Test]
        public void AmbientTemp_ResolvedOverload_ClearNoEvent_MatchesLegacyOverload()
        {
            var expected = AmbientTemp(DayPhase.Day, isRaining: false, Season.Summer);
            var actual = AmbientTemp(DayPhase.Day, WeatherState.Clear, seasonTempOffset: 5f, TemperatureEvent.None);
            Assert.AreEqual(expected, actual, Tolerance);
        }

        [Test]
        public void AmbientTemp_ResolvedOverload_SnowSubtractsPrecipitationPenalty()
        {
            // 24 (night) - 12 (this map's rolled winter offset) - 5 (snow counts as precipitation) = 7
            var actual = AmbientTemp(DayPhase.Night, WeatherState.Snow, seasonTempOffset: -12f, TemperatureEvent.None);
            Assert.AreEqual(7f, actual, Tolerance);
        }

        [Test]
        public void AmbientTemp_ResolvedOverload_HeatWaveStacksOnRolledSummerOffset()
        {
            // 37 (day) + 10 (this map's rolled summer offset) + 10 (heat wave) = 57
            var actual = AmbientTemp(DayPhase.Day, WeatherState.Clear, seasonTempOffset: 10f, TemperatureEvent.HeatWave);
            Assert.AreEqual(57f, actual, Tolerance);
        }
    }
}
