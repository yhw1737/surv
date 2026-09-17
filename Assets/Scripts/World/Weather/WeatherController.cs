using FishNet.Object;
using Isle.World.Time;
using UnityEngine;

namespace Isle.World.Weather
{
    public sealed class WeatherController : NetworkBehaviour
    {
        public static WeatherController Instance { get; private set; }

        public WeatherState State { get; private set; } = WeatherState.Clear;
        public Season Season { get; private set; } = Season.Spring;
        public TemperatureEvent TempEvent { get; private set; } = TemperatureEvent.None;
        public float AmbientTemp { get; private set; }
        public bool IsRaining => State == WeatherState.Rain;
        public bool IsSnowing => State == WeatherState.Snow;
        public bool IsPrecipitating => State != WeatherState.Clear;

        readonly System.Random _random = new();

        // This map's rolled Spring/Summer/Autumn/Winter offsets — indexed by Season. Rolled once in
        // Awake and fixed for the rest of the session; no live map-generation bootstrap exists yet
        // (Assets/Scripts/World/Generation/* is pure/seeded but never instantiated at runtime), so
        // Awake is the practical stand-in for "map generation time".
        readonly float[] _seasonTempOffsets = new float[4];

        float _remainingMinutes;
        float _tempEventRemainingMinutes;
        long _lastTotalMinutes = -1;

        void Awake()
        {
            Instance = this;

            for (var i = 0; i < _seasonTempOffsets.Length; i++)
                _seasonTempOffsets[i] = WeatherCalculator.RollSeasonTempOffset((Season)i, (float)_random.NextDouble());
            Debug.Log($"[Weather] This map's season offsets — Summer {_seasonTempOffsets[(int)Season.Summer]:+0.0;-0.0}, Winter {_seasonTempOffsets[(int)Season.Winter]:+0.0;-0.0}.");

            _remainingMinutes = WeatherCalculator.NextDurationMinutes(State, (float)_random.NextDouble());
            _tempEventRemainingMinutes = WeatherCalculator.NextTemperatureEventDurationMinutes(TempEvent, (float)_random.NextDouble());
        }

        void Update()
        {
            if (!IsServer) return;
            var clock = WorldTime.Instance?.Clock;
            if (clock == null) return;

            var totalMinutes = clock.TotalMinutes;
            Season = WeatherCalculator.SeasonAt(totalMinutes);
            if (_lastTotalMinutes >= 0) Advance(totalMinutes - _lastTotalMinutes);
            _lastTotalMinutes = totalMinutes;

            AmbientTemp = WeatherCalculator.AmbientTemp(clock.Phase, State, _seasonTempOffsets[(int)Season], TempEvent);
        }

        void Advance(long elapsedMinutes)
        {
            _remainingMinutes -= elapsedMinutes;
            while (_remainingMinutes <= 0f)
            {
                State = WeatherCalculator.NextState(State, Season);
                _remainingMinutes += WeatherCalculator.NextDurationMinutes(State, (float)_random.NextDouble());
            }

            _tempEventRemainingMinutes -= elapsedMinutes;
            while (_tempEventRemainingMinutes <= 0f)
            {
                TempEvent = WeatherCalculator.NextTemperatureEvent(TempEvent, Season);
                _tempEventRemainingMinutes += WeatherCalculator.NextTemperatureEventDurationMinutes(TempEvent, (float)_random.NextDouble());
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
