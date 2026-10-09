# SYS-WORLD-03 · Resource nodes, gathering, water

Status: prototype spec (2026-10-03). Written for the solo-loop prototype; numbers marked **[invented]**
were chosen to make the loop playable and are open for developer review — see PROJECT_STATE.md
§Decided without a spec.

## Purpose
The island has things to take. Trees, rocks and berry bushes are finite and come back slowly;
water sources are infinite but vary in safety (SYS-SURV-01 §Water sources).

## Placement

Nodes are world objects (`WorldObjectDef`) with a `spawn` block, the same shape creatures use
(SYS-HUNT-01 / SCHEMA §Creatures):

```
spawn: { biomes: [isle:forest], density: 0.012 }
```

For every land tile (`IslandGenerator.IsLand`), and every node def whose `biomes` contains that
tile's biome, a roll is taken:

```
roll = Hash(seed, tileX, tileY, defSalt) / 2^32     // deterministic per seed, same island every time
placed if roll < density
```

The island is different every game start (`IslandWorld` picks the seed), but the same seed always
produces the same placements — this is what makes EditMode tests possible.

## Island shape (2026-10-05 revision)

Per seed, deterministic (verification 12–15):

```
size          = 1152 tiles square (36 × 36 chunks); a 32-tile border is always sea
blobs         = 1–4. The first sits on the map centre (the spawn point) with radius 0.26–0.34 × size;
                the others sit 0.24–0.36 × size from the centre at radius 0.10–0.20 × size
each blob     = an ellipse, aspect 0.65–1.35, rotated at random
field(x, y)   = max over blobs (1 − ellipseDistance) + 0.35 × (fbm(x, y) − 0.5)      fbm: 4-octave value noise, base 1/140 tiles
bridge        = a 10–16 tile wide land strip from the centre to every other blob, so nothing is cut off
land          = field > 0 (or on a bridge);  coast = land with field < 0.06
interior biome= marsh where biomeNoise(x, y) < marshRatio, else forest;  marshRatio 0.30–0.55 per island, noise base 1/70 tiles
```
All values **[invented]**. Water bodies scale with the map: 30 ponds, 7 rivers.

## Water bodies (2026-10-05 revision)

SYS-WORLD-01 §Shape: the interior has "scattered ponds and rivers cut into it". Water is **terrain**, not
a node: a set of water tiles, each belonging to one water-body def. Water tiles can't be walked on, and
no node or creature is placed on one.

A def with a `water_body` block is generated per island seed:

| Field | Meaning |
|---|---|
| `water_body.kind` | `pond` — a rounded blob; `river` — a winding channel from the interior out to the sea |
| `water_body.count` | How many per island |
| `water_body.min_size`, `max_size` | Pond radius / river width, in tiles |

The sea is the def with `"ocean": true`: every tile off the island belongs to it.

