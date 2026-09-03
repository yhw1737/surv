# Project state

> **Read this first at session start. Update it at session end.**
> AI has no memory between sessions — this file is the only continuity.

## Header

- Last updated: **(before development starts)**
- Phase: **Phase 0 — rig validation**
- Task: **T-000 (not started)**
- Branch: **(none)**
- Pending commit: **none**

## Progress

```
Phase 0 rig validation      [ ] 0/7
Phase 1 foundation          [ ] 0/11
Phase 2 world               [ ] 0/7
Phase 3 inventory           [ ] 0/7
Phase 4 survival + skills   [ ] 0/8
Phase 5 production loop     [ ] 0/9
Phase 6 crafting + cooking  [ ] 0/10
Phase 7 combat              [ ] 0/6
Phase 8 artifacts           [ ] 0/7
Phase 9 modding + polish    [ ] 0/7
```

## Completed

(none)

## In progress / unfinished

(none)

## Next

**T-000** — Unity project. Mono backend pinned, URP 2D, asmdef skeleton, Git LFS

## Decided without a spec

> ⚠️ Everything here is **debt owed to the spec sheets**. Let it accumulate and balancing becomes impossible.

(none)

## Blocked / needs the developer

(none)

## Pending commits

> Claude does not commit. The developer verifies and requests it explicitly (CLAUDE.md Git rules §3).

| Branch | Task | Status |
|---|---|---|
| — | — | — |

## Gates

| Gate | Status | Result |
|---|---|---|
| G1 rig | ⬜ not reached | |
| G2 role split | ⬜ not reached | |
| G3 artifacts | ⬜ not reached | |
| G4 final | ⬜ not reached | |

## Update template

```markdown
- Last updated: 2026-XX-XX
- Phase: Phase N
- Task: T-0XX (in progress / done)
- Branch: feature/T-0XX-...
- Pending commit: yes (awaiting verification)

## Completed
- T-0XX: one-line summary
  - Files: Scripts/.../Foo.cs, Tests/EditMode/FooTests.cs
  - Verified: SYS-XXX-01 §N, all cases pass

## In progress / unfinished
- T-0YY: how far it got, what remains

## Next
- T-0ZZ

## Decided without a spec
- (record anything, or "none")

## Pending commits
| Branch | Task | Status |
|---|---|---|
| feature/T-0XX-... | T-0XX | awaiting verification |
```
