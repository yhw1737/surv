using Isle.Data;
using Isle.Gameplay.Character;
using Isle.Gameplay.Feedback;
using Isle.World.Objects;
using Isle.World.Time;
using UnityEngine;

namespace Isle.Gameplay.Building
{
    /// <summary>
    /// A planted crop (SCHEMA §Crops). Growth is computed from the planting time against the world clock, never
    /// ticked, so a rest that skips the night grows it too. <c>E</c> on a ripe crop harvests
    /// <c>output.base_count</c> and clears the plot; on an unripe one it reports progress. Traits are parsed but not
    /// used, as SCHEMA says for Core.
    /// </summary>
    [RequireComponent(typeof(WorldObjectInstance))]
    public sealed class CropPlot : MonoBehaviour, IInteractable
    {
        const float MinSproutScale = 0.2f;
        const float MaxSproutScale = 0.75f;

        Transform _sprout;
        SpriteRenderer _sproutRenderer;

        public CropDef Crop { get; private set; }
        public long PlantedAtMinutes { get; private set; }

        public float Growth => CropCalculator.Growth(PlantedAtMinutes, NowMinutes(), Crop?.GrowthDays ?? 0);

        public void Initialize(CropDef crop, long plantedAtMinutes, Sprite sprout)
        {
            Crop = crop;
            PlantedAtMinutes = plantedAtMinutes;

            var go = new GameObject("Sprout");
            go.transform.SetParent(transform, worldPositionStays: false);
            _sproutRenderer = go.AddComponent<SpriteRenderer>();
            _sproutRenderer.sprite = sprout;
            _sproutRenderer.sortingOrder = 7;
            _sprout = go.transform;
        }

        static long NowMinutes() => WorldTime.Instance != null ? WorldTime.Instance.Clock.TotalMinutes : 0;

        void Update()
        {
            if (_sprout == null) return;
            var growth = Growth;
            _sprout.localScale = Vector3.one * Mathf.Lerp(MinSproutScale, MaxSproutScale, growth);
            _sproutRenderer.color = CropCalculator.IsRipe(growth) ? new Color(1f, 0.55f, 0.65f) : new Color(0.5f, 0.85f, 0.4f);
        }

        public void Interact(GameObject user)
        {
            if (Crop == null) return;
            if (!CropCalculator.IsRipe(Growth))
            {
                GameFeed.RaiseNotice("@ui.not_ripe");
                return;
            }
            if (!user.TryGetComponent<PlayerInteraction>(out var player)) return;
            player.GiveHarvest(Crop.Output.Item, Crop.Output.BaseCount, Crop.Xp);
            StructureFactory.Remove(GetComponent<WorldObjectInstance>());
        }
    }
}
