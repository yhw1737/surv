using System.Collections.Generic;
using System.Linq;
using FishNet;
using Isle.Core;
using Isle.Core.Ids;
using Isle.Core.Util;
using Isle.Data;
using Isle.Gameplay.Character;
using Isle.Gameplay.Combat;
using Isle.Modding.Defs;
using Isle.World.Generation;
using Isle.World.Island;
using Isle.World.Time;
using UnityEngine;

namespace Isle.Gameplay.Hunting
{
    /// <summary>One live creature. Plain state; <see cref="CreatureDirector"/> owns its AI and view.</summary>
    public sealed class Creature
    {
        public CreatureDef Def { get; init; }
        public float Weight { get; init; }
        public Vector2 Home { get; init; }
        public Vector2 Position { get; set; }
        public float Health { get; set; }
        public float MaxHealth { get; init; }

        /// <summary>For the hit flash and the HP bar — presentation reads it, nothing else does.</summary>
        public float LastHitAt { get; set; } = float.NegativeInfinity;
        public CreatureState State { get; set; }
        public Vector2 WanderTarget { get; set; }
        public float NextWanderAt { get; set; }
        public float NextStrikeAt { get; set; }

        /// <summary>When the strike being wound up lands; negative when not winding up (SYS-COMBAT-01 §Melee tell).</summary>
        public float StrikeLandsAt { get; set; } = -1f;

        /// <summary>A parried creature reels until this time: no moving, no striking.</summary>
        public float StaggeredUntil { get; set; } = float.NegativeInfinity;

        public bool IsStaggered => Time.time < StaggeredUntil;
        public bool IsWindingUp => StrikeLandsAt >= 0f;
        public float AlertStartedAt { get; set; }
        public GameObject View { get; init; }
        public SpriteRenderer Renderer { get; init; }

        /// <summary>Body radius in tiles at this individual's weight — reach is measured to this edge.</summary>
        public float Radius { get; init; }
    }

    /// <summary>
    /// SYS-COMBAT-01 §Creature AI, prototype scope: spawns creatures on the island from their defs'
    /// <c>spawn</c> blocks, runs each one's <see cref="CreatureBrain"/> state, and resolves player
    /// strikes. Server-side only (Absolute Rule 2) — a solo host is the server, so this runs for the one
    /// player in the process. Not networked: a second player won't see these yet (Phase 10 work).
    /// </summary>
    public sealed class CreatureDirector : MonoBehaviour
    {
        /// <summary>Wander radius around the spawn point, in tiles. Presentation-free behaviour
        /// shape, not content — the creature's own numbers all come from its def.</summary>
        const float WanderRadiusTiles = 4f;
        const float WanderIntervalSeconds = 3f;

        public static CreatureDirector Instance { get; private set; }

        readonly List<Creature> _creatures = new();
        readonly Dictionary<CreatureDef, (int Target, IReadOnlyList<Vec2Int> Tiles)> _population = new();
        bool _spawned;
        float _nextRepopulateAt;

        /// <summary>Seconds between repopulation checks. One creature per def per check, so a hunted-out area
        /// refills over minutes, not instantly. [invented] — no spec covers creature respawn.</summary>
        const float RepopulateSeconds = 30f;

        /// <summary>Creatures further than this from every player are not simulated or drawn (the map holds hundreds).
        /// [invented] — comfortably beyond the screen and the 7-tile label range.</summary>
        const float SimulationRadiusTiles = 60f;

        /// <summary>A replacement never appears this close to a player, so nothing pops in on screen.</summary>
        const float RepopulateMinDistanceTiles = 15f;

        public IReadOnlyList<Creature> Creatures => _creatures;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (!_spawned && IslandWorld.Instance != null && IslandWorld.Instance.Island != null)
            {
                Spawn(IslandWorld.Instance);
                _spawned = true;
            }
            if (!InstanceFinder.IsServerStarted || !_spawned) return;

            var players = PlayerInteraction.All;
            foreach (var creature in _creatures)
            {
                // Only creatures near a player think and show; the rest wait, frozen, until someone comes by.
                NearestPlayer(creature.Position, players, out var distance);
                var awake = distance <= SimulationRadiusTiles;
                if (creature.View.activeSelf != awake) creature.View.SetActive(awake);
                if (awake) Think(creature, players);
            }

