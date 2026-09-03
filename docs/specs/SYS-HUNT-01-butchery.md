# SYS-HUNT-01 · Hunting, butchery, carrying

## Purpose
Yield scales with the animal's actual body weight. Skill, tools, and kill method drive a large spread.
Mid/large carcasses can't be moved alone — **the physical basis for cooperation**.

## Body weight
```
BodyWeightKg ~ LogNormal(mean, sigma), clamp[min, max]     // CreatureDef.weight_dist
HealthMax    = BodyWeightKg * CreatureDef.health_per_kg
```
Boar example: `mean=62.0, sigma=0.28, min=30, max=120`.

## Yield ★

```
ButcherQuality = ButcherBase + ButcherSkillWeight * (huntingLevel/50) * toolFactor * damageFactor
ButcherQuality = clamp(ButcherQuality, QualityFloor, QualityCeil)
TotalEdibleKg  = BodyWeightKg * EdibleRatio * ConditionFactor * ButcherQuality
```

| Constant | Value |
|---|---|
| `ButcherBase` | **0.30** |
| `ButcherSkillWeight` | **0.70** |
| `QualityFloor` | 0.15 |
| `QualityCeil` | 1.15 |

> ⚠️ Raising `ButcherBase` to 0.55 collapses the novice-to-master spread to 1.4×, and "we need our hunter" stops being true. Keep it at 0.30.

**toolFactor:** bare hands 0.40 / stone 0.70 / iron knife 1.00 / master tools 1.15

**damageFactor** (from the killing blow and overkill):

| Kill method | Value |
|---|---|
| trap / precise shot to weak point | 1.00 |
| bow / dagger | 0.95 |
| ordinary melee | 0.85 |
| blunt | 0.70 |
| overkill (excess > 50% of max HP) | additional ×0.85 |

`damageFactor = clamp(base * overkillPenalty, 0.50, 1.00)`

**ConditionFactor** 0.7–1.3, rolled at spawn from age and nutrition. No seasonal modifier in Core.

### Verification (±0.15 kg)

62 kg boar, `EdibleRatio 0.45`, `ConditionFactor 1.1`:

| # | Case | Quality | Yield |
|---|---|---|---|
| 1 | Lv5, bare 0.4, dmg 0.95 | 0.327 | **10.0 kg** |
| 2 | Lv30, iron 1.0, dmg 0.95 | 0.699 | **21.5 kg** |
| 3 | Lv50, master 1.15, dmg 1.0, cond 1.3 | 1.105 | **40.1 kg** |
| 4 | Lv30, iron 1.0, dmg 0.60 (blunt) | 0.552 | **16.9 kg** |

3 kg rabbit, `EdibleRatio 0.55`, cond 1.0, Lv20 stone 0.7 dmg 0.9 → **0.79 kg**

> Case 3 is **4.0×** case 1. That gap is why a hunter exists.

## Cuts

Split `TotalEdibleKg` by `CreatureDef.butcher.yields[].share`. Boar:

| Output | share | damage_sensitive |
|---|---|---|
| meat | 0.55 | ✗ |
| fat | 0.15 | ✗ |
| offal | 0.12 | ✓ |
| bone | 0.10 | ✓ |
| hide | 0.08 | ✓ |

```
if damage_sensitive:
    survivalRate = clamp(damageFactor * (0.4 + 0.6 * huntingLevel/50), 0, 1)
    amount *= survivalRate
```

**Blunt weapons destroy hide.** Lv30 at dmg 0.70 → 0.70 × 0.76 = 0.53.

Hide quality from `survivalRate`: <0.4 Crude / <0.6 Common / <0.75 Fine / <0.9 Superior / else Master.

> This is what forces **hunter + crafter cooperation** for bags and armor.

`5 kg meat = one raw meat chunk (2×2)`. Remainders stack per kg, fractions truncated.

## Carrying

| Carcass weight | Handling |
|---|---|
| < 5 kg | inventory item, 1×2 |
| 5–15 kg | inventory item, 2×3 |
| **> 15 kg** | **world object only** |

```
dragSpeedMult = 1.0 - DragPenalty     // 0.60, solo
coopSpeedMult = 1.0 - CoopPenalty     // 0.20, CoopMinPlayers = 2
```
Dragging blocks attacking, gathering, and rolling. Co-op carry needs both players holding `E`; if one lets go it drops to drag mode. Movement follows the **slower** player.

```
butcherSeconds = BaseButcherSeconds * (BodyWeightKg/50) * (1.5 - 0.5 * huntingLevel/50)
BaseButcherSeconds = 12.0
```
62 kg boar at Lv30 → **17.9 s**. Multiple butchers divide the time by participant count (max 3) — so two people butchering together beats one guarding, which matters because of scent (below).

## Spoilage and scent
```
spoilage += deltaMinutes * SpoilRatePerMinute * tempFactor
SpoilRatePerMinute = 0.00069          // 1.0 over 24 in-game hours
tempFactor = 1.0 + max(0, (ambientTemp - 15) / 15)     // 2× at 30 °C
```
Above 0.5 spoilage yield ×0.6; above 0.8 only rotten meat.

```
scentRadiusTiles = ScentBase + BodyWeightKg * ScentPerKg    // 6.0, 0.12
```
62 kg boar → 13.4 tiles. Roll every 5 in-game minutes, 0.15 chance to spawn a predator.

> **Time pressure is what shapes the co-op movement pattern** — butcher here and carry meat, or drag the whole thing home.

## Location
```
Scripts/Gameplay/Hunting/
  ButcheryCalculator.cs   ★ static pure — EditMode target, use the 4 cases above
  CarcassObject.cs        world object, spoilage, scent
  CarryController.cs      drag / co-op carry
  ButcherInteraction.cs   progress, multiple participants
```

## Open questions
- Predator spawn table (which creatures are drawn in)
- Whether cold storage halts spoilage and by how much
- Visual representation of two-player carry
