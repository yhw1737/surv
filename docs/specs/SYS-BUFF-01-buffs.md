# SYS-BUFF-01 · Buffs

> ## Status: schema settled (T-019, 2026-09-10) — implementation hooks land with their consumer systems
> `BuffDef`/`BuffEffect`/`BuffSet` exist in code (`Scripts/Data/BuffDef.cs`,
> `Scripts/Gameplay/Buffs/BuffSet.cs`). The seven beta buffs' numbers are all fixed below. What's
> **not** built yet is the actual hookup into `SYS-SURV-01` (Vitals) and `SYS-COMBAT-01`
> (sway/damage) — neither system has code yet (BACKLOG Phase 5/8), so `BuffSet` only tracks which
> buffs are active and for how long; applying `aim_sway_mult` etc. to a real formula happens when
> those systems get built (`T-050`, `T-110`+). Durations and the `cold_resist`/`warm` mechanics
> were either the developer's explicit call or delegated to be decided here — see
> `PROJECT_STATE.md` §Decided without a spec, 2026-09-10, for which is which.

## Purpose
A buff is a timed, stackable status effect granted by an action (cooking today; brewing and
enchanting later, BACKLOG T-106/T-107) rather than a stat a character permanently has. **Buffs
support actions; they never grant combat stats** (SYS-COOK-01) — that line is what keeps a
specialist cook from also being the best fighter (GDD's core premise).

`BuffDef` is the modder-extensible vocabulary this hangs off: any system that wants to grant a
status effect declares a `BuffDef` and points a `grant_buff` reference at it (already wired from
`CookMethodDef.TagReaction.GrantBuff`, T-011/T-012), rather than adding a new hardcoded case to
every consumer.

## Schema

```json
{
  "id": "isle:steady_hand",
  "name": "@buff.steady_hand",
  "duration_min": 180,
  "effects": [
    { "type": "aim_sway_mult", "value": 0.70 }
  ]
}
```

| Field | Type | Note |
|---|---|---|
| `id` | `NamespacedId` | |
| `name` | `string` | language key, never literal text |
| `duration_min` | `long` | in-game minutes (`GLOSSARY §Units`); `0` = until cleared some other way, not time |
| `effects` | `BuffEffect[]` | heterogeneous by `type`, one or more |

`BuffEffect` follows the same shape as `EnchantEffect` (T-012, `EnchantDef.cs`): a `Type` string
plus a nullable value field, rather than a polymorphic class hierarchy — `SchemaValidator` checks
which fields a given `type` requires, not the type system. ponytail: ship the fields the seven
beta buffs actually need below; add more only once a real buff needs them.

```csharp
public sealed class BuffEffect
{
    public string Type { get; init; }
    public float? Value { get; init; }
}
```

## Beta buff set

Carried from `SYS-COOK-01` §Tag reactions; `food_poisoning`'s numbers come from `SYS-SURV-01`
§Disease (the same debuff, defined once there — not redefined here).

| Buff | Effect type(s) | Value | Duration |
|---|---|---|---|
| `isle:warm` | `temp_approach_rate_mult` | 0.50 | 180 min (3 h) |
| `isle:steady_hand` | `aim_sway_mult` | 0.70 | 180 min (3 h) |
| `isle:endurance` | `stamina_regen_mult` | 1.25 | 180 min (3 h) |
| `isle:hydrated` | `thirst_drain_mult` | 0.70 | 180 min (3 h) |
| `isle:iron_gut` | `disease_chance_mult` | 0.60 | 180 min (3 h) |
| `isle:cold_resist` | `hypothermia_threshold_shift` | −5 | 180 min (3 h) |
| `isle:food_poisoning` ⚠️ debuff | `hp_drain_per_sec`, `stamina_max_mult` | per `SYS-SURV-01` §Disease | 480 min (8 h) |

Durations for the five non-fixed buffs (`steady_hand`, `endurance`, `hydrated`, `iron_gut`,
`cold_resist`) are a single reused placeholder — 3 h, matching the one duration `SYS-COOK-01`
already committed to (`warm`) — rather than five independently invented numbers. Beta tuning
placeholder, revisit during balancing.

**Effect meanings, resolved 2026-09-10:**
- `temp_approach_rate_mult` (`warm`) — multiplies `SYS-SURV-01`'s `TempApproachRate` (2.0/min)
  wholesale while active, i.e. Temperature moves toward `targetTemp` at half speed in either
  direction. Developer's call.
- `hypothermia_threshold_shift` (`cold_resist`) — shifts both hypothermia bands down by this many
  degrees for the duration: warning becomes < 28, HP-drain becomes < 23 (`SYS-SURV-01`'s 33/28
  bands minus 5). Granted by warm/cooked food, per the developer — an oily-fish reaction is the
  natural source (mirrors `cold_resist`'s existing grant in `SYS-COOK-01`'s tag reaction table).
- `disease_chance_mult` (`iron_gut`) — multiplies the water-source disease chance
  (`SYS-SURV-01` §Water sources, e.g. 35% → 21%). Confirmed by the developer.

Banned vocabulary: anything that reads as a combat stat (`+20% attack power`, `+X weapon damage`)
— SYS-COOK-01's line, restated here because it is `BuffDef`'s rule as much as cooking's.

## Stacking

Per-grant cap is already specified in `SYS-COOK-01`:
```
maxBuffs = 1
if method == isle:stew and distinct ingredient tag groups >= 3: maxBuffs = 2
if method == isle:ferment: maxBuffs = 2
```
Excess buffs from **one grant** drop by lowest `power`. There is no separate whole-character
buff-slot cap — a player may hold `warm` + `steady_hand` + `food_poisoning` simultaneously, each
tracked independently by `BuffDef.Id`.

**Re-granting an already-active buff refreshes its duration** (developer's call, 2026-09-10) — it
does not extend or stack in power. `BuffSet.Grant` always overwrites the expiry.

## Verification

| # | Action | Expected |
|---|---|---|
| 1 | Grant `isle:warm` at t=0, duration 180 | `IsActive(warm, t=179)` true, `IsActive(warm, t=180)` false |
| 2 | Grant `isle:warm` at t=0 (180), grant again at t=100 | expiry is now 280, not 180 — a re-grant refreshes, it does not stack |
| 3 | Grant a `duration_min=0` buff at t=0 | `IsActive` true at any later t — permanent until `Clear` |
| 4 | Grant, then `Clear` before expiry | `IsActive` false immediately |
| 5 | Grant `warm` and `steady_hand` at t=0 | `ActiveBuffIds(t=1)` contains both, independently |

## Location

```
Scripts/Data/BuffDef.cs        BuffDef, BuffEffect
Scripts/Gameplay/Buffs/
  BuffSet.cs      per-character active buffs, keyed by BuffDef.Id, expiry-based, refresh-on-regrant
```
`BuffSet` only tracks activity and expiry — it does not itself apply `aim_sway_mult` or any other
effect to a real gauge or formula, since `SYS-SURV-01` and `SYS-COMBAT-01` have no code yet
(BACKLOG Phase 5/8). Those systems read `BuffSet.ActiveBuffIds` and look up each `BuffDef`'s
effects when they're built.

## Open questions
- Whether non-cooking sources (brewing T-106/T-107, enchanting) get their own grant-cap rule or
  reuse cooking's `maxBuffs`.
- The 3 h placeholder duration on five of the seven buffs is unbalanced by construction (one
  reused number, not five tuned ones) — revisit once `SYS-SURV-01`/`SYS-COMBAT-01` exist and these
  can be played, not just read.
