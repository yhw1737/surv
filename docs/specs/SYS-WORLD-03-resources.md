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

## Gathering

A node with a `gather` block can be harvested while it has uses left.

| Field | Meaning |
|---|---|
| `gather.item` | Item granted per harvest |
| `gather.count` | Units granted per harvest |
| `gather.uses` | Harvests before the node is depleted |
| `gather.respawn_minutes` | In-game minutes until a depleted node is restored |

- Reach: `PlayerInteraction.ReachTiles` (2 tiles). The server re-checks reach (SYS-NET-01).
- One harvest per interact press.
- Respawn is measured in in-game time: `realSeconds = respawnMinutes / WorldClock.MinutesPerRealSecond`.
  60 in-game minutes = 50 real seconds (`MinutesPerRealSecond = 1.2`).

## Water

A node with a `drink` block is never depleted. Drinking calls `Vitals.Drink(source)`, which uses
`VitalsCalculator.DrinkThirstDelta` (SYS-SURV-01 §Water sources) — the table there is the single
source of truth for thirst deltas; the def only names the source.

| Def | `drink.source` |
|---|---|
| `isle:seawater` | `seawater` |
| `isle:standing_water` | `standing_water` |
| `isle:stream` | `stream` |

Disease chance from SYS-SURV-01 §Water sources is **not** rolled yet (no disease system exists;
Absolute Rule 6).

## Node defs (prototype content)

| Def | Biomes | Density | Gather | Respawn |
|---|---|---|---|---|
| `isle:tree` | forest | 0.012 **[invented]** | wood ×2, 3 uses | 60 min **[invented]** |
| `isle:rock` | forest, marsh | 0.004 **[invented]** | stone ×1, 4 uses | 90 min **[invented]** |
| `isle:berry_bush` | forest | 0.006 **[invented]** | berries ×2, 2 uses | 45 min **[invented]** |
| `isle:stream` | marsh | 0.003 **[invented]** | — (drink: `stream`) | — |
| `isle:standing_water` | marsh | 0.004 **[invented]** | — (drink: `standing_water`) | — |
| `isle:seawater` | coast | 0.02 **[invented]** | — (drink: `seawater`) | — |

## Verification

| # | Condition | Expected |
|---|---|---|
| 1 | Same seed, same island, placed twice | identical node lists |
| 2 | Forest-only def, forest tiles | placed fraction within 15% (relative) of `density` over >2000 tiles |
| 3 | Def restricted to forest | never placed on a marsh or coast tile |
| 4 | Any def | never placed off the island (`IsLand` false) |
| 5 | `respawn_minutes = 60` | 50 real seconds |
| 6 | `respawn_minutes = 0` | 0 real seconds |
