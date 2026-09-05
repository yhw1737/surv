# ISLE — Game Design Document (ISLE Core)

This document holds **why**. For exact numbers when implementing, use `docs/specs/`.

Scope: **ISLE Core** — solo dev + AI, 18-month Early Access target.

## Concept

On an island where nobody can master everything, each player becomes a specialist.
**And a cook, an angler, or a smith who went deep is not weak in combat.**

- Genre: co-op survival / sandbox / crafting
- View: top-down 2D
- Players: 2–4 recommended, solo playable
- Platform: PC (Steam), Early Access, ~$14.99

## Design axes

Every system derives from these. A feature that fits none of them is not built.

| Axis | Meaning | Where |
|---|---|---|
| **Specialization is the fun** | Nobody can be good at everything | Focus, weight limits, corpse hauling |
| **Depth equals power** | Production specialists lead in combat too | Artifacts |
| **Results come from inputs and process** | Combinations, not fixed recipes | Tag cooking, butcher yield, quality tiers |
| **The world is data** | Our tools are the players' tools | All-JSON definitions, Workshop |

## Scope

**In**

| System | Scope |
|---|---|
| Production skills | **5** — gathering, hunting, fishing, cooking, crafting |
| Combat skills | **2** — melee, ranged |
| Level cap | 50 |
| Artifacts | **3** — Great Cauldron Ladle (cooking), Abyss-Caller's Rod (fishing), Unbroken Anvil Hammer (crafting) |
| Cook methods | **8** — raw, grill, boil, porridge, dry, stew, smoke, ferment |
| Biomes | **3** — coast, forest, marsh |
| Multiplayer | 4 players, listen server |
| Quality | 5 tiers + enchant slots |
| Enchanting | **slot enchants only** (safe range) |
| Modding | **Tier 1 (JSON)**, architecture ready for Tier 2/3 |
| Farming | till, sow, harvest (no soil sim) |

**Deferred to post-EA:** magic, guns, shields, 7 more artifacts, overload enchanting and breakage, soil NPK sim, crop breeding, seasons, dedicated servers, 8 players, modding Tier 2/3, sailing, gamepad. Deferred, not deleted — this is what Early Access is for.

**Permanently out:** PvP (conflicts with co-op design), monetization.

**Never cut these three:**
1. **Artifacts** — the only real differentiator
2. **Skill focus** — without it this is just another survival game
3. **Tag-based cooking** — the only way one person can supply this much content

## Character presentation

> **Deferred to Phase 9 (2026-09-04).** Placeholder boxes until the systems are proven.
> The perspective is **quarter view** — side-on, angled slightly toward the camera and slightly
> from above (`ART_PIPELINE` `top-down 3/4`), *not* the pure side view the rig below assumes.
> `SYS-CHAR-01` carries the full list of what that changes.

Left/right sprites, with head, torso, arms, and weapon tracking the mouse.

```
Root
 Hip
   Leg_L / Leg_R          follow movement direction
   Torso                   ±20° twist toward mouse
     Head                  look-at, ±70°
     Arm_Back              IK → support grip
     Arm_Front → Socket    IK → aim point
```

Lower body follows movement, upper body follows the mouse — this is what makes backpedaling and strafing read correctly. Past 70° behind, the whole sprite flips with a transition animation.

**Why cutout rigging:** frame animation explodes as items × directions × actions. For a solo developer it is not a choice.

Spec: `SYS-CHAR-01`

## Survival loop

Gauges: health, hunger, **thirst**, temperature, stamina.

Thirst drains twice as fast as hunger. On an island, "you can't drink seawater" is the early tension, and it gives cooking (boil restores the most water) real meaning.

**Volume vs weight are separate constraints.** Volume is grid cells; weight drives speed, stamina, and whether you can roll. Separating them makes bulky-light straw feel different from small-heavy ingots — and **weight limits are what create the need for a partner.**

## Skills — the differentiator

Two pools: 5 production, 2 combat, levels 1–50.

**Framed as a bonus, never a penalty.**
- ❌ "Raising another skill tripled your XP requirement"
- ✅ "**Focus 88%** — you're specializing, so you earn 1.86× XP"

