# Backlog

**One AI session = one task.** Each is roughly 2–5 hours.

`[ ]` pending · `[~]` in progress · `[x]` done · `[!]` blocked

> ### Priority — developer decision, 2026-09-06
> Solo indie, **targeting a beta release.** The build order is four stages and does not change:
>
> ```
> 1  placeholders    every visual is a vector shape — box, circle, solid colour
> 2  solo beta       the whole loop playable by one player, bug-free      Phases 1–9
> 3  multiplayer     Steam P2P, friend invites, co-op verification        Phase 10
> 4  graphics        real rig, icons, creatures, tiles, UI                Phase 11
> ```
>
> **Systems first, art last.** Characters, weapons and props stay placeholder shapes until
> Phase 11. Every phase before it must keep art swappable — see `ARCHITECTURE.md`
> §Presentation boundary, which is the binding rule, not this note.
>
> **Networking is split, deliberately.** T-020/T-021 stay in Phase 2 because FishNet runs a
> listen server, so solo play *is* a one-player host session — the solo beta already runs on the
> multiplayer code path and nothing gets retrofitted. Only the multiplayer-*specific* work (sync,
> two-client testing, latency, Steam) waits for Phase 10. Rationale: `PROJECT_STATE.md`.

## Phase 0 · Project setup — done

- [x] **T-000** Unity project. Mono backend pinned, URP 2D, asmdef skeleton, Git LFS
- [x] **T-001** Placeholder character rig — now serves as the box stand-in (SYS-CHAR-01 §Rig)
- [x] **T-002** IK Manager 2D — Limb solver on front arm, look-at on head

> T-001/T-002 are done and stay in the tree. They are not thrown away — they become the
> placeholder, and Phase 11 resumes from them.

## Phase 1 · Foundation — definitions. Keep this order

**Everything downstream depends on this.** Weapons and cooking are pure JSON (Absolute Rule 1),
so neither can start before the loader exists.

- [ ] **T-010** `NamespacedId`, `TagRegistry` flattening (SYS-CORE-01)
- [ ] **T-011** `Data` layer POCOs — ItemDef, CreatureDef, FishDef, CookMethodDef, etc.
- [ ] **T-012** `DefinitionLoader` + `SchemaValidator`
- [ ] **T-013** ★ `ReferenceResolver` + typo-suggesting error messages
- [ ] **T-014** `DefRegistry` + tag index
- [ ] **T-015** F5 hot reload
- [ ] **T-016** 🚩 **Def gate:** editing `items/*.json` reflects without recompiling. **Do not proceed until this works**
- [ ] **T-017** ★ Placeholder visual generator — vector shapes (box / circle / solid colour, ART_PIPELINE §Placeholders). A definition with no art falls back automatically, so **no later phase ever waits on a sprite**

## Phase 2 · Netcode skeleton — solo runs as a one-player host

Not "multiplayer work". This is the authority architecture that Absolute Rule 2 requires anyway,
stood up now so the solo beta is already a listen-server session with one client attached.

- [ ] **T-020** FishNet bootstrap, listen server connection
- [ ] **T-021** Server-authoritative movement + client prediction

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

> The two-player crate test (**T-046**) moved to Phase 10 — it needs two clients. T-045 still
> lands here, because inventory authority is architecture, not a multiplayer feature.

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
- [ ] **T-074** ★ Carcass drag (solo path; the two-player carry lands with T-046)
- [ ] **T-075** Carcass spoilage + predator scent
- [ ] **T-080** `FishSelector` weighting
- [ ] **T-081** Tension minigame
- [ ] **T-082** Trap and net deferred simulation

## Phase 7 · Crafting + cooking ★

- [ ] **T-090** `QualityCalculator` + 5 verification cases
- [ ] **T-091** Forging minigame
- [ ] **T-092** `AdjacentAssist`
- [ ] **T-093** Slot enchanting + crafter mark + repair
- [ ] **T-100** ★ `TagReactionEngine` (SYS-COOK-01)
- [ ] **T-101** ★ `CookingResolver` full algorithm
- [ ] **T-102** 8 cook method definitions + stations
- [ ] **T-103** Buffs + care tag + satiety fatigue
- [ ] **T-104** `DishNamer` procedural naming + player registration
- [ ] **T-105** 🚩 **Ext gate** — one new ingredient works across all 8 methods with zero C# edits

## Phase 8 · Combat ★

- [ ] **T-110** `PowerCalculator` + `DamageResolver` + 7 verification cases
- [ ] **T-111** Melee — 3-hit combo, block, parry
- [ ] **T-112** Ranged — charge, `SwayCalculator`, projectiles
- [ ] **T-113** Hit detection + lag compensation (melee only) + i-frames
- [ ] **T-114** Dodge roll
- [ ] **T-115** `CreatureAI` 4-state machine + 3 creatures

## Phase 9 · 🚩 Solo beta — the first release milestone

**Stage 2 of the priority banner ends here.** One player, placeholder shapes, whole loop.

- [ ] **T-150** Save / load round trip — quit mid-game, resume with world, inventory and skills intact
- [ ] **T-151** Solo bug pass — play the full loop repeatedly, fix what blocks it
- [ ] **T-152** 🚩 **Solo beta gate** — gather → hunt → fish → cook → craft → fight → sleep, playable end to end by one player with no blocking bug. **Do not start Phase 10 until this passes**