            if (Time.time >= _nextRepopulateAt)
            {
                _nextRepopulateAt = Time.time + RepopulateSeconds;
                Repopulate(players);
            }
        }

        /// <summary>Strikes the nearest creature within <paramref name="reachTiles"/>. Returns true when
        /// something was hit; <paramref name="loot"/> is filled only when the hit killed it.</summary>
        /// <returns>Damage dealt, or a negative number when nothing was in reach.</returns>
        public float TryStrike(Vector2 from, float reachTiles, float damage, List<(NamespacedId Item, int Count)> loot)
        {
            var target = NearestCreature(from, reachTiles);
            return target == null ? -1f : Damage(target, damage, loot);
        }

        /// <summary>Nearest live creature whose body edge is within <paramref name="radiusTiles"/>, or null.</summary>
        public Creature NearestCreature(Vector2 from, float radiusTiles)
        {
            Creature target = null;
            var best = radiusTiles;
            foreach (var creature in _creatures)
            {
                var distance = BodyReach.SurfaceDistance(Vector2.Distance(from, creature.Position), creature.Radius);
                if (distance > best) continue;
                best = distance;
                target = creature;
            }
            return target;
        }

        /// <summary>Applies damage from any source (melee or a projectile). A survivor reacts — prey flees, the
        /// rest fight back; a kill removes it and adds its <c>butcher.yields</c> to <paramref name="loot"/>.</summary>
        /// <returns>Damage actually taken — capped at the HP it had, so overkill earns no combat XP.</returns>
        public float Damage(Creature target, float damage, List<(NamespacedId Item, int Count)> loot)
        {
            if (!_creatures.Contains(target)) return 0f;
            var dealt = Mathf.Min(damage, Mathf.Max(0f, target.Health));
            target.Health -= damage;
            target.LastHitAt = Time.time;
            Feedback.GameFeed.RaiseCreatureHit(target.Position, damage);
            if (target.Health > 0f)
            {
                var prey = target.Def.Ai.Value == CreatureBrain.Skittish || target.Def.Ai.Value == CreatureBrain.AlertThenFlee;
                target.State = prey ? CreatureState.Flee : CreatureState.Engage;
                return dealt;
            }

            _creatures.Remove(target);
            Destroy(target.View);
            if (target.Def.Butcher?.Yields != null)
                foreach (var yield in target.Def.Butcher.Yields)
                    loot.Add((yield.Item, yield.Count > 0 ? yield.Count : 1));
            return dealt;
        }

