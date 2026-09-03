# ISLE — development documents

Top-down 2D co-op survival game. Written for **solo development with Claude Code**.

## What this is

A document set that lets Claude build the game **without stopping to ask the developer**. Claude gets stuck in six places; each has a document covering it.

| Where Claude gets stuck | Covered by |
|---|---|
| "What did I do yesterday?" (no memory between sessions) | `docs/PROJECT_STATE.md` |
| "What number goes here?" | `docs/specs/` — 13 sheets |
| "Where does this file go?" | `docs/ARCHITECTURE.md` |
| "What do I call this?" | `docs/GLOSSARY.md` |
| "What's next?" | `docs/BACKLOG.md` |
| "How do I know it works?" | `docs/TESTING.md` |

## File index

### Root

| File | Contents | Audience |
|---|---|---|
| **`CLAUDE.md`** | **Project constitution.** Stack, six absolute rules, layout, naming, Git rules, never-do list | **Claude Code reads it automatically** |
| `README.md` | this file | developer |

`CLAUDE.md` is the important one — Claude Code reads it from the repo root every session.

### `docs/` — always in reach

| File | Contents | Update cadence |
|---|---|---|
| **`PROJECT_STATE.md`** | **Current state.** How far you got, what's unfinished, what's next. AI has no memory across sessions; this is the only continuity | **Claude, every session** |
| **`BACKLOG.md`** | **143 tasks**, sized one per AI session, ordered by risk. Includes the four gates | Claude ticks boxes |
| `GLOSSARY.md` | Terms, IDs, units. e.g. "focus is always `Focus`, never `Concentration`" | ~static |
| `ARCHITECTURE.md` | Folders, six layers, dependency direction, asmdef table, core patterns | ~static |
| `TESTING.md` | Verification strategy, including **how to test a co-op game alone** | ~static |
| `ART_PIPELINE.md` | Art volume estimate and sourcing strategy. **The real solo bottleneck** | early decision |

### `docs/design/`

`GDD.md` — the design document. Holds **why**: scope definition, the four design axes, the three things never to cut.

### `docs/specs/` — what you implement from ★

Exact formulas, constants, and verification cases. GDD is "why", these are "how exactly".

| File | Core content |
|---|---|
| `README.md` | index + spec-writing convention |
| `SYS-CORE-01-definitions.md` | JSON loading, tag hierarchy, IDs, typo-suggesting errors |
| `SYS-NET-01-authority.md` | authority table, prediction policy, solo testing rig |
| `SYS-CHAR-01-rig-aiming.md` | rig hierarchy, mouse-tracking angles, flipping, plan B |
| `SYS-SURV-01-vitals.md` | five gauges, drain rates, temperature, six water sources |
| `SYS-WORLD-01-chunks-time.md` | chunks, in-game time, **deferred simulation** |
| `SYS-INV-01-grid-inventory.md` | grid sizes, weight→speed formula, six required UX features |
| `SYS-SKILL-01-skills-focus.md` | XP curve, **focus formula**, 9 verification cases, rust |
| `SYS-HUNT-01-butchery.md` | **butcher yield formula**, cut splitting, two-player carry |
| `SYS-FISH-01-fishing.md` | species weighting, tension minigame |
| `SYS-COOK-01-cooking.md` | **tag combination engine**, 8-method matrix, satiety fatigue |
| `SYS-CRAFT-01-crafting.md` | five quality tiers → slots, forging minigame, adjacent assist |
| `SYS-COMBAT-01-combat.md` | power formula, power table, aim sway, hit detection |
| `SYS-ART-01-artifacts.md` | **three artifacts in detail**, balance ceiling, validation gate |

### `docs/adr/`, `docs/modding/`

| File | Contents |
|---|---|
| `adr/ADR-001-engine.md` | Why Unity 6 + Mono + FishNet, and the conditions that would reverse it |
| `modding/SCHEMA.md` | Full JSON definition format: items, creatures, fish, cook methods, weapons, artifacts, enchants, patching |

### `.claude/` — automation

| File | Purpose |
|---|---|
| `settings.json` | hook configuration |
| `hooks/remind_state.py` | Nudges Claude when code changed but `PROJECT_STATE.md` didn't |
| `hooks/precompact_state.py` | Warns before context compaction so long sessions don't lose their record |
| `skills/wrap/SKILL.md` | `/wrap` — session close-out (docs only; **never commits**) |

## Language policy

**문서와 코드는 영어, 대화는 한국어.**

| Context | Language |
|---|---|
| Claude가 개발자에게 하는 말 (설명·질문·보고) | **한국어** |
| 코드, 식별자, 주석 | English |
| 커밋 메시지, PR | English |
| 문서 (`docs/`, `CLAUDE.md`) | English |
| 정의 파일의 키와 ID | English |
| **게임 내 플레이어 노출 텍스트** | **`lang/ko.json` + `lang/en.json`** |

