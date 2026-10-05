using FishNet;
using Isle.Data;
using Isle.Gameplay.Character;
using Isle.Gameplay.Feedback;
using Isle.World.Objects;
using Isle.World.Weather;
using UnityEngine;

namespace Isle.Gameplay.Building
{
    /// <summary>SYS-SURV-01 §Water sources: rain catcher, +40 thirst, safe. Fills while it rains (snow doesn't
    /// count); each drink uses one unit. Capacity and fill rate come from the def's <c>rain_catcher</c> block.</summary>
    [RequireComponent(typeof(WorldObjectInstance))]
    public sealed class RainCatcher : MonoBehaviour, IInteractable
    {
        RainCatcherSpec _spec;

        public float Water { get; set; }
        public float Capacity => _spec?.Capacity ?? 0f;

        public void Initialize(RainCatcherSpec spec) => _spec = spec;

        void Update()
        {
            if (_spec == null || !InstanceFinder.IsServerStarted) return;
            var raining = WeatherController.Instance != null && WeatherController.Instance.IsRaining;
            Water = BuildingCalculator.Fill(Water, _spec.Capacity, _spec.FillPerSecond, Time.deltaTime, raining);
        }

        public void Interact(GameObject user)
        {
            if (Water < 1f)
            {
                GameFeed.RaiseNotice("@ui.catcher_empty");
                return;
            }
            if (!user.TryGetComponent<Vitals>(out var vitals)) return;
            Water -= 1f;
            vitals.Drink(WaterSource.RainCatcher);
        }
    }
}
