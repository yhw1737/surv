using System;
using System.Collections.Generic;
using System.Linq;
using FishNet.Object;
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

        /// <summary>Seconds between casts. Stands in for the bite wait, which SYS-FISH-01 doesn't spec for handline. [invented]</summary>
        const float CastCooldownSeconds = 3f;

        /// <summary>No combat skill is tracked on the player yet, so every swing is untrained (SYS-COMBAT-01
        /// skill floor 0.5). ponytail: wire the real melee level from SkillSet when skills reach the HUD.</summary>
        const int UntrainedSkillLevel = 0;

        float _nextAttackAt;

        bool IsDead => TryGetComponent<DeathHandler>(out var death) && death.IsDead;
        float _nextCastAt;

        void Update()
        {
            if (!IsOwner) return;

            var kb = Keyboard.current;
            if (kb != null && kb.eKey.wasPressedThisFrame) RequestInteract();

            var mouse = Mouse.current;
            if (Fight != null)
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
                if (mouse.leftButton.wasPressedThisFrame && !PointerGate.Captured)
                {
                    if (!ranged) RequestAttack();
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

            if (kb != null && kb.fKey.wasPressedThisFrame) RequestFish();
            if (kb != null && kb.rKey.wasPressedThisFrame) RequestRest();
        }

        /// <summary>Owner-local: when the bow started drawing, for the HUD's charge bar. -1 when not drawing.
        /// The server keeps its own clock for the actual charge.</summary>
        public float DrawStartedAt { get; private set; } = -1f;

        /// <summary>Test and tooling entry point for a full ranged shot at <paramref name="aim"/>.</summary>
        public void RequestBeginDraw() => CmdBeginDraw();
        public void RequestLoose(Vector2 aim) => CmdLoose(aim.x, aim.y);

        public bool HoldsRangedWeapon() =>
            TryGetComponent<InventoryNetwork>(out var inventory) && EquippedWeapon(inventory) is { } weapon && weapon.Ammo.IsValid;

        static Vector2? MouseWorld()
        {
            var camera = Camera.main;
            var mouse = Mouse.current;
            if (camera == null || mouse == null) return null;
            var p = mouse.position.ReadValue();
            return camera.ScreenToWorldPoint(new Vector3(p.x, p.y, -camera.transform.position.z));
        }

        public void RequestInteract() => CmdInteract();
        public void RequestAttack() => CmdAttack();
        public void RequestCraft(string recipeId) => CmdCraft(recipeId);
        public void RequestUse(string itemId) => CmdUse(itemId);
        public void RequestFish() => CmdFish();
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
            foreach (var instance in FindObjectsByType<WorldObjectInstance>(FindObjectsSortMode.None))
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

            var pile = LootPiles.Nearest(transform.position, ReachTiles);
            if (pile != null && TryGetComponent<InventoryNetwork>(out var bagOwner))
            {
                var taken = LootPiles.PickUp(pile, bagOwner.Slots, bagOwner.Containers);
                foreach (var (item, count) in taken) GameFeed.RaiseItemGained(item.Id, count);
                if (pile.Items.Count > 0) GameFeed.RaiseNotice("@ui.bag_full");
                return;
            }

            var drink = world.NearestNode(transform.position, ReachTiles, n => n.IsDrinkable);
            if (drink != null)
            {
                if (TryParseWaterSource(drink.Def.Drink.Source, out var source) && TryGetComponent<Vitals>(out var vitals))
                    vitals.Drink(source);
                return;
            }

            var harvest = world.NearestNode(transform.position, ReachTiles, n => n.IsHarvestable);
            var nearStation = WorldObjectRegistry.NearestInteractable(transform.position, ReachTiles);
            // A crate or fire right next to you wins over a tree a little further off.
            if (harvest != null && nearStation != null &&
                Vector2.Distance(transform.position, nearStation.transform.position) < Vector2.Distance(transform.position, harvest.Position))
                harvest = null;
            if (harvest != null)
            {
                if (!TryGetComponent<Vitals>(out var vitals) || !TryGetComponent<InventoryNetwork>(out var inventory)) return;
                if (vitals.Stamina < harvest.Def.Gather.StaminaCost)
                {
                    GameFeed.RaiseNotice("@ui.too_tired");
                    return;
                }
                if (!world.TryHarvest(harvest, out var item, out var count)) return;
                vitals.SpendStamina(harvest.Def.Gather.StaminaCost);
                count += ToolBonus(inventory, harvest.Def.Gather);
                GiveItem(inventory, item, count);
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
        void CmdAttack()
        {
            if (IsDead) return;
            if (Time.time < _nextAttackAt) return;
            if (!TryGetComponent<Vitals>(out var vitals) || !TryGetComponent<InventoryNetwork>(out var inventory)) return;

            var weapon = EquippedWeapon(inventory);
            if (weapon == null || weapon.AttackSpeed <= 0f) return;
            if (vitals.Stamina < weapon.StaminaCost)
            {
                GameFeed.RaiseNotice("@ui.too_tired");
                return;
            }

            _nextAttackAt = Time.time + 1f / weapon.AttackSpeed;
            vitals.SpendStamina(weapon.StaminaCost);
            GameFeed.RaisePlayerSwing(transform.position, weapon.Reach);

            var director = CreatureDirector.Instance;
            if (director == null) return;

            var power = PowerCalculator.FinalPower(weapon.BasePower, UntrainedSkillLevel);
            var damage = DamageResolver.Damage(power, totalArmor: 0f);
            var loot = new List<(NamespacedId Item, int Count)>();
            if (!director.TryStrike(transform.position, weapon.Reach, damage, loot)) return;

            foreach (var (item, count) in loot) GiveItem(inventory, item, count);
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
            if (!IsServer || Fight == null) return;
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
            if (weapon == null || !weapon.Ammo.IsValid) return;
            if (InventoryOps.Count(inventory.Containers(), weapon.Ammo) == 0)
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
            if (weapon == null || !weapon.Ammo.IsValid || Projectiles.Instance == null) return;

            var stance = CurrentStance();
            if (!RangedCalculator.CanAim(stance))
            {
                GameFeed.RaiseNotice("@ui.cant_aim");
                return;
            }

            if (InventoryOps.TakeOne(inventory.Containers(), weapon.Ammo) == null)
            {
                GameFeed.RaiseNotice("@ui.no_ammo");
                return;
            }
            vitals.SpendStamina(BowDrawStaminaPerSecond * charge);
            if (weapon.AttackSpeed > 0f) _nextAttackAt = Time.time + 1f / weapon.AttackSpeed;

            var origin = (Vector2)transform.position;
            var sway = RangedCalculator.SwayRadiusTiles(UntrainedSkillLevel, stance) * Buffs.BuffEffects.Mult(vitals.ActiveBuffs(), "aim_sway_mult");
            var aim = new Vector2(aimX, aimY) + UnityEngine.Random.insideUnitCircle * sway;
            var direction = (aim - origin).sqrMagnitude > 0.0001f ? (aim - origin).normalized : Vector2.right;
            var damage = PowerCalculator.FinalPower(weapon.BasePower, UntrainedSkillLevel) * RangedCalculator.ChargeMult(charge);

            Projectiles.Instance.Fire(origin + direction * MuzzleOffsetTiles, direction, weapon.ProjectileSpeed, damage,
                loot => { foreach (var (item, count) in loot) GiveItem(inventory, item, count); });
        }

        Stance CurrentStance()
        {
            if (!TryGetComponent<PlayerMovement>(out var movement)) return Stance.Standing;
            if (movement.IsSprinting) return Stance.Sprinting;
            return movement.IsMoving ? Stance.Moving : Stance.Standing;
        }

        [ServerRpc]
        void CmdFish()
        {
            if (IsDead) return;
            if (Time.time < _nextCastAt) return;
            var world = IslandWorld.Instance;
            if (world == null || !TryGetComponent<InventoryNetwork>(out var inventory)) return;

            if (Fight != null) return;
            var spot = world.NearestNode(transform.position, ReachTiles, n => n.Def.Fishing != null);
            if (spot == null) return;
            var held = inventory.Slots.Get(MainHandSlot);
            var rig = held?.Tags != null && held.Tags.Contains(RodTag) ? RodRig : HandlineRig;

            _nextCastAt = Time.time + CastCooldownSeconds;
            var clock = WorldTime.Instance != null ? WorldTime.Instance.Clock : null;
            var conditions = new FishingConditions(
                depth: spot.Def.Fishing.Depth,
                waterTemp: WeatherController.Instance != null ? WeatherController.Instance.AmbientTemp : 0f,
                terrain: TerrainOf(spot.Def.Tags),
                time: clock != null ? clock.Phase.ToString().ToLowerInvariant() : string.Empty,
                fishingLevel: 0);

            var fish = FishSelector.Select(DefRegistry.All<FishDef>(), conditions, rig, UnityEngine.Random.value);
            if (fish?.Butcher?.Yields == null)
            {
                GameFeed.RaiseNotice("@ui.no_bite");
                return;
            }

            if (rig == RodRig && fish.Fight != null)
            {
                // SYS-FISH-01: a rod hooks a fish of its own rolled weight and the fight decides the catch.
                var dist = fish.WeightDist;
                FightWeightKg = dist != null
                    ? WeightRoll.Sample(dist.Mean, dist.Sigma, dist.Min, dist.Max, Mathf.Max(UnityEngine.Random.value, 1e-6f), UnityEngine.Random.value)
                    : 1f;
                FightFish = fish;
                _fightSpot = transform.position;
                _reeling = false;
                Fight = new TensionMinigame(fish.Fight.Pattern, fish.Fight.TensionWindow, fishingLevel: 0, fish.Fight.Stamina,
                    TensionMinigame.DrainMult(FightWeightKg, fish.Fight.CoopThresholdKg, anglers: 1));
                GameFeed.RaiseNotice("@ui.fish_on");
                return;
            }

            foreach (var yield in fish.Butcher.Yields)
                GiveItem(inventory, yield.Item, yield.Count > 0 ? yield.Count : 1);
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
        void CmdCraft(string recipeText)
        {
            if (IsDead) return;
            if (!NamespacedId.TryParse(recipeText, out var recipeId, out _)) return;
            if (!DefRegistry.TryGet<CraftRecipeDef>(recipeId, out var recipe) || recipe.Output == null) return;
            if (!TryGetComponent<InventoryNetwork>(out var inventory)) return;
            if (recipe.Station.IsValid && !WorldObjectRegistry.IsActiveNear(transform.position, recipe.Station, ReachTiles)) return;

            var containers = inventory.Containers();
            if (!CraftingCalculator.HasIngredients(recipe.Ingredients, CraftingCalculator.StockOf(containers))) return;
            if (!DefRegistry.TryGet<ItemDef>(recipe.Output.Item, out _)) return;

            // No room check up front: paying frees space, and anything that still doesn't fit drops at your feet.
            ConsumeIngredients(containers, recipe.Ingredients);
            GiveItem(inventory, recipe.Output.Item, recipe.Output.Count);
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
                var result = CookingResolver.Resolve(raw, new[] { item }, CookingLevel, failureRoll: 1f);
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

        /// <summary>No cooking skill is tracked yet; SkillSet's own starting level is 1. ponytail: read the player's
        /// SkillSet once skills are on the player.</summary>
        const int CookingLevel = 1;

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
            if (method.UnlockSkill != null && method.UnlockSkill.Level > CookingLevel)
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

            var result = CookingResolver.Resolve(method, ingredients, CookingLevel, UnityEngine.Random.value);
            if (result.Failed)
            {
                if (method.Failure.Result.IsValid) GiveItem(inventory, method.Failure.Result, 1);
                GameFeed.RaiseNotice("@ui.cook_failed");
                return;
            }
            GiveItem(inventory, DishFactory.Create(method, ingredients, result), 1);
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

            var containers = inventory.Containers();
            var item = containers.SelectMany(c => c.Placements).Select(p => p.Item)
                .FirstOrDefault(i => i.Id == itemId && !string.IsNullOrEmpty(i.EquipSlot));
            if (item == null) return;
            var slot = item.EquipSlot;

            var previous = inventory.Slots.Get(slot);
            if (previous != null && !inventory.Slots.Unequip(slot))
            {
                GameFeed.RaiseNotice("@ui.empty_bag_first");
                return;
            }
            InventoryOps.TakeOne(inventory.Containers(), item.Id);
            inventory.Slots.TryEquip(slot, item);
            if (previous != null) GiveItem(inventory, previous.Id, 1);
        }

        [ServerRpc]
        void CmdUnequipSlot(string slot)
        {
            if (IsDead || !TryGetComponent<InventoryNetwork>(out var inventory)) return;
            var current = inventory.Slots.Get(slot);
            if (current == null) return;
            if (!inventory.Slots.Unequip(slot))
            {
                GameFeed.RaiseNotice("@ui.empty_bag_first");
                return;
            }
            if (!InventoryOps.TryGive(inventory.Containers(), current, 1))
            {
                inventory.Slots.TryEquip(slot, current); // nowhere to put it: keep wearing it
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
        public void GiveHarvest(NamespacedId item, int count)
        {
            if (TryGetComponent<InventoryNetwork>(out var inventory)) GiveItem(inventory, item, count);
        }

        WeaponDef EquippedWeapon(InventoryNetwork inventory)
        {
            var equipped = inventory.Slots.Get(MainHandSlot);
            if (equipped != null && equipped.Weapon.IsValid && DefRegistry.TryGet<WeaponDef>(equipped.Weapon, out var weapon))
                return weapon;

            var unarmed = DefRegistry.AllWithTag<WeaponDef>(UnarmedTag);
            return unarmed.Count > 0 ? unarmed[0] : null;
        }

        /// <summary>Same, for an item that isn't a registered def (a dish).</summary>
        void GiveItem(InventoryNetwork inventory, ItemDef item, int count)
        {
            if (InventoryOps.TryGive(inventory.Containers(), item, count))
            {
                GameFeed.RaiseItemGained(item.Id, count);
                return;
            }
            LootPiles.Drop(transform.position, new[] { (item, count) });
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

        /// <summary>Extra units from holding the right tool (SYS-NET-01 §Gather: "tool requirement"). The tool is
        /// matched by tag on the main-hand item, the bonus comes from the node's def.</summary>
        static int ToolBonus(InventoryNetwork inventory, GatherSpec gather)
        {
            if (string.IsNullOrEmpty(gather.ToolTag)) return 0;
            var held = inventory.Slots.Get(MainHandSlot);
            return held?.Tags != null && held.Tags.Contains(gather.ToolTag) ? gather.ToolBonus : 0;
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
