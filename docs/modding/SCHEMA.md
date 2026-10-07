# Mod data schema (Tier 1)

Target: Tier 1 modding — JSON definitions, no code. API version `1`.
Status: draft, finalized during T-011–T-014.

## Why this exists

> The tools we use to make content are the tools players use. (GDD §Modding)

**This schema is ours, not just modders'.** Every fish, creature, and cook method we ship is defined through it. **One hardcoded exception breaks the strategy** — twice and modding stops at retextures.

## Principles

| # | Principle | Reason |
|---|---|---|
| 1 | **All IDs are namespaced** — `modid:name` | Collision avoidance. Ours are `isle:`, no exceptions |
| 2 | **Tags are the only linking mechanism** | A modder adding `["fish","oily"]` gets existing cooking and bait systems for free |
| 3 | **Patch, never overwrite** | Two mods can touch the same item and coexist |
| 4 | **Every file declares `api`** | Compatibility warnings across engine updates |
| 5 | **Validation failures are loud** | Bad definitions error at load, never silently |
| 6 | **Include `$schema`** | Free autocomplete and validation in VS Code |

## Mod layout

```
MyIslandMod/
├─ mod.json                  required
├─ definitions/
│  ├─ items/  creatures/  fish/  crops/
│  ├─ cook_methods/  recipes/  weapons/  artifacts/
│  ├─ enchants/  tags/
├─ patches/*.json            modify existing definitions
├─ assets/{sprites,audio,icons}/
└─ lang/{ko,en}.json
```

Folder name determines type — no `"type"` field inside files, which eliminates a class of silent typo failures.

## Manifest

```json
{
  "$schema": "https://isle.game/schema/v1/mod.json",
  "id": "coolmod",
  "name": "Cold Seas Expansion",
  "version": "1.2.0",
  "authors": ["Author"],
  "api": 1,
  "game_version": ">=0.4.0 <0.6.0",
  "dependencies": [
    { "id": "isle", "version": ">=0.4.0" },
    { "id": "seasonlib", "version": ">=2.0.0", "optional": false }
  ],
  "incompatible": ["oldfishmod"],
  "load_after": ["seasonlib"],
  "server_required": true
}
```

`id` — lowercase, digits, underscores; becomes the namespace for every definition.
`api` — schema version; mismatch refuses to load with clear guidance.
`load_after` — ordering hint; cycles refuse to load.
`server_required` — `true` forces clients to download this mod on connect.

## Tags

Hierarchical: `fish/saltwater` automatically implies `fish`. Max depth 3.

```json
{
  "api": 1,
  "tags": [
    { "id": "coolmod:deep_sea", "parent": "isle:fish/saltwater", "display": "Deep sea fish" }
  ]
}
```

**Base tags (`isle`)**

| Group | Tags |
|---|---|
| Ingredient type | `meat` `fish` `vegetable` `grain` `fruit` `fat` `spice` `ferment_agent` `high_water` `starch` |
| State | `raw` `cooked` `dried` `smoked` `preserved` `spoiled` |
| Material | `metal` `wood` `stone` `leather` `cloth` `bone` `gem` |
| Habitat | `fish/saltwater` `fish/freshwater` `fish/deep` `fish/reef` |
| Weapon | `weapon/blade` `weapon/bow` `weapon/artifact` |
| Station | `station/campfire` `station/pot` `station/dryer` `station/forge` |

> Adding `["meat","fat"]` to a new ingredient makes **all 8 cook methods handle it immediately.** That's the payoff of tag architecture.

## Definition types

### Items
```json
{
  "id": "coolmod:cod_fillet",
  "name": "@item.cod_fillet",
  "tags": ["fish", "raw", "protein", "low_fat"],
  "grid": { "w": 2, "h": 1 },
  "weight": 0.6,
  "stack": 8,
  "icon": "icons/cod_fillet.png",
  "durability": null,
  "spoilage": { "base_hours": 18, "temp_factor": 1.6, "result": "isle:rotten_flesh" },
  "nutrition": { "hunger": 12, "thirst": 4, "sanitation_risk": 0.35 },
  "equip_slot": null,
  "bag_grid": null
}
```
**Every definition type carries `id` and `name`.** Names beginning `@` resolve through
`lang/*.json`; a literal string is a validation error, in ours and in mods alike. `grid` follows SYS-INV-01; rotation is inferred from `w != h`. `temp_factor` is the spoilage multiplier per degree.
`equip_slot` is one of `head chest legs feet back belt main_hand off_hand` (SYS-INV-01 §Containers),
null for items that can't be equipped — a plain string, same reasoning as `skills.pool` (T-018): the
closed set is a validator concern, not a type-system one. `bag_grid` is only set on bag-type
equipment (backpacks, belt pouches): the separate grid it opens when equipped, e.g.
`{ "w": 6, "h": 7 }` for the leather backpack — distinct from `grid`, which is the bag *item's own*
footprint while it sits inside another container.

