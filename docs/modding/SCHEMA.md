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
  "nutrition": { "hunger": 12, "thirst": 4, "sanitation_risk": 0.35 }
}
```
Names beginning `@` resolve through `lang/*.json`. `grid` follows SYS-INV-01; rotation is inferred from `w != h`. `temp_factor` is the spoilage multiplier per degree.

### Creatures
```json
{
  "id": "coolmod:mountain_boar",
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
      { "item": "isle:hide",       "share": 0.08, "damage_sensitive": true, "quality_from": "hunting" }
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
  "applicable_tags": ["weapon/artifact", "weapon/blade"],
  "max_stack": 2,
  "effects": [
    { "type": "damage_mult", "value": 1.12 },
    { "type": "conditional", "when": "near_water", "damage_mult": 1.25 }
  ],
  "catalyst": [ { "item": "isle:deep_pearl", "count": 1 }, { "tag": "stabilizer", "count": 2 } ],
  "crafting_skill_required": 24
}
```
Overload enchanting is post-EA (see BACKLOG).

### Crops
```json
{
  "id": "coolmod:frost_barley",
  "tags": ["crop", "grain"],
  "output": { "item": "coolmod:barley_grain", "base_count": 4 },
  "growth_days": 6,
  "traits": { "growth_speed": { "base": 50, "variance": 12 }, "yield": { "base": 45, "variance": 15 } }
}
```
Breeding is post-EA. Core parses `traits` but does not use them.

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
