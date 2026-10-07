# SYS-COMBAT-02 · Damage types and hit shapes

Status: **design, numbers decided 2026-10-06** (Q&A with the developer) — developer direction: "국자, 뒤집개, 식칼 등 다양한 데미지 및 피격 판정이 있어.
둔탁함, 날카로움 등 다양한 피해 종류도 있어." Extends SYS-COMBAT-01 (its Power and Damage formulas stay). Numbers come from SYS-COMBAT-01 or the **Decided values** table below, which supersedes any ❓ left in the text.

## Decided values (developer Q&A, 2026-10-06)
| Constant | Value |
|---|---|
| Typical resist / weakness | **×0.5 / ×1.5** (a def may still set any multiplier) |
| `PierceBypass` | **0.40** — pierce ignores 40% of the target's pierce armor |
| Bleed / burn / poison | **8% of the hit's power per second, 4 s**; poison stacks up to **3** |
| Stagger → stun | **3 blunt hits within the combo window → 1.0 s stun** |
| Food resistances | **yes** — buff effect `damage_resist {type, value}`; the value is set **per dish/buff def** (no global number) |

## Purpose
Make *which* weapon you bring matter, not just how strong it is: a ladle and a cleaver at the same power play
differently because they hit differently and deal different kinds of damage. Every weapon, artifact and creature
attack declares both.

## Damage types

| Type | Reads as | Good against | Poor against | Side effect |
|---|---|---|---|---|
| **blunt** | ladle, hammer, fists | shells, armor, automatons | soft/furred beasts | **stagger** (builds toward a stun) |
| **slash** | cleaver, sickle, axe | soft flesh, plants | shells, metal | **bleed** (damage over time) |
| **pierce** | knife, spear, harpoon, arrow | weak points, scaled hides | swarms, plants | **armor bypass** (ignores part of armor); crit on a weak point |
| **heat** | boiling broth, forge, fire | plants, cold/wet-hating creatures | wet targets, fire-born | **burn** (spreads to flammables) |
| **toxic** | spice clouds, marsh venom | living creatures | automatons, the undead-ish drowned | **poison** (stacks; blocks regen) |

## Formula (extends SYS-COMBAT-01 §Damage)
```
damage = FinalPower × typeMult(target, type) × (1 − armorReduction(target, type)) × partMult

typeMult        = target.resist[type]               // creature def, default 1.0; e.g. crab shell slash 0.5 ❓
armorReduction  = clamp(effectiveArmor / (effectiveArmor + ArmorConstant), 0, 0.75)   // SYS-COMBAT-01
effectiveArmor  = target.armor[type] × (1 − PierceBypass if type = pierce)            // PierceBypass ❓
```
- `armor` becomes per type on armor items (`armor: {blunt, slash, pierce, heat, toxic}`); a single number in an
  existing def means "the same for all physical types" (blunt/slash/pierce) — backward compatible.
- Side effects: stagger ❓ per blunt hit (stun at ❓), bleed ❓% power/s for ❓ s, burn ❓, poison ❓ per stack.

## Hit shapes
Each attack (each combo step, each artifact ability) declares a shape — what it actually touches:

| Shape | Parameters | Feels like |
|---|---|---|
| `arc` | `degrees`, `radius` | a swing (today's forward cone; `cone_degrees` = `arc`) |
| `thrust` | `length`, `width` | a stab or lunge — a narrow line, long reach |
| `smash` | `offset`, `radius` | an overhead slam landing in a circle ahead of you |
| `sweep` | `radius` | a full spin |
| `line` | `length`, `width`, `pull` | a whip or hook (the Rod) |
| `projectile` | `speed`, `radius`, `pierce` | thrown or shot |
| `aura` | `radius`, `tick` | a lingering zone (broth pool, censer) |

Weapons move from one stat block to a list of attacks:
```json
"attacks": [
  { "shape": "arc",    "degrees": 110, "radius": 1.3, "type": "slash", "power_mult": 1.0 },
  { "shape": "arc",    "degrees": 110, "radius": 1.3, "type": "slash", "power_mult": 1.0 },
  { "shape": "smash",  "offset": 1.0,  "radius": 0.9, "type": "blunt", "power_mult": 1.4 }
]
```
One entry per combo step (the last is the finisher, SYS-COMBAT-01 §Melee). A weapon with a single block keeps
working: it becomes one `arc` attack of its `cone_degrees`/`reach` and type `blunt` (fists) or as tagged.

## Starter weapons (proposal — types only, numbers stay as they are)
| Weapon | Steps | Type |
|---|---|---|
| Fists | arc, arc, smash | blunt |
| Stone hatchet | arc, arc, smash | slash, slash, blunt (back of the head) |
| Stone pickaxe | smash ×3 | pierce |
| Stone spear | thrust ×3 | pierce |
| Short bow | projectile | pierce |

## I/O
Weapon/artifact defs: `attacks[]`. Creature defs: `resist{type: mult}`, `weak_points` (pierce crit zones), each
creature attack's `type` and `shape`. Armor items: `armor{type: value}`. Buff defs gain the side-effect statuses.

## Open questions (developer)
- None left for the system. Per-creature resist tables and per-buff resist values are content, set with each def.

## Verification (to write as tests when built)
| # | Case | Expected |
|---|---|---|
| 1 | Same power, blunt vs slash on a shelled target | blunt > slash by the resist ratio |
| 2 | Pierce vs armored target | armor reduced by `PierceBypass` before the SYS-COMBAT-01 curve |
| 3 | Each shape's hit test | a target just inside/outside each parameter is hit/missed |
| 4 | Old single-block weapon def | loads as one `arc` attack, same damage as today |
