# CLAUDE.md

Read this first, every session.

## Project

**ISLE** — top-down 2D co-op survival strategy. Unity 6 / C# / Steam.
**Solo developer + AI.** One person; you write most of the code.
**Nearest milestone is a beta release**, then Early Access.

Concept: on an island where nobody can master everything, each player becomes a specialist. Cooks, anglers, and smiths are not weak in combat.

Build order — four stages, `docs/BACKLOG.md` is the schedule:

```
1  placeholders   every visual is a vector shape: box, circle, solid colour
2  solo beta      the whole loop playable by one player, bug-free
3  multiplayer    Steam P2P, friend invites, co-op verification
4  graphics       real rig, icons, creatures, tiles, UI
```

## Language

**Talk to the developer in Korean. Write everything else in English.**

| Context | Language |
|---|---|
| **Conversation, explanations, questions, reports** | **한국어** |
| Code, identifiers, comments | English |
| Commit messages, PR titles and bodies | English |
| Documentation (`docs/`, `CLAUDE.md`) | English |
| Definition files (`definitions/*.json`) — keys and IDs | English |
| Mod loader error messages | English (modders are international) |
| **In-game player-facing text** | **`lang/ko.json` and `lang/en.json`** — never hardcode either language |

Why: docs and specs are re-read every session, and English is roughly 2–3× more token-efficient than Korean. Conversation is read once, so use whichever is clearest for the developer.

So: think and write artifacts in English, but **respond in Korean.** Session reports, questions, and status summaries all go to the developer in Korean.

Player-facing strings always go through `@key` references into the language files (see `docs/modding/SCHEMA.md`), never inline in C# or definition values.

## Stack (do not change)

| | |
|---|---|
| Engine | Unity 6 LTS — pinned, no ad-hoc upgrades |
| Scripting backend | **Mono** — IL2CPP kills modding. Never change |
| Render | URP + 2D Renderer |
| Net | FishNet (free) |
| Rig | com.unity.2d.animation + IK Manager 2D |
| Data | JSON (StreamingAssets) + SQLite (world state) |
| Serialization | System.Text.Json |

Rationale: `docs/adr/ADR-001-engine.md`

## Absolute rules

**1. No hardcoded content.** Items, recipes, creatures, cook methods, weapons, enchants all live in `Assets/StreamingAssets/definitions/**/*.json`. Never put IDs or numbers in C#.
```csharp
if (item.Id == "isle:raw_meat") hunger += 12;   // ❌
hunger += item.Def.Nutrition.Hunger;            // ✅
```
Workshop modding is the core strategy. One "just this once" breaks it.

**2. Server authority.** All state changes (inventory, health, crafting, skill XP, world) happen server-side only. Clients predict movement only. See `SYS-NET-01`.

**3. Follow the spec sheets.** Formulas and numbers are in `docs/specs/`. Use those values verbatim. **If a value is missing, ask — do not invent one.** Invented numbers differ next session and balancing becomes impossible.

**4. Connect via tags.** Systems link through tags, not hardcoded lists. Adding `["meat","fat"]` to a new ingredient must make all cook methods handle it.

**5. Artifacts never touch combat skills.** `combat_skill: null`, `grants_combat_xp: false`. They scale off production skills. Core design — do not "fix" this for convenience.

**6. No scope additions.** Anything outside ISLE Core (`docs/design/GDD.md`) is not built. Good ideas go to `docs/BACKLOG.md` only.

**7. Placeholder shapes now, art last.** Never stall on a missing sprite — draw a box and move on. In exchange, gameplay depends on movement/aim **data** (`position`, `aimAngle`, `facingSign`) and def numbers, never on rig internals, sprites or art paths. That boundary is what lets stage 4 be a swap instead of a rewrite. Rules: `docs/ARCHITECTURE.md` §Presentation boundary.
```csharp
var tip = transform.Find("Torso/Arm_Front_Upper/…/WeaponSocket").position;  // ❌
var tip = _rig.WeaponMuzzle;                                                // ✅
```

## Layout

```
Assets/Scripts/
  Core/       def loading, tags, IDs, event bus, utils
  Data/       POCO defs — NO Unity types
  Modding/    mod loader, schema validation, patches
  Networking/ FishNet wrappers, authority
  World/      chunks, time, spoilage
  Gameplay/   skills, inventory, crafting, cooking, gathering, hunting, fishing
  Combat/     weapons, artifacts, damage, AI
  UI/
Assets/StreamingAssets/definitions/   all content JSON
Tests/EditMode/    pure logic (formula verification)
Tests/PlayMode/    integration, network
```

Dependency direction: `UI → Gameplay/Combat → World → Networking → Data → Core`. No reverse refs. Enforce with asmdef per folder.

`Data` must not reference Unity (`Vector2`, `MonoBehaviour`). Use `Vec2Int` in `Core`.

## Naming