**Depth** (answers SYS-FISH-01's open question for the prototype): a water tile's depth is its distance in
tiles from the nearest land tile × the def's `fishing.depth_per_tile`. Shore water is shallow, a pond's
middle or the open sea is deep.

**Drinking:** `E` within reach of any water tile drinks that body's `drink.source`
(`VitalsCalculator.DrinkThirstDelta`, SYS-SURV-01 §Water sources).

| Def | Kind | Count | Size | Drink | Fishing terrain | depth/tile |
|---|---|---|---|---|---|---|
| `isle:pond` | pond | 30 **[invented]** | radius 4–16 **[invented]** | `standing_water` | freshwater | 1.0 **[invented]** |
| `isle:river` | river | 7 **[invented]** | width 2–3 **[invented]** | `stream` | freshwater | 1.0 **[invented]** |
| `isle:ocean` | ocean | — | everything off-island | `seawater` | saltwater | 2.0 **[invented]** |

## Walls (2026-10-09, developer request)

- **Water's edge is the drawn shoreline, not the tile grid.** Per-tile water (sea, ponds, rivers = 1, land = 0) is
  blurred with the 5×5 binomial kernel and interpolated between tile centres (`ShoreField`); a point is water where
  that field is ≥ 0.5. This is exactly the line `GroundRenderer` paints, so you walk to the water you see. A single
  water tile is too small to draw or to block (field 0.14).
- **Standing nodes block their trunk.** A def's `block_radius` (tiles) is a circle around the node no walker's centre
  enters: tree 0.4, palm 0.3, rock 0.45 [invented]; bushes and grass 0. Felled or mined-out nodes don't block. Someone
  already inside a wall (a tree grew back on them) moves freely until out.
- **Used-up nodes leave a remnant until they grow back** (2026-10-09, developer): a def's `visual.depleted` shape —
  trees and palms a stump, rocks and veins rubble, berry bushes a bare bush, grass stubble. Remnants don't block.
  Without one, the node fades to a ghost as before.
- Standing nodes sit up to 0.3 tiles off their tile centre (hashed per tile, fixed) [invented], so woods aren't a grid.

## Gathering

A node with a `gather` block can be harvested while it has uses left. **Harvesting takes time**
(2026-10-05 revision): pressing `E` starts it, a progress bar fills, and moving away (more than 0.3 tiles
**[invented]**) or starting any other action cancels it.

```
seconds = gather.time_sec × (1 − 0.7 × (level − 1) / 49)      // level of gather.xp.skill
seconds = max(seconds, 1)
```
Lv 1 takes the full `time_sec`, Lv 50 takes 30% of it **[invented]**, never under 1 s **[invented]** — so the
range is roughly 1–10 s, with trees the longest.

| Field | Meaning |
|---|---|
| `gather.item` | Item granted per harvest |
| `gather.count` | Units granted per harvest |
| `gather.uses` | Harvests before the node is depleted |
| `gather.respawn_minutes` | In-game minutes until a depleted node is restored |
| `gather.time_sec` | Seconds a harvest takes at level 1 |

- Reach: `PlayerInteraction.ReachTiles` (2 tiles). The server re-checks reach (SYS-NET-01).
- Respawn is measured in in-game time: `realSeconds = respawnMinutes / WorldClock.MinutesPerRealSecond`.
  1 in-game day (1440 min) = 20 real minutes.

Disease chance from SYS-SURV-01 §Water sources is **not** rolled yet (no disease system exists;
Absolute Rule 6).

## Node defs (prototype content)

| Def | Biomes | Density | Gather | time_sec | Respawn |
|---|---|---|---|---|---|
| `isle:tree` | forest | 0.012 **[invented]** | wood ×4, 1 use (felled) | 10 | 3 days **[invented]** |
| `isle:rock` | forest, marsh | 0.004 **[invented]** | stone ×2, 2 uses | 8 | 5 days **[invented]** |
| `isle:berry_bush` | forest | 0.006 **[invented]** | berries ×3, 1 use | 3 | 1 day **[invented]** |
| `isle:tall_grass` | forest, marsh | 0.01 **[invented]** | fiber ×2, 1 use | 2 | 12 h **[invented]** |
| `isle:palm_tree` | coast | 0.006 **[invented]** | coconut ×1, 2 uses | 6 | 2 days **[invented]** |

## Verification

| # | Condition | Expected |
|---|---|---|
| 1 | Same seed, same island, placed twice | identical node lists |
| 2 | Forest-only def, forest tiles | placed fraction within 15% (relative) of `density` over >2000 tiles |
| 3 | Def restricted to forest | never placed on a marsh or coast tile |
| 4 | Any def | never placed off the island (`IsLand` false) |
| 5 | `respawn_minutes = 60` | 50 real seconds |
| 7 | `time_sec` 10 at Lv 1 / Lv 50 | 10 s / 3 s |
| 8 | `time_sec` 2 at Lv 50 | 1 s (floor) |
| 9 | Same seed, water generated twice | identical water tiles |
| 10 | Any pond tile | on the island, never on a coast-biome tile |
| 11 | Water tile next to land | depth 1 × depth_per_tile |
| 12 | Same seed twice | identical land and biomes |
| 13 | Map centre | always land (spawn) |
| 14 | 32-tile border | always sea |
| 15 | Flood fill from the centre over land | reaches every land tile (all blobs joined) |
| 6 | `respawn_minutes = 0` | 0 real seconds |

## Clustering (2026-10-06, developer request)

Nodes gather where they belong — groves, rock fields, meadows — and still turn up elsewhere, rarely.

```
spawn.cluster = { scale_tiles, coverage, inside, outside }
patch(x, y)   = 0.65·noise(x/scale, y/scale) + 0.35·noise(x/(0.45·scale), y/(0.45·scale))   // value noise, per def seed
density(x, y) = density × (inside in a patch, outside between; 0.06-wide smooth edge)
              × (1 in a listed biome, spawn.elsewhere in any other land biome)
```

| Node | scale | coverage | inside | outside | elsewhere |
|---|---|---|---|---|---|
| tree | 36 | 0.35 | 3.5 | 0.12 | 0.06 |
| rock | 28 | 0.12 | 7.0 | 0.06 | 0.25 |
| tall grass | 30 | 0.30 | 3.5 | 0.12 | 0.12 |
| berry bush | 22 | 0.18 | 4.0 | 0.08 | 0.04 |
| palm | 26 | 0.35 | 2.5 | 0.25 | 0.03 |

All **[invented]**. Verification: `ResourceClusterTests` (coverage within ±0.12, multiplier bounds, determinism,
neighbouring tiles agree > 90%).
