# SYS-MAGIC-01 · Mana and magic combat

## Purpose
The Enchanter's combat (GDD §Scope, 2026-09-07): spells cast from a focus — a wand, staff or rod — paid for in
**mana**. Like enchanting it raises the late-game ceiling; the core loop never needs it.

> **Developer decision 2026-10-12:** magic uses a **mana** resource. Brewing makes drinks that restore it
> (SYS-BREW-01). Every number below is [proposed] — written so the system could be built; tune freely.

## Mana

```
manaMax   = ManaBase + Σ skill.mana_per_level × level(skill)
manaRegen = ManaRegenBase + Σ skill.mana_regen_per_level × level(skill)     // per real second, always on
ManaBase = 100, ManaRegenBase = 0.5
isle:magic: mana_per_level 1.0, mana_regen_per_level 0.02                      // skills/magic.json
```
Lv 0: 100 mana, 0.5/s (full in 200 s). Lv 50: 150 mana, 1.5/s (full in 100 s). The growth sits on the skill
definitions, so a mod can give mana to another skill without code.

- A fifth gauge beside stamina, blue on the HUD. Saved with the vitals (version 10). Full on respawn.
- Unlimited difficulty (SYS-DIFF-01): always full, casting is free.

## Casting
A focus is a weapon definition with `combat_skill: isle:magic` and a `mana_cost`. Left click casts a bolt toward the
mouse:

```
needs   mana >= mana_cost              // otherwise "Not enough mana", nothing happens
damage  = PowerCalculator.FinalPower(base_power, magicLevel) × quality power × enchant damage   (SYS-COMBAT-01, SYS-CRAFT-01)
bolt    = a projectile at projectile_speed, of the focus's damage_type (its status rules apply: heat burns, toxic poisons)
rate    = attack_speed casts per second; each cast wears the focus by 1
XP      = the magic skill's xp_per_damage, like any weapon
```
No stamina cost, no charge, no sway — a bolt goes where it's aimed. Right click does nothing with a focus (no block).

## Foci

| Focus | Type | base_power | attack_speed | mana_cost | projectile_speed | durability | Recipe (enchanting table) |
|---|---|---|---|---|---|---|---|
| Apprentice wand | pierce | 12 | 1.6 | 8 | 16 | 120 | Enchanting 5: wood 2, glowcap 2, copper ingot 1 |
| Ember staff | heat | 20 | 1.1 | 14 | 14 | 150 | Enchanting 15: wood 4, glowcap 3, coal 2, copper ingot 2 |
| Venom rod | toxic | 16 | 1.3 | 12 | 15 | 150 | Enchanting 25: wood 3, glowcap 3, bitter herb 3, temple jade 1 |

The Enchanter makes them (Enchanting) and anyone can wield them; the Magic level is what makes them hit hard.

## Verification

| # | Case | Expected |
|---|---|---|
| 1 | manaMax at Magic 0 / 50 | 100 / 150 |
| 2 | manaRegen at Magic 0 / 50 | 0.5 / 1.5 per s |
| 3 | Cast with 20 mana, cost 8 | bolt fires, 12 mana left |
| 4 | Cast with 5 mana, cost 8 | nothing fires, mana unchanged |
| 5 | Wand bolt hits a creature | damage = FinalPower(12, level) |

## Location
```
Scripts/Gameplay/Combat/ManaCalculator.cs   static pure
Scripts/Gameplay/Character/Vitals.cs        the gauge
Scripts/Gameplay/Character/PlayerInteraction.cs   CastSpell
```

## Open questions
- Spells beyond the bolt (areas, shields, heals) — not in this sheet.
- Mana as a cost for anything other than casting.
