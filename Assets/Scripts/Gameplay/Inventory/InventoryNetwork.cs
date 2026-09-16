using System;
using FishNet.Connection;
using FishNet.Object;
using Isle.Core;
using Isle.Data;

namespace Isle.Gameplay.Inventory
{
    /// <summary>
    /// T-045: server authority for one player's own bag + equip slots (SYS-NET-01 "Inventory" row —
    /// server-only, no prediction; SYS-INV-01 §Network). One instance per player <c>NetworkObject</c>
    /// (see <c>player_rig_placeholder.prefab</c>, alongside <c>PlayerMovement</c>).
    /// <para>
    /// Client calls a <c>Request...</c> method; the server validates against its own authoritative
    /// <see cref="Bag"/>/<see cref="Slots"/> and applies, then a <see cref="TargetRpc"/> tells the
    /// owning client the outcome. <see cref="ServerRpcAttribute"/>'s default <c>RequireOwnership</c>
    /// gives the "ownership" check for free. "Reach distance" and "container access" (SYS-NET-01
    /// §Server validation) don't apply here — both containers are the player's own body, always in
    /// reach; "weight limit" isn't enforced as a hard block either, since SYS-INV-01 §Weight never
    /// specifies one (it's a continuous movement-speed penalty with no consumer wired up yet, see
    /// PROJECT_STATE.md §Decided without a spec).
    /// </para>
    /// <para>
    /// No snapshot sync back to the client: a player's own bag/slots are only ever mutated by that
    /// same player's own requests, so as long as every mutation goes through this gate (never a
    /// direct local call once this is attached — Absolute Rule 2), the server's and the owning
    /// client's copies can never drift. The <c>!IsServer</c> guard in each <c>Request...</c>'s result
    /// callback exists only so a listen-server host (where server and owning client share this exact
    /// component instance) doesn't apply the same mutation twice.
    /// </para>
    /// <para>
    /// Scoped to the player's own bag and equip slots — a crate or warehouse is a <c>WorldObject</c>,
    /// and that type is still a bare def-id + position placeholder with no <c>NetworkObject</c> or
    /// reach/ownership model (PROJECT_STATE.md §Decided without a spec, T-032). Moving items into or
    /// out of one under authority is Phase 6 scope once world objects have a network identity to
    /// validate against; until then that interaction stays the local-only one T-042/043/044 shipped
    /// (see <c>DragHandler</c>/<c>EquipDragHandler</c>'s <c>Network</c> checks).
    /// </para>
    /// </summary>
    public sealed class InventoryNetwork : NetworkBehaviour
    {
        // SYS-INV-01 §Containers: "Base carry 6x3, always present". A bag-type equip item opens its
        // own separate GridInventory via EquipSlots.TryEquip, same as local (non-networked) play.
        const int BagWidth = 6;
        const int BagHeight = 3;

        public GridInventory Bag { get; private set; }
        public EquipSlots Slots { get; private set; }

        // ponytail: one in-flight request assumed — a solo player only drives one drag/click at a
        // time, and this is a LAN/listen-server game (max 4 players), so request N's ack always
        // arrives well before request N+1 can be issued. Revisit with a request id if that ever
        // breaks (e.g. real internet play added later).
        Action<bool> _pendingCallback;

        void Awake()
        {
            Bag = new GridInventory(BagWidth, BagHeight);
            Slots = new EquipSlots();
            SeedTestItems();
        }

        /// <summary>Test-support fixtures only — throwaway <see cref="ItemDef"/>s, same reasoning
        /// as <c>InventoryDemo.cs</c>'s sample items (no real item defs exist yet, Absolute Rule 1
        /// is about game content, not test fixtures). Without this the Networked Bag starts and
        /// stays empty, and half the T-045 manual-test checklist (move/split/equip/drag-out on an
        /// existing item) has nothing to act on. Runs identically in <c>Awake()</c> on both the
        /// server's and the owning client's instance of this component, so both start with the same
        /// items without a network round trip — same "no snapshot sync back" reasoning as the class
        /// remarks above. Remove once real gameplay (loot pickups, etc.) populates this bag instead.</summary>
        void SeedTestItems()
        {
            var gear = new ItemDef
            {
                Name = "@item.debug_net_gear", Grid = new GridSize { W = 1, H = 1 }, Weight = 1f,
                Tags = new[] { "gear" }, EquipSlot = "main_hand",
            };
            var stackable = new ItemDef
            {
                Name = "@item.debug_net_stack", Grid = new GridSize { W = 1, H = 1 }, Weight = 0.1f,
                Tags = new[] { "consumable" },
            };

            Bag.TryPlace(gear, new Vec2Int(0, 0));
            Bag.TryPlace(stackable, new Vec2Int(1, 0), count: 3);
        }

