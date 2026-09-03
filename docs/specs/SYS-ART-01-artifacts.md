# SYS-ART-01 · Artifacts

## Purpose
**Weapons that scale off production skills.** They keep cooks, anglers, and smiths relevant in combat. This is the game's only real differentiator, so the balance rules are strict.

## Power

```
FinalPower = ArtifactBasePower
           * (SkillFloor + (1 - SkillFloor) * scalingSkillLevel / MaxLevel)
           * enchantMult * zoneMult
```

Three differences from ordinary weapons:
1. **No `qualityMult`** — artifacts are unique items with no quality tier
2. `scalingSkill` is a **production** skill
3. `zoneMult` exists

```
combat_skill      : null      // uses no combat skill
grants_combat_xp  : false     // grants no combat XP
scaling_skill     : "isle:cooking"
```

> **These two fields enforce the design at the schema level.** If an artifact granted combat XP, Focus (`SYS-SKILL-01`) would be undermined and the game would slide back to "get strong by grinding combat".

## Balance ★

Reference: a Lv50 melee character with a Superior sword (base 30) hits **39.0** (`SYS-COMBAT-01`). Call that 100%.

```
ArtifactBasePower = 31   →  Lv50 artifact = 31.0 = 79% of reference
```

| Skill Lv | Power | vs reference |
|---|---|---|
| 10 | 18.6 | 48% |
| 25 | 23.2 | 60% |
| 40 | 27.9 | 72% |
| 50 | 31.0 | **79%** |
| 50 + zone 1.4 | 43.4 | 111% |

**Hard rules**
1. Raw DPS stays at 75–85% of a combat specialist. **Never raise `ArtifactBasePower` above 33**
2. Only the zone bonus pushes past 100% — strong in your element, ordinary elsewhere
3. Fueled by your own discipline's resources; no unlimited use
4. Strong on **different axes**. A power ranking means the design failed
5. Acquired at **skill Lv 40** plus a dedicated quest, one per character
6. **Animation, VFX, and audio get triple the budget of ordinary weapons**

`zoneMult` = `ArtifactDef.zone_bonus.multiplier` when the `near_tag` within `radius` condition holds, else 1.0. Re-evaluate every 0.5 s.

## Core artifacts

### Great Cauldron Ladle `isle:artifact_ladle`
Cooking · base 31 · grid 2×4 · 4.0 kg · two-handed · 3 slots
Fuel: one `food_ingredient` per ability · Zone: `station/campfire` within 8 tiles → ×1.4

**Basic** — wide swing, 150° cone, radius 2.8 tiles. Boiling broth burns for 3 s at 12% power/s. Stamina 20.
**Hot Serving** (CD 12 s, fuel 1) — throw food to one ally within 10 tiles: hunger +25, thirst +15. If a finished dish is in inventory, **its buffs transfer as-is**.
**Boilover** (CD 25 s, fuel 3) — 4-tile radius pool for 3 s: 25% power/s burn, enemy speed −30%.

Intent: the cook becomes damage **and** support. Dishes prepared beforehand become battlefield resources — production and combat connect directly.

### Abyss-Caller's Rod `isle:artifact_rod`
Fishing · base 31 · grid 1×5 · 2.2 kg · two-handed · 3 slots
Fuel: one `bait` per ability · Zone: `water` within 6 tiles → ×1.5

**Basic** — whip lash, 5 tiles, no pierce. Applies `hooked` for 2 s (stacks to 3, +8% damage taken each). Stamina 12.
**Reel In** (CD 8 s, fuel 1) — 9 tiles, pull the target 5 tiles toward you plus 1 s stagger. **Targets over 15 kg pull you to them instead** (grapple movement).
**To The Surface** (CD 30 s, fuel 2) — execute targets in or on water below 40% HP; against land targets, ×2.2 single hit.

Intent: the only mobility/control artifact. Dominant near water, ordinary inland — terrain choice becomes strategy.

### Unbroken Anvil Hammer `isle:artifact_hammer`
Crafting · base 31 · grid 2×4 · 8.5 kg · two-handed · 4 slots
Fuel: one `metal` per ability · Zone: `station/forge` within 8 tiles → ×1.4

**Basic** — overhead smash, slow (0.6 speed). Target armor −15% for 5 s, stacks to 3. **Heat +1 per hit** (max 10). Stamina 24.
**Field Repair** (CD 20 s, fuel 1) — restore 35% max durability to your own or an ally's equipment within 4 tiles. **The only mid-combat repair.**
**Heat Release** (costs 10 heat, fuel 2) — 3.5-tile explosion at ×2.8 power. Unusable below 10 heat.

Intent: the only tank-shaped artifact. Slow, but breaks armor and keeps allies' gear alive. The 8.5 kg weight is heavy inventory pressure — a deliberate cost.

## Acquisition

| Artifact | Requires | Quest |
|---|---|---|
| Ladle | Cooking 40 | `isle:quest_great_feast` |
| Rod | Fishing 40 | `isle:quest_the_great_catch` |
| Hammer | Crafting 40 | `isle:quest_unbroken_anvil` |

One per character; swapping requires returning the old one. **The moment of acquisition should be that character's narrative peak** — invest in the presentation.

## Verification

| # | Input | FinalPower |
|---|---|---|
| 1 | base31, cooking Lv0 | 15.5 |
| 2 | base31, cooking Lv40 | 27.9 |
| 3 | base31, cooking Lv50 | 31.0 |
| 4 | base31, cooking Lv50, near campfire (1.4) | 43.4 |
| 5 | base31, fishing Lv50, near water (1.5) | 46.5 |
| 6 | Lv50 artifact ÷ Lv50 Superior sword (39.0) | 0.79 ± 0.02 |

**Case 6 is the core check.** Outside 0.75–0.85, balance is broken.

Integration tests: killing with an artifact must grant **zero** melee/ranged XP, and artifact use must **not** grant cooking/fishing/crafting XP either (combat is not a production activity).

## Location
```
Scripts/Combat/Artifacts/
  ArtifactPowerCalculator.cs  ★ static pure
  ArtifactBehaviour.cs        shared base: fuel, cooldown, zone checks
  ZoneBonusEvaluator.cs       ★ static pure
  {Ladle,Rod,Hammer}Artifact.cs
```
Abilities live in these classes, but **every number reads from `artifacts/*.json`**. No cooldowns, ranges, or multipliers in C#.

## ⚠️ Validation gate
**Build the Ladle first and playtest it.**
1. Does a cooking-focused player feel like a spectator in combat?
2. Does using it feel **spectacular**? (qualitative, mandatory)
3. Does the party **want** to bring the cook?

If any answer is no, redesign before building the other two. This is a design risk, not a schedule risk — stopping here is correct.

## Open questions
- Content of the three quests
- Artifact VFX art specs (decide in `ART_PIPELINE.md`)
- Whether artifacts can be enchanted (slots defined, but post-EA)
- Return ceremony and re-acquisition cooldown
