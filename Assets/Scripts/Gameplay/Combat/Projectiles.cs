using System.Collections.Generic;
using Isle.Core.Ids;
using Isle.Core.Util;
using Isle.Gameplay.Hunting;
using UnityEngine;

namespace Isle.Gameplay.Combat
{
    /// <summary>
    /// Arrows in flight (SYS-COMBAT-01 §Hit detection: "Ranged uses projectiles", server authority, no lag
    /// compensation). Host-side simulation like the creatures: each one flies straight, hits the first creature it
    /// passes within <see cref="HitRadiusTiles"/>, and is gone at <see cref="RangedCalculator.MaxRangeTiles"/>.
    /// </summary>
    public sealed class Projectiles : MonoBehaviour
    {
        /// <summary>How close an arrow must pass to a creature's centre to hit it. [invented]</summary>
        const float HitRadiusTiles = 0.45f;

        sealed class Arrow
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Travelled;
            public float Damage;
            public string Type;
            public System.Action<List<(NamespacedId, int)>> OnKill;
            public System.Action<float> OnDamage;
            public GameObject View;
        }

        public static Projectiles Instance { get; private set; }

        readonly List<Arrow> _arrows = new();

        public int InFlight => _arrows.Count;
        Sprite _sprite;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <param name="onKill">Called with the kill's loot, so the shooter gets it wherever they now stand.</param>
        /// <param name="onDamage">Called with the damage a hit actually dealt — the shooter's ranged XP.</param>
        public void Fire(Vector2 from, Vector2 direction, float speed, float damage, string damageType, System.Action<List<(NamespacedId, int)>> onKill, System.Action<float> onDamage = null)
        {
            _sprite ??= PlaceholderVisuals.AsSprite(PlaceholderVisuals.RoundedRect(16, 4, new Color(0.9f, 0.85f, 0.7f), cornerRadius: 1f));
            var view = new GameObject("Arrow");
            var renderer = view.AddComponent<SpriteRenderer>();
            renderer.sprite = _sprite;
            renderer.sortingOrder = 8;
            view.transform.position = from;
            view.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

            _arrows.Add(new Arrow { Position = from, Velocity = direction.normalized * speed, Damage = damage, Type = damageType ?? DamageTypes.Pierce, OnKill = onKill, OnDamage = onDamage, View = view });
        }

        void Update()
        {
            var director = CreatureDirector.Instance;
            for (var i = _arrows.Count - 1; i >= 0; i--)
            {
                var arrow = _arrows[i];
                var step = arrow.Velocity * Time.deltaTime;
                arrow.Position += step;
                arrow.Travelled += step.magnitude;
                arrow.View.transform.position = arrow.Position;

                var hit = director != null ? director.NearestCreature(arrow.Position, HitRadiusTiles) : null;
                if (hit != null)
                {
                    var loot = new List<(NamespacedId, int)>();
                    var dealt = director.DamageTyped(hit, arrow.Damage * RangedCalculator.RangeMult(arrow.Travelled), arrow.Type, loot, Hunting.KillMethod.Precise);
                    arrow.OnDamage?.Invoke(dealt);
                    if (loot.Count > 0) arrow.OnKill?.Invoke(loot);
                }

                if (hit == null && arrow.Travelled < RangedCalculator.MaxRangeTiles) continue;
                Destroy(arrow.View);
                _arrows.RemoveAt(i);
            }
        }
    }
}
