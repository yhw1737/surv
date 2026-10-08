# SYS-CRAFT-02 · Material tiers, durability, repair

Status: **numbers decided 2026-10-08** (Q&A with the developer). Developer direction: "이 게임은 scratch부터 장비를
제작하고, 끊임없이 유지보수를 하는 게 목적인 게임." Values marked [invented] are the agent's fill-ins and are listed in
`docs/PROJECT_STATE.md` §Decided without a spec.

Builds on SYS-CRAFT-01 (repair decay 0.92, adjacent assist). Quality tiers (SYS-CRAFT-01 §Quality) are **not** part of
this sheet; T-090 adds them on top.

## Decided values (developer Q&A, 2026-10-08)
| Constant | Value |
|---|---|
| Tier ladder | **stone (1) → copper (2) → iron (3) → steel (4)**; leather and wood count as tier 1. Master gear from dungeon materials comes later (SYS-ART-02) |
| Metal sources | **ore veins per biome** (copper on the coast, iron in the marsh, coal in the forest) **+ richer veins in dungeons** |
| Processing | **furnace** (ore + fuel → ingot, over the recipe time) and **anvil** (ingots → metal gear, and metal repairs) |
| Durability by tier | **60 / 150 / 300 / 500** |
| Performance by tier | **×1 / 1.25 / 1.5 / 1.8** on weapon power, tool harvest speed and armor |
| Crafting level by tier | stone 1, **copper 10, iron 20, steel 30** (smelting and gear alike; adjacent assist applies) |
| Mining gate | no skill level; a vein needs a pickaxe of at least its `tool_tier` (iron needs copper or better) |
| Wear | tools and weapons **−1 per use** (a harvest, a swing that lands, an arrow loosed); worn armor **−1 per hit taken**, every piece; a swing that misses costs nothing |
| At 0 | **broken, not destroyed**: a broken weapon fights as bare hands, a broken tool counts as no tool, broken armor gives no armor — until repaired |
| Repair | at the item's own crafting station (workbench for stone/leather/wood, anvil for metal), for **half the recipe's ingredients (rounded up)**; restores to the new maximum `max × 0.92` (SYS-CRAFT-01) |

## Gear per tier
| Kind | Tier 1 | Copper / iron / steel | Damage type |
|---|---|---|---|
| Hatchet (tool/axe) | stone hatchet | ✓ | slash (finisher blunt) |
| Pickaxe (tool/pickaxe) | stone pickaxe | ✓ | pierce |
| Knife (tool/knife) | stone knife | ✓ | slash, fast and short |
| Spear | stone spear | ✓ | pierce, long |
| Sword | — | ✓ | slash |
| Club / mace | wooden club | mace ✓ | blunt |
| Arrows | arrows | ✓ (more power per arrow) | pierce |
| Head / chest / legs / feet | hide cap, fur cloak, hide leggings, hide boots | helmet, chestplate, greaves, boots ✓ | — |

The short bow stays the one bow; arrows carry the tier. A bow uses the best arrows in the bag.

## Numbers
Weapon power = tier-1 base × tier performance, rounded half up. Tier-1 bases: spear 30, hatchet 22, pickaxe 18 (existing);
**knife 16, club/mace 26, sword 26** [invented]. Arrow power multiplier = tier performance.

Armor = leather value of that slot × tier performance (rounded): head 8, chest 10, legs 10, **feet 4** [invented].
Metal armor has no warmth [invented].

Tool harvest speed: a matching tool divides the node's harvest time by its tier performance.

### Recipes [invented]
| Recipe | Station | Inputs | Level | Time |
|---|---|---|---|---|
| charcoal ×1 | furnace | wood ×3 | 10 | 5 s |
| copper ingot | furnace | copper ore ×2, coal ×1 | 10 | 6 s |
| iron ingot | furnace | iron ore ×2, coal ×1 | 20 | 8 s |
| steel ingot | furnace | iron ingot ×2, coal ×2 | 30 | 10 s |
| furnace kit | workbench | stone ×12, wood ×4 | 10 | 6 s |
| anvil kit | workbench | copper ingot ×5, stone ×4 | 10 | 6 s |
| hatchet | anvil | ingot ×2, wood ×2 | tier | 4/6/8 s |
| pickaxe | anvil | ingot ×3, wood ×2 | tier | |
| knife | anvil | ingot ×1, wood ×1 | tier | |
| spear | anvil | ingot ×1, wood ×3 | tier | |
| sword | anvil | ingot ×3, wood ×1, hide ×1 | tier | |
| mace | anvil | ingot ×3, wood ×2 | tier | |
| arrows ×5 | anvil | ingot ×1, wood ×2, fiber ×1 | tier | |
| helmet | anvil | ingot ×3, hide ×1 | tier | |
| chestplate | anvil | ingot ×6, hide ×2 | tier | |
| greaves | anvil | ingot ×4, hide ×1 | tier | |
| boots | anvil | ingot ×2, hide ×1 | tier | |
| stone knife | — | stone ×2, wood ×1 | 1 | 3 s |
| wooden club | — | wood ×3 | 1 | 3 s |
| hide boots | workbench | hide ×2 | 1 | 4 s |

Crafting XP base 10 / 15 / 20 / 25 by tier, per unit 8 [invented]. Repair grants half the recipe's base XP [invented].

### Veins [invented]
| Vein | Biome | Gives | Uses | Harvest | Tool tier |
|---|---|---|---|---|---|
| copper vein | coast | copper ore ×2 | 3 | 10 s | 1 |
| coal seam | forest | coal ×2 | 3 | 8 s | 1 |
| iron vein | marsh | iron ore ×2 | 3 | 12 s | 2 |

Respawn as rocks (7200 in-game minutes). Dungeon floors carry **3 veins** each from the dungeon's `veins` list
(Grotto copper; Hollow copper + coal; Temple iron + coal; Ruin iron + coal), mined the same way.

## Verification
| # | Case | Expected |
|---|---|---|
| 1 | Fresh stone hatchet, 60 harvests | durability 0, broken; the 61st harvest gives no tool bonus |
| 2 | Repair a broken 60-max item | max 55 (60 × 0.92 = 55.2 → 55), durability 55, half the inputs taken |
| 3 | Repair three times | max 60 → 55 → 51 → 47 |
| 4 | Iron vein with a stone pickaxe | refused; with a copper pickaxe it yields |
| 5 | Copper hatchet harvest time | stone time ÷ 1.25 |
| 6 | Hit taken in leather cap + iron greaves | both −1 |
| 7 | Broken sword equipped | attacks as bare hands |
| 8 | Unequip a worn item, re-equip | durability kept; survives a save/load and a death drop |
