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

- [x] **T-010** `NamespacedId`, `TagRegistry` flattening (SYS-CORE-01)
- [x] **T-011** `Data` layer POCOs — ItemDef, CreatureDef, FishDef, CookMethodDef, etc.
- [x] **T-012** `DefinitionLoader` + `SchemaValidator`
- [x] **T-013** ★ `ReferenceResolver` + typo-suggesting error messages
- [x] **T-014** `DefRegistry` + tag index
- [x] **T-015** F5 hot reload
- [x] **T-016** 🚩 **Def gate:** editing `items/*.json` reflects without recompiling. **Do not proceed until this works**
- [x] **T-017** ★ Placeholder visual generator — vector shapes (box / circle / solid colour, ART_PIPELINE §Placeholders). A definition with no art falls back automatically, so **no later phase ever waits on a sprite**
- [x] **T-018** ★ SYS-SKILL-01 rewrite + `SkillDef` — skills become data-driven and **modder-addable**, so the focus formula holds for any skill count. **Blocks T-015, T-060, T-061**
- [x] **T-019** SYS-BUFF-01 spec sheet + `BuffDef` — the beta buff set plus an effect vocabulary modders can extend. **Blocks T-015, T-103**

> **T-018 and T-019 exist because T-011 found the schema had no shape for skills or buffs.**
> Both were listed as Data types in ARCHITECTURE with nothing describing their fields.
> The developer's 2026-09-07 decision made both moddable, which turns them from
> transcription into design work — see `PROJECT_STATE.md` §Decided without a spec.
>
> **Do them before T-015.** Starter definitions reference skill and buff IDs; writing those
> files first means rewriting them.
>
> **T-018 is done (2026-09-09)** — `FocusCalculator.Focus` takes any-length skill collections, not
> a fixed array, and all 9 verification cases are re-simulated against the finalized 8-skill,
> 5-production/3-combat table in `SYS-SKILL-01` §Verification. `RustSystem` stayed out of scope
> (disabled by default, no caller, open interpolation curve) — see `PROJECT_STATE.md` §Decided
> without a spec.
>
> **T-019 is done (2026-09-10)** — `BuffDef`/`BuffEffect`/`BuffSet` exist; the seven beta buffs'
> numbers are all fixed in `SYS-BUFF-01`. `BuffSet` only tracks activity and expiry — applying an
> effect to a real gauge or formula waits for `SYS-SURV-01`/`SYS-COMBAT-01` to exist (Phase 5/8).
> Both T-018 and T-019 are done, so **T-015 is unblocked** on the "starter defs need skill/buff
> IDs" front.
>
> **T-015 is done (2026-09-10)** — `DefinitionBootstrap.Load`/`Reload` wire all 11 types through
> `DefinitionLoader` → `ReferenceResolver` → `DefRegistry`; `DefRegistry.Reload<T>` updates existing
> ids **in place** via reflection (init-only setters are a compiler-only restriction, not a
> reflection one) so anything holding a def by reference sees the change; a new `Isle.Modding.Editor`
> asmdef adds the `F5` menu item. No orchestration code existed anywhere before this — T-130's
> multi-mod scan/order/patch steps are still not built, deliberately (out of scope; `Load`/`Reload`
> take a single content root today, exactly what T-130 will call per mod later). **T-016 stays
> unchecked**: 3 new EditMode tests prove the edit→reload→same-reference mechanism works, but the
> gate is a manual "confirm it in a live Editor session" check the developer should do once — press
> `F5` after hand-editing a file under `Assets/StreamingAssets/definitions/items/`.
>
> **T-016 confirmed (2026-09-10)** — developer edited a test item's `name` field and pressed `F5`
> in a live Editor session; console logged `[Isle] Definitions reloaded from .../definitions.`
> with no errors and no recompile step. Def gate passes; **Phase 1 is fully done (10/10)**.

## Phase 2 · Netcode skeleton — solo runs as a one-player host

Not "multiplayer work". This is the authority architecture that Absolute Rule 2 requires anyway,
stood up now so the solo beta is already a listen-server session with one client attached.

- [x] **T-020** FishNet bootstrap, listen server connection
- [x] **T-021** Server-authoritative movement + client prediction

