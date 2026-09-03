# Backlog

**One AI session = one task.** Each is roughly 2–5 hours. Ordered by risk, highest first.

`[ ]` pending · `[~]` in progress · `[x]` done · `[!]` blocked

## Phase 0 · Rig validation — top gate

**Nothing else matters until this passes. Do not skip ahead.**

- [ ] **T-000** Unity project. Mono backend pinned, URP 2D, asmdef skeleton, Git LFS
- [ ] **T-001** Import character PSD, place bones in Skinning Editor (SYS-CHAR-01 §Rig)
- [ ] **T-002** IK Manager 2D — Limb solver on front arm, look-at on head
- [ ] **T-003** `AimController` — mouse → aimAngle, head/torso angle limits
- [ ] **T-004** ★ Flip + transition animation (SYS-CHAR-01 §Angles). **Hardest task**
- [ ] **T-005** Weapon grip presets (one-hand / two-hand / tool)
- [ ] **T-006** 🚩 **Gate G1** — SYS-CHAR-01 verification cases 1–4 + 5 outside evaluators

> **G1 fails →** try plan B (8-direction sprites, forearm IK only) for two weeks. If that fails too, ADR-001 §10 review.

## Phase 1 · Foundation — keep this order

- [ ] **T-010** `NamespacedId`, `TagRegistry` flattening (SYS-CORE-01)
- [ ] **T-011** `Data` layer POCOs — ItemDef, CreatureDef, FishDef, CookMethodDef, etc.
- [ ] **T-012** `DefinitionLoader` + `SchemaValidator`
- [ ] **T-013** ★ `ReferenceResolver` + typo-suggesting error messages
- [ ] **T-014** `DefRegistry` + tag index
- [ ] **T-015** F5 hot reload
- [ ] **T-016** 🚩 **Gate:** editing `items/*.json` reflects without recompiling. **Do not proceed until this works**
- [ ] **T-020** FishNet bootstrap, listen server connection
- [ ] **T-021** Server-authoritative movement + client prediction
- [ ] **T-022** aimAngle 20 Hz sync, facingSign as event (SYS-CHAR-01 §Network)
- [ ] **T-023** ★ Solo testing rig — two-client script + latency simulator (SYS-NET-01)
- [ ] **T-024** 🚩 Remote aim reads correctly at 200 ms latency

## Phase 2 · World

- [ ] **T-030** `WorldClock` in-game time
- [ ] **T-031** Tilemap, Y-sort, collision, 3-step camera zoom
- [ ] **T-032** `Chunk` + `ChunkManager` load/unload
- [ ] **T-033** SQLite chunk persistence
- [ ] **T-034** `DeferredSimulation` (SYS-WORLD-01)
- [ ] **T-035** `IslandGenerator` seeded procedural (coast + forest)
- [ ] **T-036** Hand-placed landmarks (shipwreck, ruins, springs)

## Phase 3 · Inventory

- [ ] **T-040** `GridInventory` structure + EditMode tests
- [ ] **T-041** `WeightCalculator` + 5 verification cases
- [ ] **T-042** Grid UI + dragging
- [ ] **T-043** ★ Rotate (R), Ctrl+click bulk move, Shift+drag split. **Do not defer**
- [ ] **T-044** Equipment slots + bag expansion
- [ ] **T-045** Server-authoritative sync + rollback UI
- [ ] **T-046** 🚩 Two players on one crate: no duplication or loss

## Phase 4 · Survival + skills

- [ ] **T-050** `Vitals` — 5 gauges (SYS-SURV-01)
- [ ] **T-051** Temperature + wetness + campfire
- [ ] **T-052** Six water sources
- [ ] **T-060** `XpCurve` + `SkillSet`
- [ ] **T-061** ★ `FocusCalculator` + 9 verification cases (SYS-SKILL-01)
- [ ] **T-062** `ActivityTracker` 7-day median
- [ ] **T-063** Skill UI — split production/combat tabs, **show focus as a bonus**
- [ ] **T-064** `RustSystem` (flag, default off)

## Phase 5 · Production loop