| | | |
|---|---|---|
| Class/method | PascalCase | `GridInventory`, `CalculateYield()` |
| private field | `_camelCase` | `_currentWeight` |
| Definition ID | `namespace:snake_case` | `isle:raw_meat` |
| JSON field | `snake_case` | `edible_ratio` |
| Spec doc | `SYS-{AREA}-{NN}` | `SYS-HUNT-01` |
| Test | `{Target}_{Condition}_{Expected}` | `Yield_Lv5BareHands_Returns10kg` |

Terms come from `docs/GLOSSARY.md`. Never use a synonym for an existing concept.

## Workflow

**Session start**
1. `git fetch --all --prune` and assess state (Git rules §1)
2. Read `docs/PROJECT_STATE.md`. **If it disagrees with git, trust git**
3. Pick task from `docs/BACKLOG.md`
4. Read the relevant spec sheet
5. Create a feature branch (Git rules §2)
6. If no spec exists for the system, offer to write the spec first instead of coding

**During**
- One task per session. Don't touch multiple systems at once
- Write the spec's verification cases as EditMode tests **before** implementing
- Missing value → ask, don't invent

**Session end — you do this, not the developer**
Follow the `wrap` skill. Never ask the developer to update state docs; write them yourself.
1. `git status` for actual changes (not memory)
2. Update `docs/PROJECT_STATE.md` fully
3. Tick `docs/BACKLOG.md` checkboxes
4. **Record anything decided without a spec** — this debt makes balancing impossible if untracked
5. **Stop without committing** (Git rules §3)

If a context-compaction warning appears mid-session, record state then too.

## Git rules

**1. Always fetch and assess before starting.**
```bash
git fetch --all --prune
git status
git log --oneline -10
git log --oneline HEAD..@{u}   # unpulled commits?
git branch -vv
```
Check: unpulled remote commits (report and ask before pulling), current branch, working tree clean, what commits say is actually done, whether `PROJECT_STATE.md` matches reality.

> If docs and git disagree, **trust git**. Docs may have missed an update; commits don't lie. Report the mismatch and fix the doc.

**2. Branch per feature.** `feature/T-043-inventory-rotation`. Use `fix/` and `docs/` prefixes as appropriate. Never work directly on main. One branch = one task.

**3. Do NOT commit. The developer verifies first.**
When work is done: summarize changes, update `PROJECT_STATE.md` and `BACKLOG.md`, then **stop**. No `git add`, `commit`, `push`, or PR.

Commit only on an explicit request:
- ✅ "commit this", "open a PR", "commit and push" → proceed
- ❌ "done?", "looks good", "nice" → **not a commit request**

When unsure, ask. Asking costs less than an unwanted commit.

**4. Never attribute AI in commits.**
Nothing about AI in commit messages, PR bodies, or code comments.
```
Co-Authored-By: Claude <noreply@anthropic.com>   ❌
🤖 Generated with Claude Code                     ❌
Assisted-by: ...                                  ❌
```
No AI in `Co-Authored-By` trailers, no tool mentions in PR bodies, no emoji signatures or footers, no AI in contributor lists. **Every commit in this repo belongs to the developer.**

**5. Commit format** (when requested)
```
T-043: grid inventory rotation and bulk move

- R to rotate, Ctrl+click bulk move, Shift+drag split
- GridInventory.Rotate() swaps W/H then revalidates placement
- Verified: SYS-INV-01 §3.3, 5 cases pass
```
Task ID on the first line — `git log` becomes the backup progress record. No signatures, footers, or emoji.

**6. PR body** (when requested)
```markdown
## Summary
T-043 grid inventory rotation and bulk move

## Changes
- ...

## Verification
- SYS-INV-01 §3.3, 5 cases pass
- Manual: two-client test, no duplication

## Spec
docs/specs/SYS-INV-01-grid-inventory.md
```
No AI mentions here either.

## Never

- Hardcode content in C#
- Switch to IL2CPP
- Upgrade Unity ad-hoc
- Invent numbers absent from specs
- Touch multiple systems in one session
- Client-authoritative state changes
- Build outside ISLE Core
- Reference a sprite, rig bone or art path from gameplay code
- Block a system on missing art (use a placeholder shape)
- Ship grid inventory without rotation and bulk move
- Unity types in `Data`; upward refs from `Core`
- **Commit, push, or open a PR without an explicit request**
- **Attribute AI in commits or PRs**
- **Work directly on main**
- **Start a task without `git fetch`**
- **Reply to the developer in English** (use Korean)
- **Write code, docs, or commits in Korean** (use English)
- **Hardcode player-facing strings** (use `lang/*.json`)

## Doc index

| Need | File |
|---|---|
| Where am I | `docs/PROJECT_STATE.md` |
| What's next | `docs/BACKLOG.md` |
| Why designed this way | `docs/design/GDD.md` |
| **Exact formulas** | `docs/specs/` ← use when implementing |
| Folders, layers, deps | `docs/ARCHITECTURE.md` |
| JSON definition format | `docs/modding/SCHEMA.md` |
| Engine rationale | `docs/adr/ADR-001-engine.md` |
| Terminology | `docs/GLOSSARY.md` |
| How to verify | `docs/TESTING.md` |
| Art assets | `docs/ART_PIPELINE.md` |

GDD is "why", specs are "how exactly". **Implement from the specs.**