| Band | Name | Interference | Intent |
|---|---|---|---|
| 1–15 | Novice | **none** | Zero early friction; this prevents churn |
| 16–35 | Adept | half | Two or three skills stay comfortable |
| 36–50 | Master | full | Effectively one or two masteries |

**`CrossPoolFactor = 0.35` is the heart of the game.** Same-pool skills interfere fully; production↔combat only at 35%.

- Cooking 45 + Melee 35 → **works** (Focus 0.88). Cooks can fight
- Cooking 45 + Crafting 40 → **hard** (Focus 0.53). One craft mastery only
- Everything at 35 → **inefficient** (Focus 0.30). Nothing goes deep

The equilibrium is **one production mastery + one combat specialty per player**. That's the party unit.

Solo play stays possible but slower: interference scales by median active players over 7 days (0.35× at one player). Median, not concurrent, so hopping a friend on doesn't game it.

**Rust (skill decay)** exists but ships **off by default**. Levels never drop — only current-level progress, framed as stiffness that clears after a few actions. The goal is role identity, not punishment, and it's the #1 churn risk. A/B test it in EA.

Spec: `SYS-SKILL-01`

## Inventory

Tetris-style grid, 6×3 base, expanded by bags.

Co-op hooks: mid/large corpses don't fit in inventory (drag at −60% speed, or two people carry at −20%); drying and smoking shrink volume so the cook solves expedition logistics; someone ends up as the storage organizer, which is a real role.

> Grid inventory sits one step from tedium. **Rotation, bulk move, and warehouse auto-sort ship from day one.** Added later, you've already lost the player.

Spec: `SYS-INV-01`

## Combat — your weapon is your class

No class selection. Hold a sword, you're a swordsman.

```
FinalPower = base × (0.5 + 0.5 × skill/50) × quality × enchant × situational
```

**The 0.5 floor matters.** A cook with no combat skill can still pick up a blade in an emergency. Skill governs efficiency, not permission.

Spec: `SYS-COMBAT-01`

## Artifacts — the specialist's power ★

Weapons that **scale off production skills** and neither require nor grant combat XP.

A Fishing 50 player wielding the Abyss-Caller's Rod has the battlefield presence of a Ranged 50 archer.

| Artifact | Scales off | Character |
|---|---|---|
| Great Cauldron Ladle | Cooking | AoE burn + throwing prepared dishes to allies |
| Abyss-Caller's Rod | Fishing | Grapple — pull enemies in or pull yourself |
| Unbroken Anvil Hammer | Crafting | Armor shred + repairing allies' gear mid-fight |

**Balance rules (mandatory)**
1. Raw DPS ≈ **80%** of a combat specialist. Utility is what makes up the gap
2. Fueled by your own discipline's resources — food, bait, metal
3. Zone bonus: strong in your element (campfire, water, forge)
4. Requires skill 40 + a dedicated quest. One per character
5. **Animation and VFX get triple the budget of normal weapons.** They must feel spectacular
6. Artifacts are strong on different axes. If a power ranking emerges, the design failed

**Why it works:** production specialists stop being spectators, which removes the worst side effect of forced specialization; skill 40 gives depth a concrete reward; and taking no combat XP keeps Focus intact.

Spec: `SYS-ART-01`

## Hunting and butchery

Yield scales with real body weight. For a 62 kg boar:

| Situation | Yield |
|---|---|
| Lv 5, bare hands | ~10 kg |
| Lv 30, iron knife | ~22 kg |
| Lv 50, master tools | ~40 kg |

**A 4× spread is what makes "we need our hunter" true.**

Output splits into cuts (meat, fat, offal, bone, hide). Low skill destroys bone and hide, so **bags and armor require hunter + crafter cooperation.** Weapon choice matters too: bows and traps preserve hide, blunt weapons destroy it.

Spec: `SYS-HUNT-01`

## Fishing

`depth × terrain × time × weather × bait × rig` produces species weights — a probability table, not a lookup.

The minigame is **line tension management**; skill level widens the safe window from pinhole to comfortable.

Individual fish roll their own weight (log-normal). A 3.2 kg snapper and a 0.4 kg one yield different meat and cook differently.