- [ ] **T-070** Gathering interaction + resource respawn + tool durability
- [ ] **T-071** Creature spawning + individual weight rolls
- [ ] **T-072** ★ `ButcheryCalculator` + 4 verification cases (SYS-HUNT-01)
- [ ] **T-073** Cut splitting + damage-sensitive outputs
- [ ] **T-074** ★ Carcass drag / two-player carry
- [ ] **T-075** Carcass spoilage + predator scent
- [ ] **T-080** `FishSelector` weighting
- [ ] **T-081** Tension minigame
- [ ] **T-082** Trap and net deferred simulation

## Phase 6 · Crafting + cooking

- [ ] **T-090** `QualityCalculator` + 5 verification cases
- [ ] **T-091** Forging minigame
- [ ] **T-092** `AdjacentAssist`
- [ ] **T-093** Slot enchanting + crafter mark + repair
- [ ] **T-100** ★ `TagReactionEngine` (SYS-COOK-01)
- [ ] **T-101** ★ `CookingResolver` full algorithm
- [ ] **T-102** 8 cook method definitions + stations
- [ ] **T-103** Buffs + care tag + satiety fatigue
- [ ] **T-104** `DishNamer` procedural naming + player registration
- [ ] **T-105** 🚩 **Extensibility gate** — one new ingredient works across all 8 methods with zero C# edits

## Phase 7 · Combat

- [ ] **T-110** `PowerCalculator` + `DamageResolver` + 7 verification cases
- [ ] **T-111** Melee — 3-hit combo, block, parry
- [ ] **T-112** Ranged — charge, `SwayCalculator`, projectiles
- [ ] **T-113** Hit detection + lag compensation (melee only) + i-frames
- [ ] **T-114** Dodge roll
- [ ] **T-115** `CreatureAI` 4-state machine + 3 creatures

## Phase 8 · Artifacts 🚩 biggest gate

- [ ] **T-120** `ArtifactPowerCalculator` + `ZoneBonusEvaluator` + 6 verification cases
- [ ] **T-121** ★ Great Cauldron Ladle, all 3 abilities (SYS-ART-01)
- [ ] **T-122** Ladle animation set (triple the normal weapon budget)
- [ ] **T-123** Ladle VFX + audio
- [ ] **T-124** 🚩 **Gate G3** — SYS-ART-01 validation questions. **Anything but yes → stop and redesign**
- [ ] **T-125** Abyss-Caller's Rod (only after G3)
- [ ] **T-126** Unbroken Anvil Hammer (only after G3)

## Phase 9 · Modding + polish

- [ ] **T-130** `ModLoader` + `LoadOrderResolver` + `PatchApplier`
- [ ] **T-131** Two example mods (new fish / new cook method)
- [ ] **T-132** Modding documentation draft
- [ ] **T-140** Minimal tutorial
- [ ] **T-141** Options menu + key rebinding
- [ ] **T-142** Profiling + optimization
- [ ] **T-143** 🚩 **Gate G4** — final assessment

## Gates

| Gate | Task | Question |
|---|---|---|
| **G1** | T-006 | Does the mouse-tracking rig read naturally? |
| **G2** | after T-115 | Do two players naturally split roles over 90 minutes? |
| **G3** | T-124 | **Does a production specialist feel useless in combat?** |
| **G4** | T-143 | Can an outsider's mod actually load? |

**G1 and G3 send you backward.** Do not continue past a failed gate.

## Idea parking lot — not built

Everything here is **outside ISLE Core**. New ideas go here only.

### Post-EA roadmap
- Magic + catalysts + spirit gauge
- Guns + gunpowder (sulfur, saltpeter, charcoal)
- Shield skill + ally cover
- 7 more artifacts (scythe, daggers, needle case, ruler, shuttle, staff, broken staff)
- **Overload enchanting** — formula preserved:
  ```
  nth overload failure = clamp(0.20 + 0.18*(n-1) - witchLv*0.003 - stabilizer(0..0.12), 0.02, 0.85)
  three failure grades: crack (durability -20%) / loss (1 enchant + durability -30%) / destroy (returns scrap, 30% material recovery)
  witchLv * 1% chance to downgrade destroy → crack
  ```
- Soil NPK simulation + crop rotation
- Crop breeding (8 traits, two-parent crossing, named cultivars)
- Four seasons
- Dedicated servers + 8 players + server browser
- Modding Tier 2 (Lua) / Tier 3 (C#)
- Sailing + new biomes
- Castaway NPCs
- Gamepad support

### Unsorted
- (add new ideas here)
