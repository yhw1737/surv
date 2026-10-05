# SYS-SURV-01 · Survival gauges

## Purpose
Hunger, thirst, temperature, and stamina create survival pressure. **Thirst is deliberately tighter than hunger** so cooking (per-method water restore) matters.

## Gauges

All `0.0–100.0` float, starting at 100.

| Gauge | Base drain | Time to empty | At zero |
|---|---|---|---|
| `Hunger` | **1.5 / in-game hour** | 66 h ≈ 2.8 days | HP −0.4/s |
| `Thirst` | **3.0 / in-game hour** | 33 h ≈ 1.4 days | HP −1.0/s |
| `Temperature` | environmental | — | status effect |
| `Stamina` | on action | — | actions blocked |
| `Health` | damage | — | death |

**1 in-game day = 20 real minutes = 1440 in-game minutes** (GLOSSARY).

## Drain modifiers
```
hungerDrain = HungerBaseRate * activityMult * coldMult
thirstDrain = ThirstBaseRate * activityMult * heatMult * saltMult
```

| Modifier | Condition | Value |
|---|---|---|
| `activityMult` | idle | 0.7 |
| | walking | 1.0 |
| | sprinting | 1.8 |
| | combat / gathering / butchering | 1.4 |
| `coldMult` | temperature < 35 | 1.5 |
| `heatMult` | ambient > 28 °C | 1.6 |
| `saltMult` | 30 min after salty food | 1.4 |

## Temperature
```
targetTemp = ambientTemp + clothingBonus + fireBonus - wetPenalty
Temperature approaches targetTemp at TempApproachRate = 2.0 per in-game minute
```

| Band | Effect |
|---|---|
| 36–38 | comfortable |
| < 33 | hypothermia warning |
| < 28 | hypothermia severity accumulates (below); HP drain scales with it, up to −0.5/s at 100% |
| > 41 | heatstroke warning |
| > 44 | HP −0.5/s, thirst drain ×2 |

`wetPenalty` −6 from rain or swimming; dries over 20 in-game minutes. `fireBonus` +8 within 5 tiles of a campfire, falling off with distance.

### Hypothermia severity (2026-09-19)

Developer-authorized benchmark of RimWorld's publicly documented temperature-hediff mechanic
(RimWorld Wiki's `Ailments`/`Temperature`/`Hediffs/Core/Global/Temperature/Hypothermia` pages —
community-observed behavior, not decompiled source; see `PROJECT_STATE.md` §Decided without a spec
for the citation trail), reimplemented independently against ISLE's own 28 °C threshold:

```
severity ∈ [0, 1], starts at 0, persists across the band — doesn't reset the instant Temperature ≥ 28

while Temperature < 28:
  excess = 28 - Temperature
  growthPerSecond = max(excess * 6.45e-5, 0.00075)        # floor once any excess exists

while Temperature >= 28:
  recoveryPerSecond = lerp(0.0015, 0.015, clamp01(severity between 5.6% and 55.6%))  # two documented anchors

hypothermiaHpDrainPerSecond = -0.5 * severity
```
`HypothermiaHpDrainPerSecond` (0.5) is unchanged in value — it's now the *ceiling* at 100% severity
instead of a flat rate switching on at the threshold, so the drain ramps up the longer a player
stays cold rather than snapping on and off.

**Heatstroke intentionally NOT given the same treatment.** RimWorld's heatstroke severity growth
runs through a nonlinear `SimpleCurve` whose exact control points aren't published in the open wiki
text (only the qualitative shape and a 0.000375/s floor) — pulling them from a decompiled/dumped
game-file mirror would be copying, not benchmarking. Per the developer's own instruction ("따라할
수 없으면 일단 공백으로 놔둬"), left as the original flat `> 44 → HP −0.5/s` rule until either a
legitimately open source publishes the exact curve or the developer supplies numbers directly.

## Stamina
```
regen: after StaminaRecoveryDelay(1.2 s) idle, StaminaRegen(18)/s
       × overweight factor (halted when overweight, SYS-INV-01)
       × hunger/thirst factor
```
Hunger or thirst below 25 → regen ×0.5, max stamina ×0.7.

