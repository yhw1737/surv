using System.Collections.Generic;
using System.Linq;
using FishNet;
using Isle.Core;
using Isle.Core.Ids;
using Isle.Core.Util;
using Isle.Data;
using Isle.Gameplay.Character;
using Isle.Gameplay.Combat;
using Isle.Gameplay.Inventory;
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

        /// <summary>The dash after the wind-up: until this time it charges along <see cref="LungeDirection"/>.</summary>
        public float LungeEndsAt { get; set; } = -1f;
        public Vector2 LungeDirection { get; set; }
        public bool IsLunging => LungeEndsAt >= 0f;

        /// <summary>Asleep outside its active hours — presentation reads it (the figure lies down).</summary>
        public bool Asleep { get; set; }

        /// <summary>Last movement direction, for the figure's facing.</summary>
        public Vector2 Heading { get; set; } = Vector2.right;

        /// <summary>Lives in a dungeon (SYS-DUNG-01): always awake, never counted toward the island's population.</summary>
        public bool InDungeon { get; init; }

        /// <summary>SYS-DUNG-01 boss shell phase; idle for creatures without <c>boss.shell</c>.</summary>
        public BossShell Shell { get; } = new();

        /// <summary>A water creature whose water has drained: hidden and inactive until the tide returns.</summary>
        public bool Submerged { get; set; }

        /// <summary>SYS-HUNT-01 ConditionFactor (age, nutrition), rolled at spawn from <c>butcher.condition_range</c>.</summary>
        public float Condition { get; init; } = 1f;

        /// <summary>Dead: a carcass now, waiting to be butchered. Its view stays, lying down.</summary>
        public bool Dead { get; set; }

        /// <summary>SYS-HUNT-01 damageFactor of the killing blow — how much of the body the kill left usable.</summary>
        public float KillFactor { get; set; } = 1f;

        /// <summary>SYS-COMBAT-02 side effects running on it (bleed, burn, poison, stagger count).</summary>
        public CombatStatus Status { get; } = new();
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

        /// <summary>Is there water to swim in here? Set by the dungeon runtime (flooded tiles at high tide); water
        /// creatures (<c>habitat: water</c>) move only where this is true and hide where it isn't.</summary>
        public static System.Func<Vector2, bool> IsWater { get; set; }

        static bool LivesInWater(Creature creature) => creature.Def.Habitat == "water";

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
                if (LivesInWater(creature))
                {
                    creature.Submerged = IsWater == null || !IsWater(creature.Position);
                    if (creature.Submerged) awake = false;
                }
                if (creature.View.activeSelf != awake) creature.View.SetActive(awake);
                if (awake) Think(creature, players);
            }
            TickStatuses();

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

        /// <summary>SYS-COMBAT-02: nearest live creature the attack's shape touches.</summary>
        public Creature NearestCreatureInShape(Vector2 from, Vector2 aim, AttackSpec attack)
        {
            Creature target = null;
            var best = float.MaxValue;
            foreach (var creature in _creatures)
            {
                if (!HitShapes.Contains(attack, from, aim, creature.Position, creature.Radius)) continue;
                var distance = Vector2.Distance(from, creature.Position);
                if (distance >= best) continue;
                best = distance;
                target = creature;
            }
            return target;
        }

        /// <summary>SYS-COMBAT-02: a typed hit — the creature's resistance applies, then the type's side effect (bleed,
        /// burn, poison, or a stun on the third blunt hit). Creatures wear no armor. Returns damage actually taken.</summary>
        public float DamageTyped(Creature target, float finalPower, string type, List<(NamespacedId Item, int Count)> loot) =>
            DamageTyped(target, finalPower, type, loot, type == DamageTypes.Blunt ? KillMethod.Blunt : KillMethod.Melee);

        /// <summary>As above, naming how the blow was struck (a knife or an arrow is precise) for the carcass's yield.</summary>
        public float DamageTyped(Creature target, float finalPower, string type, List<(NamespacedId Item, int Count)> loot, KillMethod method)
        {
            if (!_creatures.Contains(target)) return 0f;
            var damage = DamageTypes.Damage(finalPower, type, DamageTypes.ResistOf(target.Def.Resist, type), 0f);
            target.Status.OnHit(type, damage, Time.time, out var stunned);
            if (stunned)
            {
                target.StaggeredUntil = Mathf.Max(target.StaggeredUntil, Time.time + CombatStatus.StunSeconds);
                target.Shell.Break(Time.time);
            }
            return Damage(target, damage, loot, quiet: false, method);
        }

        /// <summary>Ticks bleed/burn/poison on every creature. One that dies of it leaves a carcass like any other kill
        /// (no XP for damage over time).</summary>
        void TickStatuses()
        {
            for (var i = _creatures.Count - 1; i >= 0; i--)
            {
                var creature = _creatures[i];
                if (!creature.Status.Any) continue;
                var damage = creature.Status.Tick(Time.time, Time.deltaTime);
                if (damage <= 0f) continue;
                Damage(creature, damage, new List<(NamespacedId Item, int Count)>(), quiet: true, KillMethod.Melee);
            }
        }

        /// <summary>Nearest live creature within reach and inside a cone of <paramref name="coneDegrees"/> around
        /// <paramref name="aim"/> (SYS-COMBAT-01 §Hit detection). A zero aim or cone falls back to anything in reach.</summary>
        public Creature NearestCreatureInCone(Vector2 from, float radiusTiles, Vector2 aim, float coneDegrees)
        {
            if (aim.sqrMagnitude < 1e-6f || coneDegrees <= 0f) return NearestCreature(from, radiusTiles);
            Creature target = null;
            var best = radiusTiles;
            foreach (var creature in _creatures)
            {
                var offset = creature.Position - from;
                var distance = BodyReach.SurfaceDistance(offset.magnitude, creature.Radius);
                if (distance > best) continue;
                // Measured to the body's edge: a creature overlapping the player is always in the cone.
                if (!MeleeCone.Contains(aim, offset, creature.Radius, coneDegrees)) continue;
                best = distance;
                target = creature;
            }
            return target;
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
        public float Damage(Creature target, float damage, List<(NamespacedId Item, int Count)> loot) => Damage(target, damage, loot, quiet: false, KillMethod.Melee);

        public float Damage(Creature target, float damage, List<(NamespacedId Item, int Count)> loot, KillMethod method) => Damage(target, damage, loot, quiet: false, method);

        float Damage(Creature target, float damage, List<(NamespacedId Item, int Count)> loot, bool quiet, KillMethod method)
        {
            if (!_creatures.Contains(target) || target.Submerged) return 0f;
            var healthBefore = target.Health;
            damage *= target.Shell.DamageMult(Time.time, target.Def.Boss?.Shell);
            var dealt = Mathf.Min(damage, Mathf.Max(0f, target.Health));
            target.Health -= damage;
            if (!quiet) target.LastHitAt = Time.time; // a bleed tick doesn't flash or wake it every frame
            Feedback.GameFeed.RaiseCreatureHit(target.Position, damage);
            if (target.Health > 0f)
            {
                var prey = target.Def.Ai.Value == CreatureBrain.Skittish || target.Def.Ai.Value == CreatureBrain.AlertThenFlee;
                target.State = prey ? CreatureState.Flee : CreatureState.Engage;
                return dealt;
            }

            // SYS-HUNT-01: it doesn't come apart into items — it leaves a carcass to butcher. The kill sets how much of the
            // body survived; overkill wastes more.
            _creatures.Remove(target);
            DropBossLoot(target);
            target.KillFactor = ButcheryCalculator.DamageFactor(method, ButcheryCalculator.IsOverkill(damage, healthBefore, target.MaxHealth));
            if (target.Def.Butcher?.Yields is { Length: > 0 })
            {
                target.Dead = true;
                target.Asleep = false;
                target.State = CreatureState.Idle;
                target.StrikeLandsAt = -1f;
                target.LungeEndsAt = -1f;
                _carcasses.Add(target);
            }
            else Destroy(target.View);
            return dealt;
        }

        // ------------------------------------------------------------------ carcasses (SYS-HUNT-01)

        readonly List<Creature> _carcasses = new();

        public IReadOnlyList<Creature> Carcasses => _carcasses;

        public Creature NearestCarcass(Vector2 from, float reachTiles)
        {
            Creature best = null;
            var bestDistance = reachTiles;
            foreach (var carcass in _carcasses)
            {
                var d = BodyReach.SurfaceDistance(Vector2.Distance(from, carcass.Position), carcass.Radius);
                if (d > bestDistance) continue;
                bestDistance = d;
                best = carcass;
            }
            return best;
        }

        /// <summary>Butchers a carcass: what it yields for this Cooking level and knife, and it's gone.</summary>
        public List<(NamespacedId Item, int Count)> Butcher(Creature carcass, int cookingLevel, float toolFactor)
        {
            var cuts = new List<(NamespacedId Item, int Count)>();
            if (!_carcasses.Remove(carcass)) return cuts;
            Destroy(carcass.View);
            cuts.AddRange(Cuts(carcass.Def.Butcher, carcass.Weight, carcass.Condition, cookingLevel, toolFactor, carcass.KillFactor));
            return cuts;
        }

        /// <summary>SYS-HUNT-01 §Yield + §Cuts: the edible weight split by share; damage-sensitive cuts (hide) lose what the
        /// kill and an unskilled hand ruin. Each cut turns into whole items of its unit weight.</summary>
        public static List<(NamespacedId Item, int Count)> Cuts(ButcherSpec butcher, float weightKg, float condition, int cookingLevel, float toolFactor, float killFactor)
        {
            var cuts = new List<(NamespacedId Item, int Count)>();
            if (butcher?.Yields == null) return cuts;
            var edible = ButcheryCalculator.EdibleKg(weightKg, butcher.EdibleRatio, condition, cookingLevel, toolFactor, killFactor);
            foreach (var cut in butcher.Yields)
            {
                int count;
                if (cut.UnitKg > 0f)
                {
                    var kg = edible * cut.Share;
                    if (cut.DamageSensitive) kg *= ButcheryCalculator.SurvivalRate(killFactor, cookingLevel);
                    count = ButcheryCalculator.Pieces(kg, cut.UnitKg, cut.Min);
                }
                else count = cut.Count > 0 ? cut.Count : 1;
                if (count > 0) cuts.Add((cut.Item, count));
            }
            return cuts;
        }

        /// <summary>A boss's guaranteed drops land where it fell, for everyone to share.</summary>
        static void DropBossLoot(Creature target)
        {
            if (target.Def.Boss?.Drops == null) return;
            var drops = new List<(ItemDef, int)>();
            foreach (var drop in target.Def.Boss.Drops)
                if (DefRegistry.TryGet<ItemDef>(drop.Item, out var def)) drops.Add((def, Mathf.Max(1, drop.Count)));
            if (drops.Count > 0) LootPiles.Drop(target.Position, drops);
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
                if (creature.Def == def && !creature.InDungeon) count++;
            return count;
        }

        /// <summary>Places one creature of <paramref name="def"/> at <paramref name="position"/> — dungeons use this for
        /// their rooms' spawn marks (SYS-DUNG-01). Server-side.</summary>
        public Creature SpawnAt(CreatureDef def, Vector2 position, bool inDungeon)
        {
            var creature = Create(def, position, inDungeon);
            _creatures.Add(creature);
            return creature;
        }

        Creature Create(CreatureDef def, Vector2 position, bool inDungeon = false)
        {
            var weight = RollWeight(def);
            var radius = BodyReach.RadiusForWeight(def.Combat?.BodyRadiusTiles ?? DefaultBodyRadius, weight, def.WeightDist?.Mean ?? weight);
            var view = CreateView(def, radius);
            view.transform.position = position;
            return new Creature
            {
                Def = def,
                InDungeon = inDungeon,
                Weight = weight,
                Home = position,
                Position = position,
                Condition = RollCondition(def),
                Health = weight * def.HealthPerKg,
                MaxHealth = weight * def.HealthPerKg,
                WanderTarget = position,
                View = view,
                Radius = radius,
                Renderer = view.GetComponent<SpriteRenderer>(),
            };
        }

        /// <summary>ConditionFactor, uniform across <c>butcher.condition_range</c>. [invented distribution]</summary>
        static float RollCondition(CreatureDef def)
        {
            var range = def.Butcher?.ConditionRange;
            return range is { Length: 2 } ? Random.Range(range[0], range[1]) : 1f;
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
            creature.Asleep = !IsAwakeNow(creature);
            if (creature.Asleep)
            {
                creature.State = CreatureState.Idle;
                if (creature.Renderer != null) creature.Renderer.color = SleepTint;
                return;
            }

            if (creature.IsStaggered)
            {
                creature.StrikeLandsAt = -1f;
                creature.LungeEndsAt = -1f;
                creature.View.transform.position = creature.Position;
                Flash(creature);
                return;
            }

            if (creature.Def.Boss?.Shell != null)
            {
                creature.Shell.Tick(Time.time, creature.Health / Mathf.Max(1f, creature.MaxHealth), creature.Def.Boss.Shell);
                if (creature.Shell.IsHidden(Time.time))
                {
                    creature.StrikeLandsAt = -1f;
                    creature.LungeEndsAt = -1f;
                    creature.View.transform.position = creature.Position;
                    Flash(creature);
                    return;
                }
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
                    if (creature.IsLunging)
                    {
                        // The dash: straight along the committed line; it lands early if it reaches the player.
                        if (BodyReach.InReach(distance, creature.Radius, combat.AttackRangeTiles * 0.6f) || Time.time >= creature.LungeEndsAt)
                            LandStrike(creature, target, distance, combat);
                        else
                            Step(creature, creature.LungeDirection, combat.LungeSpeed);
                    }
                    else if (creature.IsWindingUp)
                    {
                        // Committed: the tell ends in a dash at where the player is now (or the strike, with no dash).
                        if (Time.time >= creature.StrikeLandsAt)
                        {
                            creature.StrikeLandsAt = -1f;
                            if (combat.LungeTiles > 0f && combat.LungeSpeed > 0f)
                            {
                                creature.LungeDirection = ((Vector2)target.transform.position - creature.Position).normalized;
                                creature.LungeEndsAt = Time.time + combat.LungeTiles / combat.LungeSpeed;
                            }
                            else LandStrike(creature, target, distance, combat);
                        }
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
            creature.LungeEndsAt = -1f;
            creature.NextStrikeAt = Time.time + combat.AttackIntervalSeconds;
            var sweep = creature.Def.Boss?.SweepRadiusTiles ?? 0f;
            if (sweep > 0f)
            {
                // A sweep hits everyone around it, whoever it was aiming at.
                foreach (var player in PlayerInteraction.All)
                {
                    if (player.TryGetComponent<DeathHandler>(out var death) && death.IsDead) continue;
                    if (!BodyReach.InReach(Vector2.Distance(creature.Position, player.transform.position), creature.Radius, sweep)) continue;
                    if (player.ReceiveCreatureStrike(creature.Position, combat.Damage, combat.DamageType ?? DamageTypes.Blunt) == BlockOutcome.Parried)
                        creature.StaggeredUntil = Time.time + MeleeDefense.ParryStaggerSeconds;
                }
                return;
            }
            if (target == null || !BodyReach.InReach(distance, creature.Radius, combat.AttackRangeTiles + StrikeLeewayTiles)) return;
            if (target.ReceiveCreatureStrike(creature.Position, combat.Damage, combat.DamageType ?? DamageTypes.Blunt) == BlockOutcome.Parried)
                creature.StaggeredUntil = Time.time + MeleeDefense.ParryStaggerSeconds;
        }

        /// <summary>Seconds a hit keeps a sleeping creature awake.</summary>
        const float WokenSeconds = 10f;
        static readonly Color SleepTint = new(0.6f, 0.6f, 0.75f);

        static bool IsAwakeNow(Creature creature)
        {
            if (creature.InDungeon || Time.time - creature.LastHitAt < WokenSeconds) return true;
            var clock = WorldTime.Instance != null ? WorldTime.Instance.Clock : null;
            return clock == null || CreatureBrain.IsAwake(creature.Def.Spawn?.Time, clock.Phase.ToString().ToLowerInvariant());
        }

        /// <summary>Presentation: flashes red for a moment after a hit.</summary>
        const float FlashSeconds = 0.12f;
        static readonly Color WindupTint = new(1f, 0.75f, 0.2f);
        static readonly Color StaggerTint = new(0.65f, 0.75f, 1f);
        static readonly Color ShellTint = new(0.55f, 0.55f, 0.55f);

        static void Flash(Creature creature)
        {
            if (creature.Renderer == null) return;
            // The fill colour is baked into the texture, so the renderer tint is white at rest and red on a hit.
            if (Time.time - creature.LastHitAt < FlashSeconds) creature.Renderer.color = new Color(1f, 0.3f, 0.3f);
            else if (creature.IsStaggered) creature.Renderer.color = StaggerTint;
            else if (creature.Shell.IsHidden(Time.time)) creature.Renderer.color = ShellTint;
            else if (creature.IsWindingUp || creature.IsLunging) creature.Renderer.color = Color.Lerp(Color.white, WindupTint, 0.5f + 0.5f * Mathf.Sin(Time.time * 40f));
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
            if (direction.sqrMagnitude > 0.01f) creature.Heading = direction;
            var next = creature.Position + direction * speed * Time.deltaTime;
            var world = IslandWorld.Instance;
            if (world == null || !world.IsWalkable(next)) return;
            if (LivesInWater(creature) && (IsWater == null || !IsWater(next))) return;
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
