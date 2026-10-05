# XP table — per-action base XP

Status: **first draft, 2026-10-05; gathering and fishing re-derived the same day** after timed harvests and cast/bite fishing landed. Written by delegation ("XP 표 알아서 만들어서 개발해"), not
reviewed yet. Every number here is a tuning value derived from the pacing target below; the
**assumed action rates are the part to challenge** — if a rate is wrong, the XP value moves with it.

SYS-SKILL-01 says per-action base XP lives here. The defs carry the same numbers (an `xp` block on
the thing being acted on, so a mod's new tree or fish brings its own XP); this file is the reasoning
behind them and the place to change them first.

## Pacing target

SYS-SKILL-01 §XP curve: `XpToNext(L) = round(80 · L^1.6)`, **~9,000 base XP per hour** of dedicated
activity, × focus multiplier (up to 2.0, ~1.8 for a specialist) → ~8 h to Lv 25, ~48 h to Lv 50.

| Lv | Cumulative XP | ≈ dedicated time at 9,000 × 1.8 / h |
|---|---|---|
| 4 | 787 | 3 min |
| 5 | 1,522 | 6 min |
| 10 | 10,699 | 40 min |
| 12 | 17,594 | 1.1 h |
| 15 | 32,160 | 2 h |
| 25 | 125,842 | 7.8 h |
| 50 | 783,538 | 48 h |

Early levels come fast on purpose: focus is at its 2.0 maximum below Lv 16 (novice skills don't
interfere), so a first session sees several level-ups in every skill it touches.

## How XP is awarded

```
xp = (base + per_unit × units) × focusMultiplier(skill)
```

`units` depends on the action (below). Awards are server-side only (SYS-SKILL-01 §Location).
The def block is `"xp": { "skill": "isle:gathering", "base": 25, "per_unit": 0 }`.

Combat skills earn from damage instead: `xp = damage dealt × skill.xp_per_damage × focus`, with
damage capped at the creature's remaining HP (no XP for overkill).

## Values

| Action | Skill | base | per_unit | units | Assumed rate | ≈ base XP / h |
|---|---|---|---|---|---|---|
| Fell a tree | gathering | 40 | 0 | — | 10 s chop (Lv 1) + ~5 s walk → 1 / 15 s (240/h) | 9,600 |
| Break a rock | gathering | 35 | 0 | — | 8 s + ~5 s walk → 1 / 13 s (275/h) | 9,600 |
| Pick a berry bush | gathering | 20 | 0 | — | 3 s + ~5 s walk → 1 / 8 s (450/h) | 9,000 |
| Cut tall grass | gathering | 15 | 0 | — | 2 s + ~4 s walk → 1 / 6 s (600/h) | 9,000 |
| Pick a palm | gathering | 30 | 0 | — | 6 s + ~5 s walk → 1 / 11 s (330/h) | 9,800 |
| Harvest a crop | gathering | 40 | 5 | items harvested | gated by growth time | bonus |
| Handline catch | fishing | 35 | 0 (mackerel 15/kg on a rod) | — | cast + 4–12 s wait + hook, ~1 / 14 s with misses (250/h) | 8,800 |
| Rod catch | fishing | 80 | 15 | fish kg | wait + 20–40 s fight, ~70/h, avg 2.5 kg → 118 | 8,300 |
| Cook a dish | cooking | 40 | 15 | ingredients | gated by ingredients, 1 / 22 s (165/h), 1-ingredient grill → 55 | 9,000 |
| Craft a recipe | crafting | 10 | 8 | ingredient units | gated by materials, ~150/h at 6 units → 58 | 8,700 |
| Melee damage | melee | — | 1.0 / HP | damage | fists ~10 DPS, fighting 25% of the hour | 9,000 |
| Ranged damage | ranged | — | 1.5 / HP | damage | ~13 DPS with misses, ammo-gated, ~20% | 9,000 |

Enchanting and Magic have no actions in the prototype yet, so no values.

## Level gates using these levels

| Gate | Requirement | Source |
|---|---|---|
| Boil | Cooking 4 | SYS-COOK-01 unlock table |
| Dry | Cooking 12 | SYS-COOK-01 unlock table |
| Rod | Fishing 5 | SYS-FISH-01 rig table |
| Short bow, hide armor | Crafting 5 | **[invented]** |
| Fishing rod | Crafting 3 | **[invented]** |
| Leather backpack | Crafting 8 | **[invented]** |
| Warehouse | Crafting 10 | **[invented]** |
| Hunter's backpack | Crafting 15 | **[invented]** |

Levels also feed the formulas the specs already define: combat power (SYS-COMBAT-01 skill factor),
bow sway, fishing species weights and tension window (SYS-FISH-01), cooking care tag at Lv 40.

## Things to watch in play

- Gathering speeds up with level (Lv 50 takes 30% of the time), so XP per hour rises as you master it — a
  gatherer at Lv 50 earns roughly 2× the table's hourly figure. Intended as the reward for focus; cut the
  speed-up rather than the XP if it races.
- Crafting XP scales with ingredient units, so big builds (warehouse: 26 units → 218 XP) give the most;
  that's on purpose — they cost the most gathering.
- Solo play: SYS-SKILL-01's active-player factor `p(1) = 0.35` softens interference, so a lone player
  keeps a higher focus multiplier across several skills than a 4-player group member would.
