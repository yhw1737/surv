# SYS-COOK-01 · Cooking, tag combination, buffs

## Purpose
No hardcoded recipes. **Ingredient tags × cook method** generate results procedurally. This is the only way one developer supplies this much content variety.

## Unlock tree (Core: 8)

| Cooking Lv | Method ID | Station | Ingredients |
|---|---|---|---|
| 1 | `isle:raw` | — | 1 |
| 1 | `isle:grill` | campfire | 1–2 |
| 4 | `isle:boil` | pot + water | 1–3 |
| 8 | `isle:porridge` | pot + water | 2–3 |
| 12 | `isle:dry` | drying rack | 1–4 |
| 20 | `isle:stew` | cauldron + water | **3–5** |
| 28 | `isle:smoke` | smokehouse | 1–4 |
| 40 | `isle:ferment` | fermenting jar | 1–3 |

Read unlocks from `CookMethodDef.unlock_skill`. Never hardcode.

## Resolution algorithm

```
Input:  ingredients [ItemStack], CookMethodDef, cookingLevel, cookId
Output: Dish (ItemStack)

1 validate       count within method.input.min/max_items; station present; water sufficient
2 base nutrition
    weightScale = clamp(itemWeightKg / def.reference_weight, 0.5, 2.0)
    baseHunger  = Σ(ingredient.Nutrition.Hunger * weightScale)
    baseThirst  = Σ(ingredient.Nutrition.Thirst * weightScale)
    ※ this is where a 3.2 kg snapper differs from a 0.4 kg one
3 apply modifiers  hunger/thirst/preservation *= method.modifiers.*
4 tag reactions    allTags = union of ingredient tags (hierarchy flattened)
                   for each reaction in declaration order:
                       if reaction.when ⊆ allTags: add buff / apply modifiers
5 quality          ComputeQuality(cookingLevel, avg freshness, ingredient variety)
                   failure if rand() < base_rate - cookingLevel * skill_reduction
6 care tag         if cookingLevel >= CareTagThreshold(40):
                       buff durations *= CareDurationBonus(1.5); record cook name
7 naming           §Naming
```

## Method modifiers

These are the actual values that go into the definition files.

| Method | hunger | thirst | preservation | buff_power | buff_duration | retention |
|---|---|---|---|---|---|---|
| raw | 0.60 | 1.00 | 0.30 | 0.0 | 0.0 | 1.00 |
| grill | 1.30 | 0.70 | 0.60 | 0.8 | 1.0 | 0.85 |
| boil | 1.00 | **1.80** | 0.50 | 0.9 | 1.0 | 0.90 |
| porridge | 1.00 | 1.40 | 0.40 | 1.0 | 1.2 | 0.95 |
| dry | 0.70 | 0.30 | **4.00** | 0.5 | 1.0 | 0.70 |
| stew | 1.35 | 1.30 | 0.80 | **1.3** | 1.4 | 0.95 |
| smoke | 1.10 | 0.60 | **3.00** | 1.1 | **1.6** | 0.85 |
| ferment | 1.00 | 1.00 | 2.00 | **1.5** | 1.8 | 1.10 |

Intent: boil's 1.80 thirst is the early-drought answer; dry's 4.00 preservation plus volume shrink (`SYS-INV-01`) creates expedition logistics; stew is the only method granting two buffs; smoke combines preservation with buff duration, making it the expedition answer.

**There must be no best dish — only the right dish for the situation.**

## Tag reactions

```json
"tag_reactions": [
  { "when": ["fish"],        "grant_buff": "isle:steady_hand", "power": 1.0 },
  { "when": ["fish","oily"], "grant_buff": "isle:cold_resist", "power": 1.4 },
  { "when": ["vegetable"],   "modifiers": { "thirst": 1.3 } },
  { "when": ["spoiled"],     "grant_buff": "isle:food_poisoning", "power": 2.0 }
]
```
`when` is a **subset test** against the union of ingredient tags, applied cumulatively in declaration order — an oily fish triggers both rules 1 and 2. Tag hierarchy applies (`SYS-CORE-01`).

```
maxBuffs = 1
if method == isle:stew and distinct ingredient tag groups >= 3: maxBuffs = 2
if method == isle:ferment: maxBuffs = 2
```
Excess buffs drop by lowest `power`.

**Buffs support actions; they never add combat stats.** A cook must not substitute for combat skill.

| Buff | Effect |
|---|---|
| `isle:warm` | temperature loss −50%, 3 h |
| `isle:steady_hand` | aim sway −30% |
| `isle:endurance` | stamina regen +25% |
| `isle:hydrated` | thirst drain −30% |
| `isle:iron_gut` | disease resistance +40% |
| `isle:cold_resist` | hypothermia resistance |
| `isle:food_poisoning` | ⚠️ debuff, HP drain |

Banned: anything like "+20% attack power".

## Naming
Registered combinations use their name and icon; otherwise `"{qualityAdj} {method} {mainIngredient}"` → "moist smoked boar shoulder".

Quality adjectives: Crude "undercooked" / Common "" / Fine "well-cooked" / Superior "moist" / Master "perfect".

Players can **register a name for a combination**, which then displays server-wide with discovery credit. In Core.

## Satiety fatigue
```
multiplier = max(FatigueFloor, 1.0 - FatigueStep * (count - 1))
FatigueStep = 0.15, FatigueFloor = 0.40
count decays by 1 every 2 in-game days
```
`dishSignature` = method ID + main ingredient tag — it counts **kinds** of dish, not exact repeats. The fifth identical dish gives 0.40×, forcing variety.

## Verification

| # | Input | Expected |
|---|---|---|
| 1 | 1 raw meat, grill | hunger ×1.30, thirst ×0.70, ≤1 buff |
| 2 | 1 oily fish, boil | thirst ×1.80; `cold_resist` does **not** fire (no such reaction on boil) |
| 3 | meat + vegetable + grain, stew | **2 buffs** |
| 4 | meat + vegetable, stew | **1 buff** (only 2 groups) |
| 5 | raw meat, dry | preservation ×4.0, result grid 1×1 |
| 6 | cooking Lv40, grill | buff duration ×1.5, care tag present |
| 7 | cooking Lv39, grill | no care tag |
| 8 | same dish eaten 5× | 5th at 0.40× |
| 9 | spoiled ingredient | `food_poisoning` applied |

## Location
```
Scripts/Gameplay/Cooking/
  CookingResolver.cs     ★ static pure — full algorithm
  TagReactionEngine.cs   ★ static pure
  DishNamer.cs
  SatietyTracker.cs      per-player state
  CookingStation.cs      MonoBehaviour
```

## ⚠️ Extensibility gate
**Adding one new ingredient to the definition files must make all 8 methods handle it with zero C# changes.**
If this fails, the whole modding strategy fails. Use it as the completion criterion.

## Open questions
- Failure output (burnt food? partial ingredient return?)
- Whether water quantity affects results (currently only a gate)
- Initial `known_dishes` list
- Buff stacking on re-consumption (refresh or extend?)
