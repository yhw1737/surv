using System.Collections;
using System.Collections.Generic;
using Isle.Data;
using FishNet.Object;
using Isle.Gameplay.Inventory;
using Isle.Networking;
using UnityEngine;

namespace Isle.Gameplay.Character
{
    /// <summary>
    /// SYS-SURV-01 §Death, server authority. <see cref="Vitals"/> calls <see cref="Die"/> when
    /// Health hits 0.
    /// <para>
    /// Scope note: this drops the inventory (via <see cref="InventoryNetwork"/>'s existing
    /// <c>Bag</c>/<c>Slots</c>, T-045) and runs the 15 s respawn timer — that much SYS-SURV-01
    /// specifies exactly. "Respawn at the last shelter" and "an ally recovering your body returns
    /// items immediately" both need a corpse/loot-pile <c>WorldObject</c> with a network identity
    /// and reach model, which doesn't exist (T-045 remarks — <c>WorldObject</c> is still a bare
    /// def-id + position placeholder, Phase 6). Until then: dropped items are simply gone (not
    /// recoverable), and respawn position falls back to <see cref="SpawnPosition"/> (world origin)
    /// unless something has called <see cref="SetLastShelter"/> — spec's own §Open questions lists
    /// "how respawn shelters are designated" as unresolved, so there's nothing to build that against
    /// yet. See PROJECT_STATE.md §Decided without a spec.
    /// </para>
    /// </summary>
    public sealed class DeathHandler : NetworkBehaviour
    {
        public const float RespawnDelaySeconds = 15f;

        static readonly Vector3 SpawnPosition = Vector3.zero;

        public bool IsDead { get; private set; }

        Vector3 _lastShelter = SpawnPosition;

        public Vector3 LastShelter => _lastShelter;

        /// <summary>Set when the player lights or rests at a campfire (prototype answer to SYS-SURV-01's open
        /// question "how respawn shelters are designated" — PROJECT_STATE.md §Decided without a spec).</summary>
        public void SetLastShelter(Vector3 position) => _lastShelter = position;

        public void Die()
        {
            if (!IsServer || IsDead) return;
            IsDead = true;
            if (TryGetComponent<PlayerMovement>(out var movement)) movement.Frozen = true;

            if (TryGetComponent<InventoryNetwork>(out var inventory))
            {
                LootPiles.Drop(transform.position, Everything(inventory));
                inventory.Bag.Clear();
                inventory.Slots.Clear();
            }

            StartCoroutine(RespawnAfterDelay());
        }

        /// <summary>Bag, equipped items, and the contents of any equipped bags — "entire inventory including
        /// equipment" (SYS-SURV-01 §Death).</summary>
        static List<(ItemDef Item, int Count)> Everything(InventoryNetwork inventory)
        {
            var items = new List<(ItemDef, int)>();
            foreach (var placed in inventory.Bag.Placements) items.Add((placed.Item, placed.Count));
            foreach (var slot in EquipSlots.All)
            {
                var equipped = inventory.Slots.Get(slot);
                if (equipped != null) items.Add((equipped, 1));
                var bag = inventory.Slots.BagFor(slot);
                if (bag == null) continue;
                foreach (var placed in bag.Placements) items.Add((placed.Item, placed.Count));
            }
            return items;
        }

        IEnumerator RespawnAfterDelay()
        {
            yield return new WaitForSeconds(RespawnDelaySeconds);
            Respawn();
        }

        void Respawn()
        {
            transform.position = _lastShelter;
            IsDead = false;
            if (TryGetComponent<PlayerMovement>(out var movement)) movement.Frozen = false;
            if (TryGetComponent<Vitals>(out var vitals)) vitals.ResetOnRespawn();
        }
    }
}
