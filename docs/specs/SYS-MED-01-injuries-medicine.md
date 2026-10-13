# SYS-MED-01 · Injuries, herbs and medicine

## Purpose
A hit isn't just a number off the health bar: it is a wound somewhere on the body that bleeds, slows you and takes time
to close. Herbs and medicine tend wounds — stop the bleeding and speed the healing — and better medicine, made at an
apothecary table, tends better.

> **Developer decision 2026-10-13:** various herbs; injuries can be treated with herbs; an apothecary table refines
> herbs into higher-grade medicine that tends better ("like RimWorld"); damage lands on body parts ("RimWorld, Project
> Zomboid"); telling healing herbs from poisonous ones is Gathering's job. Every number below is [proposed].

## Body and wounds
Six parts: head, torso, left/right arm, left/right leg. A hit that gets through armor lands on one part:

| Part | Chance |
|---|---|
| Torso | 40% |
| Head | 10% |
| Each arm | 12.5% |
| Each leg | 12.5% |

The wound's kind comes from the damage type and its **severity** is the damage taken:

| Damage | Wound | Bleeds (HP/s per severity, untended) |
|---|---|---|
| slash | cut | 0.008 |
| pierce | puncture | 0.010 |
| blunt | bruise | — |
| heat | burn | — |
| toxic | (no wound; poison status as before) | — |

More than 12 open wounds merge into the worst one of the same part and kind.

## Health
```
maxHealth = 100 − Σ wound severity
health regenerates RegenPerSecond (0.05) toward maxHealth while hunger and thirst are both ≥ 25
untended wounds heal HealPerMinute (0.03 severity per in-game minute)
tended wounds heal HealPerMinute × (1 + 4 × tendQuality)
bleeding = Σ severity × bleed rate over untended cuts and punctures, taken off health every second
```
A healing potion (SYS-BREW-01 `heal`) restores lost health only, up to maxHealth — it never closes wounds (developer
2026-10-13). Starvation, cold and poison still
drain health directly; regeneration brings it back once fed. Death and respawn clear every wound.

## Limbs
```
limbFraction(part) = min(1, Σ severity on part / 30)
moveSpeed  ×= 1 − 0.4 × average(limbFraction(left leg), limbFraction(right leg))
gatherSpeed and attack speed ×= 1 − 0.4 × average(limbFraction(left arm), limbFraction(right arm))
```

## Tending
Using a medical item (`medicine: { quality, harm_buff? }`) tends one wound — the one bleeding most, else the worst —
and spends the item. `tendQuality = clamp01(quality + roll)`, roll uniform in ±0.1. A tended wound stops bleeding and
heals faster. No untended wound → nothing is spent. A poisonous look-alike tends at quality 0 and gives its harm buff.

| Item | Tend quality | Made / found |
|---|---|---|
| Healing herb | 0.35 | forest (Gathering 8 to tell) |
| Stanchweed | 0.30 | coast (Gathering 12) |
| Herbal poultice | 0.55 | apothecary, Enchanting 5: healing herb 2, cloth 1 |
| Stanch bandage | 0.50 | apothecary, Enchanting 5: stanchweed 2, cloth 2 |
| Herbal medicine | 0.80 | apothecary, Enchanting 15: healing herb 3, stanchweed 1, bitter herb 1 |
| Refined medicine | 1.00 | apothecary, Enchanting 30: herbal medicine 2, glowcap 2 |

Look-alikes (SYS-FORAGE-01 identification): **hemlock** looks like healing herb (tending with it: quality 0 and
poisoned), **thornweed** looks like stanchweed (quality 0 and nauseous). The **apothecary table** (`station/apothecary`) is made at the workbench, Crafting 5:
wood 6, copper ingot 1, cloth 2.

## Verification

| # | Case | Expected |
|---|---|---|
| 1 | A 20-damage slash | a cut of severity 20 on one part; maxHealth 80 |
| 2 | An untended cut of 20 | bleeds 0.16 HP/s |
| 3 | Tend it at quality 0.5 | no bleeding; heals 0.09 severity/min |
| 4 | Both legs at severity 30 | move speed ×0.6 |
| 5 | Heal 15 with a cut of 20 open | health +15 at most up to 80; the cut stays 20 |
| 6 | Use medicine with no wounds | nothing spent |

## Location
```
Gameplay/Character/Injuries.cs   pure: wounds, healing, bleeding, limbs, tending
Gameplay/Character/Vitals.cs     owns them; TakeDamage adds a wound
```

## Medicine skill (2026-10-13)
`isle:medicine` (production). Medical items carry `medicine.skill` and `medicine.xp`; tending earns that XP and the
skill scales the medicine: `tendQuality = clamp01(quality × (0.6 + 0.8 × min(level, 50) / 50) + roll)` — ×0.6
untrained, ×1.0 at 25, ×1.4 at 50. The apothecary's recipes use Medicine (not Enchanting).

## Infection
An hour (in-game) after a cut, puncture or burn is made, it rolls once: infected with chance 0.25 × (1 − tend quality)
(0.25 untended). An infection starts at 5% and moves by `0.004 × (1 − tend quality) − 0.003` per minute (immunity):
untended it climbs; tended at better than 25% it fades. It drains 0.25 × infection HP/s and the wound doesn't close
while infected. An infected wound can be tended again (if the new tending is better).

## Fractures
A blunt hit of 15+ on an arm or leg breaks it half the time. A fracture counts double toward its limb's penalty and
heals at a quarter of the rate until a **splint** (apothecary, Medicine 1: wood 2, cloth 1; quality 0.5) sets it.

## Scars
A wound that peaked at 15+ and closes untended or tended under 40% leaves a scar: −2 max health, up to 5. Death clears
wounds and scars.

## Armor by part
Each hit picks its part first; only the armor covering it counts and only that armor wears. Head slot → head;
shirt/chest → torso and arms; legs/feet → legs; back/belt → torso.

## UI (2026-10-13)
A status window (O, or the left toolbar) lists each part with its wounds, bleeding, infection %, fracture/splint,
tending, scars and the armor covering it; the HUD keeps one summary line under the gauges.

## Open questions
- Pain, disease (waterborne illness), prosthetics.
