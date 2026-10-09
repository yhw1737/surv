# SYS-HUNT-01 · Hunting, butchery, carrying

## Purpose
Yield scales with the animal's actual body weight. Skill, tools, and kill method drive a large spread.
Mid/large carcasses can't be moved alone — **the physical basis for cooperation**.

> **2026-09-07 developer decision.** `ButcherQuality` scales off **Cooking**, not a `hunting`
> skill — the intended flow is a high-level hunter kills, a high-level cook determines the final
> yield. Killing itself is not a skill: creature damage is priced entirely by `damageFactor`
> below (trap/bow/melee/blunt), which a player's Melee or Ranged weapon already governs through
> `SYS-COMBAT-01`. "Hunter" is a flavor label for a Melee/Ranged specialist, not a skill ID.
> `isle:hunting` is retired; every `cookingLevel` below was `huntingLevel` before this date.
>
> **Bulk vs. refinement.** The Hunter sets the *initial* bulk: `damageFactor` below is entirely
> their kill method and weapon choice — a clean trap or precise shot preserves carcass bulk,
> overkill and blunt trauma shrink it before butchery ever starts. The Cook then refines however
> much bulk survived the kill: `cookingLevel`'s term in `ButcherQuality` decides what fraction of
> that bulk becomes edible product. `BodyWeightKg * damageFactor` is the ceiling the Hunter hands
> off; the Cook's skill decides how much of it is realized.

## Body weight
```
BodyWeightKg ~ LogNormal(mean, sigma), clamp[min, max]     // CreatureDef.weight_dist
HealthMax    = BodyWeightKg * CreatureDef.health_per_kg
```
Boar example: `mean=62.0, sigma=0.28, min=30, max=120`.

## Yield ★

```
ButcherQuality = ButcherBase + ButcherSkillWeight * (cookingLevel/50) * toolFactor * damageFactor
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
    survivalRate = clamp(damageFactor * (0.4 + 0.6 * cookingLevel/50), 0, 1)
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
butcherSeconds = BaseButcherSeconds * (BodyWeightKg/50) * (1.5 - 0.5 * cookingLevel/50)
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

## Implementation (T-072, 2026-10-08)

Decided with the developer (2026-10-08):

| Constant | Value |
|---|---|
| Meat piece | **2.5 kg = 1 raw meat**, at least 1 per carcass |
| toolFactor of copper / steel knives | **0.85 / 1.08** (spec: bare 0.40, stone 0.70, iron 1.00, master 1.15) |
| When butchery happens | the kill **leaves a carcass**; **E** butchers it over `butcherSeconds`; people butchering together split the time (max 3) |

How it's wired:
- `ButcheryCalculator` (static pure) — §Yield, §Cuts survival, damageFactor + overkill, butcher time, pieces.
- A killed creature stays as a carcass (`Creature.Dead`, drawn on its back, greyed) holding its weight, its
  ConditionFactor (rolled at spawn, uniform in `butcher.condition_range` [invented distribution]) and the kill's
  damageFactor. Nothing is handed out at the kill; boss drops still land as a pile.
- Kill method → damageFactor [invented mapping]: arrow or a knife (`tool/knife`) in hand → bow/dagger 0.95; a blunt
  hit → 0.70; any other melee → 0.85; death by bleed/burn/poison → 0.85. Overkill (blow excess > 50% max HP) ×0.85.
- toolFactor: the best working knife carried anywhere (hand or bags), from its material's `butcher_factor`; bare hands
  0.40. Butchering wears that knife by 1 (SYS-CRAFT-02).
- Each `butcher.yields` entry with `unit_kg` becomes `floor(kg / unit_kg)` items, at least `min`; damage-sensitive
  cuts use the survival rate. Meat 2.5 kg / min 1; hides and furs [invented unit_kg]: boar 0.3, deer leather 0.25,
  wolf 0.2, crocodile 0.8, fox 0.03, rabbit 0.015, snake 0.015 (min 0 — a ruined hide gives nothing).
- Cooking XP for butchering: 5 + 2 per piece [invented].

Not yet: hide quality from survival rate (needs T-090 quality).

## Implementation (T-074 + T-075, 2026-10-09)

Decided with the developer: scavengers are **the biome's aggressive animals** (creature tag `scavenger`: crab on the
coast, wolf in the forest, crocodile in the marsh); small carcasses go **in the bag** as the spec says.

- `CarcassCalculator` (static pure) — §Spoilage and scent and §Carrying constants and formulas.
- **Spoilage**: world carcasses spoil with the air temperature, carried ones with the carrier's, per in-game minute.
  ≥ 0.5 yields ×0.6; ≥ 0.8 only rotten meat — each meat cut turns into its own `spoilage.result` item (data-driven),
  hide is lost. At **1.5** [invented] only bones are left and the carcass disappears. Drawn greener as it spoils, with
  flies from 0.5; the prompt says "going off" / "rotten".
- **Scent**: every 5 in-game minutes each world carcass rolls 0.15 to draw a scavenger of its tile's biome, placed on
  the scent circle (6 + 0.12 × kg) and homed on the carcass. At most **2 per carcass** [invented]. None underground.
- **Carrying** (G): under 15 kg → into the bag as a generated item (`isle:carcass__<animal>`, 1×2 if the species
  averages under 5 kg, else 2×3) carrying its body (weight, condition, kill factor, spoilage) — weight counts for real,
  it survives saves and death piles, dropping it from the bag lays the carcass back down. Over 15 kg → dragged:
  speed ×0.4, it trails 0.8 tiles behind [invented], no attacking, gathering, butchering or rolling. A second player
  pressing G on it takes the other end: both move at ×0.8 and it rides between them; more than 2.5 tiles apart
  [invented] and the helper lets go. G again puts it down.

Not yet: hide quality (T-090); a two-player carry pose (the carcass just sits between them).

Revised 2026-10-09 (developer: a bagged frog couldn't be taken out, spoilage should be visible):
- **G with nothing in reach puts down a carcass carried in the bags**, 0.6 tiles ahead; G on it picks it up again.
  Dragging it out of the inventory onto the ground still works too. Picking one up with no room in the bags leaves it
  where it lies ("bag is full") instead of turning it into a loot pile.
- **Spoilage is shown as a percentage of the way from fresh to gone** (`SpoilShare = spoilage / 1.5`; going off from
  33%, rotten from 53%): a freshness bar on the carcass's bag tile, a line in its tooltip (with "G — put down"), and in
  the E prompt over a carcass on the ground.

