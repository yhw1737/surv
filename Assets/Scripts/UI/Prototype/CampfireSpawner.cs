using FishNet;
using Isle.Core.Util;
using Isle.Data;
using Isle.Gameplay.Building;
using Isle.Modding.Defs;
using Isle.World.Objects;
using UnityEngine;

namespace Isle.UI.Prototype
{
    /// <summary>
    /// Builds one campfire next to the spawn point once the server is up, unless the scene already has one
    /// (SampleScene carries a hand-placed Campfire from the T-051 walkthrough). Uses the same
    /// <see cref="StructureFactory"/> as player-built structures, and picks the def by tag, not id.
    /// </summary>
    public sealed class CampfireSpawner : MonoBehaviour
    {
        /// <summary>Tiles from the spawn point. Placement only — the fire's own numbers come from its def.</summary>
        static readonly Vector2 SpawnOffset = new(2f, 0f);

        /// <summary>Tag shared with <c>Vitals</c>' warmth check (SCHEMA §Station).</summary>
        const string CampfireTag = "station/campfire";

        bool _built;

        void Update()
        {
            if (_built || !InstanceFinder.IsServerStarted) return;
            _built = true;

            // Scene-placed objects (SampleScene's Campfire) carry a prefab sprite; give them their def's silhouette too.
            foreach (var existing in WorldObjectRegistry.All)
                if (existing.Def?.Visual != null && existing.TryGetComponent<SpriteRenderer>(out var renderer))
                {
                    renderer.sprite = ShapeLibrary.Sprite(existing.Def.Visual.Shape, ShapeLibrary.ParseColour(existing.Def.Visual.Color, Color.white));
                    existing.transform.localScale = Vector3.one * existing.Def.Visual.Size;
                }

            foreach (var existing in WorldObjectRegistry.All)
                if (existing.HasTag(CampfireTag)) return;

            var defs = DefRegistry.AllWithTag<WorldObjectDef>(CampfireTag);
            if (defs.Count > 0) StructureFactory.Build(defs[0], SpawnOffset);
        }
    }
}
