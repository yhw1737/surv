# SYS-CRAFT-02 · Materials, durability, repair

Status: **numbers decided 2026-10-08** (Q&A with the developer). Values marked [invented] are the agent's fill-ins
and are listed in `docs/PROJECT_STATE.md` §Decided without a spec.

Builds on SYS-CRAFT-01 (repair decay 0.92, adjacent assist). Quality tiers (SYS-CRAFT-01 §Quality) are **not** part
of this sheet; T-090 adds them on top.

## Principle
Freedom over fixed kits. Gear is defined **once per kind** — a T-shirt, shorts, a parka, a sword — and made from
**any material the kind accepts**. The material sets protection per damage type, warmth, durability, weight and
weapon power. A new material (a mod's, or a dungeon's) works with every kind at once, with no new item files.

## Decided values (developer Q&A, 2026-10-08)
| Constant | Value |
|---|---|
| Gear model | **kinds × materials** — not a fixed set of items per material |
| Layers | torso has an **inner layer** (`shirt` slot: T-shirt, shirt) under the **outer layer** (`chest`: jacket, parka, breastplate); head, legs, feet one each |
| Material choice | clothing, armor, weapons and tools all pick a material |
| Material properties | protection per damage group (sharp = slash + pierce, blunt, heat), warmth, durability, weight, weapon power (sharp and blunt apart) |
| Fabric / hide | cloth from plant fibre; **a hide per animal** (deer leather, boar, crocodile, snake; rabbit, fox, wolf fur) |
| Metal ladder | stone (1) → copper (2) → iron (3) → steel (4); copper on the coast, iron in the marsh, coal in the forest, plus dungeon veins |
| Processing | furnace (ore + fuel → ingot), anvil (metal work and metal repairs) |
| Durability | **60 / 150 / 300 / 500** for stone / copper / iron / steel gear of base 60 |
| Metal performance | **×1 / 1.25 / 1.5 / 1.8** — weapon sharp power, tool speed, sharp protection |
| Crafting level | copper 10, iron 20, steel 30 |
| Mining gate | a vein needs a pickaxe of at least its tier (iron: copper or better); no skill level |
| Wear | tools and weapons −1 per use (a harvest, a swing that lands, an arrow loosed); worn armor −1 per hit taken, every piece; misses cost nothing |
| At 0 | broken, not destroyed: a broken weapon fights as bare hands, a broken tool counts as no tool, broken armor gives no protection — until repaired |
| Repair | at the item's crafting station (a material's station overrides: metal → anvil), for **half its recipe and half its material, rounded up**; max becomes `max × 0.92` and the item is restored to it |

## Formulas
A variant of kind **K** made of material **M** (`isle:<kind>__<material>`, generated at load):

```
durability   = round(K.durability × M.durability)
protection   = K.armor × M.armor_sharp   vs slash, pierce
               K.armor × M.armor_blunt   vs blunt
               K.armor × M.armor_heat    vs heat
warmth       = K.warmth × M.warmth
weight       = K.weight + K.stuff.amount × M.mass
weapon power = round(K.base_power × (blunt kind ? M.power_blunt : M.power_sharp))
               each combo step's power_mult × factor(step type) / factor(kind type)
tool speed   = M.tool_speed      tool tier = M.tier
arrow power  = M.power_sharp
name         = "{material} {kind}"   (@pattern.made_of)
```
Crafting a kind takes the recipe's fixed inputs plus `stuff.amount` units of the material, at the recipe's station
unless the material names one, at `max(recipe level, material craft_level)`.

## Materials [invented, except where the decided table fixes them]
| Material | Item | Cat. | Tier | Lv | Sharp | Blunt | Heat | Warmth | Dur. | Mass | Pwr sharp | Pwr blunt | Tool |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| cloth | cloth | fabric | 1 | 1 | 0.4 | 0.3 | 0.5 | 1.0 | 0.6 | 0.2 | — | — | — |
| leather | hide (deer) | leather | 1 | 1 | 1.0 | 0.8 | 0.8 | 1.2 | 1.0 | 0.3 | — | — | — |
| boar leather | boar hide | leather | 1 | 5 | 1.2 | 1.1 | 0.8 | 1.3 | 1.4 | 0.4 | — | — | — |
| croc leather | croc hide | leather | 1 | 10 | 1.5 | 0.9 | 1.0 | 1.0 | 1.6 | 0.5 | — | — | — |
| snakeskin | snake skin | leather | 1 | 5 | 0.8 | 0.6 | 0.7 | 0.8 | 0.8 | 0.2 | — | — | — |
| rabbit fur | rabbit fur | fur | 1 | 1 | 0.6 | 0.5 | 0.6 | 2.0 | 0.7 | 0.2 | — | — | — |
| fox fur | fox fur | fur | 1 | 5 | 0.7 | 0.6 | 0.6 | 2.2 | 0.8 | 0.25 | — | — | — |
| wolf fur | wolf fur | fur | 1 | 5 | 0.9 | 0.8 | 0.7 | 2.5 | 1.0 | 0.3 | — | — | — |
| wood | wood | wood | 1 | 1 | 0.6 | 0.6 | 0.3 | 0.5 | 1.0 | 0.5 | 0.7 | 1.0 | 0.9 |
| stone | stone | stone | 1 | 1 | 0.8 | 0.7 | 0.9 | 0.2 | **1.0** | 0.6 | **1.0** | 1.0 | **1.0** |
| copper | copper ingot | metal | 2 | **10** | **1.25** | 1.0 | 1.2 | 0 | **2.5** | 0.8 | **1.25** | 1.2 | **1.25** |
| iron | iron ingot | metal | 3 | **20** | **1.5** | 1.2 | 1.3 | 0 | **5.0** | 1.0 | **1.5** | 1.4 | **1.5** |
| steel | steel ingot | metal | 4 | **30** | **1.8** | 1.4 | 1.5 | 0 | **8.33** | 1.0 | **1.8** | 1.6 | **1.8** |

