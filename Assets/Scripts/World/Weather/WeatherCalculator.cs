using System;
using Isle.World.Time;

namespace Isle.World.Weather
{
    /// <summary>SYS-WORLD-02. Precipitation type — Snow replaces Rain in Winter.</summary>
    public enum WeatherState { Clear, Rain, Snow }

    /// <summary>SYS-WORLD-02 §Season. Fixed four-season cycle, in calendar order.</summary>
    public enum Season { Spring, Summer, Autumn, Winter }

    /// <summary>SYS-WORLD-02 §Temperature events. Independent of <see cref="WeatherState"/> —
    /// a heat wave/cold snap can be in effect regardless of precipitation.</summary>
    public enum TemperatureEvent { None, ColdSnap, HeatWave }

    /// <summary>
    /// SYS-WORLD-02. Static pure, no Unity/FishNet refs — EditMode test target, same split as
    /// <see cref="Isle.Gameplay.Character.VitalsCalculator"/>. <see cref="WeatherController"/> is the
    /// impure server-side holder that draws the actual <c>System.Random</c> sample and calls these.
    /// </summary>
    public static class WeatherCalculator
    {
        // §Ambient temperature by day phase.
        public const float DayTemp = 37f;
        public const float DawnDuskTemp = 31f;
        public const float NightTemp = 24f;
        public const float PrecipitationTempPenalty = 5f;

        // §Rain/Snow — in-game-minute duration ranges. Snow reuses Rain's range rather than
        // inventing separate numbers — ponytail: no spec basis for a different snow duration.
        public const float ClearMinMinutes = 180f;
        public const float ClearMaxMinutes = 480f;
        public const float RainMinMinutes = 20f;
        public const float RainMaxMinutes = 60f;

        // §Season — additive offset on top of BaseAmbientTemp, same unit scale. Spring/Autumn are
        // the unmodified baseline the day-phase table above was tuned against; Summer/Winter push
        // away from it symmetrically-ish. 15-in-game-day season (60-day year) matches RimWorld's
        // quadrum length rather than being tuned for a single real-time sitting.
        public const int DaysPerSeason = 15;
        public const float SpringTempOffset = 0f;
        public const float SummerTempOffset = 5f;
        public const float AutumnTempOffset = 0f;
        public const float WinterTempOffset = -8f;

        // §Season temperature pool — Summer/Winter's offset is rolled once per map from this range
        // instead of using the fixed constant above every time; the old constant becomes one
        // endpoint. Spring/Autumn are not randomized — no example was given for them and the spec's
        // existing framing already treats them as the unmodified baseline.
        public const float SummerTempOffsetMax = 10f;
        public const float WinterTempOffsetMin = -12f;

        // §Temperature events — magnitude, additive on top of the rolled season offset.
        public const float HeatWaveTempBonus = 10f;
        public const float ColdSnapTempPenalty = 10f;

        // §Temperature events — in-game-minute duration ranges. Calm (None) is much longer than an
        // active event so heat waves/cold snaps feel like an event, not half of every season.
        public const float CalmMinMinutes = 720f;
        public const float CalmMaxMinutes = 2160f;
        public const float TemperatureEventMinMinutes = 120f;
        public const float TemperatureEventMaxMinutes = 300f;

        public static float BaseAmbientTemp(DayPhase phase) => phase switch
        {
            DayPhase.Day => DayTemp,
            DayPhase.Dawn or DayPhase.Dusk => DawnDuskTemp,
            DayPhase.Night => NightTemp,
            _ => throw new ArgumentOutOfRangeException(nameof(phase)),
        };

        public static float SeasonTempOffset(Season season) => season switch
        {
            Season.Spring => SpringTempOffset,
            Season.Summer => SummerTempOffset,
            Season.Autumn => AutumnTempOffset,
            Season.Winter => WinterTempOffset,
            _ => throw new ArgumentOutOfRangeException(nameof(season)),
        };

        /// <summary>Rolls this map's fixed season offset. <paramref name="t"/> is a caller-supplied
        /// [0,1) sample — same pure/impure split as <see cref="NextDurationMinutes"/>. Spring/Autumn
        /// ignore <paramref name="t"/> and return the unmodified baseline.</summary>
        public static float RollSeasonTempOffset(Season season, float t) => season switch
        {
            Season.Summer => Lerp(SummerTempOffset, SummerTempOffsetMax, t),
            Season.Winter => Lerp(WinterTempOffsetMin, WinterTempOffset, t),
            _ => SeasonTempOffset(season),
        };

