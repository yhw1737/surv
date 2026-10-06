using System;
using Isle.Core.Ids;
using UnityEngine;

namespace Isle.Gameplay.Feedback
{
    /// <summary>
    /// Server-side gameplay events for presentation to react to (toasts, hit flashes, floating numbers).
    /// Raised only after the server has applied the change, so listeners never decide anything.
    /// ponytail: a static same-process event, which reaches the HUD only on a listen-server host — the solo
    /// prototype's only mode. Phase 10 turns these into TargetRpc/ObserversRpc calls.
    /// </summary>
    public static class GameFeed
    {
        /// <summary>An item landed in a player's bag.</summary>
        public static event Action<NamespacedId, int> ItemGained;

        /// <summary>An item couldn't fit and was dropped on the ground at this position.</summary>
        public static event Action<NamespacedId, int, Vector2> ItemDropped;

        /// <summary>A creature took damage at this position.</summary>
        public static event Action<Vector2, float> CreatureHit;

        /// <summary>The player swung a weapon with this reach, hit or miss.</summary>
        public static event Action<Vector2, float> PlayerSwing;

        /// <summary>The player took damage.</summary>
        public static event Action<float> PlayerHit;

        /// <summary>A player opened a container. Typed as <c>object</c> to keep this file free of building types.</summary>
        public static event Action<object> StorageOpened;

        /// <summary>A skill earned XP; <c>newLevel</c> is the level reached when this award levelled it up, else 0.</summary>
        public static event Action<NamespacedId, float, int> XpGained;

        /// <summary>A short status line, as a language key (e.g. <c>"@ui.too_tired"</c>).</summary>
        public static event Action<string> Notice;

        public static void RaiseItemGained(NamespacedId item, int count) => ItemGained?.Invoke(item, count);
        public static void RaiseItemDropped(NamespacedId item, int count, Vector2 at) => ItemDropped?.Invoke(item, count, at);
        public static void RaiseCreatureHit(Vector2 at, float damage) => CreatureHit?.Invoke(at, damage);
        public static void RaisePlayerSwing(Vector2 at, float reach) => PlayerSwing?.Invoke(at, reach);

        /// <summary>A player's guard met a strike: blocked, parried or broken (SYS-COMBAT-01 §Melee).</summary>
        public static event Action<Vector2, Combat.BlockOutcome> Defended;

        public static void RaiseDefended(Vector2 at, Combat.BlockOutcome outcome) => Defended?.Invoke(at, outcome);
        public static void RaisePlayerHit(float damage) => PlayerHit?.Invoke(damage);
        public static void RaiseStorageOpened(object box) => StorageOpened?.Invoke(box);
        public static void RaiseXpGained(NamespacedId skill, float amount, int newLevel) => XpGained?.Invoke(skill, amount, newLevel);
        public static void RaiseNotice(string langKey) => Notice?.Invoke(langKey);
    }
}
