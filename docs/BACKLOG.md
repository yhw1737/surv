# Backlog

**One AI session = one task.** Each is roughly 2–5 hours.

`[ ]` pending · `[~]` in progress · `[x]` done · `[!]` blocked

> ### Priority — developer decision, 2026-09-04
> Solo indie, **targeting a beta release.** Characters, weapons and props stay **placeholder
> boxes**; the priority is building the *systems* that make the game fun and proving them
> bug-free — definitions, P2P + Steam networking, weapons, cooking.
>
> **Character rigging and art are deferred to Phase 9**, immediately before artifacts (the first
> thing that genuinely needs real animation). This reverses the original risk ordering, which put
> rig validation ahead of everything. Rationale and consequences: `PROJECT_STATE.md`.

## Phase 0 · Project setup — done

- [x] **T-000** Unity project. Mono backend pinned, URP 2D, asmdef skeleton, Git LFS
- [x] **T-001** Placeholder character rig — now serves as the box stand-in (SYS-CHAR-01 §Rig)
- [x] **T-002** IK Manager 2D — Limb solver on front arm, look-at on head

> T-001/T-002 are done and stay in the tree. They are not thrown away — they become the
> placeholder, and Phase 9 resumes from them.

## Phase 1 · Foundation — definitions. Keep this order

**Everything downstream depends on this.** Weapons and cooking are pure JSON (Absolute Rule 1),
so neither can start before the loader exists.

- [ ] **T-010** `NamespacedId`, `TagRegistry` flattening (SYS-CORE-01)
- [ ] **T-011** `Data` layer POCOs — ItemDef, CreatureDef, FishDef, CookMethodDef, etc.
- [ ] **T-012** `DefinitionLoader` + `SchemaValidator`
- [ ] **T-013** ★ `ReferenceResolver` + typo-suggesting error messages
- [ ] **T-014** `DefRegistry` + tag index
- [ ] **T-015** F5 hot reload
- [ ] **T-016** 🚩 **Gate:** editing `items/*.json` reflects without recompiling. **Do not proceed until this works**

## Phase 2 · Networking — P2P + Steam ★ developer priority

- [ ] **T-020** FishNet bootstrap, listen server connection
- [ ] **T-021** Server-authoritative movement + client prediction
- [ ] **T-022** aimAngle 20 Hz sync, facingSign as event (SYS-CHAR-01 §Network)
- [ ] **T-023** ★ Solo testing rig — two-client script + latency simulator (SYS-NET-01)
- [ ] **T-024** 🚩 Remote aim reads correctly at 200 ms latency
- [ ] **T-025** ★ Steamworks.NET + FishNet Steam transport — P2P lobby, friend invites, Tugboat fallback (ADR-001 §Stack)

> **T-025 is new (2026-09-04).** ADR-001 lists Steamworks.NET and the FishNet Steam transport in
> the stack table, but no task ever carried them. Named as a priority, so it is a task now.

## Phase 3 · World

- [ ] **T-030** `WorldClock` in-game time
- [ ] **T-031** Tilemap, Y-sort, collision, 3-step camera zoom
- [ ] **T-032** `Chunk` + `ChunkManager` load/unload
- [ ] **T-033** SQLite chunk persistence
- [ ] **T-034** `DeferredSimulation` (SYS-WORLD-01)
- [ ] **T-035** `IslandGenerator` seeded procedural (coast + forest)
- [ ] **T-036** Hand-placed landmarks (shipwreck, ruins, springs)

## Phase 4 · Inventory

- [ ] **T-040** `GridInventory` structure + EditMode tests
- [ ] **T-041** `WeightCalculator` + 5 verification cases
- [ ] **T-042** Grid UI + dragging
- [ ] **T-043** ★ Rotate (R), Ctrl+click bulk move, Shift+drag split. **Do not defer**
- [ ] **T-044** Equipment slots + bag expansion
- [ ] **T-045** Server-authoritative sync + rollback UI
- [ ] **T-046** 🚩 Two players on one crate: no duplication or loss

## Phase 5 · Survival + skills

- [ ] **T-050** `Vitals` — 5 gauges (SYS-SURV-01)
- [ ] **T-051** Temperature + wetness + campfire
- [ ] **T-052** Six water sources
- [ ] **T-060** `XpCurve` + `SkillSet`
- [ ] **T-061** ★ `FocusCalculator` + 9 verification cases (SYS-SKILL-01)
- [ ] **T-062** `ActivityTracker` 7-day median
- [ ] **T-063** Skill UI — split production/combat tabs, **show focus as a bonus**
- [ ] **T-064** `RustSystem` (flag, default off)

