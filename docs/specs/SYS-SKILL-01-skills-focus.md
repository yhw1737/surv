# SYS-SKILL-01 · Skills, focus, rust

## Purpose
Track 7 skills and use **focus** to stop any one player mastering everything.

## Skills

| Idx | Name | ID | Pool |
|---|---|---|---|
| 0 | Gathering | `isle:gathering` | Production |
| 1 | Hunting | `isle:hunting` | Production |
| 2 | Fishing | `isle:fishing` | Production |
| 3 | Cooking | `isle:cooking` | Production |
| 4 | Crafting | `isle:crafting` | Production |
| 5 | Melee | `isle:melee` | Combat |
| 6 | Ranged | `isle:ranged` | Combat |

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

Level array order: `[gathering, hunting, fishing, cooking, crafting, melee, ranged]`, tolerance ±0.005.

| # | Levels | Target | n | Focus | Multiplier |
|---|---|---|---|---|---|
| 1 | `[10,10,10,45,10,10,10]` | cooking | 4 | 1.000 | 2.00 |
| 2 | `[10,10,10,45,10,35,10]` | cooking | 4 | 0.880 | 1.86 |
| 3 | `[10,10,10,45,40,10,10]` | cooking | 4 | 0.529 | 1.41 |
| 4 | `[35,35,35,35,35,35,35]` | cooking | 4 | 0.299 | 1.06 |
| 5 | `[10,10,10,10,10,10,10]` | cooking | 4 | 1.000 | 2.00 |
| 6 | `[30,30,30,45,30,25,25]` | cooking | 1 | 0.652 | 1.57 |
| 7 | `[30,30,30,45,30,25,25]` | cooking | 2 | 0.522 | 1.40 |
| 8 | `[30,30,30,45,30,25,25]` | cooking | 3 | 0.450 | 1.29 |
| 9 | `[30,30,30,45,30,25,25]` | cooking | 4 | 0.396 | 1.21 |

Cases 1 and 5 both give 1.000 **by design** — sub-15 levels contribute nothing, so beginners always get the maximum multiplier.

**Cases 2 vs 3 are the whole point.** Cooking 45 + Melee 35 (cross-pool) costs almost nothing at 1.86×; Cooking 45 + Crafting 40 (same pool) is clearly worse at 1.41×.

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
  SkillSet.cs           7 skills per player
  FocusCalculator.cs    ★ static pure, formulas above
  XpCurve.cs            static, XpToNext / TotalXpTo
  RustSystem.cs         flag-gated
  ActivityTracker.cs    7-day median
```
`FocusCalculator` and `XpCurve` must not reference Unity — EditMode test targets. XP awards are server-only.

## Open questions
- Per-action base XP values (`docs/content/xp_table.md`, not yet written)
- Exact interpolation curve for the rust efficiency debuff
- Reassignment UI flow