        public void RequestMoveWithinBag(Vec2Int from, Vec2Int to, bool toRotated, Action<bool> onResult)
        {
            _pendingCallback = ok =>
            {
                if (ok && !IsServer) ApplyMoveWithinBag(from, to, toRotated);
                onResult(ok);
            };
            CmdMoveWithinBag(from.X, from.Y, to.X, to.Y, toRotated);
        }

        public void RequestSplitWithinBag(Vec2Int from, int splitCount, Vec2Int to, bool toRotated, Action<bool> onResult)
        {
            _pendingCallback = ok =>
            {
                if (ok && !IsServer) ApplySplitWithinBag(from, splitCount, to, toRotated);
                onResult(ok);
            };
            CmdSplitWithinBag(from.X, from.Y, splitCount, to.X, to.Y, toRotated);
        }

        public void RequestEquip(Vec2Int bagPos, string slot, Action<bool> onResult)
        {
            _pendingCallback = ok =>
            {
                if (ok && !IsServer) ApplyEquip(bagPos, slot);
                onResult(ok);
            };
            CmdEquip(bagPos.X, bagPos.Y, slot);
        }

        public void RequestUnequip(string slot, Vec2Int bagPos, bool bagRotated, Action<bool> onResult)
        {
            _pendingCallback = ok =>
            {
                if (ok && !IsServer) ApplyUnequip(slot, bagPos, bagRotated);
                onResult(ok);
            };
            CmdUnequip(slot, bagPos.X, bagPos.Y, bagRotated);
        }

        [ServerRpc]
        void CmdMoveWithinBag(int fx, int fy, int tx, int ty, bool toRotated) =>
            TargetResult(Owner, ApplyMoveWithinBag(new Vec2Int(fx, fy), new Vec2Int(tx, ty), toRotated));

        [ServerRpc]
        void CmdSplitWithinBag(int fx, int fy, int splitCount, int tx, int ty, bool toRotated) =>
            TargetResult(Owner, ApplySplitWithinBag(new Vec2Int(fx, fy), splitCount, new Vec2Int(tx, ty), toRotated));

        [ServerRpc]
        void CmdEquip(int bx, int by, string slot) =>
            TargetResult(Owner, ApplyEquip(new Vec2Int(bx, by), slot));

        [ServerRpc]
        void CmdUnequip(string slot, int bx, int by, bool bagRotated) =>
            TargetResult(Owner, ApplyUnequip(slot, new Vec2Int(bx, by), bagRotated));

        [TargetRpc]
        void TargetResult(NetworkConnection conn, bool ok)
        {
            _pendingCallback?.Invoke(ok);
            _pendingCallback = null;
        }

        /// <summary>Mirrors <c>DragHandler.MoveWithinSameView</c>'s local-play logic exactly — same
        /// remove/place/rollback shape, just running here instead of directly against the UI's grid.</summary>
        bool ApplyMoveWithinBag(Vec2Int from, Vec2Int to, bool toRotated)
        {
            var placement = Bag.PlacementAt(from);
            if (placement == null) return false;

            Bag.Remove(placement.Value);
            if (Bag.TryPlace(placement.Value.Item, to, toRotated, placement.Value.Count)) return true;

            Bag.TryPlace(placement.Value.Item, placement.Value.Position, placement.Value.Rotated, placement.Value.Count);
            return false;
        }

        bool ApplySplitWithinBag(Vec2Int from, int splitCount, Vec2Int to, bool toRotated)
        {
            var placement = Bag.PlacementAt(from);
            return placement.HasValue && Bag.TrySplit(placement.Value, splitCount, Bag, to, toRotated);
        }

        /// <summary>Mirrors <c>EquipSlotView.TryEquipDrop</c> — equip, then remove from the bag only
        /// once the slot actually took it.</summary>
        bool ApplyEquip(Vec2Int bagPos, string slot)
        {
            var placement = Bag.PlacementAt(bagPos);
            if (!placement.HasValue || !Slots.TryEquip(slot, placement.Value.Item)) return false;

            Bag.Remove(placement.Value);
            return true;
        }

        /// <summary>Mirrors <c>EquipDragHandler.UnequipInto</c> — unequip first (fails safely if the
        /// slot's own bag still holds items), then roll back if the bag has no room for it.</summary>
        bool ApplyUnequip(string slot, Vec2Int bagPos, bool bagRotated)
        {
            var item = Slots.Get(slot);
            if (item == null || !Slots.Unequip(slot)) return false;
            if (Bag.TryPlace(item, bagPos, bagRotated)) return true;

            Slots.TryEquip(slot, item);
            return false;
        }
    }
}