| Action | Cost |
|---|---|
| sprint | 12/s |
| melee hit 1–2 | 8 |
| melee hit 3 | 14 |
| dodge roll | 25 |
| holding bow charge | 5/s |
| gather | 6 |
| drag carcass | 4/s |

## Water sources (early gate)

| Source | Thirst | Risk |
|---|---|---|
| Seawater | **−15** | makes it worse. Do not drink |
| Standing water | +25 | 35% disease |
| Stream | +35 | 8% disease |
| Rain catcher | +40 | safe |
| Boiled water | +45 | safe |
| Coconut | +30 | safe, bulky |

**Cooking link:** boiled broth (`thirst ×1.80`) is the main early water source. This is what makes the Lv 4 boil unlock meaningful.

## Disease (simplified)
Core has two: `food_poisoning` and `waterborne_illness`. Both cause HP drain plus max stamina −30% for 8 in-game hours, halved by eating herbs.

## Death
HP 0 → death, drop entire inventory including equipment. Respawn at the last shelter (campfire/bed) after 15 s.
**An ally recovering your body** removes the wait and returns items immediately — rescue is rewarded.

## Verification

| # | Condition | Expected |
|---|---|---|
| 1 | idle, baseline, 1 in-game hour | hunger −1.05, thirst −2.1 |
| 2 | sprinting, 30 °C, 1 in-game hour | hunger −2.7, thirst −8.64 |
| 3 | hunger 0 for 60 real seconds | HP −24 |
| 4 | thirst 0 for 60 real seconds | HP −60 |
| 5 | drink seawater | thirst −15 |
| 6 | stamina regen at hunger 20 | 9/s (18 × 0.5) |

Case 1: 1.5 × 0.7 = 1.05; 3.0 × 0.7 = 2.1. Case 2: 1.5 × 1.8 = 2.7; 3.0 × 1.8 × 1.6 = 8.64.

### Hypothermia severity verification (2026-09-19)

| # | Condition | Expected |
|---|---|---|
| 7 | Temperature 8 °C (excess 20) | severity growth 0.00129/s |
| 8 | Temperature 27 °C (excess 1, below floor) | severity growth clamped to 0.00075/s |
| 9 | severity 55.6% or above, Temperature ≥ 28 | recovery 1.5%/s |
| 10 | severity 5.6% or below, Temperature ≥ 28 | recovery 0.15%/s |
| 11 | severity 30.6% (interpolated midpoint), Temperature ≥ 28 | recovery 0.825%/s |
| 12 | severity 50% | HP drain −0.25/s |
| 13 | severity 100% | HP drain −0.5/s (same ceiling as the old flat rate) |

## Location
```
Scripts/Gameplay/Character/
  Vitals.cs             server authority
  VitalsCalculator.cs   ★ static pure
  DeathHandler.cs
```
Server-only updates, once per in-game minute (0.833 real seconds). Never per frame.

## Open questions
- How respawn shelters are designated
- Visual representation of disease
- Overeating cap and whether it carries a penalty

## Prototype additions (2026-10-03)

- **Gather stamina:** `gather` = 6 (the table above). Each node def carries it as `gather.stamina_cost`.
  Harvesting is blocked below that stamina.
- **Rest at a campfire (no spec — invented):** `R` at a lit campfire during Night skips the clock to 07:00
  (`WorldClock.RestWakeMinute`) and refills stamina. Hunger and thirst are not ticked during the skip,
  which is a prototype shortcut. Decided in PROJECT_STATE.md §Decided without a spec.
- **Eating:** `nutrition.hunger` and `nutrition.thirst` from an item's def are applied as-is. Sanitation
  risk is not rolled yet (no disease system).

### Death and respawn (prototype, 2026-10-04)

- The whole inventory (bag, equipped items, contents of equipped bags) drops as one loot pile at the death
  spot. Anyone can pick it up with `E` — "an ally recovering your body" — and so can the player after respawn.
- Respawn point = the last campfire the player lit (one tile below it) or rested at. This answers the open
  question "how respawn shelters are designated" for the prototype only; record in PROJECT_STATE.md.
- While dead the player can't move or act, and creatures ignore them.
