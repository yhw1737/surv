# SYS-SKILL-01 · Skills, focus, rust

> ## Status: data-driven as of T-018 (2026-09-09)
> Skills are **data** (`SkillDef`, `Isle.Data`), not a fixed array — a mod can add one, as long as
> it joins one of the two pools below (developer decision, `PROJECT_STATE.md` §Decided without a
> spec, 2026-09-09: beta ships exactly two pools, no mod-declared third). The formula below is
> generalised over any skill count (`FocusCalculator.Focus`, `Scripts/Gameplay/Skills/`) — the
> table is the beta starter roster, not a hardcoded array size.
>
> Professions are flavor labels over these skills, not code: Cook→Cooking, Farmer/Angler→
> Gathering+Fishing, Hunter→Melee+Ranged (kill method sets the *initial* carcass bulk, `SYS-HUNT-01`; feeds Cook's butchery), Blacksmith→Crafting,
> Enchanter→Magic+Enchanting (late-game power ceiling, GDD §Scope). No profession is a real
> class — skill levels alone sort players into them (GDD §Skills).

## Purpose
Track skills and use **focus** to stop any one player mastering everything.

## Skills — beta starter roster

| Idx | Name | ID | Pool | Note |
|---|---|---|---|---|
| 0 | Gathering | `isle:gathering` | Production | + farming, land traps |
| 1 | Fishing | `isle:fishing` | Production | + water traps |
| 2 | Cooking | `isle:cooking` | Production | + butchery (`SYS-HUNT-01`) |
| 3 | Crafting | `isle:crafting` | Production | weapon/armor + enchant slot count |
| 4 | Enchanting | `isle:enchanting` | Production | enchant application + brewing (`SYS-CRAFT-01`, BACKLOG T-106/T-107) |
| 5 | Melee | `isle:melee` | Combat | |
| 6 | Ranged | `isle:ranged` | Combat | |
| 7 | Magic | `isle:magic` | Combat | magic combat — `SYS-COMBAT-01`'s power formula is already skill-agnostic, so this needs no new formula, only a weapon category (BACKLOG T-117) |

`isle:hunting` is retired — see `SYS-HUNT-01`'s 2026-09-07 note; butchery moved to Cooking.
Production has 5 members, Combat 3 — the verification cases below are simulated against this
5/3 split, not the 5/2 split the original 7-skill formula was checked against.

Levels 1–50, all start at 1.

## XP curve
```
XpToNext(L) = round(80 * L^1.6)      // L → L+1
```

| Lv | To next | Cumulative |
|---|---|---|
| 1 | 80 | 0 |
| 5 | 1,051 | 1,522 |
| 10 | 3,185 | 10,699 |
| 15 | 6,093 | 32,160 |
| 20 | 9,655 | 69,504 |
| 25 | 13,797 | 125,842 |
| 30 | 18,471 | 203,971 |
| 35 | 23,637 | 306,468 |
| 40 | 29,268 | 435,734 |
| 45 | 35,337 | 594,040 |
| 49 | 40,495 | 743,043 |
| **50** | — | **783,538** |

Target pacing: ~9,000 XP/hour × 1.8 focus → **~48 h to Lv 50**, ~8 h to Lv 25. Per-action XP is tuned to hit this in `docs/content/xp_table.md` (not yet written).

## Focus

```
w(L)   = 0.0  if L <= 15        // Novice: no interference
       = 0.5  if 16 <= L <= 35  // Adept
       = 1.0  if L >= 36        // Master

c(i,j) = 1.00 if pool(i) == pool(j)
       = 0.35 otherwise          // production <-> combat

p(n)   = 0.35 / 0.60 / 0.80 / 1.00   for n = 1 / 2 / 3 / >=4

denominator = L_i + Σ_{j≠i} ( L_j * w(L_j) * c(i,j) * p(n) )
Focus_i     = L_i / denominator
Multiplier  = FocusMin + (FocusMax - FocusMin) * Focus_i ^ FocusGamma
```

| Constant | Value | Tunable |
|---|---|---|
| `FocusMin` | 0.35 | ✓ |
| `FocusMax` | 2.00 | ✓ |
| `FocusGamma` | 0.70 | ✓ |
| `CrossPoolFactor` | 0.35 | ✓ **core** |
| `NoviceCap` | 15 | ✓ |
| `AdeptCap` | 35 | ✓ |
| `MaxLevel` | 50 | ✗ |

**Active player count `n`** is the **median distinct active players over the last 7 in-game days**, not concurrent players — this prevents gaming it by briefly connecting a friend. Keep a 7-slot ring buffer of daily distinct counts; with fewer than 7 days recorded, take the median of what exists.

```
awardedXp = baseXp * Multiplier
```

**UI wording:** "Focus 88% — earning 1.86× XP". Frame as a bonus. Never write "penalty" or "increased requirement" (GDD §Skills).

## Verification

Re-simulated 2026-09-09 (T-018) against the 8-skill, 5/3-pool table above — cases 1, 2, 3 and 5
are unaffected (their non-target skills besides the deliberate pair are novices, so the extra
Production and Combat slots contribute 0 regardless); cases 4 and 6–9 shift because Combat gained
a third member.

Level array order: `[gathering, fishing, cooking, crafting, enchanting, melee, ranged, magic]`, tolerance ±0.005.

| # | Levels | Target | n | Focus | Multiplier |
|---|---|---|---|---|---|
| 1 | `[10,10,45,10,10,10,10,10]` | cooking | 4 | 1.000 | 2.00 |
| 2 | `[10,10,45,10,10,35,10,10]` | cooking | 4 | 0.880 | 1.86 |
| 3 | `[10,10,45,40,10,10,10,10]` | cooking | 4 | 0.529 | 1.41 |
| 4 | `[35,35,35,35,35,35,35,35]` | cooking | 4 | 0.284 | 1.03 |
| 5 | `[10,10,10,10,10,10,10,10]` | cooking | 4 | 1.000 | 2.00 |
| 6 | `[30,30,45,30,30,25,25,25]` | cooking | 1 | 0.637 | 1.55 |
| 7 | `[30,30,45,30,30,25,25,25]` | cooking | 2 | 0.506 | 1.37 |
| 8 | `[30,30,45,30,30,25,25,25]` | cooking | 3 | 0.435 | 1.27 |
| 9 | `[30,30,45,30,30,25,25,25]` | cooking | 4 | 0.381 | 1.19 |

Cases 1 and 5 both give 1.000 **by design** — sub-15 levels contribute nothing, so beginners always get the maximum multiplier.

**Cases 2 vs 3 are the whole point.** Cooking 45 + Melee 35 (cross-pool) costs almost nothing at 1.86×; Cooking 45 + Crafting 40 (same pool) is clearly worse at 1.41×.

**Case 4 dropped from 0.299/1.06× (5/2) to 0.284/1.03× (5/3).** A third Combat member at Adept
level adds one more cross-pool interference term, so an all-35s player who spreads evenly now
loses slightly more focus than before — Combat gaining a skill costs Production specialists a
little, exactly the "quietly taxes that pool" effect flagged when the pool was still 5/2.

## Rust — off by default in Core

`GameConfig.EnableSkillRust = false`. A/B test in EA.

```
decay(XP) = unusedInGameDays * RustRatePerDay * (L_i / MaxLevel) * XpToNext(L_i)
RustRatePerDay = 0.008
```

Hard rules:
1. **Levels never drop.** Only current-level progress, floored at 0
2. No decay below `RustFloorLevel = 20`
3. Measured in **in-game** time, never real time
4. `PeakLevel` is permanent; recovering below peak grants `PeakRecoveryMultiplier = 3.0`
5. Rusted state applies −5% to −15% efficiency, scaled by decayed fraction
6. `RustClearActions = 10` uses of the skill clears the debuff immediately

UI: not "you lost a level" but **"you're rusty — a few tries will loosen it up"**.

## Reassignment
30 in-game day cooldown. Soft-demote a skill to Lv 20, transfer 40% of lost XP to another skill. No full reset.

## Location
```
Scripts/Gameplay/Skills/
  SkillSet.cs           per-player levels, keyed by SkillDef.Id — whatever DefRegistry loaded
  FocusCalculator.cs    ★ static pure, formulas above, generalised over any skill count (T-018)
  XpCurve.cs            static, XpToNext / TotalXpTo
  ActivityTracker.cs    7-day median
  RustSystem.cs         flag-gated — not yet implemented, see Open questions
```
`FocusCalculator` and `XpCurve` must not reference Unity — EditMode test targets. XP awards are server-only.

## Open questions
- Per-action base XP values (`docs/content/xp_table.md`, not yet written)
- **`RustSystem` is unimplemented.** `EnableSkillRust = false` by default and no caller needs it
  yet, so T-018 left it out rather than build against the still-open efficiency curve below
  (Absolute Rule 3 — don't invent a missing value). `decay(XP)` and the hard rules are fully
  specified whenever this gets picked up.
- Exact interpolation curve for the rust efficiency debuff
- Reassignment UI flow
- **`ActivityTracker`'s median, for an even number of recorded days, can land between two
  integers** (e.g. 2 and 3 average to 2.5), but `ActivePlayerFactor` has no band for a fractional
  n. Implemented as round-to-nearest for now (only matters in a world's first 6 days); flagged in
  `PROJECT_STATE.md` §Decided without a spec for the developer to confirm or override.
