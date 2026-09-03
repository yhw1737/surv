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
| < 28 | HP −0.5/s |
| > 41 | heatstroke warning |
| > 44 | HP −0.5/s, thirst drain ×2 |

`wetPenalty` −6 from rain or swimming; dries over 20 in-game minutes. `fireBonus` +8 within 5 tiles of a campfire, falling off with distance.

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