Spec: `SYS-FISH-01`

## Cooking — the variety engine

**Techniques unlock by level; ingredient combinations stay free forever.** That gives both progression satisfaction and emergent depth.

| Lv | Method | Character |
|---|---|---|
| 1 | Raw | instant, food-poisoning risk |
| 1 | Grill | hunger ↑, thirst ↓ |
| 4 | Boil | **best water restore** — the early-drought answer |
| 8 | Porridge | zero digestive load. **Invalid food** |
| 12 | Dry | max preservation, **shrinks volume** |
| 20 | **Stew** | 3+ ingredients → two buffs at once |
| 28 | **Smoke** | preservation + buff retention → the expedition answer |
| 40 | **Ferment** | unique buffs, failure risk |

Level 20 and 28 are the inflection points where the cook's standing in the party jumps.

**There is no best dish, only the right dish now.** Smoke before an expedition, broth in the heat, fried in the cold.

Why a dedicated cook is needed: **care tag** (Lv 40+ extends buff duration by 50%), **satiety fatigue** (repeating a dish decays its value, forcing variety), and the Ladle artifact putting the cook on the front line.

Spec: `SYS-COOK-01`

## Crafting, quality, enchanting

**Quality tier determines enchant slots.**

| Tier | Power | Slots |
|---|---|---|
| Crude | ×0.70 | 0 |
| Common | ×1.00 | 1 |
| Fine | ×1.15 | 2 |
| Superior | ×1.30 | 3 |
| Master | ×1.50 | 4 |

**Slot count is why the smith exists.**

Higher benches demand several skills, but a **nearby ally contributes up to 60%** of the shortfall — build it together and it gets built. Items carry the crafter's name. Repairs approach but never restore the original ceiling, so demand for the crafter persists.

Spec: `SYS-CRAFT-01`

## World

| Zone | Difficulty | Resources | Gate |
|---|---|---|---|
| Coast | ★ | shellfish, driftwood, small fish, coconut | none (start) |
| Forest | ★★ | timber, berries, rabbit/deer/boar | none |
| Marsh | ★★★ | clay, reed, herbs, crocodile | disease resistance, boots |

Procedural generation plus hand-placed landmarks (shipwreck, ruins, spring). Fully random worlds are not memorable — landmarks are what make settlement choice and mental mapping work.

**Goals are achievements, not bosses:** self-sufficiency (20 days without outside gathering), artifact acquisition, a master-tier item.

## Multiplayer

Host-authoritative with client prediction. Listen server for Core; dedicated servers post-EA.

**Ping/marker system is mandatory** — co-op must work without voice chat. On death, items drop; **an ally recovering your body reduces the penalty**, so rescue is rewarded.

Spec: `SYS-NET-01`

## Modding

> The tools we use to make content are the tools players use.

Base content — fish, cook methods, weapons — is authored in **the same JSON schema mods use**. Not one hardcoded path.

Core ships Tier 1 (JSON) only, but reserves hook points (the event bus) so Tier 2 (Lua) and Tier 3 (C#) bolt on later.

Spec: `docs/modding/SCHEMA.md`

## Risks

| # | Risk | Severity | Mitigation |
|---|---|---|---|
| 1 | Scope creep | 🔴 top | Treat the scope table as a contract; additions go to BACKLOG only |
| 2 | Art is the bottleneck | 🔴 high | `ART_PIPELINE.md` sourcing strategy |
| 3 | Co-op game, solo testing | 🔴 high | Two-client automation and bot clients built early |
| 4 | Rig doesn't work | 🟠 med | **Deferred to Phase 9 (2026-09-04).** Boxes until then; plan B is 8-direction sprites. Severity dropped because the game no longer blocks on it — but a late failure costs more |
| 5 | Artifacts aren't fun | 🔴 high | Validate with the Ladle alone; stopping here is correct |
| 6 | Focus reads as obstruction | 🟠 med | Bonus framing; zero interference below Lv 15 |
| 7 | Judging fun alone | 🟠 med | External playtest every 3 months |
| 8 | Grid inventory tedium | 🟠 med | Rotation and bulk move from day one |
