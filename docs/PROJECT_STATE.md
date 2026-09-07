# Project state

> **Read this first at session start. Update it at session end.**
> AI has no memory between sessions — this file is the only continuity.

## Header

- Last updated: **2026-09-07**
- Phase: **Phase 1 — foundation (definitions)** ← re-sequenced 2026-09-06, see below
- Task: **T-011 done + skill/profession taxonomy settled. Next is T-012** (T-018/T-019 do the
  SYS-SKILL-01/SYS-BUFF-01 rewrite; new debt T-106/T-107/T-117, see Decided without a spec)
- Branch: **feature/T-011-data-skill-taxonomy** (renamed from `feature/T-011-data-defs` — scope
  grew past T-011 alone, developer requested a single PR for both)
- Pending commit: **committed, PR opened** — see Pending commits below

## Progress

```
stage 1 · placeholders
Phase 0  project setup        [x] 3/3   T-000..T-002
stage 2 · solo beta
Phase 1  foundation           [~] 2/10  T-010..T-019  ← here
Phase 2  netcode skeleton     [ ] 0/2   T-020, T-021 (authority only)
Phase 3  world                [ ] 0/7
Phase 4  inventory            [ ] 0/6
Phase 5  survival + skills    [ ] 0/8
Phase 6  production loop      [ ] 0/9
Phase 7  crafting + cooking   [ ] 0/10
Phase 8  combat               [ ] 0/6
Phase 9  SOLO BETA 🚩         [ ] 0/3   T-150..T-152
stage 3 · multiplayer
Phase 10 multiplayer P2P+Steam[ ] 0/9   T-022..T-027, T-046, T-047, T-116
stage 4 · graphics
Phase 11 graphics + rig       [ ] 0/9   T-160, T-003..T-006, T-161..T-164
Phase 12 artifacts            [ ] 0/7
Phase 13 modding + polish     [ ] 0/7
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

- **T-001**: placeholder character rig built to SYS-CHAR-01 §Rig, seventeen named bones.
  - Files: `Assets/Scripts/Gameplay/Character/Editor/{Isle.Gameplay.Editor.asmdef,PlaceholderRigBuilder.cs}`,
    `Assets/Tests/EditMode/CharacterRigHierarchyTests.cs`,
    `Assets/Art/Characters/Placeholder/*.png` (14, generated),
    `Assets/Prefabs/Characters/player_rig_placeholder.prefab` (generated)
  - Verified: batchmode `-executeMethod ...PlaceholderRigBuilder.Build` exit 0, 0 compiler errors;
    EditMode **8/8 passed** (7 new + the T-000 Mono assertion).
  - **No PSD and no Skinning Editor were involved, by design.** ART_PIPELINE §Rig specifies a
    *cutout* rig — fourteen separate parts — so each part is a plain SpriteRenderer on its own bone
    Transform. Sprite deformation, and therefore `SpriteSkin`, the PSD Importer and the Skinning
    Editor, are only needed for skinned meshes. `IKManager2D` and `LimbSolver2D` drive Transform
    chains directly, which is all T-002 requires. The real `player_rig.psd` still imports through
    the PSD Importer later; it replaces the generated sprites part-for-part.

- **T-002**: IK Manager 2D on both arms, head look-at in code.
  - Files: `Assets/Scripts/Gameplay/Character/CharacterRig.cs`,
    `Assets/Scripts/Gameplay/Character/Editor/CharacterRigIk.cs`,
    `Assets/Tests/EditMode/CharacterRigIkTests.cs`
  - `IKManager2D` on the rig root drives one `LimbSolver2D` per arm (SYS-CHAR-01 §Rig gives
    Arm_Front the aim point and Arm_Back the support grip). Each solver's target sits under its own
    `IK_Arm_*` object, outside the chain — `IKChain2D.Validate` rejects a target descended from the
    chain root, because solving would move the target it is chasing.
  - Head is **not** IK: the package ships Limb, CCD and FABRIK solvers but no look-at, exactly as
    the spec notes, so `CharacterRig` clamps to ±70° and eases with `SmoothDampAngle` over 0.08 s.
  - Verified: builder exit 0, 0 compiler errors; EditMode **16/16 passed** (9 new).

- **T-010**: `NamespacedId` and `TagRegistry` tag flattening (SYS-CORE-01 §NamespacedId, §Tags).
  - Files: `Assets/Scripts/Core/Ids/NamespacedId.cs`, `Assets/Scripts/Core/Tags/TagRegistry.cs`,
    `Assets/Scripts/Core/Util/IdText.cs`,
    `Assets/Tests/EditMode/{NamespacedIdTests,TagRegistryTests}.cs`
  - `NamespacedId` is a readonly struct holding the full text, the colon index and a precomputed
    hash. `TryParse` returns an explanatory message instead of throwing, because §Load pipeline
    step 5 requires reporting *all* failures rather than stopping at the first.
  - `TagRegistry` interns paths to ints and precomputes each one's ancestor chain, so `Flatten`
    turns `["fish/saltwater"]` into a `HashSet<int>` holding both `fish/saltwater` and `fish`.
    Depth capped at 3. **Not a singleton** — ARCHITECTURE.md caps those at three and this is not
    one of them, so it is an instance owned by whatever loads definitions.
  - `IdText` holds the `[a-z0-9_]+` rule the spec states once but two systems need.
  - Verified: batchmode import exit 0, 0 compiler errors; EditMode **64/64 passed**
    (20 NamespacedId + 28 TagRegistry + the 16 from T-000–T-002).
    **SYS-CORE-01 verification cases 1, 2 and 3 covered** — case 1 by `TryParse_NoNamespace_Fails`,
    cases 2 and 3 by `HasTag_ChildImpliesParent_IsTrue` and
    `HasTag_ParentDoesNotImplyChild_IsFalse`. Cases 4–8 belong to T-012/T-013.

- **T-011**: `Data` layer POCOs — the nine definition types in `SCHEMA.md` §Definition types.
  - Files: `Assets/Scripts/Data/{Definition,Shared,ItemDef,CreatureDef,FishDef,CookMethodDef,`
    `CraftRecipeDef,WeaponDef,ArtifactDef,EnchantDef,CropDef,IsExternalInit}.cs`,
    `Assets/Tests/EditMode/DataDefinitionTests.cs`
  - `ItemDef CreatureDef FishDef CookMethodDef CraftRecipeDef WeaponDef ArtifactDef EnchantDef
    CropDef`, each implementing `IDefinition` (`NamespacedId Id`), plus ~25 nested value objects.
    Field-for-field from SCHEMA; nothing added and nothing dropped.
  - **Every property is `init`-only.** A def is held by reference from every stack that uses it
    (ARCHITECTURE §Patterns), so one runtime write would retune the world. Unity 6's netstandard
    profile has no `IsExternalInit`, so `Isle.Data` declares its own.
  - Cross-references are typed `NamespacedId`, not `string`, so T-013's `ReferenceResolver` has
    one thing to walk. `IDefinition` also requires `Name`, so a type cannot be added without a
    language key.
  - Verified: batchmode import exit 0, 0 compiler errors; EditMode **70/70 passed** (6 new).
    The five are architectural guards, not behaviour — a POCO has no logic to test:
    `Isle.Data` references no Unity assembly, the type roster matches SCHEMA exactly, every def is
    sealed and parameterless-constructible (System.Text.Json needs both), every property is
    `init`-only, a def can be built with an object initializer from another assembly, and a
    definition's name is a `@` language key rather than literal text.
  - Cross-checked by script: **all 127 JSON keys in SCHEMA §Definition types map to a property and
    back**, no extras in either direction. Not kept as a test — it parses markdown, which is too
    brittle to run every build.

## In progress / unfinished

(none)

## Next

**T-012** — `DefinitionLoader` + `SchemaValidator`, following SYS-CORE-01 §Load pipeline.

T-011 leaves it three concrete jobs:

1. **A `System.Text.Json` converter for `NamespacedId`.** It needs `Read`/`Write` *and*
   `ReadAsPropertyName`/`WriteAsPropertyName` — `FishDef.BaitAffinity` is a dictionary keyed by an
   ID. On a parse failure, surface `NamespacedId.TryParse`'s message rather than a bare
   `JsonException`; §Load pipeline step 5 wants every failure reported, not the first.
2. **`System.Text.Json` is not in the project yet.** No DLL, not in `manifest.json`. Getting it in
   (NuGet DLL under `Assets/Plugins/`, listed in the asmdef's `precompiledReferences`) is part of
   T-012, not a prerequisite someone else did.
3. **Naming policy, not attributes.** The POCOs carry no `[JsonPropertyName]`, so the loader must
   set `PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower`. Every property name was chosen to
   round-trip under it (`NutritionRetention` → `nutrition_retention`). Keeping the attributes out
   is what lets `Isle.Data` stay dependency-free.

Validation the POCOs deliberately do **not** do, all of it T-012's: `IngredientRef` has exactly one
of `item`/`tag`; `condition_range` and the `habitat` bands are length 2; an artifact's
`combat_skill` really is null; an `EnchantEffect` carries the fields its `type` requires.

**T-012 is not blocked by T-018/T-019** — the loader does not care which definition types exist.
But **T-018 and T-019 must both land before T-015**, because starter definitions reference skill
and buff IDs and would have to be rewritten otherwise.

## Decided without a spec

> ⚠️ Everything here is **debt owed to the spec sheets**. Let it accumulate and balancing becomes impossible.

- ### ★ 2026-09-07 — skills and buffs become moddable content; every definition gets a name
  **Developer decision**, answering the three questions T-011 raised.

  | # | Question | Answer |
  |---|---|---|
  | 1 | Are skills fixed at 7? | **No.** Magic/enchanting, brewing and others may be added during development |
  | 2 | Can modders add skills? | **Yes** — so the focus formula must be simulated across skill counts |
  | 3 | Buff spec sheet? | **Yes**, minimal beta set now, detailed during development, modder-extensible |
  | 4 | Name field on the other 8 types? | **Yes** |
  | 5 | Unify on `{namespace}:{name}`? | **Yes** |

  **Questions 4 and 5 are done** — applied to the POCOs and to `SCHEMA.md` in T-011.
  `IDefinition` now requires `Name`, so a definition type cannot be added without a language key.

  **Questions 1–3 are new work: T-018 and T-019.** They are design, not transcription, and the
  reason is question 2. The focus formula is
  `denominator = L_i + Σ_{j≠i}( L_j * w(L_j) * c(i,j) * p(n) )`, and three things in it assume a
  closed skill list:
  - **`w(L) = 0` below level 16 is what saves it.** An unlevelled skill contributes nothing, so a
    mod adding ten skills the player never touches changes no one's focus. This property is why
    modder-added skills are viable at all, and it must be preserved deliberately, not by luck.
  - **`c(i,j)` is a hardcoded 2×2** over Production and Combat. **Open: may a mod declare a new
    pool?** If yes, `c` becomes an N×N matrix and cross-pool balance is a design problem. If no,
    every modded skill picks an existing pool and the matrix stands. *Recommend no for beta.*
  - **Pool asymmetry.** Production has 5 members, Combat 2. Same-pool interference is
    `c = 1.00` against Combat's cross-pool `0.35`, so a mod loading its new skills into one pool
    quietly taxes that pool's specialists. Needs simulating.
  - **The 9 verification cases are written against a fixed 7-element array** in a fixed order.
    They have to be restated so they still mean something at any skill count.

  **Question 3's taxonomy half is now decided — see Blocked §2 (resolved).** T-018's actual
  formula rewrite is still pending; the pool-asymmetry bullet above was analyzed for a 5/2 split
  and needs redoing for 5/3 now that Combat has a third member (Magic).

  **Two follow-up design clarifications (2026-09-07), written directly into the specs rather than
  tracked here as debt:**
  - **Hunter sets the initial bulk, Cook refines it.** `SYS-HUNT-01`'s `damageFactor` (kill method
    and weapon — already existed) is the Hunter's contribution to `TotalEdibleKg`; `cookingLevel`'s
    term in `ButcherQuality` is the Cook's. No formula changed, only which existing term belongs to
    which profession is now stated explicitly.
  - **Magic/Enchanting is a late-game power-ceiling system, not a core-loop requirement.** The solo
    beta loop (gather → hunt → fish → cook → craft → fight → sleep, `T-152`) must stay completable
    on unenchanted Common gear without Magic. Recorded in `GDD.md` §Scope, §Skills, and
    `SYS-CRAFT-01` §Enchanting.

- **T-011: the type roster follows `SCHEMA.md`, not `ARCHITECTURE.md`, and the two disagreed.**
  ARCHITECTURE §Folders listed ten Data types; SCHEMA documents nine, and the sets do not match.
  `CropDef` is in SCHEMA but was missing from ARCHITECTURE's list — **added**. `SkillDef` and
  `BuffDef` were in ARCHITECTURE's list but have no schema anywhere — **not written**, see
  Blocked §2. ARCHITECTURE §Folders now names the nine that exist.

- **T-011: a def stores its tag strings, not a flattened `HashSet<int>`.** This answers the
  question T-010 left open. `Tags` is `string[]`, exactly as the JSON writes it; the flattened set
  is a load-time product and belongs to the tag index in **T-014**, which is also where
  `AllWithTag` lives. A def that carried its own set would be two sources of truth for the same
  fact.

- **T-011: `CookModifiers` fields default to `1.0`.** SCHEMA's own example has a `tag_reaction`
  declaring only `thirst`, so the absent fields must be identity or the reaction would zero the
  dish out. GLOSSARY §Units defines 1.0 as "no change" and SYS-COOK-01 steps 3–4 multiply, so this
  is read off the docs rather than invented — but the docs never say it outright.
  `TagReaction.Power` is **not** defaulted; it has no documented identity value, and it is only
  read when the reaction grants a buff, where SCHEMA always states it.

- **T-011: numeric ranges stay bare `float[]`.** `condition_range: [0.7, 1.3]`, `habitat.depth`,
  `habitat.water_temp`. A `Range { Min, Max }` type would read better but does not match the JSON,
  and length is a validator concern (T-012).

- **T-011: `EnchantEffect` is one class with nullable fields, not a polymorphic hierarchy.**
  SCHEMA's two example effects have different shapes — `{type, value}` vs
  `{type, when, damage_mult}`. A discriminated hierarchy would need the full list of `type` values,
  which is not documented. Union-of-optionals now; T-012 validates per type.

- ~~**T-011: `ButcherYield.quality_from` is unnamespaced in SCHEMA.**~~ **Resolved 2026-09-07** —
  developer confirmed `{namespace}:{name}` everywhere. SCHEMA now writes `"isle:hunting"` and the
  property is a `NamespacedId`. It changes again if T-018 renames the skill.

- **T-011: `ArtifactAbility.Id` is a plain `string`.** SCHEMA writes `"hook_pull"` — the ID is
  local to its artifact, not a definition ID, so it is deliberately not a `NamespacedId`.

- **T-011: `IsExternalInit` is declared public in `Isle.Data`.** Unity 6's netstandard profile does
  not ship it, so `init` accessors will not compile without a local copy. It is public rather than
  internal because setting an `init` property needs the marker accessible at the call site —
  internal would stop EditMode tests from building def fixtures inline, which every formula test
  from T-072 on will want to do. Ceiling: a referenced NuGet DLL declaring its own public copy is a
  CS0433 ambiguity; the fix is `internal` + `InternalsVisibleTo`.

- **T-011: type names take the `Def` suffix over GLOSSARY's concept names.** GLOSSARY says
  `CookMethod` and `CraftRecipe`; ARCHITECTURE §Folders and SYS-COOK-01 both write `CookMethodDef`.
  Used the suffixed form throughout — GLOSSARY's entries name the *concept*, and ARCHITECTURE's
  def-vs-instance pattern needs the suffix free for the loaded type.

- **⚠ T-011: SYS-COOK-01 step 2 reads `def.reference_weight`, which is not in SCHEMA §Items.**
  `weightScale = clamp(itemWeightKg / def.reference_weight, 0.5, 2.0)`. The item schema has
  `weight` and nothing else weight-like, so this is almost certainly the same field under an old
  name. **Not renamed and not added** — `ItemDef.Weight` is what exists. Resolve before T-101.

- **T-010: equality compares the hash first, then the string.** SYS-CORE-01 §NamespacedId says
  "compare hashes, not strings". Taken literally that is wrong: a 32-bit hash over a few thousand
  mod IDs collides with meaningful probability (birthday bound), and a collision would silently make
  two different definitions equal — resolving to whichever loaded last, the exact failure the
  namespace exists to prevent. `Equals` tests `_hash` first and falls back to an `Ordinal` string
  compare only when the hashes match, so the case the spec cares about, a mismatch, still costs one
  int compare. **Reads as a deviation from the sheet; it is a correction, and the sheet should be
  amended.**

- **⚠ T-010: `SYS-CORE-01` and `SCHEMA.md` disagree about whether a tag is namespaced.**
  `SYS-CORE-01` §Tags describes bare paths (`fish/saltwater`) and its verification cases use them;
  `SCHEMA.md` §Tags declares tags as `{ "id": "coolmod:deep_sea", "parent": "isle:fish/saltwater" }`
  while item definitions in the same file use bare tags (`["fish", "oily", "raw"]`). T-010
  sidesteps it: everything before the first `/` is an opaque root, so `isle:fish/saltwater` flattens
  to `{isle:fish/saltwater, isle:fish}` and `fish/saltwater` to `{fish/saltwater, fish}` — both
  correct — and `isle:fish` != `fish`. **Canonicalising the two is a load-time decision and belongs
  to T-014.** The documents need reconciling before then.

- **T-010: an undeclared tag is interned, not rejected.** `SCHEMA.md` §Tags gives a declaration
  format, yet its own item examples carry `oily`, `protein` and `low_fat`, none of which are in the
  base tag table. So use implies declaration. If T-013's `ReferenceResolver` should instead reject
  unknown tags, that is a policy change at the loader, not in `TagRegistry`.

- **T-010: tag segment charset is `[a-z0-9_]+`, invented by analogy.** The spec fixes that charset
  for `NamespacedId` and says nothing about tag segments. Reused it so one rule covers both
  (`IdText`). Consequence: uppercase and hyphenated tags are load errors.

- **T-010: `TagRegistry` is not a singleton.** SYS-CORE-01 makes `DefRegistry` one but is silent
  here; ARCHITECTURE.md caps singletons at three and does not list this. Instance, owned by the
  loader.

- **2026-09-07 — commit and PR languages are now specified, including length.** `CLAUDE.md`
  §Language previously listed commits and PR bodies together as English. Split into two rows plus a
  §Commits are English, PRs are Korean subsection, and Git rules §5/§6 updated to match: commits
  **short, plain, precise English** (subject ≤ 60 chars, `git log` stays skimmable); PR titles and
  bodies **Korean, detailed, complete**, with the rule that **no change in the diff may be missing
  from the body**. The §6 template is now Korean (왜 / 변경점 / 판단 / 검증 / 스펙). Four `Never`
  entries added.

- ### ★ 2026-09-06 — four-stage build order: placeholders → solo beta → multiplayer → graphics
  **Developer decision.** The 2026-09-04 reprioritisation (below) put networking at Phase 2, ahead
  of the whole single-player loop. The developer then stated the order explicitly as *placeholders
  → **solo beta** → **multiplayer** → graphics*. This entry resolves that conflict.

  **Networking was split rather than moved.** Whole-scale deferral would have been the wrong read:
  Absolute Rule 2 makes every state change server-side anyway, and FishNet runs a listen server, so
  **solo play is a one-player host session on the same code path.** Building the solo beta on a
  non-networked path and bolting FishNet on afterwards is the classic rewrite. So:

  | | Phase | Why |
  |---|---|---|
  | T-020 FishNet bootstrap, T-021 authoritative movement | **2** (unchanged) | architecture, not a co-op feature |
  | T-022 aim sync, T-023 two-client rig, T-024 net gate, T-025 Steam P2P | **10** | need a second client to mean anything |
  | T-046 inv gate, T-047 two-player carry, T-116 G2 | **10** | moved out of Phases 4/6/8, all inherently two-player |

  **New tasks:** T-017 placeholder shape generator (Phase 1 — closes the ART_PIPELINE gap flagged
  below), T-150/T-151/T-152 solo beta milestone (Phase 9), T-026 ping system and T-027 death drops
  (Phase 10 — both mandatory in GDD but never carried a task), T-160–T-164 the actual graphics work
  (Phase 11 — the phase was previously character rig only).

  **Renumbered:** character presentation Phase 9 → **11**, artifacts 10 → **12**, modding 11 → **13**.

  **The presentation boundary is now a rule, not a note.** It previously lived only in this file and
  a BACKLOG comment, which is not where a constraint binds. It is now `ARCHITECTURE.md`
  §Presentation boundary + `CLAUDE.md` Absolute Rule 7 + a `Never` entry in both. Gameplay reads
  `position` / `aimAngle` / `facingSign` and def numbers; never a bone, sprite or art path.

  **Rewritten:** `BACKLOG.md` (four-stage banner, 14 phases, gates re-ordered), `ARCHITECTURE.md`
  (§Presentation boundary, §Never), `CLAUDE.md` (§Project build order, Absolute Rule 7, §Never),
  `GDD.md` (genre now names *strategy*, beta milestone, risks 3/4/9), `ART_PIPELINE.md`
  (§Placeholders rewritten around vector shapes, §Order of work re-sequenced),
  `SYS-CHAR-01` + `ADR-001` (phase references).

  **Wording adopted from the developer:** placeholders are *vector shapes*; the genre is *top-down
  2D co-op survival **strategy***; the goal is a *beta release*. All three were absent from the docs.

- ### ★ 2026-09-04 — roadmap reprioritised: boxes first, rig last
  **Developer decision, explicitly authorising the doc changes.** Project reframed as *solo indie
  targeting a beta release*: characters, weapons and props stay **placeholder boxes**, and the
  priority is building the systems that make the game fun — definitions, **P2P + Steam
  networking**, weapons, cooking — and proving them bug-free.

  **Rewritten:** `BACKLOG.md` (phase order, priority banner, gates in execution order),
  `SYS-CHAR-01` (deferral + rewrite banner), `ADR-001` (requirement A deferred),
  `GDD.md` (§Character presentation, risk 4).

  | | before | after | superseded 2026-09-06 |
  |---|---|---|---|
  | Character rig | Phase 0, blocks everything | Phase 9, before artifacts | **Phase 11** |
  | Definitions | Phase 1 | **Phase 1** (now first real work) | unchanged |
  | Networking | Phase 1 tail | Phase 2, called out as priority | **split: 2 and 10** |
  | G1 | first gate | second to last | unchanged |

  **Cost, accepted knowingly:** a G1 failure now lands with mechanics already built on top of the
  character layer, instead of before them. Mitigation is architectural — gameplay depends on
  movement/aim *data* (position, `aimAngle`, `facingSign`), never on rig internals, so the
  character layer stays swappable. **Hold that line in every phase; it is the only thing making
  the deferral safe.** *(2026-09-06: promoted from this note to `ARCHITECTURE.md` §Presentation
  boundary and `CLAUDE.md` Absolute Rule 7, where it actually binds.)*

- **Perspective is quarter view, not side view.** The developer's concept sketch is predominantly
  side-on but angled slightly toward the camera and slightly from above — legs and arms visibly
  offset rather than stacked. This matches `ART_PIPELINE` `top-down 3/4` and **contradicts
  SYS-CHAR-01**, which is written throughout for a pure side view (left/right sprites, overlapping
  limbs, flip machinery). SYS-CHAR-01 must be rewritten before T-003. Weapon-driven rig complexity
  is dropped as a justification for the part count.

- **Placeholder proportions rebuilt to the sketch (2026-09-04).** `PlaceholderRigBuilder.Parts`
  went from a 48 px side-view figure with an 11 px head to a ~2.5-head chibi: head 20 px of 48,
  torso 10×12, thin limbs, no neck. Stacked from the ground — foot 3 + shin 8 + thigh 8 = hip at
  17, torso 12 = shoulder line at 29, head 20 tops out at 48. The 48 px total is the only specced
  figure (`ART_PIPELINE` line 55); everything inside it is read off the sketch by eye and is still
  **invented**. Bone hierarchy untouched, so T-002's IK and the 16 EditMode tests are unaffected.
  **Not yet built or verified** — Unity would not quit to free the project for batchmode.

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
- **`Assets/Tests/EditMode` has no per-system subfolders.** Five files sit flat. Fine for now;
  split when the count makes it awkward.
- **No `Isle.Tests.PlayMode` asmdef yet.** ARCHITECTURE.md's asmdef table lists only
  `Isle.Tests.EditMode`. The folder exists; the asmdef was pencilled in for T-023, which is now
  Phase 10 — too late. **Whichever task first needs a PlayMode test creates it** (likely T-020).
- **No leaf subfolders created** (`Core/Ids/`, `World/Chunks/`, …). ARCHITECTURE.md §Folders
  already documents them; empty directories carry no information git can hold.
- **`applicationIdentifier` left as `com.DefaultCompany.ISLE`** — see Blocked §2.
- **Placeholder character rig is generated procedurally, not bought or drawn.** The developer chose
  to unblock Phase 0 with placeholder art rather than wait on the real rig ("finishing the code
  without art is fine") and asked for free assets first. The free option evaluated and **rejected**
  was `Fei.psb`, the pre-rigged sample inside `com.unity.2d.animation` (Unity Companion License): it
  is a skinned side-view fantasy character whose arms are single meshes, so it cannot supply
  ART_PIPELINE's fourteen-part cutout structure, and once the rig is built procedurally it adds
  nothing. It was imported, verified to work, then deleted. To look at it again: Package Manager →
  2D Animation → Samples → Import.
  `PlaceholderRigBuilder` draws each part as a rounded-box capsule (dark brown outline, 32 PPU,
  48 px character) straight from `Texture2D`, so the placeholder needs no external image tooling and
  regenerates in one menu click.
  **Consequence: this does not get Gate G1 past.** G1 asks five blind evaluators whether the
  character reads naturally; capsules cannot answer that. **G1 still requires the real
  `player_rig.psd`.** T-002–T-005 are mechanical and unaffected.
- **⚠ Bone names: `SYS-CHAR-01` §Rig and `ART_PIPELINE.md` §Rig disagree, and the rig follows
  ART_PIPELINE.** The spec says `Leg_L / Leg_R` and a single `Arm_Front`; ART_PIPELINE says
  `Leg_Front_* / Leg_Back_*` and `Arm_Front_Upper / Arm_Front_Lower / Hand_Front`. ART_PIPELINE won
  on two grounds: it states "layer names become bone names", so it governs what the artist delivers;
  and a flippable side view orders limbs by *depth*, not anatomy — there is no stable left or right
  once `facingSign` flips. The spec's `Arm_Front` is read as the chain, not one bone, which is also
  what its own "Limb solver (2-bone)" requires. `Root`, `Hip` and `WeaponSocket` are structural and
  come from the spec. **Someone should reconcile the two documents;** until then
  `CharacterRigHierarchyTests.ExpectedPaths` is the single source of truth and T-002–T-005 wire
  against it.
- **Head rotation cancels its parent's local rotation.** SYS-CHAR-01 §Angles limits the head to
  ±70° and the torso to ±20°, both "relative to facing forward", but the head is a *child* of the
  torso — applied naively the two angles add and the head reaches 90°. `CharacterRig.ApplyHead`
  multiplies by the inverse of the torso's local rotation so the limit means what the table says.
  The spec does not state which reading it intends; `HeadRotation_IgnoresTorsoTwist` pins it.
- **IK object names are not in any spec**: `IK_Arm_Front` / `IK_Arm_Back`, each with a `Target`
  child. Nothing outside `CharacterRigIk` and the tests depends on them yet; T-003 will.
- **Placeholder limb proportions are invented.** ART_PIPELINE fixes only the 48 px character height
  and the 32 px tile; no per-limb sizes exist anywhere. Segment widths, lengths and the 1 px joint
  overlap were chosen to look like a body. They are thrown away with the placeholder and no gameplay
  number depends on them, so they were not escalated — but they are not spec'd.
- ~~**`ART_PIPELINE.md` order of work has no backlog entries.**~~ **Resolved 2026-09-06.** The
  placeholder generator is **T-017** (Phase 1) and the palette is **T-160** (opens Phase 11); the
  document's order of work was re-sequenced to match, and the rest of it is now explicitly a plan
  for Phase 11 rather than a prerequisite for coding.

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

2. ~~**★ Skill taxonomy.**~~ **Resolved 2026-09-07** — see `SYS-SKILL-01`'s status banner and
   `docs/design/GDD.md` §Scope for the finalized table. Not a pure rename after all: `isle:hunting`
   is retired (butchery yield moves to **Cooking**, `SYS-HUNT-01`), and a genuinely new profession,
   **Enchanter**, is added — distinct from Blacksmith, spanning a new Combat skill (`isle:magic`,
   magic combat) and a new Production skill (`isle:enchanting`, fills `EnchantDef` slots + brews).
   Production stays at 5 members, **Combat grows from 2 to 3** — this changes the pool-asymmetry
   math below, which was written for 5/2. **T-018 must still do the actual formula rewrite** and
   re-simulate all 9 verification cases against the new 5/3 split; only the taxonomy question
   itself is closed. New backlog debt: `BACKLOG.md` T-106 (move enchant application off Crafting —
   mechanical, formula already exists), T-107 (SYS-BREW-01 — **no spec exists**, ask before
   inventing brewing numbers), T-117 (magic combat weapon category — `SYS-COMBAT-01`'s
   `PowerCalculator` is skill-agnostic, so this is cheap unless a mana/resource system turns out to
   be wanted, which is itself unasked and unspecced).

## Pending commits

> Claude does not commit. The developer verifies and requests it explicitly (CLAUDE.md Git rules §3).

Split one-task-per-branch on 2026-09-05, each with its own PR.

| PR | Branch | Task | Status |
|---|---|---|---|
| #1 | feature/T-000-unity-project | T-000 | **merged to main** |
| #2 | feature/T-001-character-rig | T-001 | **merged to main** |
| #3 | feature/T-002-character-ik | T-002 | **merged to main** |
| #4 | docs/roadmap-reprioritisation | — | **merged to main** |
| #5 | docs/solo-beta-first-roadmap | — | **merged to main** |
| #6 | docs/commit-pr-language-rule | — | **merged to main** |
| #7 | feature/T-010-namespaced-id-tags | T-010 | **merged to main** |
| #8 | feature/T-011-data-skill-taxonomy | T-011 + skill/profession taxonomy | [PR #8](https://github.com/yhw1737/surv/pull/8) — open |

T-011's branch also carries the `SCHEMA.md` change for developer answers 4 and 5 (a `name` on all
nine types, `quality_from` namespaced), plus the full skill/profession taxonomy redesign that came
up mid-review (Enchanter split off Blacksmith, `isle:hunting` retired, `isle:magic` and
`isle:enchanting` added) — the developer asked for one bundled PR rather than splitting the
taxonomy docs out, and renamed the branch accordingly (was `feature/T-011-data-defs`).

Rebuilding the split meant reconstructing the T-001 tree without any T-002 code, so each commit
builds and passes its own tests on its own: T-001 is **8/8** with an IK-free prefab, T-002 is
**16/16** with the solvers attached. The Unity 6.2 serialization churn in `SampleScene.unity` and
`ProjectSettings.asset` was folded into T-000, where those files belong.

**PR bodies are Korean, commit messages English** — no longer an ad-hoc split. Confirmed by the
developer and written into `CLAUDE.md` §Language on 2026-09-07, with the sizes specified too:
commits short, PRs detailed and complete.

## Gates

In execution order (re-sequenced 2026-09-06).

| Gate | Phase | Status | Result |
|---|---|---|---|
| Def gate T-016 | 1 | ⬜ not reached | next up |
| Ext gate T-105 | 7 | ⬜ not reached | |
| **Solo beta T-152** | 9 | ⬜ not reached | **stage 2 ends here** |
| Net gate T-024 | 10 | ⬜ not reached | |
| Inv gate T-046 | 10 | ⬜ not reached | |
| G2 role split T-116 | 10 | ⬜ not reached | |
| G1 rig T-006 | 11 | ⬜ not reached | **deferred to Phase 11** |
| G3 artifacts T-124 | 12 | ⬜ not reached | |
| G4 final T-143 | 13 | ⬜ not reached | |

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
