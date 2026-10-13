# SYS-FORAGE-01 · Foraging: berries, mushrooms, poison

## Purpose
Wild food is plentiful but not all of it is safe. Poisonous berries and mushrooms look exactly like edible ones; only a
forager with enough **Gathering** can tell them apart. The plain wild berry is always safe and always poor.

> **Developer decisions 2026-10-13:** mushrooms are food and spawn on the map; berries and mushrooms come in varieties;
> below the needed Gathering level you can't tell whether one is poisonous; a poisonous one drains health over time or
> gives a debuff; the basic berry has no poison but restores very little hunger and thirst; berry bushes are rarer and
> give more per bush. Every number below is [proposed].

## Varieties

| Item | Looks like | Lv to identify | Hunger | Thirst | Poison | Where |
|---|---|---|---|---|---|---|
| Wild berries (basic) | — | 0 | 3 | 1 | — | forest bushes |
| Sunberry | red berry | 10 | 9 | 5 | — | forest |
| Duskberry | red berry | 10 | 4 | 2 | nausea | forest |
| Frostberry | blue berry | 20 | 5 | 12 | — | marsh |
| Ghostberry | blue berry | 20 | 3 | 6 | toxin | marsh |
| Brown cap | brown mushroom | 5 | 6 | 0 | — | forest |
| Fool's cap | brown mushroom | 5 | 4 | 0 | nausea | forest |
| Honey cap | golden mushroom | 25 | 12 | 1 | — | forest, marsh |
| Death cap | golden mushroom | 25 | 6 | 0 | toxin | forest, marsh |

Twins share an icon, a bush/patch drawing and the node's name ("Red berry bush", "Golden mushroom"), so the world never
tells. Mushrooms are also `vegetable` (they count as a vegetable group in a stew).

## Identification
An item carries `identify: { skill, level, unknown_name }`. Below `level` in `skill`, the viewer sees `unknown_name`
("Unknown red berry") and its tooltip hides what it does ("Gathering Lv 10 to tell"). A dish made from an ingredient
the viewer can't identify shows as "Questionable dish". This is display only — eating it does what it does.

## Poison
Poisonous items carry a tag: `toxin/deadly` or `toxin/nausea`. Every cook method reacts to them (power 5, so the poison
wins over any other buff), including eating raw — cooking doesn't make a death cap safe.

| Buff | Effect | Duration (in-game min) |
|---|---|---|
| `isle:toxin` (Poisoned) | HP −0.4/s, stamina max ×0.8 | 120 |
| `isle:nausea` (Nauseous) | stamina max ×0.6, move speed ×0.85, hunger drain ×1.5 | 180 |

Poison buffs have `fixed_duration: true`: a method's buff-duration modifier (raw is 0, stew 1.4…) doesn't stretch or
cut them. `isle:food_poisoning` gets the same flag — before, eating spoiled food raw gave a poisoning that ended at once.

## Bushes and patches
- The basic berry bush: density 0.006 → **0.003**, elsewhere 0.04 → **0.02**, berries per bush 3 → **8**.
- Variety bushes: density 0.0015 each, 6 per bush, one use, back in a day.
- Mushroom patches: density 0.002 each, 3 per patch, one use, back in a day.

## Verification

| # | Case | Expected |
|---|---|---|
| 1 | Gathering 0 looks at a sunberry | "Unknown red berry", no effects shown |
| 2 | Gathering 10 looks at a sunberry | "Sunberry" with its nutrition |
| 3 | Eat a death cap raw | Poisoned for 120 min, health falling |
| 4 | Stew with a fool's cap | Nauseous (poison beats the stew's buffs) |
| 5 | Eat wild berries | hunger +3 × raw 0.6, no buff |

## Location
```
Data/ItemDef.cs (IdentifySpec)   Data/BuffDef.cs (FixedDuration)   Gameplay/Gathering/Identification.cs
UI/Prototype/ItemText.cs (the name a viewer sees)
```