        /// <summary><paramref name="season"/> defaults to <see cref="Season.Spring"/> (offset 0) so
        /// this reads exactly as it did before seasons existed for any caller that doesn't pass one.</summary>
        public static float AmbientTemp(DayPhase phase, bool isRaining, Season season = Season.Spring) =>
            BaseAmbientTemp(phase) + SeasonTempOffset(season) - (isRaining ? PrecipitationTempPenalty : 0f);

        /// <summary>Controller-facing overload — takes the map's already-rolled season offset and the
        /// live <see cref="WeatherState"/>/<see cref="TemperatureEvent"/> directly, rather than
        /// deriving them from a bare bool/enum default. Distinct parameter types from the overload
        /// above keep both legal without ambiguity.</summary>
        public static float AmbientTemp(DayPhase phase, WeatherState state, float seasonTempOffset, TemperatureEvent tempEvent) =>
            BaseAmbientTemp(phase) + seasonTempOffset + TemperatureEventOffset(tempEvent)
                - (state != WeatherState.Clear ? PrecipitationTempPenalty : 0f);

        public static float TemperatureEventOffset(TemperatureEvent tempEvent) => tempEvent switch
        {
            TemperatureEvent.None => 0f,
            TemperatureEvent.HeatWave => HeatWaveTempBonus,
            TemperatureEvent.ColdSnap => -ColdSnapTempPenalty,
            _ => throw new ArgumentOutOfRangeException(nameof(tempEvent)),
        };

        /// <summary>Pure function of elapsed in-game minutes since world start — same input
        /// <see cref="WorldClock.TotalMinutes"/> already exposes, no new state to hold.</summary>
        public static Season SeasonAt(long totalMinutes) =>
            (Season)(totalMinutes / WorldClock.MinutesPerDay / DaysPerSeason % 4);

        /// <summary><paramref name="season"/> defaults to <see cref="Season.Spring"/> so pre-Snow
        /// call sites keep alternating Clear/Rain exactly as before. Only Winter rains as Snow.</summary>
        public static WeatherState NextState(WeatherState current, Season season = Season.Spring) =>
            current != WeatherState.Clear
                ? WeatherState.Clear
                : season == Season.Winter ? WeatherState.Snow : WeatherState.Rain;

        /// <summary><paramref name="t"/> is a caller-supplied [0,1) sample, not drawn here — keeps
        /// this pure and testable; the actual <c>System.Random</c> draw is <see cref="WeatherController"/>'s job.</summary>
        public static float NextDurationMinutes(WeatherState state, float t) => state switch
        {
            WeatherState.Clear => Lerp(ClearMinMinutes, ClearMaxMinutes, t),
            WeatherState.Rain or WeatherState.Snow => Lerp(RainMinMinutes, RainMaxMinutes, t),
            _ => throw new ArgumentOutOfRangeException(nameof(state)),
        };

        /// <summary>Summer alternates None/HeatWave, Winter alternates None/ColdSnap, Spring/Autumn
        /// always resolve back to None — mirrors <see cref="NextState"/>'s season gating.</summary>
        public static TemperatureEvent NextTemperatureEvent(TemperatureEvent current, Season season)
        {
            if (current != TemperatureEvent.None) return TemperatureEvent.None;
            return season switch
            {
                Season.Summer => TemperatureEvent.HeatWave,
                Season.Winter => TemperatureEvent.ColdSnap,
                _ => TemperatureEvent.None,
            };
        }

        /// <summary>Same pure [0,1) sample contract as <see cref="NextDurationMinutes"/>.</summary>
        public static float NextTemperatureEventDurationMinutes(TemperatureEvent state, float t) => state switch
        {
            TemperatureEvent.None => Lerp(CalmMinMinutes, CalmMaxMinutes, t),
            TemperatureEvent.HeatWave or TemperatureEvent.ColdSnap => Lerp(TemperatureEventMinMinutes, TemperatureEventMaxMinutes, t),
            _ => throw new ArgumentOutOfRangeException(nameof(state)),
        };

        static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
