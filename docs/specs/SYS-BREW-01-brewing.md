# SYS-BREW-01 · Brewing

## Purpose
The Enchanter's second craft (GDD §Scope): drinks, medicines and drugs brewed at a brewing stand. Cooking feeds and
buffs; brewing heals, restores mana, and pushes the body past its limits — at a price.

> **Developer decision 2026-10-12:** brewing makes **drinks, medicines and drugs**. Effects are either food-like buffs
> or health/mana restoration, **instant or over time**. Every number below is [proposed]; tune freely.

## How it's made
Brews are ordinary craft recipes (SCHEMA §Craft recipes) whose station is the **brewing stand**
(`station/brewing`) and whose skill is **Enchanting** (`isle:enchanting`). Everything crafting already does applies:
tag ingredients, skill gates with adjacent assist, XP. Brews are instant like other crafts and have no quality tier.
(BACKLOG's planned `BrewDef` type isn't needed — a recipe already says everything a brew needs.)

## Effects
A brewed item carries `consume` effects (SCHEMA §Items) on top of any `nutrition` and `buffs`:

| type | fields | effect |
|---|---|---|
| `heal` | `amount` | health + amount, at once |
| `heal_over_time` | `amount`, `seconds` | amount spread evenly over seconds |
| `mana` | `amount` | mana + amount, at once (SYS-MAGIC-01) |
| `mana_over_time` | `amount`, `seconds` | amount spread over seconds |

- Drinking one never counts toward satiety fatigue (SYS-COOK-01) and isn't run through the raw-eating method: a brew is
  what it says.
- Over-time effects stack (two draughts heal twice as fast); they end on death and aren't saved.

Kinds are tags: `drink`, `medicine`, `drug` (all also `brew`). A drug's buff always carries a cost.

## Brews

| Brew | Kind | Lv | Ingredients | Effect |
|---|---|---|---|---|
| Berry juice | drink | 1 | berries 3 | thirst +25, hunger +4, hydrated |
| Herbal tea | drink | 5 | bitter herb 1, berries 1 | thirst +18, warm |
| Health tonic | medicine | 10 | bitter herb 2, berries 2 | heal 30 |
| Mana tonic | medicine | 10 | glowcap 2, berries 1 | mana 50 |
| Mending draught | medicine | 20 | bitter herb 2, wild onion 2, glowcap 1 | heal 60 over 30 s |
| Stimulant | drug | 25 | glowcap 1, coconut 1, wild grain 2 | **stimulated** 30 min: stamina regen ×1.5, hunger drain ×1.5 |
| Focus draught | drug | 30 | glowcap 3, bitter herb 1 | mana 120 over 60 s, thirst −15 |

New ingredient: **glowcap** — a pale glowing mushroom in the marsh (tags `herb`, `arcane`), gathered like bitter herb.
The **brewing stand** is made at the workbench (Crafting 5: wood 6, copper ingot 2, stone 2).

## Verification

| # | Case | Expected |
|---|---|---|
| 1 | Drink a health tonic at 50 HP | 80 HP at once |
| 2 | Drink a mending draught at 20 HP | 80 HP after 30 s, about 50 at 15 s |
| 3 | Drink a mana tonic at 10 mana (max 100) | 60 |
| 4 | Brew without a stand near | refused |
| 5 | A stimulant | stimulated buff, hunger drains ×1.5 |

## Location
```
Data/ItemDef.cs (ConsumeEffect)   Gameplay/Character/Vitals.cs (instant + over time)   definitions/recipes/brew_*.json
```

## Open questions
- Brew quality or potency from the brewer's level (now level only gates).
- Addiction or tolerance for drugs.