Bold = from the decided table (durability 60/150/300/500 on base 60; ×1/1.25/1.5/1.8). Metal is worked at the anvil.

## Kinds [invented]
Clothing and armor (durability base 60, made at the workbench):

| Kind | Slot | Materials | Units | Armor | Warmth | Lv |
|---|---|---|---|---|---|---|
| T-shirt | shirt | fabric, leather | 2 | 1 | 1 | 1 |
| shirt | shirt | fabric, leather | 3 | 1.5 | 2 | 3 |
| jacket | chest | fabric, leather, fur | 4 | 4 | 3 | 5 |
| parka | chest | fabric, leather, fur | 6 | 4 | 6 | 8 |
| breastplate | chest | metal | 6 | 8 | 0 | 1 |
| shorts | legs | fabric, leather | 2 | 1 | 0.5 | 1 |
| pants | legs | fabric, leather, fur | 3 | 3 | 1.5 | 3 |
| greaves | legs | metal | 4 | 6 | 0 | 1 |
| cap | head | fabric, leather, fur | 1 | 2 | 1 | 1 |
| helmet | head | metal | 3 | 6 | 0 | 1 |
| shoes | feet | fabric, leather | 1 | 1 | 0.5 | 1 |
| boots | feet | leather, fur, metal | 2 | 3 | 1 | 5 |

Weapons and tools (durability base 60, no station unless the material names one):

| Kind | Materials | Units + fixed | Base power | Type | Notes |
|---|---|---|---|---|---|
| hatchet | stone, metal | 2 + wood 2 | 22 | slash (blunt finisher) | `tool/axe` |
| pickaxe | stone, metal | 3 + wood 2 | 18 | pierce | `tool/pickaxe` |
| knife | stone, metal | 1 + wood 1 | 16 | slash | `tool/knife` (butchery doesn't read it yet) |
| spear | wood, stone, metal | 1 + wood 2 | 30 | pierce | two-handed |
| sword | metal | 3 + wood 1 + any leather 1 | 26 | slash | |
| club | wood, stone | 3 + wood 1 | 26 | blunt | |
| mace | metal | 3 + wood 2 | 26 | blunt | |
| arrows ×5 | stone, metal | 1 + wood 2 + fibre 1 | — | pierce | arrow power = material sharp power |

The short bow is the one bow; the best arrows in the bag are shot first.

Other recipes: cloth = fibre ×3 at the workbench; charcoal = wood ×3 at the furnace; copper ingot = copper ore ×2 +
coal; iron ingot = iron ore ×2 + coal (Lv 20); steel ingot = iron ingot ×2 + coal ×2 (Lv 30); furnace kit = stone 12 +
wood 4, anvil kit = copper ingot 5 + stone 4 (both workbench, Lv 10). Repair grants half the recipe's base XP.

## Veins [invented]
| Vein | Biome | Gives | Uses | Harvest | Tool tier |
|---|---|---|---|---|---|
| copper vein | coast | copper ore ×2 | 3 | 10 s | 1 |
| coal seam | forest | coal ×2 | 3 | 8 s | 1 |
| iron vein | marsh | iron ore ×2 | 3 | 12 s | 2 |

Dungeon floors carry 3 veins each from the dungeon's `veins` list.

## Verification
| # | Case | Expected |
|---|---|---|
| 1 | Fresh stone hatchet, 60 harvests | durability 0, broken; the next harvest gives no tool bonus |
| 2 | Repair a broken 60-max item | max 55, durability 55, half the inputs and half the material taken |
| 3 | Repair three times | max 60 → 55 → 51 → 47 |
| 4 | Iron vein with a stone pickaxe | refused; a copper pickaxe yields |
| 5 | Copper hatchet harvest time | stone time ÷ 1.25 |
| 6 | Hit taken in two armor pieces | both −1 |
| 7 | Broken sword equipped | attacks as bare hands |
| 8 | Unequip, re-equip, save, die and recover | durability kept |
| 9 | A fabric kind × a metal material | no variant |
| 10 | Pickaxe of stone / copper / iron / steel | durability 60 / 150 / 300 / 500 |
