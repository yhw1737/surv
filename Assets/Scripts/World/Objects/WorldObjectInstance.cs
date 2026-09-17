using System;
using FishNet.Object;
using Isle.Core.Ids;
using Isle.Data;
using Isle.Modding.Defs;
using UnityEngine;

namespace Isle.World.Objects
{
    /// <summary>
    /// Live counterpart to the save file's <see cref="Isle.World.Chunks.WorldObject"/> record —
    /// resolves a placed prefab's def and tracks the on/off state a station-type interaction (e.g.
    /// <c>Isle.Gameplay.World.CampfireInteraction</c>) toggles. One per placed instance, spawned as
    /// a <c>NetworkObject</c> like a player.
    /// </summary>
    public sealed class WorldObjectInstance : NetworkBehaviour
    {
        [Tooltip("e.g. \"isle:campfire\" — resolved against DefRegistry on Awake.")]
        [SerializeField] string _defId;

        public WorldObjectDef Def { get; private set; }

        /// <summary>Whether this object is currently "on" — lit, running, etc. Off by default: a
        /// freshly placed campfire needs lighting, same as a real one would (PROJECT_STATE.md
        /// §Decided without a spec — SYS-SURV-01 only specs the fire bonus itself, not a lit state).
        /// Server-only for now, no client sync — same "nothing client-facing reads it yet" gap as
        /// <see cref="Isle.World.Weather.WeatherController"/>, since there's no visual to drive.</summary>
        public bool IsActive { get; set; }

        void Awake()
        {
            if (!NamespacedId.TryParse(_defId, out var id, out var error))
            {
                Debug.LogError($"{name}: invalid world object def id \"{_defId}\": {error}");
                return;
            }
            if (!DefRegistry.TryGet<WorldObjectDef>(id, out var def))
            {
                Debug.LogError($"{name}: unknown world object def \"{_defId}\".");
                return;
            }
            Def = def;
        }

        public bool HasTag(string tag) => Def?.Tags != null && Array.IndexOf(Def.Tags, tag) >= 0;

        void OnEnable() => WorldObjectRegistry.Register(this);
        void OnDisable() => WorldObjectRegistry.Unregister(this);
    }
}
