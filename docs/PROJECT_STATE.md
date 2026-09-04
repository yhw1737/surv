# Project state

> **Read this first at session start. Update it at session end.**
> AI has no memory between sessions — this file is the only continuity.

## Header

- Last updated: **2026-09-04**
- Phase: **Phase 0 — rig validation**
- Task: **T-000 (done, awaiting verification)**
- Branch: **feature/T-000-unity-project**
- Pending commit: **no (T-000 committed on its branch, not pushed)**

## Progress

```
Phase 0 rig validation      [~] 1/7
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

- **T-000**: Unity project created. Mono pinned, URP 2D, asmdef skeleton, Git LFS rules.
  - Seeded from the editor's own `com.unity.template.2d-cross-platform-2d-6.1.1` template, not
    hand-written — it already carries URP 17.0.3 + 2D Renderer, `com.unity.2d.animation` 10.1.4,
    PSD Importer 10.0.0 and the test framework, i.e. the ADR-001 stack.
  - Files: `ProjectSettings/` (22 assets + `ProjectVersion.txt`), `Packages/manifest.json`,
    `Assets/Scripts/{Core,Data,Modding,Networking,World,Gameplay,Combat,UI}/Isle.*.asmdef`,
    `Assets/Tests/EditMode/Isle.Tests.EditMode.asmdef`,
    `Assets/Tests/EditMode/ScriptingBackendTests.cs`, `.gitignore`, `.gitattributes`
  - Verified: `Unity -batchmode -quit` imports with **exit 0 and 0 compiler errors**;
    `Isle.Tests.EditMode.dll` builds; `scriptingBackend.Standalone: 0` (Mono2x) survives an
    editor round-trip; URP is bound in `GraphicsSettings.asset`.
  - EditMode test **executed and passing**: `ScriptingBackendTests.StandaloneBackend_IsMono`,
    1/1 passed via `Unity -batchmode -runTests -testPlatform EditMode` (exit 0). The Mono pin is
    now asserted by a test, not just by inspection, which is what ADR-001 §1 asks for.
  - **Git LFS installed** (`git-lfs/3.8.0`, `filter.lfs.process` registered), which was the last
    open item in T-000's scope. `git check-attr` confirms `*.psb` routes through the LFS filter.

## In progress / unfinished

(none)

## Next

**T-001** — Import character PSD, place bones in Skinning Editor (SYS-CHAR-01 §Rig).
**No longer blocked on art**: a placeholder rig is now in the project (see "Decided without a spec").
Start it on a fresh `feature/T-001-...` branch.

## Decided without a spec

> ⚠️ Everything here is **debt owed to the spec sheets**. Let it accumulate and balancing becomes impossible.

- **Unity pinned to `6000.2.7f2`** (ADR-001 §2 requires a pin but names no minor). Two editors were
  installed, 6000.2.7f2 and 6000.3.10f1; the older was chosen because ADR-001 §2 warns that
  2D Animation upgrades break existing rigs, and the rig is the top risk item (T-001–T-006).
  Revision `2b518236b676`. **This is the one pin; ADR-001 allows at most one upgrade before EA.**
- **Removed two template packages**: `com.unity.collab-proxy` (Unity Version Control — the project
  uses Git + LFS, and it was the *only* source of compiler errors on first import) and
  `com.unity.visualscripting` (not in the ADR-001 stack; modding is C# + Lua/MoonSharp).
- **`Isle.Data` asmdef sets `noEngineReferences: true`.** ARCHITECTURE.md forbids Unity types in
  `Data` but names no enforcement; this makes the compiler enforce it rather than review.
  Consequence: if `Isle.Core` ever exposes a Unity type on a member `Data` touches, `Data` fails
  to compile. That is intended.
- **No `Isle.Tests.PlayMode` asmdef yet.** ARCHITECTURE.md's asmdef table lists only
  `Isle.Tests.EditMode`. The folder exists; the asmdef lands at T-023 with the two-client rig.
- **No leaf subfolders created** (`Core/Ids/`, `World/Chunks/`, …). ARCHITECTURE.md §Folders
  already documents them; empty directories carry no information git can hold.
- **`applicationIdentifier` left as `com.DefaultCompany.ISLE`** — see Blocked §2.
- **Placeholder character rig = `Fei.psb`, the 2D Animation package's own sample.** The developer
  chose to unblock Phase 0 with placeholder art rather than wait on the real rig ("finishing the
  code without art is fine"), and asked for free assets first. `Fei.psb` ships inside the already
  installed `com.unity.2d.animation` 10.1.4 under the **Unity Companion License**, which permits use
  in a Unity-based project. It arrives pre-rigged (`characterMode: 1`, 21 bones, weights painted),
  so T-001 becomes verification rather than authoring. Copied verbatim — file, `.meta` and GUID
  untouched — to `Assets/Samples/2D Animation/10.1.4/Samples/3 Character/`, which is exactly where
  Package Manager's *Import* button would place it, so a later click does not duplicate it.
  **Consequence:** Fei is a side-view fantasy character, not ISLE's top-down 3/4 islander.
  T-001–T-005 are mechanical and unaffected. **T-006 / Gate G1 is not** — G1 asks five blind
  evaluators whether the character reads naturally, and that question cannot be answered with a
  borrowed character in the wrong perspective. **G1 still requires the real `player_rig.psd`.**
- **`ART_PIPELINE.md` order of work has no backlog entries.** Its steps 1 (fix the 48-colour
  palette) and 3 (placeholder generator script) are both prerequisites to its own step 4
  "start coding", but `BACKLOG.md` jumps T-000 → T-001 with no task for either. Unresolved; needs
  a task number or an explicit decision to drop them.

## Operational notes

- **Unity Hub must be running before any headless `Unity -batchmode` run.** A Personal licence is
  activated (since 2025-10-13) but Unity 6 Personal is a *floating* licence: no `.ulf` lands on
  disk, the licensing client fetches it over the Hub's session. With the Hub closed the CLI reports
  *"Found 0 entitlement groups and 0 free entitlements"* and exits 198 — asset import and
  compilation still succeed (they run before the licence check), only the test runner is gated.
  Start the Hub, and `-runTests` works. CI (GameCI) has no Hub, so it needs a licence activated
  its own way — solve that when CI lands, not before.
- **Close the Unity Editor before a headless `-batchmode` run.** Two instances cannot open one
  project; batchmode aborts with exit 1 and *"another Unity instance is running with this project
  open"*. With the Editor open instead, focusing its window triggers the asset refresh, and
  `~/Library/Logs/Unity/Editor.log` shows the import result.
- **2D IK needs no separate package.** ADR-001 names `IK Manager 2D`; in `com.unity.2d.animation`
  10.1.4 it lives *inside* that package (`IK/Runtime/IKManager2D.cs`), together with
  `LimbSolver2D`, `CCDSolver2D` and `FabrikSolver2D`. Do not add `com.unity.2d.ik` at T-002.

## Blocked / needs the developer

1. **Company name.** `productName` is `ISLE`; `companyName` is still `DefaultCompany` and
   `applicationIdentifier` is `com.DefaultCompany.ISLE`. Both want the real name before anything
   ships to Steam. One-line edits in `ProjectSettings/ProjectSettings.asset`.

## Pending commits

> Claude does not commit. The developer verifies and requests it explicitly (CLAUDE.md Git rules §3).

| Branch | Task | Status |
|---|---|---|
| feature/T-000-unity-project | T-000 | **committed**, not pushed |

T-000 was committed with explicit pathspecs so the placeholder rig under `Assets/Samples/` stayed
out of it; those files belong to the T-001 branch.

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