### Creatures
```json
{
  "id": "coolmod:mountain_boar",
  "name": "@creature.mountain_boar",
  "tags": ["animal", "mammal", "aggressive"],
  "weight_dist": { "type": "lognormal", "mean": 62.0, "sigma": 0.28, "min": 30, "max": 120 },
  "health_per_kg": 2.4,
  "ai": "isle:territorial_charger",
  "spawn": { "biomes": ["isle:forest"], "time": ["day","dusk"], "density": 0.04 },
  "butcher": {
    "edible_ratio": 0.45,
    "condition_range": [0.7, 1.3],
    "yields": [
      { "item": "isle:raw_meat",   "share": 0.55 },
      { "item": "isle:animal_fat", "share": 0.15 },
      { "item": "isle:offal",      "share": 0.12, "damage_sensitive": true },
      { "item": "isle:bone",       "share": 0.10, "damage_sensitive": true },
      { "item": "isle:hide",       "share": 0.08, "damage_sensitive": true, "quality_from": "isle:cooking" }
    ]
  },
  "carry": { "inventory_allowed": false, "drag_speed_penalty": 0.6, "coop_carry_penalty": 0.2 }
}
```
The engine applies SYS-HUNT-01's formula. Modders fill in `edible_ratio` and `yields` without knowing the math. `damage_sensitive` outputs degrade by weapon type and skill — the rule that makes guns cost you the hide.

### Fish
```json
{
  "id": "coolmod:lanternfish",
  "name": "@fish.lanternfish",
  "tags": ["fish", "fish/deep", "oily", "raw"],
  "weight_dist": { "type": "lognormal", "mean": 0.8, "sigma": 0.4, "min": 0.15, "max": 4.5 },
  "habitat": {
    "depth": [18, 60], "water_temp": [4, 14],
    "terrain": ["deep","trench"], "time": ["night"], "weather": ["any"]
  },
  "bait_affinity": { "isle:offal": 2.4, "isle:glowworm": 3.8, "isle:berry": 0.1 },
  "rig_allowed": ["rod", "longline", "net"],
  "min_skill": 28,
  "fight": { "pattern": "dive", "tension_window": 0.22, "stamina": 140, "coop_threshold_kg": 3.0 },
  "butcher": { "edible_ratio": 0.55, "yields": [
    { "item": "isle:fish_meat", "share": 0.8 }, { "item": "isle:fish_oil", "share": 0.2 } ] }
}
```
`habitat` feeds SYS-FISH-01's probability weights. Smaller `tension_window` is harder; skill widens it. Above `coop_threshold_kg` a solo angler is penalized.

### Cook methods — the heart of cooking
```json
{
  "id": "coolmod:steam_bake",
  "name": "@cook_method.steam_bake",
  "unlock_skill": { "skill": "isle:cooking", "level": 26 },
  "station": "coolmod:steam_oven",
  "duration_sec": 90,
  "input": { "min_items": 1, "max_items": 5, "requires_water_ml": 200 },
  "modifiers": {
    "hunger": 1.15, "thirst": 0.9, "preservation": 1.4,
    "buff_power": 1.2, "buff_duration": 1.3, "nutrition_retention": 0.95
  },
  "tag_reactions": [
    { "when": ["fish"],        "grant_buff": "isle:steady_hand", "power": 1.0 },
    { "when": ["fish","oily"], "grant_buff": "isle:cold_resist", "power": 1.4 },
    { "when": ["vegetable"],   "modifiers": { "thirst": 1.3 } }
  ],
  "failure": { "base_rate": 0.18, "skill_reduction": 0.003, "result": "isle:burnt_food" },
  "naming": "@pattern.steam_baked"
}
```
**One new method applies to every existing ingredient, and vice versa.** That bidirectionality is what produces combinatorial depth. `tag_reactions` accumulate in declaration order.

