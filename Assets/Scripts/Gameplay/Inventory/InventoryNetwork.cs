using System;
using System.Collections.Generic;
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

        /// <summary>Base carry first, then every equipped bag's grid — the order items are given and taken in.</summary>
        public List<GridInventory> Containers()
        {
            var containers = new List<GridInventory> { Bag };
            foreach (var slot in EquipSlots.All)
                if (Slots.BagFor(slot) is { } bag) containers.Add(bag);
            return containers;
        }

        // ponytail: one in-flight request assumed — a solo player only drives one drag/click at a
        // time, and this is a LAN/listen-server game (max 4 players), so request N's ack always
        // arrives well before request N+1 can be issued. Revisit with a request id if that ever
        // breaks (e.g. real internet play added later).
        Action<bool> _pendingCallback;

        void Awake()
        {
            Bag = new GridInventory(BagWidth, BagHeight);
            Slots = new EquipSlots();
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

        /// <summary>Moves (or, with <paramref name="count"/> below the stack, splits) an item between any two of this
        /// player's own containers — indices into <see cref="Containers"/> (0 = base carry, then opened bags).</summary>
        public void RequestMove(int fromContainer, Vec2Int from, int toContainer, Vec2Int to, bool toRotated, int count, Action<bool> onResult)
        {
            _pendingCallback = ok =>
            {
                if (ok && !IsServer) ApplyMove(fromContainer, from, toContainer, to, toRotated, count);
                onResult(ok);
            };
            CmdMove(fromContainer, from.X, from.Y, toContainer, to.X, to.Y, toRotated, count);
        }

        /// <summary>Ctrl+click: the whole stack into the first spot that fits in another container.</summary>
        public void RequestQuickMove(int fromContainer, Vec2Int from, int toContainer, Action<bool> onResult)
        {
            _pendingCallback = ok =>
            {
                if (ok && !IsServer) ApplyQuickMove(fromContainer, from, toContainer);
                onResult(ok);
            };
            CmdQuickMove(fromContainer, from.X, from.Y, toContainer);
        }

        public void RequestEquipFrom(int container, Vec2Int pos, string slot, Action<bool> onResult)
        {
            _pendingCallback = ok =>
            {
                if (ok && !IsServer) ApplyEquipFrom(container, pos, slot);
                onResult(ok);
            };
            CmdEquipFrom(container, pos.X, pos.Y, slot);
        }

        public void RequestUnequipInto(string slot, int container, Vec2Int pos, bool rotated, Action<bool> onResult)
        {
            _pendingCallback = ok =>
            {
                if (ok && !IsServer) ApplyUnequipInto(slot, container, pos, rotated);
                onResult(ok);
            };
            CmdUnequipInto(slot, container, pos.X, pos.Y, rotated);
        }

        /// <summary>Drops <paramref name="count"/> of a stack (all of it by default) on the ground at the player's feet.</summary>
        public void RequestDrop(int container, Vec2Int pos, int count, Action<bool> onResult)
        {
            _pendingCallback = ok =>
            {
                if (ok && !IsServer) ApplyDrop(container, pos, count);
                onResult(ok);
            };
            CmdDrop(container, pos.X, pos.Y, count);
        }

        /// <summary>Takes off what a slot holds and drops it (a bag only when it's empty).</summary>
        public void RequestDropEquipped(string slot, Action<bool> onResult)
        {
            _pendingCallback = ok =>
            {
                if (ok && !IsServer) ApplyDropEquipped(slot);
                onResult(ok);
            };
            CmdDropEquipped(slot);
        }

        [ServerRpc]
        void CmdDrop(int c, int x, int y, int count) => TargetResult(Owner, ApplyDrop(c, new Vec2Int(x, y), count));

        [ServerRpc]
        void CmdDropEquipped(string slot) => TargetResult(Owner, ApplyDropEquipped(slot));

        bool ApplyDrop(int container, Vec2Int pos, int count)
        {
            var source = ContainerAt(container);
            var placement = source?.PlacementAt(pos);
            if (!placement.HasValue || count <= 0) return false;
            var p = placement.Value;
            var dropped = Math.Min(count, p.Count);
            source.Remove(p);
            if (dropped < p.Count) source.TryPlace(p.Item, p.Position, p.Rotated, p.Count - dropped, p.Wear);
            // Only the server places the pile; a remote client's mirror just removes it from its own copy.
            if (IsServer)
            {
                // A carried carcass goes back on the ground as a carcass, ready to butcher.
                if (p.Wear?.Carcass is { } body && Hunting.CreatureDirector.Instance != null)
                    Hunting.CreatureDirector.Instance.PutCarcass(body, transform.position);
                else
                    LootPiles.Drop(transform.position, new[] { new LootEntry(p.Item, dropped, p.Wear) });
                Feedback.GameFeed.RaiseItemDropped(p.Item.Id, dropped, transform.position);
            }
            return true;
        }

        bool ApplyDropEquipped(string slot)
        {
            var item = Slots.Get(slot);
            if (item == null || !Slots.Unequip(slot, out var wear)) return false;
            if (IsServer)
            {
                LootPiles.Drop(transform.position, new[] { new LootEntry(item, 1, wear) });
                Feedback.GameFeed.RaiseItemDropped(item.Id, 1, transform.position);
            }
            return true;
        }

        [ServerRpc]
        void CmdMove(int fc, int fx, int fy, int tc, int tx, int ty, bool toRotated, int count) =>
            TargetResult(Owner, ApplyMove(fc, new Vec2Int(fx, fy), tc, new Vec2Int(tx, ty), toRotated, count));

        [ServerRpc]
        void CmdQuickMove(int fc, int fx, int fy, int tc) => TargetResult(Owner, ApplyQuickMove(fc, new Vec2Int(fx, fy), tc));

        [ServerRpc]
        void CmdEquipFrom(int c, int x, int y, string slot) => TargetResult(Owner, ApplyEquipFrom(c, new Vec2Int(x, y), slot));

        [ServerRpc]
        void CmdUnequipInto(string slot, int c, int x, int y, bool rotated) =>
            TargetResult(Owner, ApplyUnequipInto(slot, c, new Vec2Int(x, y), rotated));

        /// <summary>A container by index, or null for a bad index — never trust client values.</summary>
        GridInventory ContainerAt(int index)
        {
            var containers = Containers();
            return index >= 0 && index < containers.Count ? containers[index] : null;
        }

        bool ApplyMove(int fromContainer, Vec2Int from, int toContainer, Vec2Int to, bool toRotated, int count)
        {
            var source = ContainerAt(fromContainer);
            var target = ContainerAt(toContainer);
            var placement = source?.PlacementAt(from);
            if (target == null || placement == null || count <= 0) return false;
            var p = placement.Value;

            if (count < p.Count) return source.TrySplit(p, count, target, to, toRotated);

            source.Remove(p);
            if (target.TryPlace(p.Item, to, toRotated, p.Count, p.Wear)) return true;
            source.TryPlace(p.Item, p.Position, p.Rotated, p.Count, p.Wear); // put it back exactly where it was
            return false;
        }

        bool ApplyQuickMove(int fromContainer, Vec2Int from, int toContainer)
        {
            var source = ContainerAt(fromContainer);
            var target = ContainerAt(toContainer);
            var placement = source?.PlacementAt(from);
            return target != null && target != source && placement.HasValue && source.TryMoveTo(target, placement.Value);
        }

        bool ApplyEquipFrom(int container, Vec2Int pos, string slot)
        {
            var source = ContainerAt(container);
            var placement = source?.PlacementAt(pos);
            if (!placement.HasValue || placement.Value.Item.EquipSlot != slot) return false;
            // Whatever the slot held goes back where the new item came from (swap), or the equip doesn't happen.
            var previous = Slots.Get(slot);
            var p = placement.Value;
            if (previous != null)
            {
                if (Slots.BagFor(slot) == source || !Slots.Unequip(slot, out var previousWear)) return false;
                source.Remove(p);
                if (!Slots.TryEquip(slot, p.Item, p.Wear) || !InventoryOps.TryGive(new List<GridInventory> { source }, previous, 1, previousWear))
                {
                    Slots.Unequip(slot);
                    Slots.TryEquip(slot, previous, previousWear);
                    source.TryPlace(p.Item, p.Position, p.Rotated, p.Count, p.Wear);
                    return false;
                }
                return true;
            }
            if (!Slots.TryEquip(slot, p.Item, p.Wear)) return false;
            source.Remove(p);
            return true;
        }

        bool ApplyUnequipInto(string slot, int container, Vec2Int pos, bool rotated)
        {
            var target = ContainerAt(container);
            var item = Slots.Get(slot);
            // A bag can't be unequipped into its own grid — the grid goes away with it.
            if (target == null || item == null || Slots.BagFor(slot) == target || !Slots.Unequip(slot, out var wear)) return false;
            if (target.TryPlace(item, pos, rotated, 1, wear)) return true;
            Slots.TryEquip(slot, item, wear);
            return false;
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

            var p = placement.Value;
            Bag.Remove(p);
            if (Bag.TryPlace(p.Item, to, toRotated, p.Count, p.Wear)) return true;

            Bag.TryPlace(p.Item, p.Position, p.Rotated, p.Count, p.Wear);
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
            if (!placement.HasValue || !Slots.TryEquip(slot, placement.Value.Item, placement.Value.Wear)) return false;

            Bag.Remove(placement.Value);
            return true;
        }

        /// <summary>Mirrors <c>EquipDragHandler.UnequipInto</c> — unequip first (fails safely if the
        /// slot's own bag still holds items), then roll back if the bag has no room for it.</summary>
        bool ApplyUnequip(string slot, Vec2Int bagPos, bool bagRotated)
        {
            var item = Slots.Get(slot);
            if (item == null || !Slots.Unequip(slot, out var wear)) return false;
            if (Bag.TryPlace(item, bagPos, bagRotated, 1, wear)) return true;

            Slots.TryEquip(slot, item, wear);
            return false;
        }
    }
}
