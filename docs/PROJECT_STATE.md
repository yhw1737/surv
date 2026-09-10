# Project state

> **Read this first at session start. Update it at session end.**
> AI has no memory between sessions — this file is the only continuity.

## Header

- Last updated: **2026-09-10**
- Phase: **Phase 2 — netcode skeleton**
- Task: **T-020 done and confirmed. T-021 (server-authoritative movement) is next**
- Branch: **feature/T-020-fishnet-bootstrap**
- Pending commit: **yes — developer requested commit + PR**

## Progress

```
stage 1 · placeholders
Phase 0  project setup        [x] 3/3   T-000..T-002
stage 2 · solo beta
Phase 1  foundation           [x] 10/10 T-010..T-019  ← done
Phase 2  netcode skeleton     [~] 1/2   T-020 done, confirmed 2026-09-10; T-021 next
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

- **T-012**: `DefinitionLoader` + `SchemaValidator` (SYS-CORE-01 §Load pipeline steps 4–5).
  - Files: `Assets/Scripts/Modding/Defs/{NamespacedIdJsonConverter,DefinitionLoader,SchemaValidator}.cs`,
    `Assets/Scripts/Modding/Isle.Modding.asmdef` (vendored references), `Assets/Plugins/SystemTextJson/*.dll`
    (8 files — `System.Text.Json` 8.0.5 and its netstandard2.0 dependency chain, none of them in the
    project before this task), `Assets/Tests/EditMode/DefinitionLoaderTests.cs`
  - `NamespacedIdJsonConverter` implements `Read`/`Write`/`ReadAsPropertyName`/`WriteAsPropertyName`
    (the last pair for `FishDef.BaitAffinity`'s dictionary key) and turns a parse failure into a
    `JsonException` carrying `NamespacedId.TryParse`'s own message. `HandleNull` is on, because
    `ArtifactDef.CombatSkill` is always `null` in JSON and must deserialize to `default`, not throw.
  - `DefinitionLoader.LoadAll<T>(directoryPath)` reads every `*.json` under a directory
    (`PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower`, no attributes on the POCOs, as T-011
    left it), aggregating failures across the whole batch rather than stopping at the first file or
    the first field. Per file: missing `id`/`name` and a JSON parse exception are errors that drop
    the file; an unrecognized top-level field is a warning that does not.
  - `SchemaValidator` does the four checks the POCOs' own doc comments deferred here: `IngredientRef`
    exactly-one-of `item`/`tag` (`CraftRecipeDef.Ingredients`, `EnchantDef.Catalyst`); `[min,max]`
    bands are length 2 (`ButcherSpec.ConditionRange`, `HabitatSpec.Depth`/`WaterTemp`);
    `ArtifactDef.CombatSkill`/`GrantsCombatXp` are unset (Absolute Rule 5); an `EnchantEffect`
    carries the fields its `type` needs. An unrecognized `EnchantEffect.Type` is left alone —
    SYS-CRAFT-01 §Open questions leaves the type list undecided, so hard-failing would foreclose it.
  - Verified: batchmode import exit 0, **0 compiler errors — no `CS0433` conflict between the
    vendored DLLs and Unity's own Mono BCL**, which was the open risk going in; EditMode **85/85
    passed** (15 new). Covers SYS-CORE-01 verification case 1 through the loader (not just
    `NamespacedId` directly), case 6 (unknown field → warning, def still loads) and case 7 (missing
    `id`/`name` → error), plus all four `SchemaValidator` checks in both pass and fail form. Cases
    4/5/8 (multi-mod) and case 1's typo-suggestion half stay T-013/T-130.
  - Fixtures are temp directories written per test (`Assets/StreamingAssets/definitions/` is still
    empty — no real content JSON exists yet), so nothing here depends on content that doesn't exist.

- **T-013**: `ReferenceResolver` (SYS-CORE-01 §Load pipeline step 7), scoped to item references.
  - Files: `Assets/Scripts/Modding/Defs/ReferenceResolver.cs`,
    `Assets/Scripts/Modding/Defs/DefinitionLoader.cs` (adds `LoadError.Line`/`Suggestion` and
    `LoadResult<T>.SourcePaths`), `Assets/Tests/EditMode/ReferenceResolverTests.cs`.
  - `ReferenceResolver.ResolveItemRefs` walks every `NamespacedId` field that targets `ItemDef` —
    `IngredientRef.Item` (`CraftRecipeDef.Ingredients`, `EnchantDef.Catalyst`), `RecipeOutput.Item`,
    `ButcherYield.Item` (creatures and fish), `FishDef.BaitAffinity` keys, `CropOutput.Item`,
    `ItemDef.Spoilage.Result`, `CookFailure.Result` — against the full set of loaded item ids, and
    reports the ones that don't resolve with a Levenshtein ≤2 suggestion (verification case 4).
  - **Scoped to items only, deliberately** — see Decided without a spec. Skill
    (`SkillRequirement.Skill`, `ArtifactDef.ScalingSkill`, `WeaponDef.CombatSkill`,
    `ButcherYield.QualityFrom`), buff (`TagReaction.GrantBuff`), AI (`CreatureDef.Ai`), biome
    (`SpawnSpec.Biomes`), station, quest and moveset ids have no backing definition type yet
    (T-014/T-018/T-019/T-115...), so checking them now would just flag every one of them as
    unresolved; they stay unchecked until a type exists to check them against.
  - `LoadError` gained `Line` (1-based, 0 when no meaningful location exists) and `Suggestion`.
    Parse/deserialize errors get their line for free from `JsonException.LineNumber`; reference
    errors get theirs from a plain string search for the offending quoted literal in the file's
    raw text (`JsonLine.OfValue`) — ponytail: not a real JSON span tracker, upgrade if a duplicate
    literal in one file ever misattributes a line. Missing `id`/`name` (case 7) now reports line 1.
  - `LoadResult<T>` gained `SourcePaths`, parallel to `Definitions`, so a reference-resolution
    failure found after the fact can still be attributed to its file.
  - Verified: batchmode import exit 0, 0 compiler errors; EditMode **91/91 passed** (6 new resolver
    tests + 1 new line-number assertion on the existing case-7 test). Covers verification case 4
    end-to-end (bad ID → error + suggestion + line). Cases 5/8 (circular mod deps, patch conflicts)
    stay T-130 — no multi-mod loading exists to produce them yet.

- **T-014**: `DefRegistry` (SYS-CORE-01 §Load pipeline steps 9–10).
  - Files: `Assets/Scripts/Modding/Defs/DefRegistry.cs`, `Assets/Tests/EditMode/DefRegistryTests.cs`.
  - A static class — one of ARCHITECTURE.md's three allowed singletons, no instance — placed in
    `Isle.Modding`, not `Isle.Core` as the spec says. See Decided without a spec.
  - `Get<T>`/`TryGet<T>` key on `NamespacedId`; `Get<T>` throws `KeyNotFoundException` on a miss.
    `AllWithTag<T>` resolves the tag string to `TagRegistry`'s interned int once, then a single
    dictionary lookup — this is the first thing that actually queries T-010's flattened ancestor
    sets, so `AllWithTag<ItemDef>("fish")` also returns an item only tagged `fish/saltwater`.
    `All<T>` returns everything registered for that type. `Register<T>(LoadResult<T>)` indexes by
    id and by tag; `Freeze()` makes further `Register` calls throw; `Clear()` exists for test
    isolation only.
  - Tag indexing is reflection over a `string[] Tags` property found by name, cached per type — see
    Decided without a spec. `CraftRecipeDef`, `CookMethodDef` and `EnchantDef` have none and are
    simply never tag-indexed; `AllWithTag<T>` on one of those always returns empty.
  - Whether a failed `ReferenceResolver`/`SchemaValidator` check should block `Register`/`Freeze` is
    not decided — see Decided without a spec.
  - Verified: batchmode import exit 0, 0 compiler errors; EditMode **100/100 passed** (9 new).

- **T-017**: placeholder visual generator (ART_PIPELINE §Placeholders).
  - Files: `Assets/Scripts/Core/Util/PlaceholderVisuals.cs`, `Assets/Scripts/UI/PlaceholderIcons.cs`,
    `Assets/Tests/EditMode/PlaceholderVisualsTests.cs`, `Assets/Tests/EditMode/PlaceholderIconsTests.cs`.
  - `PlaceholderVisuals` (`Isle.Core.Util`) draws flat vector shapes at runtime — no pixel art, no
    baked assets — reusing `PlaceholderRigBuilder`'s (T-001) rounded-box signed-distance approach:
    `RoundedRect(w, h, fill, cornerRadius)` and `Circle(diameter, fill)` (a square `RoundedRect`
    whose corner radius is half its side, same trick the rig's Head part already uses).
    `ColorForTags(tags)` hashes the first tag (FNV-1a, not `string.GetHashCode` — not guaranteed
    stable across runs) into a hue, so the same tag always draws the same colour and a new tag needs
    no C# edit (Absolute Rule 4); no tags falls back to neutral grey. `AsSprite` wraps a texture with
    `Sprite.Create`.
  - Placed in `Isle.Core`, not behind a UI/system presentation class — see Decided without a spec.
  - `PlaceholderIcons.ItemIcon(ItemDef)` (`Isle.UI`) is the one concrete consumer wired up: a 32px
    rounded rect coloured by the item's own tags, exactly the fallback `ItemDef.Icon`'s own doc
    comment already named for T-017. Creature/weapon/tile/world-object shapes from ART_PIPELINE's
    table are not wired to anything — see Decided without a spec.
  - Verified: batchmode import exit 0, 0 compiler errors; EditMode **110/110 passed** (10 new).

- **T-018**: `SkillDef` + SYS-SKILL-01 formula rewrite — skills become data-driven and
  modder-addable.
  - Files: `Assets/Scripts/Data/SkillDef.cs`; `Assets/Scripts/Gameplay/Skills/{FocusCalculator.cs,
    XpCurve.cs, ActivityTracker.cs, SkillSet.cs}`; `Assets/Tests/EditMode/{FocusCalculatorTests.cs,
    XpCurveTests.cs, ActivityTrackerTests.cs, SkillSetTests.cs}`; `DataDefinitionTests.cs` (schema
    type roster, 9→10) and `docs/modding/SCHEMA.md` (new §Skills) updated to match.
  - `SkillDef.Pool` is a plain `string`, not an enum — exactly two legal values (`"production"`,
    `"combat"`) per the 2026-09-09 decision, but a string keeps JSON deserialization trivial and
    matches the existing `WeightDistribution.Type` precedent; the closed set is a validator concern,
    not a type-system one.
  - `FocusCalculator.Focus` takes `IEnumerable<SkillLevel>` (level + pool only), not a fixed array —
    this is the actual point of T-018 (BACKLOG: "not a rename job"). A second overload reads
    straight off a `SkillSet` + the loaded `SkillDef` roster for real call sites.
  - `SkillSet` is a `Dictionary<NamespacedId, int>` seeded from whatever `SkillDef`s exist, not a
    fixed 7- or 8-element array — a mod adding a skill needs no change to this class.
  - **Re-simulated all 9 verification cases against the beta 8-skill, 5/3-pool roster** (Production:
    gathering/fishing/cooking/crafting/enchanting; Combat: melee/ranged/magic) — written into
    `SYS-SKILL-01`'s own Verification section, not just the test file, so the spec stays the source
    of truth. Only case 4 (all-35s) and cases 6–9 (the n-scaling set) actually moved: 0.299/1.06×
    → 0.284/1.03× for case 4, since a third Combat member adds one more cross-pool interference
    term. Cases 1, 2, 3, 5 are numerically unchanged — their filler skills are novices, which
    contribute 0 regardless of how many extra pool members exist.
  - **`RustSystem` and its `docs/content/xp_table.md` per-action values are explicitly out of
    scope** — see Decided without a spec. `EnableSkillRust` defaults to false, has no caller, and
    its efficiency-debuff interpolation curve is an open question in the spec itself; building it
    now would mean inventing the missing number (Absolute Rule 3).
  - Verified: batchmode import exit 0, 0 compiler errors; EditMode **140/140 passed** (30 new).

- **T-019**: `BuffDef` + SYS-BUFF-01 spec sheet — buffs become a modder-extensible vocabulary
  instead of a bare `NamespacedId` with nowhere to look up what it does.
  - Files: `docs/specs/SYS-BUFF-01-buffs.md` (new), `docs/specs/README.md` (index row),
    `Assets/Scripts/Data/BuffDef.cs` (`BuffDef`, `BuffEffect`), `Assets/Scripts/Gameplay/Buffs/BuffSet.cs`,
    `Assets/Tests/EditMode/BuffSetTests.cs`, `DataDefinitionTests.cs` (schema type roster, 10→11)
    and `docs/modding/SCHEMA.md` (new §Buffs) updated to match.
  - `BuffEffect` copies `EnchantEffect`'s shape exactly (`Type` string + nullable `Value`) — same
    reasoning as T-012's original choice, restated in the spec rather than re-litigated.
  - `BuffSet` only tracks which `BuffDef.Id`s are active and until when (`Dictionary<NamespacedId,
    long>` of expiry in in-game minutes) — it does not apply any effect to a real gauge or formula,
    because `SYS-SURV-01` (Vitals) and `SYS-COMBAT-01` have no code yet (Phase 5/8). Those systems
    will read `BuffSet.ActiveBuffIds` and look up each `BuffDef`'s effects once they exist.
  - Re-granting an already-active buff **refreshes** its expiry (developer's call) rather than
    stacking or extending — `BuffSet.Grant` always overwrites.
  - The beta buff set's numbers came from three places: `SYS-COOK-01`'s existing tag-reaction table
    (five buffs' effect *types*), `SYS-SURV-01`'s existing disease numbers (`food_poisoning`, reused
    verbatim, not redefined), and this task filling the remaining gaps — see Decided without a spec.
  - Verified: batchmode import exit 0, 0 compiler errors; EditMode **147/147 passed** (7 new).

- **T-015**: F5 hot reload (SYS-CORE-01 §Hot reload) — the first orchestration code wiring the
  eleven definition types together at all; nothing before this actually called `DefinitionLoader`
  → `ReferenceResolver` → `DefRegistry` in sequence for real content, only EditMode tests exercised
  each piece alone.
  - Files: `Assets/Scripts/Modding/Defs/DefinitionLoader.cs` (adds `ILoadResult`),
    `Assets/Scripts/Modding/Defs/DefRegistry.cs` (adds `Reload<T>`, `CopyProperties`,
    `RebuildTagIndex`), `Assets/Scripts/Modding/Defs/DefinitionBootstrap.cs` (new),
    `Assets/Scripts/Modding/Editor/{Isle.Modding.Editor.asmdef,DefinitionHotReload.cs}` (new),
    `Assets/Tests/EditMode/DefRegistryTests.cs` (5 new `Reload` cases),
    `Assets/Tests/EditMode/DefinitionBootstrapTests.cs` (new, 3 cases).
  - `DefinitionBootstrap.Load(contentRoot)` names all eleven types explicitly (no reflection loop —
    matches `ReferenceResolver`'s existing style, and there is no common base beyond `IDefinition`
    to loop over generically): loads each from its `ARCHITECTURE.md`-listed subfolder, runs
    `ReferenceResolver.ResolveItemRefs`, `Register`s all eleven, then `Freeze()`s. `Reload(contentRoot)`
    repeats load + resolve but calls `DefRegistry.Reload<T>` instead.
  - **The init-only-vs-hot-reload tension**: `IDefinition` properties are compiler-enforced
    init-only (T-011, `DataProperties_AreInitOnly_NeverSettable`), but the spec's "runtime objects
    hold `ItemDef` by reference, so reload propagates automatically" needs the *same instance*
    updated, not replaced. `modreq(IsExternalInit)` is a C# compiler restriction on ordinary
    assignment syntax at the call site — it does not stop `PropertyInfo.SetValue` from writing an
    init-only property directly, which is well-established, unremarkable reflection behaviour, and
    already has a precedent in this codebase (`DefRegistry.TagsOf<T>`'s own reflection property
    access). `DefRegistry.Reload<T>` copies every public property from the incoming def onto the
    existing instance for an id that already exists, inserts new ids fresh, removes ids missing
    from the new load, and rebuilds the tag index from scratch (no incremental-update story exists
    for it, and `Register`'s own indexing loop is additive-only, so reusing it verbatim on a second
    call would double-count).
  - **`Reload<T>` is the one sanctioned exception to "no `Register` after `Freeze()`"** — it does
    not check `_frozen` at all, by design: the guard exists to stop *other* callers mutating
    definitions, not to block the hot-reload mechanism itself.
  - Multi-mod orchestration (scan mods, dependency order, patches — §Load pipeline steps 1–3/6,
    `ModLoader`/`LoadOrderResolver`/`PatchApplier`) is **T-130**, not touched — `DefinitionBootstrap`
    takes one content root today, exactly the shape T-130 will eventually call once per mod.
  - New `Isle.Modding.Editor` asmdef (`includePlatforms: ["Editor"]`, references only
    `Isle.Modding`) hosts `DefinitionHotReload`'s `[MenuItem("Isle/Reload Definitions _F5")]` — the
    `_` prefix binds plain `F5`, matching the spec's literal wording.
  - Verified: batchmode import exit 0, 0 compiler errors; EditMode **155/155 passed** (8 new).
    Covers editing a file on disk and reloading into the same object reference
    (`DefinitionBootstrapTests.Reload_ChangedFile_UpdatesSameReference`), new/stale id handling and
    tag-index rebuild on `Reload`, and that `Reload` (unlike `Register`) works after `Freeze()`.
  - **T-016 confirmed (2026-09-10)**: developer edited a test item's `name` field under
    `Assets/StreamingAssets/definitions/items/` and pressed `F5` in a live Editor session; console
    logged `[Isle] Definitions reloaded from .../definitions.` with no errors and no recompile.
    **Phase 1 is fully done (10/10).**

- **T-020**: FishNet bootstrap, listen server connection (SYS-NET-01 §Transports) — Phase 2, the
  first Phase 2 task.
  - Files: `Packages/manifest.json` (adds `com.firstgeargames.fishnet` 4.7.2 via git URL, pinned
    to match `ADR-001`'s "4.7.2R"), `Packages/packages-lock.json` (resolved lock entry, automatic),
    `ProjectSettings/ProjectSettings.asset` (`scriptingDefineSymbols.Standalone: FISHNET;FISHNET_V4`,
    added automatically by FishNet's own installer), `Assets/DefaultPrefabObjects.asset` (+`.meta`,
    auto-generated empty `DefaultPrefabObjects` — `NetworkManager`'s required spawnable-prefabs
    asset, no spawnable prefabs exist yet so `_prefabs: []`),
    `Assets/Scripts/Networking/Isle.Networking.asmdef` (added a `FishNet.Runtime` reference to the
    empty placeholder that already existed on `main`), `Assets/Scripts/Networking/IsleNetworkManager.cs`
    (new), `Assets/Scenes/SampleScene.unity` (new `IsleNetworkManager` GameObject),
    8× `Assets/Plugins/SystemTextJson/*.dll.meta` (bugfix, see below).
  - **Blocking prerequisite bug found and fixed: a T-012 defect, not new scope.** FishNet's Synapse
    transport ships its own `Microsoft.Bcl.AsyncInterfaces.dll`, which collided (`CS0433`, ambiguous
    type) with the identically-named DLL T-012 vendored for `System.Text.Json`. Root cause: T-012's
    eight `.meta` files were hand-written down to the minimal two-line form and never had Unity's
    Plugin Importer "Auto Referenced" checkbox turned off, so every vendored DLL was silently
    auto-added to every assembly, including ones that bring their own copy. Fixed by rewriting all
    eight `.meta` files to the full `PluginImporter` YAML block with `isExplicitlyReferenced: 1`
    (guids preserved). This was a latent T-012 bug that any second DLL with a name collision would
    have hit; FishNet's Synapse transport was just the first thing to trip it. Tugboat (the
    transport actually used) does not carry this DLL and would not have surfaced the bug at all.
  - `IsleNetworkManager` (`Isle.Networking`) `[RequireComponent]`s FishNet's own `NetworkManager`
    and `Tugboat`, wires `TransportManager.Transport = Tugboat` in `Awake`, then calls
    `ServerManager.StartConnection()` and `ClientManager.StartConnection()` in `Start` — a listen
    server, so the host's own client is the same session co-op joins later, per SYS-NET-01 and the
    2026-09-06 "solo beta already runs on the multiplayer code path" decision. Steam P2P is a
    second transport, deliberately not touched here — T-025, Phase 10.
  - Verified: batchmode compile-only run **0 `error CS`** (retried once after an unrelated flaky
    SIGBUS crash mid-run, clean on retry — not caused by these changes); full EditMode suite
    **155/155 passed**, twice (once after the `.meta` fix, once again after wiring the scene, to
    rule out a regression from either step).
  - **No EditMode test written for `IsleNetworkManager` — see Decided without a spec.** Manual
    verification substitutes: press Play, check the Console for FishNet's own
    `"Local server is started for Tugboat."` and `"Local client is started for Tugboat."` lines.
  - **T-020 confirmed (2026-09-10)**: developer pressed Play in a live Editor session; Console
    showed `Local server is started for Tugboat.`, `Remote connection started for Id 0.` (the local
    client connecting to the local server), then `Local client is started for Tugboat.` — no
    errors. Listen server bootstrap works end to end.

## In progress / unfinished

(none)

## Next

**T-021** (server-authoritative movement + client prediction) is next in Phase 2 —
`docs/BACKLOG.md`. Read `SYS-NET-01` for the authority split before starting.

**Phase 1 is fully done.** T-015 and T-016 both confirmed 2026-09-10 — the developer manually
verified F5 hot reload in a live Editor session.

**RustSystem stays unbuilt** — deferred out of T-018's scope (see Completed, T-018 entry, and
Decided without a spec). Pick it up only once the efficiency-debuff interpolation curve and the
per-action XP table (`docs/content/xp_table.md`) have real numbers; nothing currently calls
`GameConfig.EnableSkillRust` so nothing is blocked on it.

**A new backlog item, T-028** (SYS-DIFF-01 spec + world difficulty setting), was added
2026-09-10 at the developer's request — see `BACKLOG.md` Phase 2. No spec sheet yet; it touches
`SYS-SURV-01` (T-050) and `SYS-COMBAT-01` (T-110), neither of which exist in code yet, so it isn't
blocking anything right now.

## Decided without a spec

> ⚠️ Everything here is **debt owed to the spec sheets**. Let it accumulate and balancing becomes impossible.

- ### 2026-09-10 — T-020: fixed a T-012 bug en route (Plugin Importer auto-reference), scoped Tugboat-only, wired the scene directly, skipped an EditMode test
  Four judgment calls, none in `SYS-NET-01` or `BACKLOG.md`'s one-line T-020 scope:
  - **The `CS0433` fix (`isExplicitlyReferenced: 1` on all 8 `Assets/Plugins/SystemTextJson/*.dll.meta`)
    is a bugfix, not scope creep.** FishNet's Synapse transport carries its own
    `Microsoft.Bcl.AsyncInterfaces.dll`, which collided with T-012's identically-named vendored copy
    because T-012's `.meta` files never disabled Unity's Plugin Importer auto-reference. Fixing the
    `.meta` files was a required prerequisite to get anything compiling at all — Tugboat itself
    doesn't carry the colliding DLL, so this bug would have stayed latent until some other package
    happened to ship the same DLL name. Could not be scripted (a C# Editor fixup script can't run
    via `-executeMethod` while the very compile error it targets is blocking all compilation), so
    the 8 `.meta` files were hand-edited as plain YAML text instead.
  - **Tugboat only; Steam P2P stays out.** `ADR-001`/`SYS-NET-01` name FishNet without committing to
    a transport for T-020 specifically; Tugboat (FishNet's built-in UDP transport) is what "listen
    server" needs for a LAN/localhost host, and Steam P2P is its own ticket (T-025, Phase 10) with
    its own SDK dependency. Building both now would be scope creep into a Phase 10 task.
  - **`IsleNetworkManager` was wired directly into `Assets/Scenes/SampleScene.unity`, not left as an
    unused class.** No scene-bootstrap precedent existed to follow — `DefinitionBootstrap` (T-015)
    is confirmed **not** wired into any scene anywhere in the codebase, so Phase 1 never actually
    established a "how does gameplay code start at runtime" pattern. BACKLOG.md's T-020 wording
    ("listen server connection") reads as something that has to actually happen when the game runs,
    not just compile, so a GameObject carrying `IsleNetworkManager` (which
    `[RequireComponent]`-adds FishNet's own `NetworkManager` and `Tugboat`) was added to the
    project's one and only scene (`ProjectSettings/EditorBuildSettings.asset` lists no others).
  - **No EditMode test for `IsleNetworkManager`.** `docs/TESTING.md` states plainly that
    MonoBehaviour/network-state code is untestable via EditMode, which is also why T-016's def gate
    was a manual check rather than a test. Same policy applied here: verification is the developer
    pressing Play and reading FishNet's own `"Local server is started for Tugboat."` /
    `"Local client is started for Tugboat."` console lines, not an automated EditMode assertion.

- ### 2026-09-10 — durability check (developer request, before T-015): weapons ✓, food ✓ (different mechanic), equipment ✗ (no schema yet)
  The developer asked, before starting the next task, whether weapons/equipment/food all have
  durability specified. Findings, not a code change:
  - **Weapons: yes, fully specified.** `WeaponDef.Durability` is a non-nullable `int` — every
    weapon always has one. `SYS-CRAFT-01`'s Quality table gives a per-tier durability multiplier
    (Crude 0.60× → Master 2.20×) and a repair formula
    (`newMaxDurability = previousMaxDurability * RepairDecay(0.92)`); `SYS-ART-01`'s Field Repair
    ability restores 35% of it.
  - **Food: deliberately does not use durability — not a gap.** `ItemDef.Durability` is nullable,
    and `SCHEMA.md`'s own food example (`cod_fillet`) sets it to `null`. Food decays through a
    separate mechanic instead — `ItemDef.Spoilage` (`BaseHours`, `TempFactor`, `Result`), matching
    `ARCHITECTURE.md`'s `ItemStack` example, which tracks `Freshness` for perishables, not
    `Durability`. Time/temperature decay into a spoiled result item is a different concept from
    wear-through-use-and-repair, and the schema already treats them as two fields, not one.
  - **Equipment/armor: no schema exists yet — a real gap, not answered here.** None of the eleven
    `IDefinition` types represent armor; `GLOSSARY.md` has only a glossary row (`EquipSlot`) with no
    backing type. `SYS-ART-01`'s Field Repair text ("restore ... to your own or an ally's
    equipment") implies armor durability is intended eventually, but there is nothing to specify it
    on today. Tracked only as **`BACKLOG.md` T-044** (Equipment slots + bag expansion, Phase 4, not
    started). Per Absolute Rule 3, flagged here rather than inventing an `ArmorDef` mid-T-015.

- ### 2026-09-10 — T-015: hot reload writes onto the existing instance via reflection, not replacement
  SYS-CORE-01 §Hot reload requires reload to "propagate automatically" through existing references,
  but `IDefinition` properties are compiler-enforced init-only (T-011). `DefRegistry.Reload<T>`
  resolves this by copying the incoming def's property values onto the **already-registered
  instance** with `PropertyInfo.SetValue` rather than swapping the dictionary entry — the init-only
  restriction is enforced by the C# compiler at ordinary assignment call sites, not by the CLR, so
  reflection can write it directly (same category of reflection access as the existing
  `DefRegistry.TagsOf<T>`). This is also why `Reload<T>` is allowed to run after `Freeze()` — the
  guard's purpose is to block *other* code from mutating definitions, not the hot-reload path
  itself, which is what keeps the registry live in the first place. Full writeup in Completed,
  T-015 entry.

  **Also decided, same task:** no orchestration code existed anywhere to run the load pipeline
  against real content before this — `DefinitionBootstrap` is new and scoped to Isle's own single
  content root (§Load pipeline steps 4/5/7/9/10 only); multi-mod scanning/ordering/patching (steps
  1–3/6) stays T-130, deliberately not pulled forward.

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
  - **`c(i,j)` is a hardcoded 2×2** over Production and Combat. **Resolved 2026-09-09 — no.** The
    developer confirmed beta ships exactly two pools; a mod may not declare a new pool, and every
    modded skill must join Production or Combat. `c` stays a fixed 2×2, no N×N generalisation needed.
  - **Pool asymmetry.** Production has 5 members, Combat 3 (post-Enchanter-split). Same-pool
    interference is `c = 1.00` against Combat's cross-pool `0.35`, so a mod loading its new skills
    into one pool quietly taxes that pool's specialists. **Resolved 2026-09-09** — re-simulated
    against the 5/3 split in `SYS-SKILL-01` §Verification; only case 4 (all-35s) moved
    (0.299/1.06× → 0.284/1.03×), since one more Combat member adds one more cross-pool term.
  - **The 9 verification cases were written against a fixed 7-element array** in a fixed order.
    **Resolved 2026-09-09** — `FocusCalculator.Focus` (T-018) takes any-length `SkillLevel`
    collections, and the cases are restated in `SYS-SKILL-01` §Verification against the 8-skill
    beta roster.

  **All of questions 1–3 are now done as of T-018/T-019** — T-018 landed 2026-09-09
  (`SkillDef`, `FocusCalculator`, `XpCurve`, `ActivityTracker`, `SkillSet`); T-019 (`BuffDef`,
  `BuffSet`, `SYS-BUFF-01`) landed 2026-09-10.

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

- ### 2026-09-10 — T-019's five open questions, answered
  `SYS-BUFF-01`'s draft (see its own Open questions when it was first written) had five gaps.
  Three were the developer's explicit call, two were delegated ("네가 알아서 정해" — you decide):

  | Question | Answer | Who decided |
  |---|---|---|
  | Duration for `steady_hand`/`endurance`/`hydrated`/`iron_gut`/`cold_resist` | 180 min (3 h), reusing `warm`'s already-fixed number rather than inventing five separate ones | delegated |
  | `cold_resist`'s mechanic | shifts both hypothermia bands (`SYS-SURV-01`, 33/28) down 5°: warning at 28, HP-drain at 23 | delegated |
  | `warm`'s hookup into the Temperature formula | multiplies `TempApproachRate` (2.0/min) by 0.5 wholesale, both directions | developer |
  | `iron_gut`'s "+40% disease resistance" | ×0.60 multiplier on `SYS-SURV-01`'s water-source disease chance | developer, confirming the spec draft's own reading |
  | Re-granting an already-active buff | refreshes duration; never stacks or extends | developer |

  The two delegated numbers (the reused 3 h duration, the −5° `cold_resist` shift) are flagged in
  `SYS-BUFF-01` itself as still-unbalanced placeholders — nothing plays them yet since
  `SYS-SURV-01`/`SYS-COMBAT-01` have no code, so there's nothing to feel wrong until those exist.

- **T-012: `DefinitionLoader`/`SchemaValidator` live in `Isle.Modding`, not `Isle.Core` as
  SYS-CORE-01 §Location says.** That note predates the `Isle.Data` split — a generic loader has to
  constrain on `IDefinition`, which is in `Isle.Data`, and `Isle.Core` has `"references": []`
  (ARCHITECTURE.md's own table). Putting the loader in `Core` would mean `Core` referencing `Data`,
  reversing the one-way dependency graph the asmdefs enforce. `Isle.Modding` already references
  both, and CLAUDE.md's own Layout table calls that folder "mod loader, schema validation,
  patches" — an exact match. Same reasoning will place `ReferenceResolver` (T-013) and
  `DefRegistry` (T-014) there too. SYS-CORE-01 §Location should be corrected to match.

- **T-013: `ReferenceResolver` only checks references to `ItemDef`.** SYS-CORE-01 §Load pipeline
  step 7 says "verify every referenced id exists" without listing which fields that covers. Walking
  every `NamespacedId` field in the nine types turns up mostly skill ids (`SkillRequirement.Skill`,
  `ArtifactDef.ScalingSkill`, `WeaponDef.CombatSkill`, `ButcherYield.QualityFrom`), plus one buff
  reference (`TagReaction.GrantBuff`), one AI reference (`CreatureDef.Ai`), one biome reference
  (`SpawnSpec.Biomes`), and station/quest/moveset/minigame ids — **none of which have a backing
  definition type yet.** Skills and buffs are T-018/T-019; AI profiles are T-115; biomes, stations,
  quests, movesets and minigames have no ticket at all. Resolving them now would mean either
  flagging every single one as unresolved (there is nothing loaded to check them against) or
  inventing a temporary allow-list — both would be guessing at a design that isn't decided.
  Only `ItemDef` is both referenced by other types and itself loadable today, so that's what's
  checked. Each excluded field is named in `ReferenceResolver`'s own doc comment so this isn't a
  silent gap; revisit as each target type gets built.

- **T-013: the `[modid]` prefix in SYS-CORE-01's required error format is dropped for now.** The
  spec's example is `[coolmod] definitions/recipes/harpoon.json:14`, but nothing in the codebase
  has a concept of "which mod loaded this file" yet — `DefinitionLoader.LoadAll` takes one
  directory, not a mod manifest. Multi-mod loading is T-130. `LoadError.ToString()` renders just
  `path:line` until a mod id actually exists to put in front of it.

- **T-014: `DefRegistry` placed in `Isle.Modding`, confirmed against `ARCHITECTURE.md`'s asmdef
  table.** The spec says "Singleton in `Isle.Core`", but `Get<T>`/`TryGet<T>` return `IDefinition`
  (`Isle.Data`), and `Isle.Core`'s asmdef has `"references": []` — it cannot see `Isle.Data` at
  all. Same one-way-dependency reasoning as the T-012 entry above; `Isle.Modding` already
  references both. SYS-CORE-01 §Location should be corrected to match.

- **T-014: tag indexing is reflection over a `Tags` property, not a new `IDefinition` member.**
  `CraftRecipeDef`, `CookMethodDef` and `EnchantDef` have no `Tags` field — widening `IDefinition`
  to require one would force it onto three types nothing has ever asked to carry it, and
  `IDefinition`'s shape is T-011's contract, not this task's to change. `DefRegistry` instead looks
  up a public `string[] Tags` property by name per type, caching the `PropertyInfo` (or its
  absence) the first time each type is registered. A type without one is simply never tag-indexed —
  `AllWithTag<T>` on it always returns empty, not an error.

- **T-014: `Register<T>` does not check `ReferenceResolver`/`SchemaValidator` errors before
  indexing.** Flagged as undecided when this was written into Next (ask, don't invent). Today
  `Register<T>` indexes whatever `LoadResult<T>.Definitions` contains; skipping a definition that
  failed a check, or refusing to `Freeze()` at all if any load produced errors, is the caller's job
  until the developer decides which policy is wanted.

- **T-017: `PlaceholderVisuals` lives in `Isle.Core`, not behind a UI/system presentation class.**
  ARCHITECTURE.md's Presentation boundary says every visual sits behind a component in `UI` or the
  owning system's own presentation class, but the shape generator itself has to be reachable from
  every layer that draws something — `UI` for icons, `Gameplay`/`Combat`/`World` for creatures,
  weapons, tiles once those exist — and `Core` is the only asmdef all of them already reference
  (`UI → Gameplay/Combat → World → ... → Core` is a chain, not a diamond, so no layer in the middle
  can host something every other layer needs). The boundary still holds for what calls it: the
  generator returns a `Texture2D`/`Sprite`, nothing here touches a `SpriteRenderer` or a
  `GameObject` — that wiring is each future system's own presentation class, e.g. `PlaceholderIcons`
  in `Isle.UI` for items.

- **T-017: only item icons are wired up; creatures/weapons/tiles/world objects are not.**
  ART_PIPELINE §Placeholders' shape table lists all five, but nothing outside items has an owning
  presentation class yet to attach a placeholder to — Phase 3 (world), 4 (inventory) and 8 (combat)
  haven't started. Wiring them now would mean guessing at a component that doesn't exist. The shape
  primitives (`RoundedRect`, `Circle`) are generic enough that each one is a few lines when its
  system actually gets built.

- **T-017: the "first letter" part of the item-icon placeholder is skipped.** ART_PIPELINE's table
  wants a rounded rectangle, a tag colour, *and* the item's first letter. Drawing text without a
  font system means hand-rasterizing glyphs pixel-by-pixel, which is real complexity for a shape
  that's thrown away at Phase 11 anyway. Shape and colour alone already satisfy Absolute Rule 7
  ("no later phase ever waits on a sprite") — a letter is a legibility nicety, not the requirement.
  Left for whichever Phase 4 inventory UI component actually renders the icon, which can overlay a
  `TextMeshPro` label far more cheaply than this generator hand-drawing one.

- **T-017: ART_PIPELINE says "puts the generator in `Art/Placeholder/`"; it isn't.** That folder is
  where the T-001 rig's *baked PNG output* lives — files an Editor tool wrote once and Unity imports
  as assets. Item/creature/etc. shapes here are generated at runtime from a live definition's tags,
  which aren't known until mods load, so there is nothing to bake or put in `Art/`. The code follows
  CLAUDE.md's Layout table instead (`Core/Util`, `UI`) — same kind of doc disagreement as T-011's
  type roster and the rig's bone names below, resolved the same way: pick the one that matches how
  the system actually has to work, and record it here.

- **T-018: `SkillDef.Pool` is a `string`, not an enum.** Exactly two values are legal
  (`"production"`, `"combat"`), fixed by the developer for beta, so an enum would be defensible —
  but nothing in this codebase's JSON pipeline registers a `JsonStringEnumConverter`
  (`DefinitionLoader.BuildOptions`), and adding one to support one field risked more than it saved.
  A plain string matches the `WeightDistribution.Type` precedent (T-011) and pushes the closed-set
  check to `SchemaValidator`, which is where every other JSON-shape rule already lives. Not yet
  enforced there — `SchemaValidator` has no "value must be one of" check for any field today.
- **T-018: `RustSystem` was not built.** `SYS-SKILL-01`'s Location section lists it, but
  `EnableSkillRust` defaults to false, nothing calls it, and its efficiency-debuff interpolation
  curve is the spec's own listed open question — building it now would mean inventing the missing
  number (Absolute Rule 3). `decay(XP)` and the hard rules that don't depend on that curve are
  fully specified whenever this gets picked up; not started.
- **T-018: `ActivityTracker.MedianActivePlayers` rounds a fractional median to the nearest int.**
  `FocusCalculator.ActivePlayerFactor` only has bands for integer n (1/2/3/≥4); a 7-day window with
  an even number of recorded days can produce a median like 2.5, which the spec never addresses.
  Rounds to nearest (away from zero on a tie) rather than flooring or ceiling — arbitrary but
  symmetric, and only reachable during a world's first 6 days before the window fills. Flagged for
  the developer to confirm or override; not blocking anything.

- **T-011: the type roster follows `SCHEMA.md`, not `ARCHITECTURE.md`, and the two disagreed.**
  ARCHITECTURE §Folders listed ten Data types; SCHEMA documents nine, and the sets do not match.
  `CropDef` is in SCHEMA but was missing from ARCHITECTURE's list — **added**. `SkillDef` and
  `BuffDef` were in ARCHITECTURE's list but have no schema anywhere — **`SkillDef` written
  2026-09-09 (T-018), `BuffDef` written 2026-09-10 (T-019)**. ARCHITECTURE §Folders now names the
  eleven that exist.

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
   Production stays at 5 members, **Combat grows from 2 to 3** — this changed the pool-asymmetry
   math, which was written for 5/2. **Resolved 2026-09-09** — T-018 did the formula rewrite and
   re-simulated all 9 verification cases against the 5/3 split (see Completed, T-018 entry).
   Remaining backlog debt from the taxonomy split: `BACKLOG.md` T-106 (move enchant application off Crafting —
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
| #8 | feature/T-011-data-skill-taxonomy | T-011 + skill/profession taxonomy | [PR #8](https://github.com/yhw1737/surv/pull/8) — **merged to main** |
| #9 | feature/T-012-definition-loader | T-012 | [PR #9](https://github.com/yhw1737/surv/pull/9) — **merged to main** |
| #10 | feature/T-013-reference-resolver | T-013 | [PR #10](https://github.com/yhw1737/surv/pull/10) — **merged to main** |
| #11 | feature/T-014-def-registry | T-014 | [PR #11](https://github.com/yhw1737/surv/pull/11) — **merged to main** |
| #12 | feature/T-017-placeholder-visuals | T-017 | [PR #12](https://github.com/yhw1737/surv/pull/12) — **merged to main** |
| #13 | feature/T-018-skill-def | T-018 | [PR #13](https://github.com/yhw1737/surv/pull/13) — **merged to main** |
| #14 | docs/T-019-buff-spec | T-019 | [PR #14](https://github.com/yhw1737/surv/pull/14) — **merged to main** |
| — | feature/T-015-hot-reload | T-015 | implemented and verified, not committed |
| #16 | feature/T-020-fishnet-bootstrap | T-020 | [PR #16](https://github.com/yhw1737/surv/pull/16) — awaiting merge |

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
| Def gate T-016 | 1 | ✅ passed | 2026-09-10, developer confirmed F5 hot reload in a live Editor session |
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

- **T-012**: `DefinitionLoader` + `SchemaValidator` (SYS-CORE-01 §Load pipeline steps 4–5).
  - Files: `Assets/Scripts/Modding/Defs/{NamespacedIdJsonConverter,DefinitionLoader,SchemaValidator}.cs`,
    `Assets/Scripts/Modding/Isle.Modding.asmdef` (vendored references), `Assets/Plugins/SystemTextJson/*.dll`
    (8 files — `System.Text.Json` 8.0.5 and its netstandard2.0 dependency chain, none of them in the
    project before this task), `Assets/Tests/EditMode/DefinitionLoaderTests.cs`
  - `NamespacedIdJsonConverter` implements `Read`/`Write`/`ReadAsPropertyName`/`WriteAsPropertyName`
    (the last pair for `FishDef.BaitAffinity`'s dictionary key) and turns a parse failure into a
    `JsonException` carrying `NamespacedId.TryParse`'s own message. `HandleNull` is on, because
    `ArtifactDef.CombatSkill` is always `null` in JSON and must deserialize to `default`, not throw.
  - `DefinitionLoader.LoadAll<T>(directoryPath)` reads every `*.json` under a directory
    (`PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower`, no attributes on the POCOs, as T-011
    left it), aggregating failures across the whole batch rather than stopping at the first file or
    the first field. Per file: missing `id`/`name` and a JSON parse exception are errors that drop
    the file; an unrecognized top-level field is a warning that does not.
  - `SchemaValidator` does the four checks the POCOs' own doc comments deferred here: `IngredientRef`
    exactly-one-of `item`/`tag` (`CraftRecipeDef.Ingredients`, `EnchantDef.Catalyst`); `[min,max]`
    bands are length 2 (`ButcherSpec.ConditionRange`, `HabitatSpec.Depth`/`WaterTemp`);
    `ArtifactDef.CombatSkill`/`GrantsCombatXp` are unset (Absolute Rule 5); an `EnchantEffect`
    carries the fields its `type` needs. An unrecognized `EnchantEffect.Type` is left alone —
    SYS-CRAFT-01 §Open questions leaves the type list undecided, so hard-failing would foreclose it.
  - Verified: batchmode import exit 0, **0 compiler errors — no `CS0433` conflict between the
    vendored DLLs and Unity's own Mono BCL**, which was the open risk going in; EditMode **85/85
    passed** (15 new). Covers SYS-CORE-01 verification case 1 through the loader (not just
    `NamespacedId` directly), case 6 (unknown field → warning, def still loads) and case 7 (missing
    `id`/`name` → error), plus all four `SchemaValidator` checks in both pass and fail form. Cases
    4/5/8 (multi-mod) and case 1's typo-suggestion half stay T-013/T-130.
  - Fixtures are temp directories written per test (`Assets/StreamingAssets/definitions/` is still
    empty — no real content JSON exists yet), so nothing here depends on content that doesn't exist.

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