> **T-020 confirmed (2026-09-10)** — FishNet 4.7.2 (Tugboat transport) installed;
> `IsleNetworkManager` starts a listen server (host server + local client) and is wired into
> `SampleScene.unity`. Fixed a latent T-012 bug found along the way: FishNet's Synapse transport's
> own `Microsoft.Bcl.AsyncInterfaces.dll` collided (`CS0433`) with T-012's vendored copy because
> those `.meta` files never had Plugin Importer auto-reference disabled — see `PROJECT_STATE.md`
> §Decided without a spec. Batchmode compile clean, EditMode **155/155**. No EditMode test for
> `IsleNetworkManager` itself (`docs/TESTING.md`'s MonoBehaviour/network-state policy) — developer
> confirmed in a live Editor session instead: Console showed `Local server is started for
> Tugboat.`, `Remote connection started for Id 0.`, `Local client is started for Tugboat.`, no
> errors.

> **T-021 confirmed (2026-09-15)** — `PlayerMovement` (FishNet Prediction v2,
> `TickNetworkBehaviour` + `[Replicate]`/`[Reconcile]`) added to the existing `player_rig_placeholder`
> prefab alongside a new `NetworkObject`; FishNet's built-in `PlayerSpawner` spawns it per connecting
> client. Flat `BaseSpeed = 4.2` (SYS-CHAR-01 §Movement) only — no weight/terrain/stance multiplier,
> no collider; see `PROJECT_STATE.md` §Decided without a spec for why. Batchmode compile clean,
> EditMode **155/155**, no new tests added. Developer confirmed in a live Editor session: WASD
> moves the rig smoothly, no console errors. **Phase 2 is fully done (2/2).**

- [ ] **T-028** SYS-DIFF-01 spec sheet + world difficulty setting — host picks it when creating a
      room: Peaceful / Easy / Normal / Hard. Normal = 100%, Easy = 50%, Hard = 200% on hunger/thirst
      drain rate and enemy attack power; enemy HP is unaffected at every tier; bosses may get faster
      patterns on top of that at higher tiers (developer decision, 2026-09-10). Peaceful matches
      Minecraft's peaceful mode. No spec sheet yet — write it before coding (workflow: spec before
      code). Touches `SYS-SURV-01` (T-050) and `SYS-COMBAT-01` (T-110), neither built yet, so this
      isn't blocking anything today.

## Phase 3 · World

