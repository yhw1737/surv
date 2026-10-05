using System.Collections.Generic;
using Isle.Gameplay.Character;
using Isle.Gameplay.Inventory;
using Isle.World.Objects;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Isle.UI.Prototype
{
    /// <summary>
    /// Point lights for things that glow: an active world object with <c>light_radius</c> (a lit campfire) and a
    /// player holding an item with <c>light_radius</c> (a torch). The radius is def data; the colour and
    /// flicker are presentation. Lights are created and removed as their sources come and go.
    /// </summary>
    public sealed class LightRig : MonoBehaviour
    {
        static readonly Color FireColour = new(1f, 0.72f, 0.4f);
        const float Intensity = 1.1f;
        const float FlickerAmount = 0.08f;
        const float FlickerSpeed = 7f;

        readonly Dictionary<Transform, Light2D> _lights = new();
        readonly HashSet<Transform> _seen = new();

        void LateUpdate()
        {
            _seen.Clear();

            foreach (var instance in FindObjectsByType<WorldObjectInstance>(FindObjectsSortMode.None))
                if (instance.IsActive && instance.Def != null && instance.Def.LightRadius > 0f)
                    Show(instance.transform, instance.Def.LightRadius);

            foreach (var player in FindObjectsByType<PlayerInteraction>(FindObjectsSortMode.None))
            {
                var radius = HeldLightRadius(player);
                if (radius > 0f) Show(player.transform, radius);
            }

            var stale = new List<Transform>();
            foreach (var (source, light) in _lights)
            {
                if (source != null && _seen.Contains(source)) continue;
                if (light != null) Destroy(light.gameObject);
                stale.Add(source);
            }
            foreach (var source in stale) _lights.Remove(source);
        }

        static float HeldLightRadius(PlayerInteraction player)
        {
            if (!player.TryGetComponent<InventoryNetwork>(out var inventory)) return 0f;
            var radius = 0f;
            foreach (var slot in EquipSlots.All) radius = Mathf.Max(radius, inventory.Slots.Get(slot)?.LightRadius ?? 0f);
            return radius;
        }

        void Show(Transform source, float radius)
        {
            _seen.Add(source);
            if (!_lights.TryGetValue(source, out var light) || light == null)
            {
                var go = new GameObject("Light");
                go.transform.SetParent(source, worldPositionStays: false);
                light = go.AddComponent<Light2D>();
                light.lightType = Light2D.LightType.Point;
                light.color = FireColour;
                _lights[source] = light;
            }

            // A small per-source phase offset so two fires don't flicker in lockstep.
            var flicker = 1f + Mathf.Sin(Time.time * FlickerSpeed + source.GetInstanceID()) * FlickerAmount;
            light.pointLightOuterRadius = radius * flicker;
            light.pointLightInnerRadius = radius * 0.3f;
            light.intensity = Intensity;
        }
    }
}
