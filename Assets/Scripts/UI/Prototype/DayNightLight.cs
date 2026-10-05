using Isle.World.Time;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Isle.UI.Prototype
{
    /// <summary>
    /// Dims the scene's global 2D light with the in-game day phase, so the island reads as night without
    /// any art. Brightness levels are presentation values, not balance numbers, and are kept here rather
    /// than in a def for that reason.
    /// </summary>
    public sealed class DayNightLight : MonoBehaviour
    {
        const float Smoothing = 1.5f;

        Light2D _light;

        void Update()
        {
            if (_light == null) _light = FindGlobalLight();
            if (_light == null || WorldTime.Instance == null) return;

            var target = BrightnessFor(WorldTime.Instance.Clock.Phase);
            _light.intensity = Mathf.MoveTowards(_light.intensity, target, Smoothing * Time.deltaTime);
        }

        /// <summary>The scene's global light — not one of the point lights <see cref="LightRig"/> adds.</summary>
        static Light2D FindGlobalLight()
        {
            foreach (var light in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
                if (light.lightType == Light2D.LightType.Global) return light;
            return null;
        }

        static float BrightnessFor(DayPhase phase) => phase switch
        {
            DayPhase.Day => 1.0f,
            DayPhase.Dawn => 0.7f,
            DayPhase.Dusk => 0.6f,
            _ => 0.35f,
        };
    }
}