> **T-030 merged (2026-09-15, [PR #18](https://github.com/yhw1737/surv/pull/18))** — `WorldClock`
> (`Assets/Scripts/World/Time/WorldClock.cs`) is plain C# (no `MonoBehaviour`, no FishNet), matching
> the `Isle.Gameplay.Skills` pure-formula pattern. `MinutesPerDay = 1440`, `MinutesPerRealSecond =
> 1.2`, `DayPhase` (Dawn/Day/Dusk/Night) per SYS-WORLD-01 §Time's boundary table, `double` accumulator
> to avoid rounding drift. No FishNet sync yet — see `PROJECT_STATE.md` §Decided without a spec.
> Batchmode compile clean, EditMode **169/169** (155 pre-existing + 14 new). Branched off
> `feature/T-021-server-movement` for doc continuity while PR #16/#17 were open; rebuilt from `main`
> once both merged.

> **T-031 skipped for now (2026-09-15, developer decision)** — no spec sheet covers tilemaps,
> Y-sort, collision, or camera zoom, and it's inherently visual (Editor-only verification). Needs
> either the missing numbers/behavior from the developer or a spec-writing session first.

> **T-032 merged (2026-09-15, [PR #18](https://github.com/yhw1737/surv/pull/18))** — `Chunk`
> (`Assets/Scripts/World/Chunks/Chunk.cs`) matches the spec's struct exactly (32×32 `Tile[]`,
> `List<WorldObject>`, `LastSimulatedTime`); `Tile`/`WorldObject` are minimal placeholders pending
> T-035/landmark work (see `PROJECT_STATE.md` §Decided without a spec). `ChunkManager` loads the
> union of every player's 3×3 neighborhood and unloads+saves anything that falls out, via
> constructor-injected load/save delegates (no SQLite dependency yet — `ChunkSerializer` is T-033).
> New `Isle.Core.Vec2Int` (integer coordinate, no `UnityEngine` dependency). Batchmode compile
> clean, EditMode **182/182** (169 pre-existing + 13 new). Same branch as T-030.

> **T-033 merged (2026-09-15, [PR #18](https://github.com/yhw1737/surv/pull/18))** — `ChunkSerializer`
> (`Assets/Scripts/World/Chunks/ChunkSerializer.cs`) via `com.gilzoide.sqlite-net`, one row per
> chunk keyed by coordinate, storing JSON **text** rather than the spec's literal BLOB (developer
> wants save files inspectable for mod debugging — see `PROJECT_STATE.md` §Decided without a
> spec). Batchmode compile clean, EditMode **184/184** (182 pre-existing + 2 new). Same branch as
> T-030/T-032.

> **T-034 explicitly deferred (2026-09-15, developer decision)** — builds later, bundled with the
> Phase 4/6 systems it depends on (inventory, crops, cooking). Not blocked, just not now; no
> placeholder version wanted.

> **T-035/T-036 merged (2026-09-15, [PR #18](https://github.com/yhw1737/surv/pull/18))** — `IslandGenerator`
> (`Assets/Scripts/World/Generation/IslandGenerator.cs`) paints biome per-tile as a pure function
> of (seed, position): elliptical falloff for coast vs. interior, hashed-grid patches for the
> forest/marsh mix, per the developer's shape answer. `LandmarkPlacer` places the fixed counts
> (1 shipwreck, 2 ruins, 3 springs, def ids from `definitions/world/landmarks.json`) via greedy
> farthest-point placement, maximizing separation rather than enforcing the literal 256-tile
> (2-minute) target — that target isn't reachable for all 6 pairs on a 384-tile island (achieved
> ~129 tiles for seed 42; see `PROJECT_STATE.md` §Decided without a spec for the full finding).
> Batchmode compile clean, EditMode **194/194** (184 pre-existing + 10 new). Same branch as
> T-030/T-032/T-033.

- [x] **T-030** `WorldClock` in-game time
- [ ] **T-031** Tilemap, Y-sort, collision, 3-step camera zoom — skipped, no spec + visual-only
- [x] **T-032** `Chunk` + `ChunkManager` load/unload
- [x] **T-033** SQLite chunk persistence
- [ ] **T-034** `DeferredSimulation` (SYS-WORLD-01) — deferred to Phase 4/6 (not blocked)
- [x] **T-035** `IslandGenerator` seeded procedural (coast + forest)
- [x] **T-036** Hand-placed landmarks (shipwreck, ruins, springs)

## Phase 4 · Inventory

> **T-040, T-041 implemented and verified (2026-09-15), not yet committed.** `GridInventory`
> (`Assets/Scripts/Gameplay/Inventory/GridInventory.cs`) — pure structure, no weight/UI/network
> (T-041/T-042/T-045). `TryPlace` checks in-bounds + AABB overlap against existing placements
> (spec: no L-shaped items, so AABB is sufficient); rotation swaps the existing `GridSize`'s W/H
> rather than adding a new type. No literal placement verification table in the spec (its table
> covers §Weight) — test cases derived from the placement rules directly, see `PROJECT_STATE.md`
> §Decided without a spec. `WeightCalculator`
> (`Assets/Scripts/Gameplay/Inventory/WeightCalculator.cs`) — the spec's formula and 5-row
> verification table copied verbatim, no invented values. Batchmode compile clean, EditMode
> **211/211** (194 pre-existing at the start of T-040 + 7 + 10 new). Branch:
> `feature/T-040-grid-inventory`.
>
> **T-042/T-043 implemented (2026-09-15)**, same branch. Pushed everything pure into
> `GridInventory.cs` (`Placement.Count`, `FindFreePosition`, `TryMoveTo`, `MoveAllTo`, `AutoSort`,
> `TrySplit`, `TotalWeightKg`, all EditMode-tested) and confined the untestable surface to three new
> `Scripts/UI/Inventory/*.cs` MonoBehaviours (`GridView`, `DragHandler`, `ItemTooltip`). Batchmode
> compile clean, EditMode **224/224** (211 pre-existing + 13 new). Unlike T-031 (no spec at all),
> `SYS-INV-01` fully specifies this feature — only its UI half can't be verified by an EditMode test,
> so it's built in full and handed to the developer as a manual-test checklist instead of being
> skipped. Branch: `feature/T-040-grid-inventory`.
>
> **T-044 implemented (2026-09-15)**, same branch. `ItemDef` gained `EquipSlot`/`BagGrid` (schema
> fields the spec's own §Containers table names but nothing had wired up yet); `EquipSlots.cs`
> (pure, EditMode-tested) holds the eight named slots and opens a `GridInventory` when a bag-type
> item is equipped. UI: `EquipSlotView`/`EquipDragHandler` (new) — drag an item onto a slot to equip,
> drag the equipped icon back out to unequip; `DragHandler` now also recognizes `EquipSlotView` as a
> drop target. Batchmode compile clean, EditMode **232/232** (224 pre-existing + 8 new). Branch:
> `feature/T-040-grid-inventory`.
>
> **Manual-test harness added (2026-09-16).** T-042/T-043/T-044's UI was built but unreachable —
> no scene ever had a Canvas/EventSystem/wired `GridView`, so there was nothing to actually press
> Play on. Added `InventoryDemo.cs`, a runtime bootstrap (add the component to any GameObject,
> press Play) that builds the whole test setup itself: Canvas, EventSystem, a paired player-bag +
> warehouse `GridView`, all 8 `EquipSlotView`s, and a few sample items. See `PROJECT_STATE.md` for
> the full checklist and the reasoning for fabricating sample items in C#.
>
> **T-045 implemented (2026-09-16)**, `feature/T-045-inventory-network`. New `InventoryNetwork`
> (`Scripts/Gameplay/Inventory/InventoryNetwork.cs`, a `NetworkBehaviour`) holds the server's
> authoritative copy of one player's bag + equip slots; client requests a move/split/equip/unequip
> by grid position, a `[ServerRpc]` validates and applies against the server's own copy, a
> `[TargetRpc]` reports the result back to the owner. Scoped to the player's own bag/slots only —
> warehouse/crates stay local-only (Phase 6, `WorldObject` has no network identity yet). Wired onto
> `player_rig_placeholder.prefab` alongside `PlayerMovement`. `GridView`/`EquipSlotView` gained a
> `Network` property; `DragHandler`/`EquipDragHandler` branch on it (byte-for-byte the old local
> behaviour when unset). See `PROJECT_STATE.md` §Decided without a spec for the scope/identification/
> single-in-flight judgment calls. Batchmode compile clean, EditMode **238/238** — first actual
> batchmode run in a while (earlier "232/232" figures were hand-counted, not run, per the
> now-resolved gap below); 3 new `PlacementAt` cases added this session, the rest of the delta is
> `[TestCase]`-expanded counts a static grep undercounts. Needs a live
> client/server session to verify the translucent-pending render and rollback — see the manual-test
> checklist in `PROJECT_STATE.md`.

- [x] **T-040** `GridInventory` structure + EditMode tests
- [x] **T-041** `WeightCalculator` + 5 verification cases
- [x] **T-042** Grid UI + dragging
- [x] **T-043** ★ Rotate (R), Ctrl+click bulk move, Shift+drag split. **Do not defer**
- [x] **T-044** Equipment slots + bag expansion
- [x] **T-045** Server-authoritative sync + rollback UI

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
- [ ] **T-093** Crafter mark + repair (slot *count* only — application moves to **T-106**)
- [ ] **T-100** ★ `TagReactionEngine` (SYS-COOK-01)
- [ ] **T-101** ★ `CookingResolver` full algorithm
- [ ] **T-102** 8 cook method definitions + stations
- [ ] **T-103** Buffs + care tag + satiety fatigue
- [ ] **T-104** `DishNamer` procedural naming + player registration
- [ ] **T-105** 🚩 **Ext gate** — one new ingredient works across all 8 methods with zero C# edits
- [ ] **T-106** `EnchantSystem` on the **Enchanting** skill — slot application moves off Crafting (SYS-CRAFT-01 §Enchanting). Formula already exists, this is a skill-owner move, not new math
- [ ] **T-107** ★ SYS-BREW-01 spec + `BrewDef` — brewing has **no existing spec or formula** anywhere; write the sheet before coding (workflow: spec exists before code)

## Phase 8 · Combat ★

- [ ] **T-110** `PowerCalculator` + `DamageResolver` + 7 verification cases
- [ ] **T-111** Melee — 3-hit combo, block, parry
- [ ] **T-112** Ranged — charge, `SwayCalculator`, projectiles
- [ ] **T-113** Hit detection + lag compensation (melee only) + i-frames
- [ ] **T-114** Dodge roll
- [ ] **T-115** `CreatureAI` 4-state machine + 3 creatures
- [ ] **T-117** Magic combat — `isle:magic` weapon/focus category. `SYS-COMBAT-01`'s `PowerCalculator` is already skill-agnostic (`combat_skill` is just a `NamespacedId`), so this is mostly a new weapon type; a mana/resource cost, if any, is an **open question — ask, don't invent**

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
- **Modding Tier 2 (Lua) / Tier 3 (C#)** — needed for interaction-level mods like RimWorld's
  `dragselect` (drag across a row of quantity arrows to auto-click every one) or `AllowTool`
  (bulk-select/priority tools). These change *behavior*, not content, so Tier 1's JSON-only
  pipeline (`DefinitionLoader`/`ReferenceResolver`/`DefRegistry`) can't express them no matter
  how the schema grows. RimWorld's equivalent is Harmony (runtime monkey-patching of arbitrary
  methods) plus a plain DLL loader — both viable here since Mono is pinned specifically to keep
  this door open (`ADR-001`). Two pieces neither exists yet: a mod-assembly loader (scan a mods
  folder, `Assembly.LoadFrom`, run a discovered entry point) and a patch mechanism (Harmony, or
  hand-authored hooks in `Modding/Hooks/` per `ARCHITECTURE.md`'s reserved folder — Harmony covers
  far more surface for far less code). A trust-model call is needed too: RimWorld just trusts mod
  code outright, which is fine for UI-only mods like dragselect (they only relay clicks a player
  could already make one at a time) but needs more thought for anything that could bypass server
  authority (Absolute Rule 2). Write `docs/specs/SYS-MOD-0x` before building.
- Sailing + new biomes
- Castaway NPCs
- Gamepad support

### Unsorted
- (add new ideas here)
