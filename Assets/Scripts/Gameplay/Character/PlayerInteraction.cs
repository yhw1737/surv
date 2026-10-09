using System;
using System.Collections.Generic;
using System.Linq;
using FishNet.Object;
using Isle.Core;
using Isle.Core.Ids;
using Isle.Data;
using Isle.Gameplay.Combat;
using Isle.Gameplay.Cooking;
using Isle.Gameplay.Building;
using Isle.Gameplay.Crafting;
using Isle.Gameplay.Feedback;
using Isle.Gameplay.Fishing;
using Isle.Gameplay.Hunting;
using Isle.Gameplay.Inventory;
using Isle.Gameplay.Skills;
using Isle.Modding.Defs;
using Isle.Networking;
using Isle.World.Island;
using Isle.World.Objects;
using Isle.World.Time;
using Isle.World.Weather;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Isle.Gameplay.Character
{
    /// <summary>
    /// Player-side actions: the world-object interaction (T-052-era work, PROJECT_STATE.md §Decided
    /// without a spec) plus the solo-prototype verbs — harvest, drink, attack, craft, eat. The owner
    /// sends a request; the server re-validates reach, stamina, ingredients and space before applying
    /// anything (Absolute Rule 2, SYS-NET-01 §Server validation).
    /// <para>
    /// Prototype scope: the interact key (E) does drink → harvest → campfire toggle, in that order of
    /// priority. The attack key (left mouse) strikes the nearest creature within the equipped weapon's
    /// reach. Crafting and eating are driven by the HUD via the public <c>Request*</c> methods.
    /// </para>
    /// </summary>
    public sealed class PlayerInteraction : NetworkBehaviour
    {
        /// <summary>Tiles (GLOSSARY §Units). Picked, not specced — separate from SYS-SURV-01's
        /// 5-tile fire bonus radius, which is ambient warmth, not an interact reach.</summary>
        public const float ReachTiles = 2f;

        /// <summary>Weapon tag for the fists fallback. Looked up by tag, not id (Absolute Rule 1 / Principle 2).</summary>
        const string UnarmedTag = "weapon/unarmed";
        const string MainHandSlot = "main_hand";

        /// <summary>SYS-FISH-01 rigs: handline (no item, trash fish only) and rod (a held <c>tool/rod</c>, minigame).</summary>
        const string HandlineRig = "handline";
        const string RodRig = "rod";
        const string RodTag = "tool/rod";

        /// <summary>Raw fish item weight the catch is cut into (kg per unit). [invented] — SYS-HUNT-01's kg-to-units step isn't specced.</summary>
        const float FishUnitKg = 0.4f;

        /// <summary>Walking this far from the spot mid-fight snaps the line. [invented]</summary>
        const float FightLeashTiles = 3f;

        /// <summary>Tag on a campfire def, the same tag <c>Vitals</c> uses for its warmth check (SCHEMA §Station).</summary>
        const string CampfireTag = "station/campfire";


        static readonly List<PlayerInteraction> _all = new();

        /// <summary>Every player in the scene — a registry instead of FindObjectsByType, which allocated an array
        /// per call from a dozen per-frame call sites (a GC stutter source on the big island).</summary>
        public static IReadOnlyList<PlayerInteraction> All => _all;

        /// <summary>This machine's own player, or null before it spawns.</summary>
        public static PlayerInteraction Local
        {
            get
            {
                foreach (var player in _all)
                    if (player.IsOwner) return player;
                return null;
            }
        }

        void OnEnable() => _all.Add(this);
        void OnDisable() => _all.Remove(this);

        SkillProgress _skills;

        /// <summary>This player's skills (SYS-SKILL-01): XP earned and the levels it buys. Server state; the owning
        /// host's HUD reads it directly. ponytail: not synced to a remote client — Phase 10.</summary>
        public SkillProgress Skills => _skills ??= new SkillProgress(DefRegistry.All<SkillDef>());

        public int LevelOf(NamespacedId skill) => skill.IsValid ? Skills.Level(skill) : 1;

        /// <summary>SYS-SKILL-01 focus multiplier for <paramref name="skill"/>: what one base XP is worth right now.</summary>
        public float FocusMultiplier(NamespacedId skill) => FocusCalculator.Multiplier(FocusOf(skill));

        /// <summary>SYS-SKILL-01 Focus_i (0..1) for <paramref name="skill"/>.</summary>
        public float FocusOf(NamespacedId skill) =>
            Skills.Skills.Count == 0 ? 1f : FocusCalculator.Focus(Skills.Levels, Skills.Skills, skill, ActivePlayers());

        /// <summary>SYS-SKILL-01: n = median distinct active players over the last 7 in-game days. One shared
        /// tracker per world, fed once per in-game day with the players connected then.</summary>
        static readonly ActivityTracker Activity = new();
        static long _lastRecordedDay = -1;

        static int ActivePlayers()
        {
            var day = WorldTime.Instance != null ? WorldTime.Instance.Clock.TotalMinutes / WorldClock.MinutesPerDay : 0;
            if (day != _lastRecordedDay)
            {
                _lastRecordedDay = day;
                Activity.RecordDay(Mathf.Max(1, PlayerInteraction.All.Count));
            }
            return Mathf.Max(1, Activity.MedianActivePlayers());
        }

        /// <summary>SYS-START-01: who this player is (name, traits, starting skills). Null before a game starts.</summary>
        public Survivor Survivor { get; set; }

        /// <summary>Server-side: grants an action's XP (docs/content/xp_table.md) through the focus multiplier.</summary>
        public void AwardXp(XpAward award, float units)
        {
            if (award == null || !award.Skill.IsValid) return;
            Grant(award.Skill, SkillProgress.Amount(award, units));
        }

        /// <summary>xp_mult from traits and buffs (SYS-START-01: fast / slow learner).</summary>
        float XpMult() => TryGetComponent<Vitals>(out var vitals) ? Buffs.BuffEffects.Mult(vitals.ActiveBuffs(), "xp_mult") : 1f;

        /// <summary>gather_speed_mult: nimble hands harvest faster. Other systems (dungeon veins) divide by it too.</summary>
        public float GatherSpeed() => TryGetComponent<Vitals>(out var vitals) ? Mathf.Max(0.1f, Buffs.BuffEffects.Mult(vitals.ActiveBuffs(), "gather_speed_mult")) : 1f;

        void AwardCombatXp(WeaponDef weapon, float damageDealt)
        {
            if (weapon == null || damageDealt <= 0f || !DefRegistry.TryGet<SkillDef>(weapon.CombatSkill, out var skill)) return;
            Grant(skill.Id, damageDealt * skill.XpPerDamage);
        }

        void Grant(NamespacedId skill, float baseXp)
        {
            if (baseXp <= 0f) return;
            var before = Skills.Level(skill);
            var amount = baseXp * FocusMultiplier(skill) * XpMult();
            var after = Skills.AddXp(skill, amount);
            GameFeed.RaiseXpGained(skill, amount, after > before ? after : 0);
        }

        float _nextAttackAt;
        readonly MeleeCombo _combo = new();
        bool _attackQueued;
        Vector2 _attackAim, _queuedAim;

        /// <summary>A press this close to the weapon being ready again is held and swung the moment it is, so a combo
        /// doesn't need frame-perfect clicks. [invented] — SYS-COMBAT-01 lists the buffer as an open question.</summary>
        const float AttackBufferSeconds = 0.3f;

        /// <summary>Step of the latest melee swing (1-based) and how long that combo is; the figure picks its swing
        /// from these. Server state the host reads.</summary>
        public int ComboStep { get; private set; }
        public int ComboLength { get; private set; } = 3;

        /// <summary>SYS-COMBAT-01 §Melee block: held right button. Server state — direction is where the guard faces.</summary>
        public bool Blocking { get; private set; }
        public float BlockStartedAt { get; private set; } = float.NegativeInfinity;
        public Vector2 BlockDirection { get; private set; } = Vector2.right;
        bool _sentBlocking;
        Vector2 _sentBlockDirection;

        /// <summary>Re-send the guard direction when the mouse swings it more than this. Network thrift, not balance.</summary>
        const float BlockAimResendDeg = 10f;

        bool IsDead => TryGetComponent<DeathHandler>(out var death) && death.IsDead;

        void Update()
        {
            if (IsServer) ServerUpdate();
            if (!IsOwner) return;

            var kb = Keyboard.current;
            if (kb != null && kb.eKey.wasPressedThisFrame) RequestInteract();

            var mouse = Mouse.current;
            if (Cast != null)
            {
                // A line is out: the left button hooks a bite; nothing else.
                if (mouse != null && mouse.leftButton.wasPressedThisFrame && !PointerGate.Captured) CmdHook();
            }
            else if (Fight != null)
            {
                // While a fish is on, the left button reels instead of attacking.
                var reeling = mouse != null && mouse.leftButton.isPressed;
                if (reeling != _sentReeling)
                {
                    _sentReeling = reeling;
                    CmdReel(reeling);
                }
            }
            else if (mouse != null)
            {
                var ranged = HoldsRangedWeapon();
                UpdateBlockInput(mouse, ranged);
                if (mouse.leftButton.wasPressedThisFrame && !PointerGate.Captured && !_sentBlocking)
                {
                    if (!ranged)
                    {
                        // The swing goes where the mouse points (SYS-COMBAT-01 forward cone).
                        var aim = MouseWorld() is { } at ? (Vector2)at - (Vector2)transform.position : Vector2.zero;
                        CmdAttack(aim.x, aim.y);
                    }
                    else
                    {
                        DrawStartedAt = Time.time;
                        CmdBeginDraw();
                    }
                }
                if (DrawStartedAt >= 0f && mouse.leftButton.wasReleasedThisFrame)
                {
                    DrawStartedAt = -1f;
                    var aim = MouseWorld();
                    if (aim != null) CmdLoose(aim.Value.x, aim.Value.y);
                }
            }

            // SYS-HUNT-01 §Carrying: G picks up a small carcass, drags a big one (or helps carry it), G again puts it down.
            if (kb != null && kb.gKey.wasPressedThisFrame) RequestHaul();

            if (kb != null && kb.fKey.wasPressedThisFrame)
            {
                if (Cast != null) CmdReelIn();
                else if (MouseWorld() is { } aim) RequestCast(aim);
            }
            if (kb != null && kb.rKey.wasPressedThisFrame) RequestRest();
        }

        /// <summary>Right button held = block, facing the mouse. Not with a bow (its right button stays free).</summary>
        void UpdateBlockInput(Mouse mouse, bool ranged)
        {
            var want = !ranged && mouse.rightButton.isPressed && (_sentBlocking || !PointerGate.Captured);
            var aim = MouseWorld() is { } world ? ((Vector2)world - (Vector2)transform.position) : _sentBlockDirection;
            if (aim.sqrMagnitude < 1e-6f) aim = _sentBlockDirection.sqrMagnitude > 0f ? _sentBlockDirection : Vector2.right;
            if (want != _sentBlocking)
            {
                _sentBlocking = want;
                _sentBlockDirection = aim.normalized;
                CmdBlock(want, _sentBlockDirection.x, _sentBlockDirection.y);
            }
            else if (want && Vector2.Angle(aim, _sentBlockDirection) > BlockAimResendDeg)
            {
                _sentBlockDirection = aim.normalized;
                CmdBlockAim(_sentBlockDirection.x, _sentBlockDirection.y);
            }
        }

        [ServerRpc]
        void CmdBlock(bool blocking, float x, float y)
        {
            if (blocking && IsDead) return;
            if (blocking && !Blocking) BlockStartedAt = Time.time;
            Blocking = blocking;
            SetBlockDirection(x, y);
        }

        [ServerRpc]
        void CmdBlockAim(float x, float y) => SetBlockDirection(x, y);

        void SetBlockDirection(float x, float y)
        {
            var d = new Vector2(x, y);
            if (float.IsNaN(d.x) || float.IsNaN(d.y) || d.sqrMagnitude < 1e-6f) return; // never trust client values
            BlockDirection = d.normalized;
        }

        /// <summary>Test and tooling entry point: raise or lower the guard facing <paramref name="direction"/>.</summary>
        public void RequestBlock(bool blocking, Vector2 direction) => CmdBlock(blocking, direction.x, direction.y);

        /// <summary>Server-side: a creature's strike reaches this player. Block and parry are resolved here (SYS-COMBAT-01
        /// §Melee); whatever gets through goes to <see cref="Vitals.TakeDamage"/> (armor, i-frames).</summary>
        public BlockOutcome ReceiveCreatureStrike(Vector2 from, float damage, string damageType = DamageTypes.Blunt)
        {
            if (!IsServer || !TryGetComponent<Vitals>(out var vitals)) return BlockOutcome.None;
            var level = LevelOf(MeleeSkill());
            var angle = Vector2.Angle(BlockDirection, from - (Vector2)transform.position);
            var result = MeleeDefense.Resolve(Blocking && !IsDead, Time.time - BlockStartedAt, MeleeDefense.ParryWindow(level), angle, damage, vitals.Stamina);
            if (result.StaminaCost > 0f) vitals.SpendStamina(result.StaminaCost);
            if (result.DamageThrough > 0f) vitals.TakeDamage(result.DamageThrough, damageType);
            if (result.Outcome != BlockOutcome.None) GameFeed.RaiseDefended(transform.position, result.Outcome);
            return result.Outcome;
        }

        /// <summary>The skill the guard scales with: the held melee weapon's, else the fists'.</summary>
        NamespacedId MeleeSkill() =>
            TryGetComponent<InventoryNetwork>(out var inventory) && EquippedWeapon(inventory) is { } weapon ? weapon.CombatSkill : default;

        /// <summary>Owner-local: when the bow started drawing, for the HUD's charge bar. -1 when not drawing.
        /// The server keeps its own clock for the actual charge.</summary>
        public float DrawStartedAt { get; private set; } = -1f;

        /// <summary>Test and tooling entry point for a full ranged shot at <paramref name="aim"/>.</summary>
        public void RequestBeginDraw() => CmdBeginDraw();
        public void RequestLoose(Vector2 aim) => CmdLoose(aim.x, aim.y);

        public bool HoldsRangedWeapon() =>
            TryGetComponent<InventoryNetwork>(out var inventory) && EquippedWeapon(inventory) is { } weapon && IsRanged(weapon);

        static Vector2? MouseWorld()
        {
            var camera = Camera.main;
            var mouse = Mouse.current;
            if (camera == null || mouse == null) return null;
            var p = mouse.position.ReadValue();
            return Isle.Core.Util.ViewTilt.ScreenToGround(camera, p);
        }

        public void RequestInteract() => CmdInteract();
        /// <summary>Test and tooling entry point: a swing with no aim (hits the nearest creature in reach).</summary>
        public void RequestAttack() => CmdAttack(0f, 0f);

        /// <summary>A swing toward <paramref name="aim"/> (a direction from the player).</summary>
        public void RequestAttack(Vector2 aim) => CmdAttack(aim.x, aim.y);
        public void RequestCraft(string recipeId) => CmdCraft(recipeId, string.Empty);

        /// <summary>SYS-CRAFT-02: craft a template recipe from <paramref name="materialId"/>.</summary>
        public void RequestCraft(string recipeId, string materialId) => CmdCraft(recipeId, materialId ?? string.Empty);
        public void RequestUse(string itemId) => CmdUse(itemId);
        public void RequestRest() => CmdRest();
        public void RequestEquipItem(string itemId) => CmdEquipItem(itemId);
        public void RequestUnequipSlot(string slot) => CmdUnequipSlot(slot);
        public void RequestPlace(string itemId, Vector2 at) => CmdPlace(itemId, at.x, at.y);
        public void RequestStore(string itemId) => CmdStore(itemId);
        public void RequestTake(string itemId) => CmdTake(itemId);

        /// <summary>Every structure position, wherever it came from — for the placement spacing rule.</summary>
        public static List<Vector2> StructurePositions()
        {
            var positions = new List<Vector2>();
            foreach (var instance in WorldObjectRegistry.All)
                positions.Add(instance.transform.position);
            return positions;
        }

        /// <summary>The placement check both the HUD preview and the server use, so they agree.</summary>
        public bool CanPlaceAt(Vector2 at)
        {
            var world = IslandWorld.Instance;
            return world != null && BuildingCalculator.CanPlace(transform.position, at, world.IsWalkable, StructurePositions());
        }

        [ServerRpc]
        void CmdInteract()
        {
            if (IsDead) return;
            var world = IslandWorld.Instance;
            if (world == null) return;
            if (Dungeons.DungeonDirector.Instance != null && Dungeons.DungeonDirector.Instance.TryInteract(this)) return;

            var pile = LootPiles.Nearest(transform.position, ReachTiles);
            if (pile != null && TryGetComponent<InventoryNetwork>(out var bagOwner))
            {
                var taken = LootPiles.PickUp(pile, bagOwner.Slots, bagOwner.Containers);
                foreach (var (item, count) in taken) GameFeed.RaiseItemGained(item.Id, count);
                if (pile.Items.Count > 0) GameFeed.RaiseNotice("@ui.bag_full");
                return;
            }

            // Pressing E again while gathering or butchering stops it.
            if (Gathering != null || Butchering != null)
            {
                CancelGather();
                Butchering = null;
                return;
            }

            if (Hauling != null) return; // put the carcass down first (G)

            // SYS-HUNT-01: a carcass in reach gets butchered.
            var carcass = CreatureDirector.Instance != null ? CreatureDirector.Instance.NearestCarcass(transform.position, ReachTiles) : null;
            if (carcass != null)
            {
                StartButcher(carcass);
                return;
            }

            // The nearest of: a harvestable node, a structure, a water tile — so a tree on a pond's shore can still be cut.
            var here = (Vector2)transform.position;
            var harvest = world.NearestNode(here, ReachTiles, n => n.IsHarvestable);
            var nearStation = WorldObjectRegistry.NearestInteractable(here, ReachTiles);
            var waterTile = world.NearestWater(here, ReachTiles);
            var harvestDistance = harvest != null ? Vector2.Distance(here, harvest.Position) : float.MaxValue;
            var stationDistance = nearStation != null ? Vector2.Distance(here, nearStation.transform.position) : float.MaxValue;
            var waterDistance = waterTile != null ? Vector2.Distance(here, IslandWorld.TileToWorld(waterTile.Value)) : float.MaxValue;

            if (waterDistance < harvestDistance && waterDistance < stationDistance)
            {
                var body = world.WaterAt(waterTile.Value);
                if (body?.Drink != null && TryParseWaterSource(body.Drink.Source, out var source) && TryGetComponent<Vitals>(out var drinker))
                    drinker.Drink(source);
                return;
            }

            if (harvest != null && harvestDistance <= stationDistance)
            {
                if (!TryGetComponent<Vitals>(out var vitals)) return;
                if (vitals.Stamina < harvest.Def.Gather.StaminaCost)
                {
                    GameFeed.RaiseNotice("@ui.too_tired");
                    return;
                }
                // The best tool for the job comes out of the bags on its own (SYS-CRAFT-02: a vein needs a pickaxe of its
                // tier — an iron vein, copper or better).
                var gather = harvest.Def.Gather;
                if (!string.IsNullOrEmpty(gather.ToolTag)) EquipBestTool(gather.ToolTag);
                if (gather.ToolTier > 0 && TryGetComponent<InventoryNetwork>(out var held)
                    && !ToolTiers.CanWork(MatchingTool(held, gather)?.Tier ?? 0, gather.ToolTier))
                {
                    GameFeed.RaiseNotice("@ui.needs_better_tool");
                    return;
                }
                StartGather(harvest);
                return;
            }

            var station = WorldObjectRegistry.NearestInteractable(transform.position, ReachTiles);
            if (station == null || !station.TryGetComponent<IInteractable>(out var interactable)) return;
            interactable.Interact(gameObject);

            // Lighting a campfire makes it your respawn point (SYS-SURV-01 §Death "last shelter").
            if (station.IsActive && station.HasTag(CampfireTag) && TryGetComponent<DeathHandler>(out var death))
            {
                death.SetLastShelter(station.transform.position + Vector3.down);
                GameFeed.RaiseNotice("@ui.shelter_set");
            }
        }

        [ServerRpc]
        void CmdAttack(float aimX, float aimY)
        {
            var aim = new Vector2(aimX, aimY);
            if (float.IsNaN(aim.x) || float.IsNaN(aim.y)) aim = Vector2.zero; // never trust client values
            if (IsDead || Blocking) return;
            if (TwoHandsUnusable()) return;
            if (Hauling != null) return; // hands full with a carcass
            if (Time.time < _nextAttackAt)
            {
                // Pressed just before the weapon is ready: swing as soon as it is (combo input buffer).
                if (_nextAttackAt - Time.time <= AttackBufferSeconds)
                {
                    _attackQueued = true;
                    _queuedAim = aim;
                }
                return;
            }
            _attackAim = aim;
            PerformAttack();
        }

        /// <summary>Server tick for the buffered attack.</summary>
        void ServerUpdate()
        {
            if (_attackQueued && Time.time >= _nextAttackAt)
            {
                _attackQueued = false;
                _attackAim = _queuedAim;
                if (!IsDead && !Blocking) PerformAttack();
            }
        }

        /// <summary>SYS-COMBAT-01 §Melee: one swing of the combo — step, stamina, power with the finisher and staggered
        /// multipliers, execute at Lv35.</summary>
        void PerformAttack()
        {
            if (!TryGetComponent<Vitals>(out var vitals) || !TryGetComponent<InventoryNetwork>(out var inventory)) return;

            var weapon = EquippedWeapon(inventory);
            if (weapon == null || weapon.AttackSpeed <= 0f) return;
            var level = LevelOf(weapon.CombatSkill);
            var length = MeleeCombo.Length(level);

            // Peek at the step without committing, so a tired swing doesn't advance the combo.
            var continues = _combo.Step > 0 && _combo.Step < length && Time.time - _nextAttackAt <= MeleeCombo.WindowSeconds;
            var step = continues ? _combo.Step + 1 : 1;
            var cost = MeleeCombo.StaminaCost(weapon.StaminaCost, step, length, level);
            if (vitals.Stamina < cost)
            {
                GameFeed.RaiseNotice("@ui.too_tired");
                _combo.Reset();
                return;
            }

            _combo.Advance(_nextAttackAt, Time.time, length);
            ComboStep = step;
            ComboLength = length;
            _nextAttackAt = Time.time + 1f / weapon.AttackSpeed;
            vitals.SpendStamina(cost);
            GameFeed.RaisePlayerSwing(transform.position, weapon.Reach);

            // SYS-COMBAT-02: this combo step's attack decides what it touches and what kind of damage it deals.
            var attack = WeaponAttacks.For(weapon, step, length);
            var director = CreatureDirector.Instance;
            var target = director != null ? director.NearestCreatureInShape(transform.position, _attackAim, attack) : null;
            if (target == null) return;

            var power = PowerCalculator.FinalPower(weapon.BasePower, level, situationalMult: MeleeCombo.SituationalMult(target.IsStaggered))
                        * attack.PowerMult * Buffs.BuffEffects.Mult(vitals.ActiveBuffs(), "melee_power_mult");
            var loot = new List<(NamespacedId Item, int Count)>();
            float dealt;
            // SYS-HUNT-01: a knife kills cleanly (like a dagger), blunt trauma ruins the most, the rest is ordinary melee.
            var held = inventory.Slots.Working(MainHandSlot);
            var method = held?.Tags != null && held.Tags.Contains(KnifeTag) ? KillMethod.Precise
                : attack.Type == DamageTypes.Blunt ? KillMethod.Blunt : KillMethod.Melee;
            if (target.MaxHealth > 0f && MeleeCombo.Executes(level, target.Health / target.MaxHealth))
                dealt = director.Damage(target, target.Health, loot, method); // Lv35 execute ignores type and resist
            else
                dealt = director.DamageTyped(target, power, attack.Type, loot, method);
            AwardCombatXp(weapon, dealt);
            if (dealt > 0f) WearSlot(inventory, MainHandSlot); // a swing that lands wears the weapon; a miss doesn't
            foreach (var (item, count) in loot) GiveItem(inventory, item, count);
        }

        /// <summary>SYS-CRAFT-02: one use off the slot's item; tells the player when it breaks.</summary>
        static void WearSlot(InventoryNetwork inventory, string slot)
        {
            var item = inventory.Slots.Get(slot);
            if (item != null && inventory.Slots.Wear(slot, 1)) GameFeed.RaiseNotice($"@ui.item_broke|{item.Name}");
        }

        /// <summary>
        /// Puts the best working item tagged <paramref name="toolTag"/> (highest tier, then speed) carried anywhere into the
        /// main hand, sending what was there into the bags (or to the ground when they're full). True when such a tool is
        /// in hand afterwards. Server-side.
        /// </summary>
        public bool EquipBestTool(string toolTag)
        {
            if (!TryGetComponent<InventoryNetwork>(out var inventory)) return false;
            static float Score(ItemDef item) => item.Tier * 10f + (item.ToolPower > 0f ? item.ToolPower : 1f);
            bool Fits(ItemDef item) => item?.Tags != null && item.Tags.Contains(toolTag);

            var held = inventory.Slots.Working(MainHandSlot);
            GridInventory from = null;
            Placement best = default;
            var bestScore = Fits(held) ? Score(held) : float.NegativeInfinity;
            foreach (var container in inventory.Containers())
            foreach (var placed in container.Placements)
            {
                if (!Fits(placed.Item) || placed.Wear is { Broken: true } || Score(placed.Item) <= bestScore) continue;
                (from, best, bestScore) = (container, placed, Score(placed.Item));
            }
            if (from == null) return Fits(held);

            // Swap: the tool out of its spot, whatever was in hand back into the bags.
            var previous = inventory.Slots.Get(MainHandSlot);
            ItemWear previousWear = null;
            if (previous != null && !inventory.Slots.Unequip(MainHandSlot, out previousWear)) return Fits(held);
            InventoryOps.SetCount(from, best, best.Count - 1);
            inventory.Slots.TryEquip(MainHandSlot, best.Item, best.Wear);
            if (previous != null) GiveItem(inventory, previous, 1, previousWear);
            return true;
        }

        /// <summary>For other systems (dungeon veins): the held tool that works <paramref name="gather"/>, or null.</summary>
        public ItemDef HeldToolFor(GatherSpec gather) => TryGetComponent<InventoryNetwork>(out var inventory) ? MatchingTool(inventory, gather) : null;

        /// <summary>For other systems: one use off the held tool.</summary>
        public void WearHeldTool()
        {
            if (TryGetComponent<InventoryNetwork>(out var inventory)) WearSlot(inventory, MainHandSlot);
        }

        /// <summary>For other systems: into the bags, or at the player's feet when full.</summary>
        public void Receive(NamespacedId item, int count)
        {
            if (TryGetComponent<InventoryNetwork>(out var inventory)) GiveItem(inventory, item, count);
        }

        /// <summary>The held tool that counts for <paramref name="gather"/> (right tag, not broken), or null.</summary>
        static ItemDef MatchingTool(InventoryNetwork inventory, GatherSpec gather)
        {
            if (string.IsNullOrEmpty(gather.ToolTag)) return null;
            var held = inventory.Slots.Working(MainHandSlot);
            return held?.Tags != null && held.Tags.Contains(gather.ToolTag) ? held : null;
        }

        /// <summary>SYS-CRAFT-02: the ammo a shot uses — by tag, the strongest arrow carried; else the weapon's one
        /// ammo item. Null when none is carried.</summary>
        static ItemDef BestAmmo(InventoryNetwork inventory, WeaponDef weapon)
        {
            if (!string.IsNullOrEmpty(weapon.AmmoTag))
                return inventory.Containers().SelectMany(c => c.Placements).Select(p => p.Item)
                    .Where(i => i.Tags != null && i.Tags.Contains(weapon.AmmoTag))
                    .OrderByDescending(i => i.AmmoPower > 0f ? i.AmmoPower : 1f).FirstOrDefault();
            return weapon.Ammo.IsValid && InventoryOps.Count(inventory.Containers(), weapon.Ammo) > 0 && DefRegistry.TryGet<ItemDef>(weapon.Ammo, out var ammo) ? ammo : null;
        }

        static bool IsRanged(WeaponDef weapon) => weapon.Ammo.IsValid || !string.IsNullOrEmpty(weapon.AmmoTag);

        /// <summary>SYS-WORLD-03 §Gathering: the node being harvested, or null. Harvests take time; moving away or
        /// pressing E again cancels. Server state the owning host's HUD reads for the progress bar.</summary>
        public ResourceNode Gathering { get; private set; }

        /// <summary>0..1 through the current harvest.</summary>
        public float GatherProgress => Gathering == null || _gatherSeconds <= 0f ? 0f : Mathf.Clamp01((Time.time - _gatherStartedAt) / _gatherSeconds);

        /// <summary>Moving further than this from where the harvest started cancels it. [invented]</summary>
        const float GatherLeashTiles = 0.3f;

        float _gatherStartedAt;
        float _gatherSeconds;
        Vector2 _gatherFrom;

        void StartGather(ResourceNode node)
        {
            Gathering = node;
            _gatherFrom = transform.position;
            _gatherStartedAt = Time.time;
            var skill = node.Def.Gather.Xp?.Skill ?? default;
            _gatherSeconds = node.Def.Gather.TimeSec <= 0f ? 0f : GatherCalculator.GatherSeconds(node.Def.Gather.TimeSec, LevelOf(skill));
            // SYS-CRAFT-02: a better tool is faster.
            if (TryGetComponent<InventoryNetwork>(out var inventory) && MatchingTool(inventory, node.Def.Gather) is { } tool)
                _gatherSeconds = ToolTiers.HarvestSeconds(_gatherSeconds, tool.ToolPower);
            _gatherSeconds /= GatherSpeed();
        }

        void CancelGather() => Gathering = null;

        // ------------------------------------------------------------------ hauling (SYS-HUNT-01 §Carrying)

        /// <summary>The carcass this player is dragging or helping to carry, or null.</summary>
        public Creature Hauling { get; private set; }

        /// <summary>True when someone else leads this carcass and this player is the second pair of hands.</summary>
        public bool HaulHelping { get; private set; }

        /// <summary>Movement multiplier from hauling: dragging alone ×0.4, carried by two ×0.8, otherwise 1.</summary>
        public float HaulSpeed =>
            Hauling == null ? 1f : CarriersOf(Hauling) >= CarcassCalculator.CoopMinPlayers ? CarcassCalculator.CoopSpeedMult : CarcassCalculator.DragSpeedMult;

        /// <summary>Two carriers further apart than this and the helper lets go. [invented]</summary>
        const float CoopLeashTiles = 2.5f;

        /// <summary>A dragged carcass trails this far behind. [invented]</summary>
        const float DragTrailTiles = 0.8f;

        static int CarriersOf(Creature carcass) => All.Count(p => p.Hauling == carcass);

        public void RequestHaul() => CmdHaul();

        [ServerRpc]
        void CmdHaul()
        {
            if (IsDead) return;
            if (Hauling != null)
            {
                Hauling = null;
                HaulHelping = false;
                return;
            }
            var director = CreatureDirector.Instance;
            var carcass = director != null ? director.NearestCarcass(transform.position, ReachTiles) : null;
            if (carcass == null) return;
            CancelGather();
            Butchering = null;
            // Light enough: into the bag.
            if (CarcassCalculator.ClassFor(carcass.Weight) != CarryClass.WorldOnly
                && DefRegistry.TryGet<ItemDef>(Modding.Defs.CarcassItems.IdFor(carcass.Def.Id), out var item)
                && TryGetComponent<InventoryNetwork>(out var inventory))
            {
                var body = new ItemWear(1, 1) { Carcass = director.TakeCarcass(carcass) };
                GiveItem(inventory, item, 1, body);
                return;
            }
            // Too heavy: drag it — or, if someone already is, take the other end.
            HaulHelping = All.Any(p => p != this && p.Hauling == carcass);
            Hauling = carcass;
        }

        void UpdateHaul()
        {
            if (Hauling == null) return;
            var director = CreatureDirector.Instance;
            if (IsDead || director == null || !director.Carcasses.Contains(Hauling))
            {
                Hauling = null;
                HaulHelping = false;
                return;
            }
            var leader = All.FirstOrDefault(p => p.Hauling == Hauling && !p.HaulHelping);
            if (HaulHelping)
            {
                // The helper lets go if they drift apart (or the leader put it down): it's back to one person dragging.
                if (leader == null || Vector2.Distance(leader.transform.position, transform.position) > CoopLeashTiles)
                {
                    Hauling = null;
                    HaulHelping = false;
                }
                return;
            }
            var helper = All.FirstOrDefault(p => p != this && p.Hauling == Hauling && p.HaulHelping);
            Vector2 at = transform.position;
            Vector2 to;
            if (helper != null) to = Vector2.Lerp(at, helper.transform.position, 0.5f); // carried between the two
            else
            {
                var heading = TryGetComponent<PlayerMovement>(out var movement) ? movement.LastDirection : Vector2.down;
                to = at - heading * DragTrailTiles;
            }
            CreatureDirector.MoveCarcass(Hauling, to);
        }

        // ------------------------------------------------------------------ butchery (SYS-HUNT-01)

        const string KnifeTag = "tool/knife";
        static readonly NamespacedId CookingSkill = NamespacedId.Parse("isle:cooking");

        /// <summary>The carcass being butchered, or null. Server state the owning host's HUD reads.</summary>
        public Creature Butchering { get; private set; }

        public float ButcherProgress => Butchering == null || _butcherSeconds <= 0f ? 0f : Mathf.Clamp01((Time.time - _butcherStartedAt) / _butcherSeconds);

        float _butcherStartedAt, _butcherSeconds;
        Vector2 _butcherFrom;

        void StartButcher(Creature carcass)
        {
            // People butchering the same carcass together split the time (up to 3).
            var together = 1 + All.Count(p => p != this && p.Butchering == carcass);
            Butchering = carcass;
            _butcherFrom = transform.position;
            _butcherStartedAt = Time.time;
            _butcherSeconds = ButcheryCalculator.ButcherSeconds(carcass.Weight, LevelOf(CookingSkill), together);
        }

        void UpdateButcher()
        {
            if (Butchering == null) return;
            var director = CreatureDirector.Instance;
            if (IsDead || director == null || !director.Carcasses.Contains(Butchering) || Vector2.Distance(transform.position, _butcherFrom) > GatherLeashTiles)
            {
                if (!IsDead && director != null && director.Carcasses.Contains(Butchering)) GameFeed.RaiseNotice("@ui.butcher_cancelled");
                Butchering = null;
                return;
            }
            if (Time.time - _butcherStartedAt < _butcherSeconds) return;

            var carcass = Butchering;
            Butchering = null;
            if (!TryGetComponent<InventoryNetwork>(out var inventory)) return;
            var (toolFactor, wearKnife) = BestKnife(inventory);
            var cuts = director.Butcher(carcass, LevelOf(CookingSkill), toolFactor);
            wearKnife?.Invoke();
            // Cooking XP for the work: a base plus a little per kg of meat. [invented]
            var kg = cuts.Where(c => DefRegistry.TryGet<ItemDef>(c.Item, out _)).Sum(c => c.Count);
            AwardXp(new XpAward { Skill = CookingSkill, Base = ButcherXpBase, PerUnit = ButcherXpPerPiece }, kg);
            foreach (var (item, count) in cuts) GiveItem(inventory, item, count);
        }

        const float ButcherXpBase = 5f;
        const float ButcherXpPerPiece = 2f;

        /// <summary>SYS-HUNT-01 toolFactor: the best working knife carried (hand or bags) by its material's butcher factor,
        /// else bare hands. Also how to wear that knife by one use.</summary>
        (float Factor, System.Action Wear) BestKnife(InventoryNetwork inventory)
        {
            var best = ButcheryCalculator.BareHandsToolFactor;
            System.Action wear = null;
            float FactorOf(ItemDef item) =>
                item?.Tags != null && item.Tags.Contains(KnifeTag) && item.Material.IsValid && DefRegistry.TryGet<MaterialDef>(item.Material, out var m) ? m.ButcherFactor : 0f;
            var inHand = inventory.Slots.Working(MainHandSlot);
            if (FactorOf(inHand) > best)
            {
                best = FactorOf(inHand);
                wear = () => WearSlot(inventory, MainHandSlot);
            }
            foreach (var container in inventory.Containers())
            foreach (var placed in container.Placements)
            {
                if (placed.Wear is { Broken: true } || FactorOf(placed.Item) <= best) continue;
                best = FactorOf(placed.Item);
                var (c, p) = (container, placed);
                wear = () => InventoryOps.WearPlacement(c, p);
            }
            return (best, wear);
        }

        void UpdateGather()
        {
            if (Gathering == null) return;
            if (IsDead || Vector2.Distance(transform.position, _gatherFrom) > GatherLeashTiles || !Gathering.IsHarvestable)
            {
                if (!IsDead && Gathering.IsHarvestable) GameFeed.RaiseNotice("@ui.gather_cancelled");
                CancelGather();
                return;
            }
            if (Time.time - _gatherStartedAt < _gatherSeconds) return;

            var node = Gathering;
            Gathering = null;
            var world = IslandWorld.Instance;
            if (world == null || !TryGetComponent<Vitals>(out var vitals) || !TryGetComponent<InventoryNetwork>(out var inventory)) return;
            if (!world.TryHarvest(node, out var item, out var count)) return;
            vitals.SpendStamina(node.Def.Gather.StaminaCost);
            AwardXp(node.Def.Gather.Xp, count);
            if (MatchingTool(inventory, node.Def.Gather) != null)
            {
                count += node.Def.Gather.ToolBonus;
                WearSlot(inventory, MainHandSlot);
            }
            GiveItem(inventory, item, count);
        }

        /// <summary>The fight in progress, or null. Server state; the owning host's HUD reads it directly.
        /// ponytail: not synced to a remote client — Phase 10.</summary>
        public TensionMinigame Fight { get; private set; }
        public FishDef FightFish { get; private set; }
        public float FightWeightKg { get; private set; }

        Vector2 _fightSpot;
        bool _reeling;
        bool _sentReeling;

        [ServerRpc]
        void CmdReel(bool reeling) => _reeling = reeling;

        void FixedUpdate()
        {
            if (!IsServer) return;
            UpdateGather();
            UpdateButcher();
            UpdateHaul();
            UpdateCast();
            if (Fight == null) return;
            if (IsDead || Vector2.Distance(transform.position, _fightSpot) > FightLeashTiles)
            {
                EndFight("@ui.fish_lost");
                return;
            }

            Fight.Step(Time.fixedDeltaTime, _reeling);
            switch (Fight.Result)
            {
                case FightResult.Caught:
                    if (TryGetComponent<InventoryNetwork>(out var inventory))
                        foreach (var yield in FightFish.Butcher?.Yields ?? Array.Empty<ButcherYield>())
                            GiveItem(inventory, yield.Item, Mathf.Max(1, Mathf.RoundToInt(FightWeightKg * (FightFish.Butcher?.EdibleRatio ?? 1f) * yield.Share / FishUnitKg)));
                    AwardXp(FightFish.Xp, FightWeightKg);
                    EndFight("@ui.fish_caught");
                    break;
                case FightResult.LineBroke:
                    EndFight("@ui.line_broke");
                    break;
                case FightResult.HookSlipped:
                    EndFight("@ui.hook_slipped");
                    break;
            }
        }

        void EndFight(string noticeKey)
        {
            GameFeed.RaiseNotice(noticeKey);
            Fight = null;
            FightFish = null;
            _reeling = false;
        }

        /// <summary>Server clock for the current draw; -1 when not drawing.</summary>
        float _serverDrawStart = -1f;

        /// <summary>SYS-SURV-01 §Stamina table: "holding bow charge 5/s".</summary>
        const float BowDrawStaminaPerSecond = 5f;

        /// <summary>Arrows leave from just in front of the player, not its centre.</summary>
        const float MuzzleOffsetTiles = 0.6f;

        [ServerRpc]
        void CmdBeginDraw()
        {
            if (IsDead || !TryGetComponent<InventoryNetwork>(out var inventory)) return;
            var weapon = EquippedWeapon(inventory);
            if (weapon == null || !IsRanged(weapon)) return;
            if (BestAmmo(inventory, weapon) == null)
            {
                GameFeed.RaiseNotice("@ui.no_ammo");
                return;
            }
            _serverDrawStart = Time.time;
        }

        /// <summary>SYS-COMBAT-01 §Ranged: charge (measured here, not trusted from the client), sway by stance,
        /// ammo spent, then a projectile. Range falloff applies when it lands.</summary>
        [ServerRpc]
        void CmdLoose(float aimX, float aimY)
        {
            if (_serverDrawStart < 0f) return;
            var charge = Time.time - _serverDrawStart;
            _serverDrawStart = -1f;
            if (IsDead || Time.time < _nextAttackAt) return;
            if (!TryGetComponent<Vitals>(out var vitals) || !TryGetComponent<InventoryNetwork>(out var inventory)) return;

            var weapon = EquippedWeapon(inventory);
            if (weapon == null || !IsRanged(weapon) || Projectiles.Instance == null) return;

            var stance = CurrentStance();
            if (!RangedCalculator.CanAim(stance))
            {
                GameFeed.RaiseNotice("@ui.cant_aim");
                return;
            }

            var ammo = BestAmmo(inventory, weapon);
            if (ammo == null || InventoryOps.TakeOne(inventory.Containers(), ammo.Id) == null)
            {
                GameFeed.RaiseNotice("@ui.no_ammo");
                return;
            }
            WearSlot(inventory, MainHandSlot); // each arrow loosed wears the bow
            vitals.SpendStamina(BowDrawStaminaPerSecond * charge);
            if (weapon.AttackSpeed > 0f) _nextAttackAt = Time.time + 1f / weapon.AttackSpeed;

            var origin = (Vector2)transform.position;
            var sway = RangedCalculator.SwayRadiusTiles(LevelOf(weapon.CombatSkill), stance) * Buffs.BuffEffects.Mult(vitals.ActiveBuffs(), "aim_sway_mult");
            var aim = new Vector2(aimX, aimY) + UnityEngine.Random.insideUnitCircle * sway;
            var direction = (aim - origin).sqrMagnitude > 0.0001f ? (aim - origin).normalized : Vector2.right;
            var damage = PowerCalculator.FinalPower(weapon.BasePower, LevelOf(weapon.CombatSkill)) * RangedCalculator.ChargeMult(charge)
                         * Buffs.BuffEffects.Mult(vitals.ActiveBuffs(), "ranged_power_mult")
                         * (ammo.AmmoPower > 0f ? ammo.AmmoPower : 1f);

            Projectiles.Instance.Fire(origin + direction * MuzzleOffsetTiles, direction, weapon.ProjectileSpeed, damage, weapon.DamageType ?? DamageTypes.Pierce,
                loot => { foreach (var (item, count) in loot) GiveItem(inventory, item, count); },
                dealt => AwardCombatXp(weapon, dealt));
        }

        Stance CurrentStance()
        {
            if (!TryGetComponent<PlayerMovement>(out var movement)) return Stance.Standing;
            if (movement.IsSprinting) return Stance.Sprinting;
            return movement.IsMoving ? Stance.Moving : Stance.Standing;
        }

        /// <summary>SYS-FISH-01 prototype revision: cast range, bite wait and hook window. All [invented].</summary>
        const float CastRangeTiles = 8f;
        const float MinBiteSeconds = 4f, MaxBiteSeconds = 12f;
        const float HookWindowSeconds = 1f;

        /// <summary>The line in the water, or null. Server state; the host's HUD draws the bobber and the "!".</summary>
        public FishingCast Cast { get; private set; }
        public Vector2 CastPoint { get; private set; }

        Vec2Int _castTile;
        string _castRig;
        bool _hookClicked;

        public void RequestCast(Vector2 at) => CmdCast(at.x, at.y);
        public void RequestHook() => CmdHook();

        /// <summary>Throws the line at a water tile within range. The fish isn't chosen yet — that happens at the bite.</summary>
        [ServerRpc]
        void CmdCast(float x, float y)
        {
            if (IsDead || Cast != null || Fight != null) return;
            if (Swimming)
            {
                GameFeed.RaiseNotice("@ui.swimming_two_hands");
                return;
            }
            var world = IslandWorld.Instance;
            if (world == null || !TryGetComponent<InventoryNetwork>(out var inventory)) return;

            var target = new Vector2(x, y);
            var tile = IslandWorld.WorldToTile(target);
            var body = world.WaterAt(tile);
            if (body?.Fishing == null || Vector2.Distance(transform.position, IslandWorld.TileToWorld(tile)) > CastRangeTiles)
            {
                GameFeed.RaiseNotice("@ui.cast_needs_water");
                return;
            }

            // No fishing bare-handed: the best rod carried comes out, or there's no cast.
            if (!EquipBestTool(RodTag))
            {
                GameFeed.RaiseNotice("@ui.needs_rod");
                return;
            }
            var held = inventory.Slots.Get(MainHandSlot);
            _castRig = RodRig;
            if (held.Requires != null && LevelOf(held.Requires.Skill) < held.Requires.Level)
            {
                // Below the rod's level it's just a line on a stick: a bite lands the fish, no fight.
                GameFeed.RaiseNotice("@ui.rod_locked");
                _castRig = HandlineRig;
            }

            CancelGather();
            _castTile = tile;
            CastPoint = IslandWorld.TileToWorld(tile);
            _fightSpot = transform.position;
            _hookClicked = false;
            Cast = new FishingCast(UnityEngine.Random.Range(MinBiteSeconds, MaxBiteSeconds), HookWindowSeconds);
        }

        [ServerRpc]
        void CmdHook() => _hookClicked = true;

        /// <summary>F again with a line out pulls it back in.</summary>
        [ServerRpc]
        void CmdReelIn() => Cast = null;

        void UpdateCast()
        {
            if (Cast == null) return;
            if (IsDead || Vector2.Distance(transform.position, _fightSpot) > FightLeashTiles)
            {
                Cast = null;
                GameFeed.RaiseNotice("@ui.reeled_in");
                return;
            }

            var before = Cast.State;
            Cast.Step(Time.fixedDeltaTime, _hookClicked);
            _hookClicked = false;
            if (before == CastState.Waiting && Cast.State == CastState.Bite) GameFeed.RaiseNotice("@ui.bite");

            if (Cast.State == CastState.Missed)
            {
                Cast = null;
                GameFeed.RaiseNotice("@ui.fish_lost");
            }
            else if (Cast.State == CastState.Hooked)
            {
                Cast = null;
                LandOrFight();
            }
        }

        /// <summary>Hooked: roll the species at the bobber's depth and water, then a rod fights it and a handline lands it.</summary>
        void LandOrFight()
        {
            var world = IslandWorld.Instance;
            if (world == null || !TryGetComponent<InventoryNetwork>(out var inventory)) return;
            var body = world.WaterAt(_castTile);
            if (body?.Fishing == null) return;

            var fishingLevel = LevelOf(body.Fishing.Skill);
            var clock = WorldTime.Instance != null ? WorldTime.Instance.Clock : null;
            var conditions = new FishingConditions(
                depth: world.DepthAt(_castTile),
                waterTemp: WeatherController.Instance != null ? WeatherController.Instance.AmbientTemp : 0f,
                terrain: TerrainOf(body.Tags),
                time: clock != null ? clock.Phase.ToString().ToLowerInvariant() : string.Empty,
                fishingLevel: fishingLevel);

            var fish = FishSelector.Select(DefRegistry.All<FishDef>(), conditions, _castRig, UnityEngine.Random.value);
            if (fish?.Butcher?.Yields == null)
            {
                GameFeed.RaiseNotice("@ui.no_bite");
                return;
            }

            if (_castRig == RodRig && fish.Fight != null)
            {
                // SYS-FISH-01: a rod hooks a fish of its own rolled weight and the fight decides the catch.
                var dist = fish.WeightDist;
                FightWeightKg = dist != null
                    ? WeightRoll.Sample(dist.Mean, dist.Sigma, dist.Min, dist.Max, Mathf.Max(UnityEngine.Random.value, 1e-6f), UnityEngine.Random.value)
                    : 1f;
                FightFish = fish;
                _reeling = false;
                Fight = new TensionMinigame(fish.Fight.Pattern, fish.Fight.TensionWindow, fishingLevel, fish.Fight.Stamina,
                    TensionMinigame.DrainMult(FightWeightKg, fish.Fight.CoopThresholdKg, anglers: 1));
                GameFeed.RaiseNotice("@ui.fish_on");
                return;
            }

            foreach (var yield in fish.Butcher.Yields)
                GiveItem(inventory, yield.Item, yield.Count > 0 ? yield.Count : 1);
            AwardXp(fish.Xp, 0f);
        }

        /// <summary>Rest at a lit campfire at night: skip to morning and refill stamina. Prototype rule — see
        /// PROJECT_STATE.md §Decided without a spec.</summary>
        [ServerRpc]
        void CmdRest()
        {
            if (IsDead) return;
            var clock = WorldTime.Instance != null ? WorldTime.Instance.Clock : null;
            if (clock == null || clock.Phase != DayPhase.Night)
            {
                GameFeed.RaiseNotice("@ui.rest_only_night");
                return;
            }
            if (WorldObjectRegistry.NearestDistanceTiles(transform.position, CampfireTag) > ReachTiles)
            {
                GameFeed.RaiseNotice("@ui.rest_need_fire");
                return;
            }
            if (!TryGetComponent<Vitals>(out var vitals)) return;

            clock.AdvanceToNextMinuteOfDay(WorldClock.RestWakeMinute);
            vitals.Rest();
            if (TryGetComponent<DeathHandler>(out var death)) death.SetLastShelter(transform.position);
            GameFeed.RaiseNotice("@ui.rested");
        }

        [ServerRpc]
        void CmdCraft(string recipeText, string materialText)
        {
            if (IsDead) return;
            if (!NamespacedId.TryParse(recipeText, out var recipeId, out _)) return;
            if (!DefRegistry.TryGet<CraftRecipeDef>(recipeId, out var recipe) || recipe.Output == null) return;
            if (!TryGetComponent<InventoryNetwork>(out var inventory)) return;

            // SYS-CRAFT-02: a template recipe needs a material it accepts; the material can move it to another station
            // and raise the level it needs, and adds its units to the bill.
            MaterialDef material = null;
            var needs = recipe.Ingredients ?? System.Array.Empty<IngredientRef>();
            var output = recipe.Output.Item;
            if (recipe.Stuff != null)
            {
                if (!NamespacedId.TryParse(materialText, out var materialId, out _) || !DefRegistry.TryGet(materialId, out material)) return;
                var made = StuffCrafting.OutputFor(recipe, material);
                if (made == null) return;
                output = made.Id;
                needs = needs.Append(new IngredientRef { Item = material.Item, Count = StuffCrafting.CountFor(recipe) }).ToArray();
            }
            var station = StuffCrafting.StationFor(recipe, material);
            if (station.IsValid && !WorldObjectRegistry.IsActiveNear(transform.position, station, ReachTiles)) return;
            if (!MeetsSkills(recipe.Skills) || (material != null && LevelOf(PrimarySkill(recipe)) < material.CraftLevel))
            {
                GameFeed.RaiseNotice("@ui.skill_too_low");
                return;
            }

            var containers = inventory.Containers();
            if (!CraftingCalculator.HasIngredients(needs, CraftingCalculator.StockOf(containers))) return;
            if (!DefRegistry.TryGet<ItemDef>(output, out _)) return;

            // No room check up front: paying frees space, and anything that still doesn't fit drops at your feet.
            ConsumeIngredients(containers, needs);
            AwardXp(recipe.Xp, needs.Sum(i => i.Count));
            GiveItem(inventory, output, recipe.Output.Count);
        }

        static NamespacedId PrimarySkill(CraftRecipeDef recipe) => StuffCrafting.PrimarySkill(recipe);

        // ------------------------------------------------------------------ repair (SYS-CRAFT-02)

        /// <summary>The recipe that makes <paramref name="item"/> — it also sets where and for what it's repaired. Null
        /// when nothing makes it (not repairable).</summary>
        public static CraftRecipeDef RepairRecipe(ItemDef item) => StuffCrafting.RecipeFor(item);

        /// <summary>Repairs the item in an equip slot.</summary>
        public void RequestRepairEquipped(string slot) => CmdRepairEquipped(slot);

        /// <summary>Repairs the item placed at <paramref name="position"/> in container <paramref name="container"/>
        /// (index into <see cref="InventoryNetwork.Containers"/>).</summary>
        public void RequestRepairPlaced(int container, Vec2Int position) => CmdRepairPlaced(container, position.X, position.Y);

        [ServerRpc]
        void CmdRepairEquipped(string slot)
        {
            if (IsDead || !TryGetComponent<InventoryNetwork>(out var inventory)) return;
            TryRepair(inventory, inventory.Slots.Get(slot), inventory.Slots.WearOf(slot));
        }

        [ServerRpc]
        void CmdRepairPlaced(int container, int x, int y)
        {
            if (IsDead || !TryGetComponent<InventoryNetwork>(out var inventory)) return;
            var containers = inventory.Containers();
            if (container < 0 || container >= containers.Count) return; // never trust client values
            if (containers[container].PlacementAt(new Vec2Int(x, y)) is not { } placed) return;
            TryRepair(inventory, placed.Item, placed.Wear);
        }

        /// <summary>SYS-CRAFT-02 §Repair: at the item's own station, meeting its recipe's skills, for half its inputs
        /// (rounded up); the maximum drops to ×0.92 and the item is restored to it. The wear is shared with wherever the
        /// item sits, so fixing it in place is enough.</summary>
        bool TryRepair(InventoryNetwork inventory, ItemDef item, ItemWear wear)
        {
            if (item == null) return false;
            var recipe = RepairRecipe(item);
            if (recipe == null)
            {
                GameFeed.RaiseNotice("@ui.repair_none");
                return false;
            }
            if (wear == null || !wear.NeedsRepair)
            {
                GameFeed.RaiseNotice("@ui.repair_full");
                return false;
            }
            var material = StuffCrafting.MaterialOf(item);
            var station = StuffCrafting.StationFor(recipe, material);
            if (station.IsValid && !WorldObjectRegistry.IsActiveNear(transform.position, station, ReachTiles))
            {
                GameFeed.RaiseNotice("@ui.repair_station");
                return false;
            }
            if (!MeetsSkills(recipe.Skills) || LevelOf(PrimarySkill(recipe)) < StuffCrafting.LevelFor(recipe, material))
            {
                GameFeed.RaiseNotice("@ui.skill_too_low");
                return false;
            }
            var cost = StuffCrafting.RepairCost(item, recipe);
            var containers = inventory.Containers();
            if (!CraftingCalculator.HasIngredients(cost, CraftingCalculator.StockOf(containers)))
            {
                GameFeed.RaiseNotice("@ui.repair_missing");
                return false;
            }
            ConsumeIngredients(containers, cost);
            wear.Repair();
            if (recipe.Xp != null)
                AwardXp(new XpAward { Skill = recipe.Xp.Skill, Base = recipe.Xp.Base * RepairCalculator.RepairXpShare }, 0f);
            GameFeed.RaiseNotice($"@ui.repaired|{item.Name}");
            return true;
        }

        /// <summary>Eats one of an item. A dish brings its cooked nutrition and buffs; anything else is eaten by the
        /// def marked <c>eat_raw</c> (SYS-COOK-01 <c>isle:raw</c>), so raw food gets raw's modifiers and reactions.
        /// Either way satiety fatigue scales the nutrition.</summary>
        [ServerRpc]
        void CmdUse(string itemText)
        {
            if (IsDead) return;
            if (!NamespacedId.TryParse(itemText, out var itemId, out _)) return;
            if (!TryGetComponent<InventoryNetwork>(out var inventory) || !TryGetComponent<Vitals>(out var vitals)) return;

            // Dishes aren't registered defs, so look in the bags rather than the registry.
            var containers = inventory.Containers();
            var item = containers.SelectMany(c => c.Placements).Select(p => p.Item).FirstOrDefault(i => i.Id == itemId && i.Nutrition != null);
            if (item == null || InventoryOps.TakeOne(containers, itemId) == null) return;

            float hunger, thirst, durationMult;
            IEnumerable<NamespacedId> buffs;
            string signature;
            var raw = DishFactory.IsDish(item) ? null : DefRegistry.All<CookMethodDef>().FirstOrDefault(m => m.EatRaw);
            if (raw != null)
            {
                var result = CookingResolver.Resolve(raw, new[] { item }, CookingLevelFor(raw), failureRoll: 1f);
                (hunger, thirst, durationMult) = (result.Hunger, result.Thirst, result.BuffDurationMult);
                buffs = result.Buffs.Select(x => x.Buff);
                signature = DishFactory.SignatureFor(raw, item);
            }
            else
            {
                (hunger, thirst, durationMult) = (item.Nutrition.Hunger, item.Nutrition.Thirst, item.BuffDurationMult);
                buffs = item.Buffs ?? Array.Empty<NamespacedId>();
                signature = item.DishSignature ?? item.Id.Value;
            }

            var fatigue = vitals.Satiety.Eat(signature, Vitals.NowMinutes);
            vitals.Eat(hunger * fatigue, thirst * fatigue);
            foreach (var buff in buffs) vitals.GrantBuff(buff, durationMult);
            if (fatigue < 1f) GameFeed.RaiseNotice("@ui.tired_of_dish");
        }

        /// <summary>The level of the skill a method unlocks with (its <c>unlock_skill.skill</c>) — cooking for the core methods.</summary>
        int CookingLevelFor(CookMethodDef method) => method.UnlockSkill != null ? LevelOf(method.UnlockSkill.Skill) : 1;

        /// <summary>Every <c>skills</c> requirement on a recipe met (SCHEMA §Craft recipes).</summary>
        public bool MeetsSkills(SkillRequirement[] requirements) =>
            requirements == null || requirements.All(r => LevelOf(r.Skill) >= r.Level);

        public void RequestCook(string methodId, IEnumerable<string> ingredientIds) => CmdCook(methodId, string.Join(",", ingredientIds));

        /// <summary>SYS-COOK-01 §Resolution: validate (count, unlock level, station), take the ingredients, resolve,
        /// and hand over the dish — or the method's failure result.</summary>
        [ServerRpc]
        void CmdCook(string methodText, string ingredientsText)
        {
            if (IsDead || !TryGetComponent<InventoryNetwork>(out var inventory)) return;
            if (!NamespacedId.TryParse(methodText, out var methodId, out _) || !DefRegistry.TryGet<CookMethodDef>(methodId, out var method)) return;
            var ids = (ingredientsText ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (!CookingResolver.CountAllowed(method, ids.Length)) return;
            if (method.UnlockSkill != null && method.UnlockSkill.Level > CookingLevelFor(method))
            {
                GameFeed.RaiseNotice("@ui.method_locked");
                return;
            }
            if (method.Station.IsValid && !WorldObjectRegistry.IsActiveNear(transform.position, method.Station, ReachTiles))
            {
                GameFeed.RaiseNotice("@ui.need_station");
                return;
            }

            // Check everything is held (counting repeats) before taking anything.
            var containers = inventory.Containers();
            var parsed = new List<NamespacedId>();
            foreach (var text in ids)
            {
                if (!NamespacedId.TryParse(text, out var id, out _)) return;
                parsed.Add(id);
            }
            foreach (var group in parsed.GroupBy(id => id))
                if (InventoryOps.Count(containers, group.Key) < group.Count()) return;

            var ingredients = new List<ItemDef>();
            foreach (var id in parsed)
            {
                var taken = InventoryOps.TakeOne(containers, id);
                if (taken?.Nutrition == null) return;
                ingredients.Add(taken);
            }

            var result = CookingResolver.Resolve(method, ingredients, CookingLevelFor(method), UnityEngine.Random.value);
            if (result.Failed)
            {
                if (method.Failure.Result.IsValid) GiveItem(inventory, method.Failure.Result, 1);
                GameFeed.RaiseNotice("@ui.cook_failed");
                return;
            }
            GiveItem(inventory, DishFactory.Create(method, ingredients, result), 1);
            AwardXp(method.Xp, ingredients.Count);
        }

        /// <summary>Equips an item into its own slot (<c>equip_slot</c>), putting whatever was there back into the
        /// bags. A bag that still holds items can't be swapped out (EquipSlots' rule: items never vanish with
        /// their container).</summary>
        [ServerRpc]
        void CmdEquipItem(string itemText)
        {
            if (IsDead) return;
            if (!NamespacedId.TryParse(itemText, out var itemId, out _)) return;
            if (!TryGetComponent<InventoryNetwork>(out var inventory)) return;

            // The placement itself, not just its def: a worn tool keeps its wear when it's equipped (SYS-CRAFT-02).
            GridInventory from = null;
            Placement placed = default;
            foreach (var container in inventory.Containers())
            foreach (var p in container.Placements)
                if (from == null && p.Item.Id == itemId && !string.IsNullOrEmpty(p.Item.EquipSlot))
                    (from, placed) = (container, p);
            if (from == null) return;
            var item = placed.Item;
            var slot = item.EquipSlot;

            var previous = inventory.Slots.Get(slot);
            ItemWear previousWear = null;
            if (previous != null && !inventory.Slots.Unequip(slot, out previousWear))
            {
                GameFeed.RaiseNotice("@ui.empty_bag_first");
                return;
            }
            InventoryOps.SetCount(from, placed, placed.Count - 1);
            inventory.Slots.TryEquip(slot, item, placed.Wear);
            if (previous != null) GiveItem(inventory, previous, 1, previousWear);
        }

        [ServerRpc]
        void CmdUnequipSlot(string slot)
        {
            if (IsDead || !TryGetComponent<InventoryNetwork>(out var inventory)) return;
            var current = inventory.Slots.Get(slot);
            if (current == null) return;
            if (!inventory.Slots.Unequip(slot, out var wear))
            {
                GameFeed.RaiseNotice("@ui.empty_bag_first");
                return;
            }
            if (!InventoryOps.TryGive(inventory.Containers(), current, 1, wear))
            {
                inventory.Slots.TryEquip(slot, current, wear); // nowhere to put it: keep wearing it
                GameFeed.RaiseNotice("@ui.bag_full");
            }
        }

        [ServerRpc]
        void CmdPlace(string itemText, float x, float y)
        {
            if (IsDead) return;
            if (!NamespacedId.TryParse(itemText, out var itemId, out _)) return;
            if (!TryGetComponent<InventoryNetwork>(out var inventory)) return;
            var at = new Vector2(x, y);
            if (!CanPlaceAt(at))
            {
                GameFeed.RaiseNotice("@ui.cant_place");
                return;
            }

            if (!DefRegistry.TryGet<ItemDef>(itemId, out var kit) || !kit.Places.IsValid) return;
            if (!DefRegistry.TryGet<WorldObjectDef>(kit.Places, out var def)) return;
            CropDef crop = null;
            if (kit.Plants.IsValid && !DefRegistry.TryGet(kit.Plants, out crop)) return;
            if (InventoryOps.TakeOne(inventory.Containers(), itemId) == null) return;
            var built = StructureFactory.Build(def, at);
            if (crop != null) StructureFactory.Plant(built, crop, WorldTime.Instance != null ? WorldTime.Instance.Clock.TotalMinutes : 0);
        }

        /// <summary>Moves one stack from the bag into the open container within reach.</summary>
        [ServerRpc]
        void CmdStore(string itemText)
        {
            if (IsDead || !TryGetComponent<InventoryNetwork>(out var inventory)) return;
            var box = StructureFactory.NearestStorage(transform.position, ReachTiles);
            if (box == null) return;
            foreach (var container in inventory.Containers())
            {
                var held = container.Placements.FirstOrDefault(p => p.Item.Id.Value == itemText);
                if (held.Item == null) continue;
                if (!container.TryMoveTo(box.Contents, held)) GameFeed.RaiseNotice("@ui.box_full");
                return;
            }
        }

        [ServerRpc]
        void CmdTake(string itemText)
        {
            if (IsDead || !TryGetComponent<InventoryNetwork>(out var inventory)) return;
            var box = StructureFactory.NearestStorage(transform.position, ReachTiles);
            if (box == null) return;
            var held = box.Contents.Placements.FirstOrDefault(p => p.Item.Id.Value == itemText);
            if (held.Item == null) return;
            if (InventoryOps.TryGive(inventory.Containers(), held.Item, held.Count)) box.Contents.Remove(held);
            else GameFeed.RaiseNotice("@ui.bag_full");
        }

        /// <summary>Server-side: a harvest from something in the world (a ripe crop) lands in this player's bags.</summary>
        public void GiveHarvest(NamespacedId item, int count, XpAward xp = null)
        {
            if (TryGetComponent<InventoryNetwork>(out var inventory)) GiveItem(inventory, item, count);
            AwardXp(xp, count);
        }

        /// <summary>Set by the dungeon runtime: is the player swimming here (a flooded room at high tide)?</summary>
        public static Func<Vector2, bool> IsSwimmingAt { get; set; }

        public bool Swimming => IsSwimmingAt != null && IsSwimmingAt(transform.position);

        /// <summary>SYS-DUNG-01: two-handed items can't be used while swimming (a rod casts from dry ground only).</summary>
        bool TwoHandsUnusable()
        {
            if (!Swimming || !TryGetComponent<InventoryNetwork>(out var inventory)) return false;
            if (EquippedWeapon(inventory)?.Grip != "two_hand") return false;
            GameFeed.RaiseNotice("@ui.swimming_two_hands");
            return true;
        }

        WeaponDef EquippedWeapon(InventoryNetwork inventory)
        {
            // A broken weapon fights as bare hands (SYS-CRAFT-02).
            var equipped = inventory.Slots.Working(MainHandSlot);
            if (equipped != null && equipped.Weapon.IsValid && DefRegistry.TryGet<WeaponDef>(equipped.Weapon, out var weapon))
                return weapon;

            var unarmed = DefRegistry.AllWithTag<WeaponDef>(UnarmedTag);
            return unarmed.Count > 0 ? unarmed[0] : null;
        }

        /// <summary>Same, for an item that isn't a registered def (a dish).</summary>
        void GiveItem(InventoryNetwork inventory, ItemDef item, int count, ItemWear wear = null)
        {
            if (InventoryOps.TryGive(inventory.Containers(), item, count, wear))
            {
                GameFeed.RaiseItemGained(item.Id, count);
                return;
            }
            LootPiles.Drop(transform.position, new[] { new LootEntry(item, count, wear) });
            GameFeed.RaiseItemDropped(item.Id, count, transform.position);
        }

        /// <summary>Puts <paramref name="count"/> of an item into the bag, merging onto a matching stack
        /// when one exists. When there's no room it drops on the ground at the player's feet instead.</summary>
        void GiveItem(InventoryNetwork inventory, NamespacedId itemId, int count)
        {
            if (!DefRegistry.TryGet<ItemDef>(itemId, out var item)) return;
            if (InventoryOps.TryGive(inventory.Containers(), item, count))
            {
                GameFeed.RaiseItemGained(itemId, count);
                return;
            }
            LootPiles.Drop(transform.position, new[] { (item, count) });
            GameFeed.RaiseItemDropped(itemId, count, transform.position);
        }

        /// <summary>Takes each requirement in the same order <see cref="CraftingCalculator.HasIngredients"/> counted
        /// it, so a recipe that passed the check always has enough to pay.</summary>
        static void ConsumeIngredients(IReadOnlyList<GridInventory> containers, IngredientRef[] needs)
        {
            if (needs == null) return;
            foreach (var need in needs)
            {
                var stillNeeded = need.Count;
                foreach (var container in containers)
                foreach (var placed in container.Placements.ToList())
                {
                    if (stillNeeded <= 0) break;
                    if (!CraftingCalculator.Matches(need, new Stock(placed.Item.Id, placed.Item.Tags, placed.Count))) continue;
                    var take = Math.Min(stillNeeded, placed.Count);
                    stillNeeded -= take;
                    InventoryOps.SetCount(container, placed, placed.Count - take);
                }
            }
        }

        /// <summary>The <c>water/*</c> tag's suffix, e.g. <c>"saltwater"</c>. Empty when the spot has none.</summary>
        static string TerrainOf(string[] tags)
        {
            var water = tags?.FirstOrDefault(t => t.StartsWith("water/"));
            return water == null ? string.Empty : water.Substring("water/".Length);
        }

        /// <summary>Snake-case def value (<c>"standing_water"</c>) to the <see cref="WaterSource"/> member.</summary>
        static bool TryParseWaterSource(string snake, out WaterSource source)
        {
            var pascal = string.Concat(snake.Split('_').Select(p => p.Length == 0 ? p : char.ToUpperInvariant(p[0]) + p.Substring(1)));
            return Enum.TryParse(pascal, out source);
        }
    }
}