### Craft recipes
```json
{
  "id": "coolmod:harpoon_steel",
  "name": "@recipe.harpoon_steel",
  "station": "isle:forge",
  "skills": [
    { "skill": "isle:crafting", "level": 22, "primary": true }
  ],
  "allow_adjacent_assist": true,
  "ingredients": [
    { "item": "isle:steel_ingot", "count": 3 },
    { "tag": "wood", "count": 2 },
    { "tag": "cord", "count": 1 }
  ],
  "output": { "item": "coolmod:steel_harpoon", "count": 1, "inherit_quality": true },
  "time_sec": 45,
  "minigame": "isle:forging"
}
```
Ingredients accept `tag` instead of `item`, so new woods work automatically. `allow_adjacent_assist` enables SYS-CRAFT-01's nearby-ally rule; `inherit_quality` enables quality tiers and enchant slots.

### Weapons
```json
{
  "id": "coolmod:steel_harpoon",
  "name": "@weapon.steel_harpoon",
  "tags": ["weapon", "weapon/blade", "metal"],
  "grid": { "w": 1, "h": 4 }, "weight": 3.2,
  "combat_skill": "isle:melee",
  "base_power": 34, "attack_speed": 0.85, "reach": 2.4, "stamina_cost": 14,
  "grip": "two_hand", "quality_enabled": true, "durability": 260,
  "moveset": "isle:spear_basic"
}
```

### Artifacts ★
```json
{
  "id": "coolmod:tide_callers_reel",
  "name": "@artifact.tide_callers_reel",
  "tags": ["weapon", "weapon/artifact"],
  "scaling_skill": "isle:fishing",
  "combat_skill": null,
  "grants_combat_xp": false,
  "power": { "base": 31, "skill_ratio": 0.5 },
  "fuel": { "tags": ["bait", "fish"], "per_use": 1 },
  "zone_bonus": [ { "near_tag": "water", "radius": 6, "multiplier": 1.5 } ],
  "enchant_slots": 3,
  "abilities": [
    { "id": "hook_pull",  "type": "grapple",   "cooldown": 6,  "range": 9 },
    { "id": "reel_dash",  "type": "self_pull", "cooldown": 10, "range": 12 },
    { "id": "deep_exec",  "type": "execute",   "hp_threshold": 0.25, "condition": "target_in_water" }
  ],
  "acquisition": { "min_skill_level": 40, "quest": "coolmod:the_great_catch" }
}
```
**`combat_skill: null` plus `grants_combat_xp: false` is the definition of an artifact** (SYS-ART-01). Keep `base` at or below 33.

### Enchants
```json
{
  "id": "coolmod:tidebound",
  "name": "@enchant.tidebound",
  "applicable_tags": ["weapon/artifact", "weapon/blade"],
  "max_stack": 2,
  "effects": [
    { "type": "damage_mult", "value": 1.12 },
    { "type": "conditional", "when": "near_water", "damage_mult": 1.25 }
  ],
  "catalyst": [ { "item": "isle:deep_pearl", "count": 1 }, { "tag": "stabilizer", "count": 2 } ],
  "enchanting_skill_required": 24
}
```
Overload enchanting is post-EA (see BACKLOG). `enchanting_skill_required` gates on the
**Enchanting** skill (`isle:enchanting`), not Crafting — Crafting sets slot count, Enchanting
fills them (SYS-CRAFT-01 §Enchanting).

### Crops
```json
{
  "id": "coolmod:frost_barley",
  "name": "@crop.frost_barley",
  "tags": ["crop", "grain"],
  "output": { "item": "coolmod:barley_grain", "base_count": 4 },
  "growth_days": 6,
  "traits": { "growth_speed": { "base": 50, "variance": 12 }, "yield": { "base": 45, "variance": 15 } }
}
```
Breeding is post-EA. Core parses `traits` but does not use them.

### Skills
```json
{
  "id": "coolmod:brewing",
  "name": "@skill.brewing",
  "pool": "production"
}
```
Skills are data, not a fixed roster (SYS-SKILL-01, T-018) — a mod can add one. `pool` is closed
to exactly `"production"` or `"combat"`: a mod cannot declare a third pool, only join one of the
two the focus formula already balances against (developer decision, PROJECT_STATE.md §Decided
without a spec, 2026-09-09). A skill has no `tags` — nothing indexes skills by tag today.