        void Spawn(IslandWorld world)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            SpawnAll(world);
            Debug.Log($"[Isle] {_creatures.Count} creatures spawned in {timer.ElapsedMilliseconds} ms");
        }

        void SpawnAll(IslandWorld world)
        {
            var defs = DefRegistry.All<CreatureDef>();
            for (var i = 0; i < defs.Count; i++)
            {
                var def = defs[i];
                if (def.Spawn == null) continue;
                var tiles = ResourcePlacer.Roll(world.Island, def.Spawn, world.Seed, salt: 5000 + i)
                    .Where(t => world.WaterAt(t) == null).ToList();
                _population[def] = (tiles.Count, tiles);
                foreach (var tile in tiles) _creatures.Add(Create(def, IslandWorld.TileToWorld(tile)));
            }
        }

        /// <summary>Tops each def back up towards its starting count, one at a time, at one of its own spawn
        /// tiles that no player is near.</summary>
        void Repopulate(IReadOnlyList<PlayerInteraction> players)
        {
            foreach (var (def, (target, tiles)) in _population)
            {
                if (tiles.Count == 0 || CountOf(def) >= target) continue;
                var position = IslandWorld.TileToWorld(tiles[Random.Range(0, tiles.Count)]);
                NearestPlayer(position, players, out var distance);
                if (distance < RepopulateMinDistanceTiles) continue;
                _creatures.Add(Create(def, position));
            }
        }

        int CountOf(CreatureDef def)
        {
            var count = 0;
            foreach (var creature in _creatures)
                if (creature.Def == def) count++;
            return count;
        }

        Creature Create(CreatureDef def, Vector2 position)
        {
            var weight = RollWeight(def);
            var radius = BodyReach.RadiusForWeight(def.Combat?.BodyRadiusTiles ?? DefaultBodyRadius, weight, def.WeightDist?.Mean ?? weight);
            var view = CreateView(def, radius);
            view.transform.position = position;
            return new Creature
            {
                Def = def,
                Weight = weight,
                Home = position,
                Position = position,
                Health = weight * def.HealthPerKg,
                MaxHealth = weight * def.HealthPerKg,
                WanderTarget = position,
                View = view,
                Radius = radius,
                Renderer = view.GetComponent<SpriteRenderer>(),
            };
        }

        static float RollWeight(CreatureDef def)
        {
            var dist = def.WeightDist;
            var u1 = Mathf.Max(Random.value, 1e-6f);
            return WeightRoll.Sample(dist.Mean, dist.Sigma, dist.Min, dist.Max, u1, Random.value);
        }

        /// <summary>Body radius for a def without one. [invented]</summary>
        const float DefaultBodyRadius = 0.4f;

        /// <summary>Presentation follows the gameplay radius: the silhouette is drawn to cover the body.</summary>
        static GameObject CreateView(CreatureDef def, float radius)
        {
            var view = new GameObject(def.Id.Name);
            view.hideFlags = HideFlags.HideInHierarchy; // hundreds of these; see IslandWorld's node views
            var renderer = view.AddComponent<SpriteRenderer>();
            var colour = ShapeLibrary.ParseColour(def.Visual?.Color, PlaceholderVisuals.ColorForTags(def.Tags));
            renderer.sprite = ShapeLibrary.Sprite(def.Visual?.Shape, colour);
            renderer.sortingOrder = 5;
            view.transform.localScale = Vector3.one * (radius * 2.2f);
            return view;
        }

        void Think(Creature creature, IReadOnlyList<PlayerInteraction> players)
        {
            var combat = creature.Def.Combat;
            // No combat block means a passive creature: it stays put and never reacts.
            if (combat == null) return;

            // Asleep outside its active hours, unless something just hit it.
            if (!IsAwakeNow(creature))
            {
                creature.State = CreatureState.Idle;
                if (creature.Renderer != null) creature.Renderer.color = SleepTint;
                return;
            }

            if (creature.IsStaggered)
            {
                creature.StrikeLandsAt = -1f;
                creature.View.transform.position = creature.Position;
                Flash(creature);
                return;
            }

            var target = NearestPlayer(creature.Position, players, out var distance);
            var preset = creature.Def.Ai.IsValid ? creature.Def.Ai.Value : null;
            var previous = creature.State;
            creature.State = CreatureBrain.Next(creature.State, preset, distance, combat.VisionTiles,
                Time.time - creature.AlertStartedAt, combat.AlertSeconds);
            if (creature.State == CreatureState.Alert && previous != CreatureState.Alert) creature.AlertStartedAt = Time.time;

            switch (creature.State)
            {
                case CreatureState.Alert:
                    // Stands still and watches; only the state change matters here.
                    break;
                case CreatureState.Flee:
                    Step(creature, (creature.Position - (Vector2)target.transform.position).normalized, combat.ChaseSpeed);
                    break;
                case CreatureState.Engage:
                    if (creature.IsWindingUp)
                    {
                        // Committed: it stands and lands the strike when the tell ends — on whoever is still in reach.
                        if (Time.time >= creature.StrikeLandsAt) LandStrike(creature, target, distance, combat);
                    }
                    else if (!BodyReach.InReach(distance, creature.Radius, combat.AttackRangeTiles))
                        Step(creature, ((Vector2)target.transform.position - creature.Position).normalized, combat.ChaseSpeed);
                    else if (Time.time >= creature.NextStrikeAt)
                    {
                        creature.StrikeLandsAt = Time.time + combat.WindupSeconds;
                        if (combat.WindupSeconds <= 0f) LandStrike(creature, target, distance, combat);
                    }
                    break;
                default:
                    if (CreatureBrain.IsStationary(preset)) break;
                    Wander(creature, combat.MoveSpeed);
                    break;
            }
            creature.View.transform.position = creature.Position;
            Flash(creature);
        }

        /// <summary>Extra tiles past attack range a wound-up strike still connects — stepping back at the last instant
        /// dodges, a shuffle doesn't. [invented]</summary>
        const float StrikeLeewayTiles = 0.3f;

        static void LandStrike(Creature creature, PlayerInteraction target, float distance, CreatureCombatSpec combat)
        {
            creature.StrikeLandsAt = -1f;
            creature.NextStrikeAt = Time.time + combat.AttackIntervalSeconds;
            if (target == null || !BodyReach.InReach(distance, creature.Radius, combat.AttackRangeTiles + StrikeLeewayTiles)) return;
            if (target.ReceiveCreatureStrike(creature.Position, combat.Damage) == BlockOutcome.Parried)
                creature.StaggeredUntil = Time.time + MeleeDefense.ParryStaggerSeconds;
        }

        /// <summary>Seconds a hit keeps a sleeping creature awake.</summary>
        const float WokenSeconds = 10f;
        static readonly Color SleepTint = new(0.6f, 0.6f, 0.75f);

        static bool IsAwakeNow(Creature creature)
        {
            if (Time.time - creature.LastHitAt < WokenSeconds) return true;
            var clock = WorldTime.Instance != null ? WorldTime.Instance.Clock : null;
            return clock == null || CreatureBrain.IsAwake(creature.Def.Spawn?.Time, clock.Phase.ToString().ToLowerInvariant());
        }

        /// <summary>Presentation: flashes red for a moment after a hit.</summary>
        const float FlashSeconds = 0.12f;
        static readonly Color WindupTint = new(1f, 0.75f, 0.2f);
        static readonly Color StaggerTint = new(0.65f, 0.75f, 1f);

        static void Flash(Creature creature)
        {
            if (creature.Renderer == null) return;
            // The fill colour is baked into the texture, so the renderer tint is white at rest and red on a hit.
            if (Time.time - creature.LastHitAt < FlashSeconds) creature.Renderer.color = new Color(1f, 0.3f, 0.3f);
            else if (creature.IsStaggered) creature.Renderer.color = StaggerTint;
            else if (creature.IsWindingUp) creature.Renderer.color = Color.Lerp(Color.white, WindupTint, 0.5f + 0.5f * Mathf.Sin(Time.time * 40f));
            else creature.Renderer.color = Color.white;
        }

        void Wander(Creature creature, float speed)
        {
            if (Time.time >= creature.NextWanderAt)
            {
                creature.NextWanderAt = Time.time + WanderIntervalSeconds;
                var angle = Random.value * Mathf.PI * 2f;
                creature.WanderTarget = creature.Home + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (Random.value * WanderRadiusTiles);
            }
            var toTarget = creature.WanderTarget - creature.Position;
            if (toTarget.sqrMagnitude > 0.01f) Step(creature, toTarget.normalized, speed);
            creature.View.transform.position = creature.Position;
        }

        /// <summary>Moves on land only — a creature never walks into the sea.</summary>
        static void Step(Creature creature, Vector2 direction, float speed)
        {
            var next = creature.Position + direction * speed * Time.deltaTime;
            var world = IslandWorld.Instance;
            if (world == null || !world.IsWalkable(next)) return;
            creature.Position = next;
            // Silhouettes face right; flip to face where it's going.
            if (creature.Renderer != null && Mathf.Abs(direction.x) > 0.05f) creature.Renderer.flipX = direction.x < 0f;
        }

        static PlayerInteraction NearestPlayer(Vector2 from, IReadOnlyList<PlayerInteraction> players, out float distance)
        {
            PlayerInteraction nearest = null;
            distance = float.PositiveInfinity;
            foreach (var player in players)
            {
                // A dead player waiting to respawn isn't prey or a threat.
                if (player.TryGetComponent<DeathHandler>(out var death) && death.IsDead) continue;
                var d = Vector2.Distance(from, player.transform.position);
                if (d >= distance) continue;
                distance = d;
                nearest = player;
            }
            return nearest;
        }
    }
}
