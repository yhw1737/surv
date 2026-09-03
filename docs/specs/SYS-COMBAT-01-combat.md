# SYS-COMBAT-01 · Combat, weapons, hit detection

## Purpose
**No class selection — your weapon is your class.** Swapping weapons swaps the governing skill and the available techniques.

## Power ★

```
FinalPower = BasePower
           * (SkillFloor + (1 - SkillFloor) * skillLevel / MaxLevel)
           * qualityMult * enchantMult * situationalMult

SkillFloor = 0.50, MaxLevel = 50
```

> **The 0.50 floor matters.** A cook with no combat skill can still fight in an emergency. Skill governs efficiency, not permission.

| Tier | qualityMult | Enchant slots |
|---|---|---|
| Crude | 0.70 | 0 |
| Common | 1.00 | 1 |
| Fine | 1.15 | 2 |
| Superior | 1.30 | 3 |
| Master | 1.50 | 4 |

### Power table (BasePower 30)

| Lv | Crude | Common | Fine | Superior | Master |
|---|---|---|---|---|---|
| 0 | 10.5 | 15.0 | 17.2 | 19.5 | 22.5 |
| 10 | 12.6 | 18.0 | 20.7 | 23.4 | 27.0 |
| 20 | 14.7 | 21.0 | 24.1 | 27.3 | 31.5 |
| 30 | 16.8 | 24.0 | 27.6 | 31.2 | 36.0 |
| 40 | 18.9 | 27.0 | 31.0 | 35.1 | 40.5 |
| 50 | 21.0 | 30.0 | 34.5 | **39.0** | 45.0 |

**Lv50 Superior sword = 39.0** is the reference point for artifact balance (`SYS-ART-01`).

**situationalMult** (multiplicative, capped at 2.50): base 1.00 / backstab 1.35 / weak point (ranged) 1.60 / beyond max range 0.60 / target staggered 1.20

## Weapon classes (Core: 2)

### Melee `isle:melee`
Stamina-based. 3-hit combo, third hit ×1.4. Stamina 8 / 8 / 14. Combo window 1.2 s. Right-click blocks (frontal 90°, costs stamina).

**Parry:** block input within 0.25 s before impact → zero damage, 1.0 s enemy stagger, no stamina cost.

Unlocks: Lv10 parry window 0.25→0.35 s · Lv20 4-hit combo · Lv35 execute below 20% HP · Lv45 combo stamina −30%

### Ranged `isle:ranged`
Arrows (crafted) + stamina. 0.8 s full charge; uncharged shots ×0.4. Effective range 12 tiles, max 20 (falloff past 12).

```
swayRadiusTiles = SwayBase * (1 - skillLevel/MaxLevel) * stanceMult
SwayBase = 1.8
stanceMult: standing 1.0, moving 2.2, crouched 0.5, sprinting = cannot aim
```
Lv0 standing → 1.8 tiles. Lv50 crouched → 0.0 (pinpoint).

Unlocks: Lv10 charge 0.8→0.65 s · Lv20 sway −20% · Lv35 pierce up to 2 · Lv45 triple shot

## Hit detection

| Type | Resolution |
|---|---|
| Melee | server authority **+ lag compensation** (collider rollback, max 200 ms) |
| Ranged | server authority, **no lag compensation** |

> Lag-compensating ranged rewards high ping. There is no PvP, so server resolution suffices.

Melee uses a forward cone (angle and radius from the weapon def). Ranged uses projectiles with per-tick circle-vs-rect checks.

I-frames: dodge roll invulnerable 0.1–0.45 s of a 0.6 s animation; 0.3 s after taking a hit. Rolling is disabled while overweight (`SYS-INV-01`).

## Damage
```
damage = FinalPower * (1 - armorReduction) * partMult
armorReduction = clamp(totalArmor / (totalArmor + ArmorConstant), 0, 0.75)
ArmorConstant = 60
```
Armor 60 → 50% reduction. Armor 180 → 75% (cap).

`partMult` (creatures only): body 1.0 / head 1.8 / legs 0.7

## Creature AI (Core minimum)
Four-state machine: `Idle → Alert → Engage → Flee`

| Creature | Behavior |
|---|---|
| Rabbit | flees on sight at 8 tiles |
| Deer | alerts, then flees on noise |
| Boar | territorial, engages within 6 tiles, charges |
| Crocodile (marsh) | ambush from water |

Transition presets come from `CreatureDef.ai`; all parameters (vision, speed, damage) load from definitions.

## Verification (±0.05)

| # | Input | FinalPower |
|---|---|---|
| 1 | base30, Lv0, Common | 15.0 |
| 2 | base30, Lv50, Common | 30.0 |
| 3 | base30, Lv50, Superior | 39.0 |
| 4 | base30, Lv25, Master | 33.75 |
| 5 | base30, Lv50, Superior, backstab | 52.65 |
| 6 | 40 damage vs armor 60 | 20.0 taken |
| 7 | 40 damage vs armor 300 | 10.0 taken (75% cap) |

## Location
```
Scripts/Combat/
  PowerCalculator.cs        ★ static pure
  DamageResolver.cs         ★ static pure
  Weapons/{MeleeWeapon,RangedWeapon}.cs
  Weapons/SwayCalculator.cs ★ static pure
  Damage/HitDetection.cs
  AI/CreatureAI.cs
```

## Open questions
- Combo input buffer window
- Enemy stagger duration on hit
- Arrow recovery chance
- Weapon durability loss rate