### Buffs
```json
{
  "id": "coolmod:well_fed",
  "name": "@buff.well_fed",
  "duration_min": 180,
  "effects": [
    { "type": "stamina_regen_mult", "value": 1.25 }
  ]
}
```
Buffs are the timed-status vocabulary a `grant_buff` reference points at (SYS-BUFF-01, T-019) —
`CookMethodDef.TagReaction.GrantBuff` is the only granter today, brewing and enchanting join later
(BACKLOG T-106/T-107). `effects` is heterogeneous by `type`, same shape as an enchant's `effects`.
`duration_min` is in-game minutes; `0` means the buff lasts until something else clears it, not
time. **Buffs never carry a combat-stat effect** (`+X attack power`, `+X weapon damage`) — SYS-COOK-01's
rule, enforced here because it is `BuffDef`'s as much as cooking's.

### World objects
```json
{
  "id": "isle:campfire",
  "name": "@world_object.campfire",
  "tags": ["station/campfire"]
}
```
The def a placed `WorldObjectInstance` resolves against (world-object-interaction work,
PROJECT_STATE.md §Decided without a spec) — currently just an id, a name, and tags, since nothing
beyond tag-based proximity queries (SYS-SURV-01's fire bonus: "within 5 tiles of a campfire") reads
one yet. `tags` reuses the existing `station/*` group rather than inventing a new one.

### Prototype additions (2026-10-03, solo-loop prototype)

Fields added for the solo prototype. Each one is a schema change, so each is listed here.

**World objects** — resource nodes and water, placed by the island generator (SYS-WORLD-03):

```json
{
  "id": "isle:tree",
  "name": "@world_object.tree",
  "tags": ["node/tree", "wood"],
  "spawn":  { "biomes": ["isle:forest"], "density": 0.012 },
  "gather": { "item": "isle:wood", "count": 2, "uses": 3, "respawn_minutes": 60, "stamina_cost": 6 }
}
```

| Block | Fields | Meaning |
|---|---|---|
| `spawn` | `biomes`, `density` | Per-tile chance on matching land. Same shape as a creature's `spawn` |
| `gather` | `item`, `count`, `uses`, `respawn_minutes`, `stamina_cost` | One harvest per interact. Depleted nodes return after `respawn_minutes` in-game |
| `drink` | `source` | A `WaterSource` name in snake_case. The thirst value comes from `VitalsCalculator`, not the def |
| `fishing` | `depth` | Where a line can be cast. Terrain comes from the def's `water/*` tag, water temperature from ambient |

**Items** — `weapon` (optional): the `isle:` id of the `WeaponDef` this item wields as. Omitted means the
item is not a weapon.

**Weapons** — the fists fallback is picked by tag `weapon/unarmed`, not by id, so no C# names it.

**Creatures** — `combat` block, read by the creature AI (SYS-COMBAT-01 §Creature AI):

| Field | Meaning |
|---|---|
| `vision_tiles` | Radius the AI preset reacts within |
| `move_speed`, `chase_speed` | Idle wander speed, and speed while fleeing or charging |
| `damage`, `attack_range_tiles`, `attack_interval_seconds` | Strikes against the player |
| `alert_seconds` | Alert-then-flee preset only: how long it watches before running |

AI presets (`ai`): `isle:skittish` (flees on sight), `isle:territorial_charger` (charges on sight),
`isle:alert_then_flee` (alerts, then flees), `isle:ambusher` (lies still, charges at close range).

`butcher.yields[].count` — units dropped per yield (prototype shortcut for SYS-HUNT-01's kg-to-units
conversion, which isn't specced). Unset means 1.

**Fish** — `rig_allowed` lists rigs (the prototype has `handline` only). `habitat.terrain` matches the water
tag suffix (`saltwater`, `freshwater`). `habitat.time` accepts `any` or a `DayPhase` name.

**Recipes** — `station` is optional. Omitted means crafted by hand. A station is a world-object def id that
must be lit within reach.

## Patch system

**A mod that wholly redefines `isle:mackerel` collides with every other mod that touches it.** Patch instead — an abbreviated RFC 6902.

```json
{
  "api": 1,
  "patches": [
    {
      "target": "isle:mackerel",
      "ops": [
        { "op": "add",     "path": "/tags/-",        "value": "coolmod:deep_sea" },
        { "op": "replace", "path": "/habitat/depth", "value": [5, 30] },
        { "op": "merge",   "path": "/bait_affinity", "value": { "coolmod:glowbait": 2.2 } }
      ]
    },
    {
      "target_selector": { "has_tags": ["fish", "oily"] },
      "ops": [ { "op": "merge", "path": "/nutrition", "value": { "thirst": -2 } } ]
    }
  ]
}
```

`target_selector` enables **tag-wide patching** — "reduce thirst on all oily fish" becomes a one-line balance mod. Multiple mods apply in `load_after` order, and **two `replace` ops on the same path raise a conflict warning** rather than silently overwriting.

## Load pipeline

See `docs/specs/SYS-CORE-01-definitions.md` §Load pipeline. The critical step is reference resolution: a typo like `"isle:steel_ingott"` must fail at load with a suggestion, not mid-game.

## Server sync

On connect, compare the server's mod list and version hashes. Missing `server_required: true` mods download from Workshop and reconnect. Client-only visual mods (`server_required: false`) are permitted. Hash mismatches refuse the connection and name the offending mod.

## Rules we follow

This document constrains us before it serves modders.

1. **All `isle` content uses this format.** No exceptions
2. Design the schema **before** writing the code for any new system
3. A hardcoded item or recipe found in C# is treated as a bug
4. Bumping `api` ships **with a migration tool** — an ecosystem dies if every update breaks mods
5. The T-131 example mods are validated by whether **an outsider can reproduce them from the docs alone**
6. Tier 2 (Lua) hook points are reserved now via the event bus, even though they ship post-EA

## Open questions
- Hand-write JSON Schema files or generate from `Data` classes? (generation preferred)
- Steam Workshop path detection
- Handling in-flight crafting during hot reload

### Prototype additions, round 4 (2026-10-04)

| Type | Field | Meaning |
|---|---|---|
| Item | `places` | World-object id this item becomes when placed (kits) |
| Item | `warmth` | Degrees added to `clothingBonus` while equipped |
| Item | `light_radius` | Tiles of light while equipped (torch) |
| World object | `storage` `{w,h}` | Container grid (crate 10×6) |
| World object | `rain_catcher` `{capacity, fill_per_second}` | Collects rain; one drink per unit, +40 thirst |
| World object | `light_radius` | Tiles of light while active (lit campfire) |
| Weapon | `ammo` | Item each shot uses up — set means the weapon is ranged |
| Weapon | `projectile_speed` | Tiles per second |
| Creature | `spawn.time` | Now also the creature's waking hours — it sleeps outside them |
| Item | `spoilage` | Now live: `base_hours` until it becomes `result`. `temp_factor` not applied yet |

### Prototype additions, round 5 (2026-10-05)

| Type | Field | Meaning |
|---|---|---|
| Item | `plants` | Crop id a seed plants (its `places` names the plot object) |
| Item | `armor` | Added to `totalArmor` while worn |
| Gather | `tool_tag`, `tool_bonus` | Extra units when the main-hand item carries the tag |
| Cook method | `max_buffs`, `max_buffs_min_groups` | Stew/ferment's two-buff rule as data |
| Cook method | `result_grid` | Grid every dish of the method shrinks to (dry: 1×1) |
| Cook method | `eat_raw` | The method applied when food is eaten uncooked (`isle:raw`) |
| Weapon/item tag | `tool/rod` | A held rod switches fishing to the rod rig and its minigame |

### XP and skills (2026-10-05)

| Type | Field | Meaning |
|---|---|---|
| Gather / crop / recipe / cook method / fish | `xp` `{skill, base, per_unit}` | XP granted, before the focus multiplier (`docs/content/xp_table.md`) |
| Skill | `xp_per_damage` | Combat skills: XP per point of damage dealt |
| Item | `requires` `{skill, level}` | Level needed to use the item for its purpose (rod: Fishing 5) |
| World object fishing | `skill` | The skill a water spot fishes with |
| Recipe | `skills` | Now enforced: every requirement's level must be met to craft |

### Equipment on the stick figure (2026-10-06)

Presentation only — gameplay never reads these (Absolute Rule 7). Rules and styles: `docs/specs/SYS-CHAR-02-stickman.md`.

| Type | Field | Meaning |
|---|---|---|
| Item | `wear` `{style, color}` | How the item looks worn. `style`: `cap hood shirt cloak pants boots backpack pouch` |
| Item | `hold` `{style, length, color, tip}` | How the item looks held. `style`: `spear hatchet pickaxe rod torch bow sword`; `length` in tiles; `tip` colours the head (blade, stone, flame) |

No block means the item isn't drawn on the figure. An unknown style falls back to a generic shape in the item's colour, so a mod item with a new style still shows.

### Melee (2026-10-06)

| Type | Field | Meaning |
|---|---|---|
| Creature combat | `windup_seconds` | Tell before a strike lands — the player's chance to block or parry. 0 or absent strikes instantly |
| Weapon | `cone_degrees` | Melee forward cone, full angle around the aim (SYS-COMBAT-01 §Hit detection); 0 = anything in reach |
| Weapon | `attacks[]` `{shape, degrees, radius, length, width, offset, type, power_mult}` | One per combo step, last = finisher (SYS-COMBAT-02); absent = one arc of `cone_degrees`/`reach` |
| Weapon | `damage_type` | Type for a weapon without `attacks`, and for its arrows |
| Creature | `resist {type: mult}` | Damage multiplier per type taken; missing = 1 |
| Creature combat | `damage_type` | Type of its strike (default blunt) |
| Item | `armor_types {type: value}` | Armor against specific types; `armor` covers blunt/slash/pierce |
| Buff effect | `damage_type` (with `type: damage_resist`) | Food resistance: `value` is the share removed |
| Creature combat | `lunge_tiles`, `lunge_speed` | After the wind-up, dash this far at this speed toward the player before the strike lands |
| Spawn | `cluster` `{scale_tiles, coverage, inside, outside}` | Gather into noise patches (SYS-WORLD-03 §Clustering) |
| Spawn | `elsewhere` | Density multiplier in land biomes not listed (rare elsewhere); 0 = never |

### Presentation blocks (2026-10-06)

| Type | Field | Meaning |
|---|---|---|
| Item | `icon_style` `{shape, color, accent}` | Inventory icon drawing: `log rock fiber pelt steak fish berries seeds coconut rotten arrow kit`. Not needed with `hold`/`wear` |
| Cook method | `icon_style` `{shape}` | Vessel dishes are drawn in: `bowl skewer strips plate` |
| Creature | `look` `{body, color, accent, belly, ears, tail, gait, tusks, antlers, body_length, body_height, leg_length, head_size}` | Cartoon figure: `body` is `quadruped reptile snake crab bird frog turtle`; proportions in tiles at the mean weight |

### Dungeons (T-201, SYS-DUNG-01)

Two directories, kept apart because the loader reads each one recursively: `definitions/dungeons/` and
`definitions/dungeon_rooms/`.

**Dungeon** (`DungeonDef`) — one entrance is placed per def on every island.

| Field | Meaning |
|---|---|
| `danger` | 1–4 (★). Sets locks per floor (1/1/2/2) and the soft-gate skill level (10/20/30) |
| `floors` | Floors; the last one ends in the boss room |
| `entrance` `{biome, shape, color, size}` | Island biome id (`isle:coast`/`forest`/`marsh`; omit = any land), placeholder shape, colour, size in tiles |
| `floor_color`, `wall_color` | Placeholder tile colours |
| `creatures` | Creature ids cycled over the spawn marks of combat/key/stairs rooms |
| `gate` `{name, skill, shape, color}` | The vault's soft gate: name key, the production skill that clears it fast, placeholder look |

**Room template** (`RoomTemplateDef`)

| Field | Meaning |
|---|---|
| `tags` | Room kinds it can fill: `entrance combat rest key vault stairs boss` |
| `dungeons` | Optional dungeon ids; omit = usable in every dungeon |
| `rows` | 20 strings of 20 chars, top row first. `#` wall, `.` floor, `S` spawn, `C` chest, `K` key spot, `X` feature (stairs/exit/boss), `F` campfire, `,` decor. The centre tile must be floor. Templates are rotated/mirrored per seed; doorways are carved on top |

### Tides, bosses, dungeon creatures (T-202)

| Type | Field | Meaning |
|---|---|---|
| Dungeon | `boss` | Creature id placed in the last floor's boss room; the exit opens when it dies |
| Dungeon | `tides` `{cycle_hours, high_at_hour, flooded_share, swim_speed, cache {item, min, max}}` | Tidal floors: hours between high tides, hour of a peak, share of rooms that flood, swim speed multiplier, the low-tide cache's contents |
| Creature | `habitat` | `water`: only in flooded dungeon water, hidden when it drains |
| Creature | `group_size` | Creatures per dungeon spawn mark (a swarm) |
| Creature | `boss` `{sweep_radius_tiles, shell {below_health, seconds, every_seconds, damage_mult}, drops [{item, count}]}` | Sweep strike hits every player in range; shell phase below a health share; guaranteed drops as a loot pile |
| Item | `icon_style.shape` | adds `pearl`, `sigil` |