## Phase 6 · Production loop

- [ ] **T-070** Gathering interaction + resource respawn + tool durability
- [ ] **T-071** Creature spawning + individual weight rolls
- [ ] **T-072** ★ `ButcheryCalculator` + 4 verification cases (SYS-HUNT-01)
- [ ] **T-073** Cut splitting + damage-sensitive outputs
- [ ] **T-074** ★ Carcass drag / two-player carry
- [ ] **T-075** Carcass spoilage + predator scent
- [ ] **T-080** `FishSelector` weighting
- [ ] **T-081** Tension minigame
- [ ] **T-082** Trap and net deferred simulation

## Phase 7 · Crafting + cooking ★ developer priority

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

## Phase 8 · Combat ★ developer priority

- [ ] **T-110** `PowerCalculator` + `DamageResolver` + 7 verification cases
- [ ] **T-111** Melee — 3-hit combo, block, parry
- [ ] **T-112** Ranged — charge, `SwayCalculator`, projectiles
- [ ] **T-113** Hit detection + lag compensation (melee only) + i-frames
- [ ] **T-114** Dodge roll
- [ ] **T-115** `CreatureAI` 4-state machine + 3 creatures

## Phase 9 · Character presentation — deferred from Phase 0

**Boxes carry the game until here.** Resume when mechanics are proven and artifacts need real
animation. SYS-CHAR-01 must be rewritten for **quarter view** before T-003 starts — it is
currently written for a pure side view (see PROJECT_STATE).

- [ ] **T-003** `AimController` — mouse → aimAngle, head/torso angle limits
- [ ] **T-004** ★ Flip + transition animation (SYS-CHAR-01 §Angles). **Hardest task**
- [ ] **T-005** Weapon grip presets (one-hand / two-hand / tool)
- [ ] **T-006** 🚩 **Gate G1** — SYS-CHAR-01 verification cases 1–4 + 5 outside evaluators

> **G1 fails →** try plan B (8-direction sprites, forearm IK only) for two weeks. If that fails too, ADR-001 §10 review.
>
> ⚠️ Deferring this trades risk for velocity: a G1 failure now lands late, with mechanics already
> built on top. Accepted deliberately — the box placeholder keeps rendering isolated from
> gameplay, so the character layer can be swapped without touching systems.

## Phase 10 · Artifacts 🚩 biggest gate

- [ ] **T-120** `ArtifactPowerCalculator` + `ZoneBonusEvaluator` + 6 verification cases
- [ ] **T-121** ★ Great Cauldron Ladle, all 3 abilities (SYS-ART-01)
- [ ] **T-122** Ladle animation set (triple the normal weapon budget)
- [ ] **T-123** Ladle VFX + audio
- [ ] **T-124** 🚩 **Gate G3** — SYS-ART-01 validation questions. **Anything but yes → stop and redesign**
- [ ] **T-125** Abyss-Caller's Rod (only after G3)
- [ ] **T-126** Unbroken Anvil Hammer (only after G3)

## Phase 11 · Modding + polish

- [ ] **T-130** `ModLoader` + `LoadOrderResolver` + `PatchApplier`
- [ ] **T-131** Two example mods (new fish / new cook method)
- [ ] **T-132** Modding documentation draft
- [ ] **T-140** Minimal tutorial
- [ ] **T-141** Options menu + key rebinding
- [ ] **T-142** Profiling + optimization
- [ ] **T-143** 🚩 **Gate G4** — final assessment

## Gates

Listed in **execution order**, which changed on 2026-09-04: G1 used to come first and now runs
second to last, because character presentation moved to Phase 9.

| Gate | Task | Phase | Question |
|---|---|---|---|
| **Def gate** | T-016 | 1 | Does editing `items/*.json` reflect without recompiling? |
| **Net gate** | T-024 | 2 | Does remote aim read correctly at 200 ms latency? |
| **Inv gate** | T-046 | 4 | Two players on one crate — no duplication or loss? |
| **Ext gate** | T-105 | 7 | One new ingredient across all 8 cook methods, zero C# edits? |
| **G2** | after T-115 | 8 | Do two players naturally split roles over 90 minutes? |
| **G1** | T-006 | 9 | Does the mouse-tracking rig read naturally? |
| **G3** | T-124 | 10 | **Does a production specialist feel useless in combat?** |
| **G4** | T-143 | 11 | Can an outsider's mod actually load? |

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