> Everything up to here is playable and shippable to a closed solo beta. That is the point of
> ordering it this way: a broken multiplayer build hides which layer is broken.

## Phase 10 · Multiplayer — P2P + Steam

Stage 3. The authority model already exists (Phase 2); this is the co-op surface on top of it.

- [ ] **T-022** aimAngle 20 Hz sync, facingSign as event (SYS-CHAR-01 §Network)
- [ ] **T-023** ★ Two-client test script + latency simulator (SYS-NET-01)
- [ ] **T-024** 🚩 **Net gate** — remote aim reads correctly at 200 ms latency
- [ ] **T-025** ★ Steamworks.NET + FishNet Steam transport — P2P lobby, friend invites, Tugboat fallback (ADR-001 §Stack)
- [ ] **T-026** Ping / marker system (GDD §Multiplayer — mandatory, co-op must work without voice)
- [ ] **T-027** Death drops + ally body recovery penalty reduction
- [ ] **T-046** 🚩 **Inv gate** — two players on one crate: no duplication or loss
- [ ] **T-047** Two-player carcass carry (−20% speed, SYS-HUNT-01)
- [ ] **T-116** 🚩 **G2** — do two players naturally split roles over 90 minutes?

> ⚠️ GDD risk 3 said "build two-client automation early". It now lands here instead. **The
> mitigation that replaces it is Phase 2**: solo play already runs through the server-authoritative
> path, so T-023 adds a second client rather than introducing the concept of one.

## Phase 11 · Graphics — real art replaces the shapes

Stage 4. Nothing here is a prerequisite for anything above it; that is the whole design.

- [ ] **T-160** Fix the 48-colour palette in `Art/palette.png` and remap everything through it (ART_PIPELINE §Style)
- [ ] **T-003** `AimController` — mouse → aimAngle, head/torso angle limits
- [ ] **T-004** ★ Flip + transition animation (SYS-CHAR-01 §Angles). **Hardest task**
- [ ] **T-005** Weapon grip presets (one-hand / two-hand / tool)
- [ ] **T-006** 🚩 **G1** — SYS-CHAR-01 verification cases 1–4 + 5 outside evaluators
- [ ] **T-161** Item icons — buy and retouch, 150–200, filenames matching definition IDs
- [ ] **T-162** Creature sprites and clips — 5 × 6, buy or commission
- [ ] **T-163** Tilesets (3 biomes) + world objects
- [ ] **T-164** UI art pass

> **SYS-CHAR-01 must be rewritten for quarter view before T-003 starts** — it is currently written
> for a pure side view. See PROJECT_STATE.
>
> **G1 fails →** try plan B (8-direction sprites, forearm IK only) for two weeks. If that fails too, ADR-001 §10 review.
>
> ⚠️ Deferring the rig this far trades risk for velocity: a G1 failure lands with the whole game
> built on top. Accepted deliberately, and it is only safe because gameplay reads movement/aim
> **data** and never rig internals — `ARCHITECTURE.md` §Presentation boundary. Hold that line in
> every phase.

## Phase 12 · Artifacts 🚩 biggest gate

Placed after graphics because artifacts are the one thing that genuinely needs real animation —
ART_PIPELINE gives their VFX triple the normal budget.

- [ ] **T-120** `ArtifactPowerCalculator` + `ZoneBonusEvaluator` + 6 verification cases
- [ ] **T-121** ★ Great Cauldron Ladle, all 3 abilities (SYS-ART-01)
- [ ] **T-122** Ladle animation set (triple the normal weapon budget)
- [ ] **T-123** Ladle VFX + audio
- [ ] **T-124** 🚩 **G3** — SYS-ART-01 validation questions. **Anything but yes → stop and redesign**
- [ ] **T-125** Abyss-Caller's Rod (only after G3)
- [ ] **T-126** Unbroken Anvil Hammer (only after G3)

## Phase 13 · Modding + release polish

- [ ] **T-130** `ModLoader` + `LoadOrderResolver` + `PatchApplier`
- [ ] **T-131** Two example mods (new fish / new cook method)
- [ ] **T-132** Modding documentation draft
- [ ] **T-140** Minimal tutorial
- [ ] **T-141** Options menu + key rebinding
- [ ] **T-142** Profiling + optimization
- [ ] **T-143** 🚩 **G4** — final assessment

## Gates

Listed in **execution order**, which changed on 2026-09-06 when the solo beta became stage 2.

| Gate | Task | Phase | Question |
|---|---|---|---|
| **Def gate** | T-016 | 1 | Does editing `items/*.json` reflect without recompiling? |
| **Ext gate** | T-105 | 7 | One new ingredient across all 8 cook methods, zero C# edits? |
| **Solo beta** | T-152 | 9 | Can one player play the whole loop with no blocking bug? |
| **Net gate** | T-024 | 10 | Does remote aim read correctly at 200 ms latency? |
| **Inv gate** | T-046 | 10 | Two players on one crate — no duplication or loss? |
| **G2** | T-116 | 10 | Do two players naturally split roles over 90 minutes? |
| **G1** | T-006 | 11 | Does the mouse-tracking rig read naturally? |
| **G3** | T-124 | 12 | **Does a production specialist feel useless in combat?** |
| **G4** | T-143 | 13 | Can an outsider's mod actually load? |

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