문서와 스펙은 매 세션 다시 읽히므로 영어가 토큰 기준 2~3배 효율적입니다. 대화는 한 번만 읽히니 편한 언어를 쓰면 됩니다.

지시는 한국어로 하셔도 됩니다. Claude가 영어 문서를 읽고 한국어로 답합니다.

플레이어에게 보이는 문자열은 항상 `@key` 참조로 언어 파일을 거칩니다 — C#이나 정의 파일 값에 직접 쓰지 않습니다.

```json
{ "id": "isle:raw_meat", "name": "@item.raw_meat" }
```

## Installation

Unzip at the repository root. If the Unity project doesn't exist yet, just make the folder.

```
ISLE/                      ← repo root
├─ CLAUDE.md               ← must be at root for auto-loading
├─ README.md
├─ .claude/{settings.json,hooks/,skills/wrap/}
├─ docs/{PROJECT_STATE.md,BACKLOG.md,...,design/,specs/,adr/,modding/}
└─ Assets/                 ← Unity project lands here at T-000
```

```bash
mkdir ISLE && cd ISLE
unzip ~/Downloads/ISLE_docs.zip

git init
git add -A
git commit -m "docs: initial project documentation"

git remote add origin <your-repo-url>   # if you have one
git push -u origin main
```

Confirm `CLAUDE.md` is at the **root**. Inside `docs/` it won't load automatically.

Hooks need only Python 3 (`python3 --version`). Run `/hooks` in Claude Code to inspect them.

## Starting development

**First session** — run `claude` in the project folder:

```
docs/BACKLOG.md의 T-000부터 시작해줘.
CLAUDE.md와 docs/PROJECT_STATE.md 먼저 읽고 진행해.
```

Claude will `git fetch`, assess state, create a branch, and begin.

**Later sessions**

```
계속
```

It reads `PROJECT_STATE.md` and picks up.

**Ending a session**

```
/wrap
```

Updates the docs and stops. **It does not commit.**

**After you verify**

Run it in Unity, check it, then:

```
커밋해줘
```
```
커밋하고 PR 올려줘
```

Only an explicit request triggers a commit. "좋네", "다 됐어?" will not.

## Git rules Claude follows

Specified in `CLAUDE.md`.

| # | Rule |
|---|---|
| 1 | **Always `git fetch` before a task.** Check unpulled commits, current branch, actual progress |
| 2 | When docs and git disagree, **trust git** and fix the doc |
| 3 | **Branch per feature:** `feature/T-043-inventory-rotation` |
| 4 | Never work directly on main |
| 5 | **Finishing work does not mean committing.** Explicit request only |
| 6 | **Never attribute AI** in commits or PRs — no `Co-Authored-By: Claude`, no "Generated with Claude Code" |
| 7 | Task ID on the first line of the commit, so `git log` backs up the progress record |

## Who updates PROJECT_STATE.md

**Claude writes it. You don't have to touch it.**

Sessions can end abruptly or run out of context, so there are five layers:

| Layer | Mechanism |
|---|---|
| 1 | `CLAUDE.md` instruction — "you update this, not the developer" |
| 2 | `/wrap` skill — one word runs the whole close-out |
| 3 | **Stop hook** — auto-reminder when code changed and state didn't |
| 4 | **PreCompact hook** — warning before context compaction |
| 5 | `git log` — task IDs in commits are the fallback record |

To disable, delete `.claude/settings.json` or adjust via `/hooks`.

## Gates — don't proceed past a failure

| Gate | Task | Question | On failure |
|---|---|---|---|
| **G1** | T-006 | Does the mouse-tracking rig read naturally? | **Go back.** Try plan B (8-direction) |
| **G2** | after T-115 | Do two players naturally split roles? | Adjust focus constants |
| **G3** | T-124 | **Does a production specialist feel useless in combat?** | **Go back.** Redesign artifacts |
| **G4** | T-143 | Can an outsider's mod actually load? | Improve the mod API |

**G1 and G3 send you backward.** If G3 produces "the cook just watched", stop before building the other two artifacts. That's a design judgment, not a schedule loss.

## Three things to remember

1. **No hardcoded content.** All JSON. One exception collapses the Workshop strategy six months later
2. **When Claude says "I decided this without a spec", don't let it slide.** Accumulated, spec and code diverge and balancing becomes impossible
3. **Art is the real bottleneck.** Read `ART_PIPELINE.md` before coding. Drawing everything yourself is 8–12 months
4. **문서는 영어, 대화는 한국어.** 지시는 한국어로 하셔도 됩니다
