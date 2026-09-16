# Project state

> **Read this first at session start. Update it at session end.**
> AI has no memory between sessions — this file is the only continuity.

## Header

- Last updated: **2026-09-16**
- Phase: **Phase 3 done except T-031 (skipped, needs spec) and T-034 (deferred to Phase 4/6). Phase 4 (inventory): T-040 through T-045 all implemented — T-045 not yet committed.**
- Task: **T-045 (server-authoritative inventory sync + rollback UI) implemented, committed on `feature/T-045-inventory-network`, and open as [PR #20](https://github.com/yhw1737/surv/pull/20).** New `InventoryNetwork` (`NetworkBehaviour`) holds server-authoritative bag + equip slots for a player's own body only (warehouse/crates stay local, Phase 6 scope); `GridView`/`EquipSlotView` gained a `Network` property, `DragHandler`/`EquipDragHandler` branch on it with the local (non-networked) path byte-for-byte unchanged. Wired onto `player_rig_placeholder.prefab`. Batchmode EditMode run **238/238**, 0 failures — see In progress/unfinished and Next for the manual-test checklist. T-040~T-044 remain merged into `main` — [PR #19](https://github.com/yhw1737/surv/pull/19). Developer pressed Play repeatedly and reported bugs/requests each time, all fixed the same day: round 1 (unstyled Auto-Sort bar, no item/panel labels), round 2 (NullReferenceException crash, grids overlapping, item sizes not reflected), round 3 (drop position mismatched the dragged icon, tooltip flickering), round 4 (sword equip-slot fixture bug, Q/E rotate-in-place feature — later corrected), round 5 (Q/E moved to rotate-while-dragging instead, tooltip z-order behind newer UI, tooltip now follows the cursor), round 6 (backpack-unequip UI desync, split-drag visual gap + merge-on-drop-back, Q/E reliability fix, Ctrl+click-unequip + equipped-item tooltip), round 7 (Ctrl+click merge-onto-stack, dragged icon z-order above other panels, backpack contents can now bulk-move to the warehouse) — see Next for all seven rounds and the checklist. `docs/BACKLOG.md` also gained a Tier 2/3 modding note (dragselect/AllowTool-style mods need a DLL loader + Harmony-style patch mechanism, not just a richer JSON schema) — parked, not built, per Absolute Rule 6. Developer then actually ran the network checklist (one build + one Editor instance) and reported 6 findings; one was a real bug — `DragHandler.TryDrop`'s networked guard only checked the drag source, not the destination, so a local item dragged into the Networked Bag silently bypassed the server — now fixed to check both sides. The other 5 are expected/cosmetic/unrelated-system, see Next for the full breakdown. Developer then
spotted two more gaps in the checklist/harness itself: the Networked Bag had no way to be seeded
with a test item (fixed — `InventoryNetwork.SeedTestItems`), and equipping the backpack into the
Networked Bag's own row doesn't open a "Backpack (opened)" panel (developer decided: leave out of
scope for T-045). Developer then asked for this to be committed and a PR opened.
- Branch: **feature/T-045-inventory-network** (branched from `main` post-PR#19; local feature branches for T-020/T-021/T-030/T-040 deleted post-merge)
- Pending commit: **none — developer requested commit + PR (2026-09-16), see below for the link
  once opened.**

## Progress

```
stage 1 · placeholders
Phase 0  project setup        [x] 3/3   T-000..T-002
stage 2 · solo beta
Phase 1  foundation           [x] 10/10 T-010..T-019  ← done
Phase 2  netcode skeleton     [x] 2/2   T-020..T-021 ← done, both merged into main (PR #16, #17, 2026-09-15)
Phase 3  world                [ ] 5/7   T-030, T-032, T-033, T-035, T-036 merged (PR #18, 2026-09-15); T-031 skipped, T-034 deferred
Phase 4  inventory            [x] 6/6   T-040..T-044 merged into main (PR #19); T-045 open (PR #20, 2026-09-16)
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

- **T-021**: server-authoritative movement + client prediction (SYS-NET-01 §Authority — Position
  row, "server-validated" + prediction, 20 Hz). Branch `feature/T-021-server-movement` (stacked on
  `feature/T-020-fishnet-bootstrap`, which is still PR #16, unmerged).
  - `Assets/Scripts/Networking/PlayerMovement.cs` (new) — `TickNetworkBehaviour` with
    `[Replicate]`/`[Reconcile]`, FishNet's own Prediction v2 template (mirrors the package's
    `CharacterControllerPrediction` demo). Moves `transform.position` directly at
    `BaseSpeed = 4.2` (SYS-CHAR-01 §Movement; GLOSSARY §Units: 1 Unity unit = 1 tile), diagonal
    input normalized. Reads WASD via the new Input System (`Keyboard.current`) — the project has
    `activeInputHandler: 1` (Input System Package only), so `UnityEngine.Input` throws at runtime.
  - `Assets/Scripts/Networking/Isle.Networking.asmdef` — added `Unity.InputSystem` reference.
  - `Assets/Prefabs/Characters/player_rig_placeholder.prefab` — added `NetworkObject` and
    `PlayerMovement` components to the existing T-001 rig prefab (no rig-internal changes).
  - `Assets/DefaultPrefabObjects.asset` — auto-updated by FishNet's own `AssetPostprocessor`
    (`Generator.cs`) to register the prefab now that it carries a `NetworkObject`; not a manual edit.
  - `Assets/Scenes/SampleScene.unity` — removed the static, hand-posed `player_rig_placeholder`
    instance (it would otherwise double-spawn once the prefab is a `NetworkObject`); added
    FishNet's built-in `PlayerSpawner` component to the `IsleNetworkManager` object so each
    connecting client gets its own instance, instead of writing spawn code from scratch.
  - No new/changed test files — see "Decided without a spec" for why.
  - Verified: batchmode compile **0 `error CS`**, clean exit; full EditMode suite **155/155
    passed** (no new tests added, per the "Decided without a spec" entry).
  - **T-021 confirmed (2026-09-15)**: developer pressed Play in a live Editor session, moved with
    WASD — rig moves smoothly, no console errors. **Phase 2 is fully done (2/2).**

- **T-030**: in-game clock (SYS-WORLD-01 §Time). Branch `feature/T-030-world-clock`, stacked on
  `feature/T-021-server-movement` (no code dependency — pure doc continuity, since `main` doesn't
  yet carry T-020/T-021's `PROJECT_STATE.md` updates; see Decided without a spec).
  - `Assets/Scripts/World/Time/WorldClock.cs` (new) — plain C# (no `MonoBehaviour`, no FishNet),
    matching the `Isle.Gameplay.Skills` pure-formula pattern. `MinutesPerDay = 1440`,
    `MinutesPerRealSecond = 1.2` (1 in-game day = 20 real minutes), both verbatim from the spec.
    `DayPhase` enum (Dawn/Day/Dusk/Night) with `PhaseAt(minuteOfDay)` matching the spec's boundary
    table (dawn 300–420, day 420–1020, dusk 1020–1140, else night). Internal accumulator is a
    `double` (`_totalMinutes`), truncated to `long` only on read, to avoid rounding drift across
    many small `Tick()` calls.
  - `Assets/Tests/EditMode/WorldClockTests.cs` (new) — 14 cases: 9 `[TestCase]` rows covering every
    phase-boundary edge (inclusive/exclusive) from the spec table, plus 5 `[Test]` methods for tick
    rate, multi-day minute-of-day wraparound, and phase-after-wraparound.
  - No asmdef changes needed — `Isle.World` already referenced `Isle.Networking`/`Isle.Data`, and
    `Isle.Tests.EditMode` already referenced `Isle.World`.
  - Verified: batchmode compile **0 `error CS`**; full EditMode suite **169/169 passed** (155
    pre-existing + 14 new), run twice (once per branch base, see Decided without a spec).
  - **Merged** — [PR #18](https://github.com/yhw1737/surv/pull/18), 2026-09-15.

- **T-032**: `Chunk` + `ChunkManager` load/unload (SYS-WORLD-01 §Chunks). Same branch/session as
  T-030, continued per the developer's "keep going through logic-only tasks" instruction (T-031
  skipped — see Blocked / needs the developer).
  - `Assets/Scripts/Core/Vec2Int.cs` (new) — integer 2D coordinate, no Unity dependency (Absolute
    rule: `Vector2Int` isn't allowed outside Unity-facing code). Used for tile/chunk coordinates.
  - `Assets/Scripts/World/Chunks/Chunk.cs` (new) — `Chunk` (32×32 `Tile[]`, `List<WorldObject>`,
    `LastSimulatedTime`) exactly matching the spec's struct sketch, plus `Chunk.CoordFromTilePosition`
    (floor-division so the grid is well-defined on both sides of the origin). `Tile` and
    `WorldObject` are minimal placeholders — the spec only names their container types, not their
    fields; see Decided without a spec.
  - `Assets/Scripts/World/Chunks/ChunkManager.cs` (new) — `UpdateActiveChunks(playerChunkCoords,
    worldTime)` loads the union of every player's 3×3 neighborhood and unloads (stamping
    `LastSimulatedTime`, then calling an injected save callback) anything that falls out of range.
    Load/save are constructor-injected `Func`/`Action` delegates, not an interface, so this class
    has zero SQLite dependency — `ChunkSerializer` (T-033) supplies the real ones later.
  - `Assets/Tests/EditMode/ChunkTests.cs` (new) — constructor defaults, 7 `CoordFromTilePosition`
    cases including negative coordinates.
  - `Assets/Tests/EditMode/ChunkManagerTests.cs` (new) — 5 cases covering SYS-WORLD-01 verification
    #3: first-load 3×3, move-away unload+save, stays-in-range chunks aren't reloaded, two distant
    players load the union of both neighborhoods, zero players unloads everything.
  - No asmdef changes needed — `Isle.World` already references `Isle.Core`; `Isle.Tests.EditMode`
    already references both.
  - Verified: batchmode compile **0 `error CS`**; full EditMode suite **182/182 passed** (169
    pre-existing + 13 new).

- **T-033**: `ChunkSerializer` (SYS-WORLD-01 §Chunks persistence). Same branch/session, unblocked
  2026-09-15 by the developer's library choice and storage-format answer (see Decided without a
  spec).
  - `Packages/manifest.json` — added `com.gilzoide.sqlite-net` (git URL, pinned `#1.3.2`), the
    developer's chosen SQLite binding. Resolves into `Library/PackageCache/`, root namespace
    `SQLite`.
  - `Assets/Scripts/World/Chunks/ChunkSerializer.cs` (new) — one row per chunk in a `chunks`
    table (`cx`, `cy` primary key, `data` TEXT), `Load(Vec2Int)` (null if no row), `Save(Chunk)`
    (`INSERT OR REPLACE`). Stores the chunk as a JSON **TEXT** column, not the spec's literal
    "BLOB" — see Decided without a spec. Two private converters (`Vec2IntJsonConverter`,
    `NamespacedIdJsonConverter`) — the latter a deliberate trimmed duplicate of
    `Isle.Modding.Defs.NamespacedIdJsonConverter` since `Isle.World` doesn't reference `Modding`;
    consolidate if a third consumer needs it.
  - `Assets/Tests/EditMode/ChunkSerializerTests.cs` (new) — round-trip (all fields, non-default
    biome, one `WorldObject`) and missing-row-returns-null, both against an in-memory
    (`:memory:`) database.
  - `Assets/Scripts/World/Isle.World.asmdef` — added `Gilzoide.SqliteNet` to `references`, **and**
    fixed `overrideReferences`/`precompiledReferences` for `System.Text.Json` (see Decided
    without a spec / Operational notes — this was a latent gap from T-030, not something T-033
    introduced).
  - `Assets/Scripts/World/Chunks/Chunk.cs` — doc-comment only, points at `ChunkSerializer` now
    that it exists. No logic touched.
  - Verified: batchmode compile **0 `error CS`**; full EditMode suite **184/184 passed** (182
    pre-existing + 2 new).

- **T-035/T-036**: `IslandGenerator` + `LandmarkPlacer` (SYS-WORLD-01 §Island generation). Same
  branch/session, unblocked 2026-09-15 by the developer's shape and separation answers (see
  Decided without a spec).
  - `Assets/Scripts/World/Generation/IslandGenerator.cs` (new) — `BiomeAt(worldX, worldY)` is a
    pure function of (seed, tile): normalized elliptical distance from centre decides coast vs.
    interior (`CoastRingStart = 0.82`), semi-axes randomized per seed (0.82–0.95 of the half-width)
    for the "different island every game" requirement, interior biome sampled on a 24-tile cell
    grid (clumped patches, not per-tile speckle) via a small deterministic hash — no whole-island
    array generated or stored, any chunk can compute its own tiles independently. `IsLand` exposes
    the same ellipse test for landmark placement. Marsh doubles as the "ponds/rivers" the developer
    described — the spec fixes exactly three biomes, no fourth "water" type invented.
  - `Assets/Scripts/World/Generation/LandmarkDefs.cs` (new) — `LandmarkDef` (def id + count).
    Counts (1 shipwreck, 2 ruins, 3 springs) are a fixed design constant (same status as `Biome`);
    the def id each kind actually places is moddable, so it's read from
    `Assets/StreamingAssets/definitions/world/landmarks.json` (new), not hardcoded.
  - `Assets/Scripts/World/Generation/LandmarkPlacer.cs` (new) — greedy farthest-point placement:
    samples land tiles on an 8-tile grid, places each landmark at the candidate that maximizes the
    minimum distance to every already-placed landmark (deterministic per-seed tiebreak for the
    first pick, which has nothing to maximize against yet). Returns positions pre-grouped by chunk
    coordinate, ready to append to that `Chunk`'s `Objects` once it's generated. See Decided without
    a spec for why this maximizes rather than enforces the literal 256-tile target.
  - `Assets/StreamingAssets/definitions/world/landmarks.json` (new) — the three def ids
    (`isle:shipwreck`, `isle:ruins`, `isle:freshwater_spring`); none of these defs exist yet
    (Phase 4/6+ content), same "not fleshed out yet" status as other `WorldObject` placeholders.
  - `Assets/Tests/EditMode/IslandGeneratorTests.cs` (new) — determinism (same seed → same biome
    everywhere sampled), edges/corners always coast, interior contains both forest and marsh,
    different seeds differ, `IsLand` sanity.
  - `Assets/Tests/EditMode/LandmarkDefsTests.cs` (new) — loads counts/def ids from a JSON file.
  - `Assets/Tests/EditMode/LandmarkPlacerTests.cs` (new) — correct total count (6), every landmark
    on land, determinism, and a floor on the achieved minimum pairwise separation (80 tiles — just
    catches a degenerate/clustered placement, not the literal 256 target; see Decided without a
    spec for the real achieved numbers).
  - No asmdef changes needed.
  - Verified: batchmode compile **0 `error CS`**; full EditMode suite **194/194 passed** (184
    pre-existing + 10 new).

- **T-040**: `GridInventory` structure (SYS-INV-01 §Placement) — pure data structure only, no
  weight/UI/network (those are T-041/T-042/T-045).
  - `Assets/Scripts/Gameplay/Inventory/GridInventory.cs` (new) — `GridInventory` holds a
    `Width`/`Height` and a list of `Placement` (item + `Vec2Int` position + rotated flag).
    `TryPlace` checks bounds and an AABB overlap against every existing placement (spec: "no
    L-shaped items", so AABB is sufficient). `Placement.EffectiveSize()` swaps `W`/`H` when
    rotated — reuses the existing `GridSize` (`Assets/Scripts/Data/Shared.cs`) rather than adding
    a second grid-size type. One instance per container; a bag is a second instance, not a resize
    of the base grid, matching "the base grid never grows".
  - `Assets/Tests/EditMode/GridInventoryTests.cs` (new) — in-bounds placement, out-of-bounds
    rejected, negative position rejected, overlap rejected, adjacent (non-overlapping) accepted,
    rotation swaps W/H (a 1×4 item fits a 3-tall container only rotated), remove frees its cells.
    No literal verification table exists for placement (the spec's table is for §Weight, T-041's
    scope) — see Decided without a spec.
  - No asmdef changes — `Isle.Gameplay` already references `Isle.Data`.
  - Verified: batchmode compile **0 `error CS`**; full EditMode suite **201/201 passed** (194
    pre-existing + 7 new).

- **T-041**: `WeightCalculator` (SYS-INV-01 §Weight) — static pure, formula copied verbatim from
  the spec, no invented values.
  - `Assets/Scripts/Gameplay/Inventory/WeightCalculator.cs` (new) — `SpeedMultiplier`,
    `StaminaDrainMultiplier`, `IsOverloaded`, and the four constants (`FreeWeightKg=15.0`,
    `MaxWeightKg=45.0`, `MaxSpeedPenalty=0.50`, `OverloadSpeedMult=0.35`) straight from the spec
    table. `StaminaDrainMultiplier` uses the same `clamp((total-Free)/range, 0, 1)` formula
    regardless of overload — the spec's pseudocode only overrides `speedMult` in the overload
    branch, not the stamina formula below it, so overload doesn't clamp it any further than the
    formula already does.
  - `Assets/Tests/EditMode/WeightCalculatorTests.cs` (new, written before the implementation) —
    the spec's own 5-row verification table (10/15/30/45/50 kg → 1.000/1.000/0.750/0.500/0.350
    speedMult, roll yes/yes/yes/yes/no) copied verbatim as `TestCase`s, plus a
    `StaminaDrainMultiplier` table computed directly from the formula (1.0/1.0/1.4/1.8/1.8) — no
    judgment call needed here, unlike T-040, since the formula is exact.
  - No asmdef changes.
  - Verified: batchmode compile **0 `error CS`**; full EditMode suite **211/211 passed** (201
    pre-existing + 10 new).

- **T-042/T-043**: Grid UI + dragging, ★ rotate/bulk-move/split (SYS-INV-01 §Required UX) — treated
  as one task, per the backlog's own note that they're tightly coupled and non-deferrable together.
  Everything that's pure logic went into `GridInventory.cs` (EditMode-tested); only the genuinely
  visual/input-driven glue is a MonoBehaviour.
  - `Assets/Scripts/Gameplay/Inventory/GridInventory.cs` — `Placement` gained `Count` (default 1,
    additive, existing `TryPlace` calls unaffected). New pure methods: `FindFreePosition` (first-fit
    row-major scan, backs bulk move/move-all/auto-sort), `TryMoveTo`/`MoveAllTo` (Ctrl+click and
    "move all"), `TrySplit` (Shift+drag split — all-or-nothing, rolls back the source on any
    destination failure, rejects a same-position no-op split), `AutoSort` (warehouse-only auto-sort
    button — packs into a scratch grid first so a packing failure can't leave the live inventory
    half-sorted), `TotalWeightKg` (bridges to `WeightCalculator` for the UI's weight readout).
  - `Assets/Tests/EditMode/GridInventoryTests.cs` — 13 new cases, written alongside the
    implementation: `FindFreePosition` (empty grid, row-wrap, no room), `TryMoveTo`/`MoveAllTo` (fits,
    no room, partial fit), `TrySplit` (partial count, full-count rejected, destination-full rollback,
    same-spot rejected), `AutoSort` (largest packs first), `TotalWeightKg`.
  - `Assets/Scripts/UI/Isle.UI.asmdef` — added `Unity.InputSystem` (R/Ctrl/Shift detection via
    `Keyboard.current`, matching `PlayerMovement.cs`'s existing convention) and `UnityEngine.UI`
    (Canvas/Image/Text) to `references`. Neither was there before — this is the project's first
    Canvas/UI feature.
  - `Assets/Scripts/UI/Inventory/GridView.cs` (new) — MonoBehaviour wrapping one `GridInventory`;
    draws cells + item icons at runtime via `PlaceholderVisuals` (no baked prefabs, same convention
    as `PlaceholderIcons`). `PairedView` (settable at runtime by whoever opens a paired container UI)
    drives Ctrl+click/move-all's destination; `IsWarehouse` gates the auto-sort button
    (spec: warehouse only, bags stay manual). `CellSizePx = 32` is a placeholder UI dimension
    (Absolute Rule 7), not a spec value.
  - `Assets/Scripts/UI/Inventory/DragHandler.cs` (new) — one per spawned item icon.
    `OnPointerClick` does the Ctrl+click bulk move. `OnBeginDrag` reads Shift at drag-start to decide
    the drag count (half the stack, or all of it). `OnDrag` polls `rKey.wasPressedThisFrame` each
    frame to flip a visual-only rotation flag (square items excluded — can't meaningfully rotate).
    `OnEndDrag` raycasts for the drop target `GridView` and routes through `TrySplit` or a plain
    remove+place move; any failure snaps the icon back to its start position so an item can never
    disappear on a bad drop.
  - `Assets/Scripts/UI/Inventory/ItemTooltip.cs` (new) — hover tooltip showing only fields that
    exist today (name as a raw lang key, weight, size, stack count). Freshness/quality/enchants/
    crafter are explicitly omitted — see Decided without a spec.
  - Verified: batchmode compile **0 `error CS`**; full EditMode suite **224/224 passed** (211
    pre-existing + 13 new). **The UI/drag half (`GridView`/`DragHandler`/`ItemTooltip`) is not
    EditMode-testable** — see the manual-test checklist in Next.

- **T-044**: equipment slots + bag expansion (SYS-INV-01 §Containers: "Equipment slots: `Head Chest
  Legs Feet Back Belt MainHand OffHand`", "Bags add a separate grid; the base grid never grows").
  - `Assets/Scripts/Data/ItemDef.cs` — two new fields the spec's own tables name but nothing had
    wired up yet: `EquipSlot` (string, one of `head chest legs feet back belt main_hand off_hand`,
    null for non-equippable items — a plain string, same reasoning as `SkillDef.Pool`, T-018) and
    `BagGrid` (`GridSize?`, bag-type equipment only — the separate grid it opens when equipped,
    distinct from `Grid`, the bag item's own footprint while it sits inside another container).
    `docs/modding/SCHEMA.md` §Items updated to match.
  - `Assets/Scripts/Gameplay/Inventory/EquipSlots.cs` (new) — one `ItemDef` per named slot
    (`EquipSlots.All`, the fixed eight); `TryEquip` checks the item's own `EquipSlot` matches and the
    slot is free (no implicit swap), and opens a `GridInventory` sized from `BagGrid` if the item is
    a bag. `Unequip` fails — leaving the item equipped — if its bag still holds items, so unequipping
    can never make items vanish.
  - `Assets/Tests/EditMode/EquipSlotsTests.cs` (new) — 8 cases: matching/mismatched slot,
    already-occupied slot, bag item opens a `GridInventory` of the right size, non-bag item opens
    none, unequip empty bag succeeds, unequip non-empty bag fails, unequip nothing-equipped fails.
  - `Assets/Scripts/UI/Inventory/EquipSlotView.cs` (new) — one equip slot's UI. Drag-drop onto it is
    routed by `DragHandler` (extended to recognize `EquipSlotView` as a second kind of drop target,
    alongside `GridView`); the slot itself doesn't spawn or bind a bag's `GridView` — that's scene
    wiring it doesn't own — instead it exposes a `UnityEvent Changed` for whoever does own that
    wiring to hook up in the Inspector.
  - `Assets/Scripts/UI/Inventory/EquipDragHandler.cs` (new) — drags an equipped item back out into a
    grid; mirrors `DragHandler`'s rotate-while-dragging logic but stays a separate, smaller class
    since equip items have no split/bulk-move/tooltip concerns. Unequips first (fails safely, item
    stays put, if the bag isn't empty), then tries to place at the destination, rolling the equip
    back if the destination doesn't fit.
  - `Assets/Scripts/UI/Inventory/DragHandler.cs` — `FindTargetView` generalized to `FindInHovered<T>`
    so `OnEndDrag` can check for either a `GridView` or an `EquipSlotView` among the hovered targets.
  - No further asmdef changes — `Isle.UI` already referenced everything T-044's UI needed.
  - Verified: batchmode compile **0 `error CS`**; full EditMode suite **232/232 passed** (224
    pre-existing + 8 new). **The UI half (`EquipSlotView`/`EquipDragHandler`) is not
    EditMode-testable** — see the manual-test checklist in Next.

- **T-045**: server-authoritative inventory sync + rollback UI (SYS-NET-01 "Inventory" row —
  server-only, no prediction; Absolute Rule 2). Scoped to a player's own bag + equip slots only —
  see Decided without a spec for why the warehouse/crates stay local.
  - `Assets/Scripts/Gameplay/Inventory/InventoryNetwork.cs` (new) — a `NetworkBehaviour`, one per
    player `NetworkObject`, holding the server's authoritative `GridInventory` bag (6×3) and
    `EquipSlots`. Four `Request...` methods (move/split within the bag, equip, unequip) send a
    `[ServerRpc]`; the server validates against its own copy (mirroring the exact local logic
    `DragHandler`/`EquipSlotView`/`EquipDragHandler` already had) and reports the result back via
    `[TargetRpc]`. Identifies "which item" by its grid position (`GridInventory.PlacementAt`, new),
    not a `NamespacedId` — avoids `Isle.Gameplay` needing to reference `Isle.Modding`. `Awake()`
    also seeds the bag with two throwaway `ItemDef` fixtures (`SeedTestItems`, added after the
    developer's manual test pass found the Networked Bag otherwise had no legitimate way to get an
    item into it — see Next's checklist item 1) so the checklist has something to act on.
  - `Assets/Scripts/Gameplay/Inventory/GridInventory.cs` — added `PlacementAt(Vec2Int)`, an exact
    lookup by a placement's stored top-left position (used above; also the natural counterpart to
    `Remove`, which already takes a `Placement`).
  - `Assets/Tests/EditMode/GridInventoryTests.cs` — 3 new cases for `PlacementAt`: occupied position,
    empty position, and a non-origin cell of a multi-cell item (must return null — position
    identification only matches the stored origin, not the whole footprint).
  - `Assets/Scripts/UI/Inventory/GridView.cs` / `EquipSlotView.cs` — both gained a settable
    `Network` property (null for local-only views: warehouse, demo crates).
  - `Assets/Scripts/UI/Inventory/DragHandler.cs` / `EquipDragHandler.cs` — every mutation call site
    now branches on `_owner.Network != null`; the local (non-networked) path is byte-for-byte
    unchanged. The networked path sends a request and returns immediately (`true`, meaning "request
    sent", not "applied") — the actual `Redraw()` happens in the request's result callback once the
    server's `[TargetRpc]` acks. A networked bag item can only be dropped back into that same bag
    (rejected onto a warehouse/crate) or unequipped into that same player's networked bag — see
    Decided without a spec.
  - `OnEndDrag` in both files: moved the dragged icon's `CanvasGroup.alpha`/`blocksRaycasts` reset
    from the top of the method to the bottom (reached only on total failure) — this alone makes a
    pending networked request render as translucent until its ack's `Redraw()` replaces the icon,
    with zero extra code, since the local success path's `Redraw()` still runs synchronously before
    the reset would matter.
  - `Assets/Scripts/UI/Inventory/InventoryDemo.cs` — added an isolated "Networked Bag (T-045)"
    section, deliberately separate from the existing sample Player Bag/Warehouse/Equip-slots demo.
    Polls (`WaitForNetworkedBag` coroutine, `FindObjectsByType<InventoryNetwork>()` for one with
    `IsOwner == true`) since the player's `NetworkObject` may not have spawned yet when the demo
    starts, then binds a `GridView` to `network.Bag` and one `EquipSlotView` per slot to
    `network.Slots`, both with `Network` set.
  - `Assets/Scripts/Gameplay/Isle.Gameplay.asmdef` — added `FishNet.Runtime` (for
    `NetworkBehaviour`/`ServerRpc`/`TargetRpc`). `Assets/Scripts/UI/Isle.UI.asmdef` — same addition,
    needed because `GridView`/`EquipSlotView`'s new `Network` property exposes `InventoryNetwork`
    (a `NetworkBehaviour`) in `Isle.UI`'s own public surface.
  - `Assets/Prefabs/Characters/player_rig_placeholder.prefab` — added an `InventoryNetwork`
    component (new `.meta` GUID generated for the new script) to the same root GameObject that
    already carries `NetworkObject` and `PlayerMovement`.
  - Verified: batchmode compile **0 `error CS`**; full EditMode suite **238/238 passed** — the
    first real batchmode run in a while, not a hand-count (see In progress/unfinished below for the
    now-resolved gap and a `-quit`/`-runTests` gotcha it surfaced). **The network round-trip itself
    (translucent-pending render, rollback on a denied move) is not EditMode-testable** — needs a
    live client/server session, see the manual-test checklist in Next.

## In progress / unfinished

T-030, T-032, T-033, T-035, T-036 all merged ([PR #18](https://github.com/yhw1737/surv/pull/18),
2026-09-15). T-034 is explicitly **deferred**, not blocked — nothing to build for it right now.
**Phase 3 is done except T-031 (skipped, needs a spec) and T-034 (deferred to Phase 4/6).**

**T-040 through T-044** (`GridInventory`, `WeightCalculator`, grid UI + dragging/rotate/bulk-move/
split, equipment slots + bag expansion) are implemented, verified, and merged into `main`
([PR #19](https://github.com/yhw1737/surv/pull/19), 2026-09-16) — see Completed. `InventoryDemo.cs`
makes the UI half actually reachable in the Editor (see Next).

**T-045** (server-authoritative inventory sync + rollback UI) is implemented, committed on
`feature/T-045-inventory-network`, and open as [PR #20](https://github.com/yhw1737/surv/pull/20) —
batchmode-verified (238/238); see Completed for the full file list and Decided without a spec for
the scope/identification/single-in-flight judgment calls. Needs a live client/server session to
verify the network round-trip itself; see the manual-test checklist in Next.

## Next

**T-045 manual test checklist for the developer** — none of this is covered by the EditMode suite;
it needs at least two clients (Host + Client, or two standalone builds) against the same session,
since the whole point is server vs. owning-client behaviour. Press Play with `InventoryDemo` in the
scene, start the client/server session however the project currently does that (`IsleNetworkManager`,
T-020/T-021), and once each player's own `InventoryNetwork` spawns, a third "Networked Bag (T-045)"
panel should appear below the existing sample panels:

1. **Networked bag appears** — once your own player's `NetworkObject` spawns, a "Networked Bag
   (T-045)" grid + a full row of equip slots should appear (`InventoryDemo.WaitForNetworkedBag`);
   it starts with two test-support fixtures (`InventoryNetwork.SeedTestItems`, not shipped content)
   so the rest of this checklist has something to move/split/equip/drag out — a `main_hand`-tagged
   gear item at (0,0) and a stack of 3 at (1,0).
2. **Move within the networked bag** — drag an item to an empty cell inside this bag; it should go
   translucent immediately, then resolve to fully opaque at the new cell shortly after (the round
   trip to the server and back) — not snap there instantly the way the local demo bag does.
3. **Rejected move rolls back visually** — drag an item onto a cell already occupied inside this
   bag; it should stay translucent briefly, then snap back to its original cell once the server's
   rejection comes back, never disappearing or duplicating.
4. **Split within the networked bag** — Shift+drag part of a stack to an empty cell in this same
   bag; same translucent-then-resolve behaviour as #2, ending with two correctly-counted stacks.
5. **Equip from the networked bag** — drag an item from this bag onto one of its own equip slots
   (matching `EquipSlot`); it should go translucent, then land on the slot and disappear from the
   bag once acked.
6. **Unequip into the networked bag** — drag an equipped item on this row back into this bag; same
   translucent-then-resolve pattern, landing in the bag once acked.
7. **Cross-container drop is rejected** — drag an item from this networked bag onto the *local*
   demo warehouse (or the local demo bag) instead; it should refuse and snap back — this is the
   deliberate Phase-6-scope limitation (`DragHandler.TryDrop`'s `target == _owner` check), not a
   bug. Confirm the reverse also fails: dragging a *local* item onto the networked bag.
8. **Unequip into a different container is rejected** — if reachable, try dragging an item equipped
   in this row into the local demo warehouse/bag instead of this networked bag; should refuse
   (`EquipDragHandler.UnequipIntoNetworked`'s `target.Network != _owner.Network` check).
9. **Ctrl+click is a no-op on the networked panel** — Ctrl+click an item in this bag or an equipped
   item on this row; nothing should happen (no `PairedView` is wired for the networked demo panel —
   expected, not a bug, since Ctrl+click routes through local-only code).
10. **Host (listen-server) self-play doesn't double-apply** — if testing as the host (server +
    owning client in one process), confirm a move/equip/split/unequip each apply exactly once, not
    twice or with a visible flicker (`InventoryNetwork`'s `!IsServer` guard in each `Request...`'s
    callback exists specifically for this).
11. **Two separate players don't see each other's networked bags** — with two clients connected,
    confirm each player's "Networked Bag (T-045)" panel only reflects their own `InventoryNetwork`
    (`IsOwner`-filtered) — nobody sees or can drag into another player's bag.

**Developer ran the checklist (2026-09-16, one build + one Editor instance) and reported 6 findings
— investigated, one real bug found and fixed:**

1. **Position sync depends on launch order** (Editor-first: build player's position doesn't sync;
   build-first: works) — **not fixed, not fully explained.** `PlayerMovement.cs`'s prediction code
   is symmetric per-process; nothing in it explains an order dependency. Leading hypothesis:
   `IsleNetworkManager.Start()` races both instances' `ServerManager.StartConnection()` for the same
   `localhost:7770` bind (see In progress/unfinished's connection-method note) — which process
   actually wins that race isn't guaranteed to match launch order (Editor's domain-reload/Play-mode
   entry adds variable delay a standalone build doesn't have). Next step if this recurs: check each
   instance's Console for the Tugboat bind success/failure log line to see who actually became host,
   rather than assuming from launch order.
2. **Equip slots look like "2 rows"** — expected, not a bug. `InventoryDemo.BuildEquipRow` (the
   pre-existing local demo row, `y = -320`) and `InventoryDemo.WaitForNetworkedBag`'s own equip row
   (`y = -420 - 100`) are two separate single-row panels for two separate `EquipSlots` instances
   (local demo vs. the player's real `InventoryNetwork.Slots`), stacked vertically — not a wrapped
   row.
3. **Backpack-open panel renders on top of the Networked Bag panel** — confirmed, cosmetic-only.
   `BuildEquipRow`'s backpack-open handler places its "Backpack (opened)" grid at `bagY = y - 100f`
   = `-420`; `WaitForNetworkedBag` independently hardcodes its own panel at `y = -420`. Same `x`
   (`Margin`) too, so they land in the exact same rect by coincidence — both are demo-harness-only
   layout constants, not shipped UI, so not worth spending a task on; only fix if it gets in the way
   of further manual testing.
4. **Networked Bag → other storage move is rejected** — confirmed correct, matches checklist #7's
   deliberate Phase-6-scope limitation.
5. **Item equipped from the Networked Bag "disappears"** — **real bug, found and fixed.** Root
   cause: checklist #7 also asks to test the *reverse* of #4 (dragging a **local** item into the
   Networked Bag), and that direction was never actually rejected. `DragHandler.TryDrop`'s network
   guard only checked `_owner.Network` (the drag source), never `target.Network` (the destination) —
   `EquipDragHandler.UnequipIntoNetworked` already checked both sides correctly, but `TryDrop` didn't
   mirror it. So dragging a local item into the Networked Bag silently placed it straight into
   `network.Bag` client-side only, with the server never told. Equipping that item then always failed
   server-side (the server's `Bag.PlacementAt` doesn't have it) — but `TryEquipNetworked` always
   returns `true` immediately (fire-and-forget), so `OnEndDrag`'s failure-cleanup path never ran, and
   the dragged icon (already reparented to the canvas root) was left behind as a stray dimmed "ghost"
   next to a fresh, correct icon from `Redraw()`. Looked like the item vanishing. **Fixed:**
   `DragHandler.TryDrop`'s guard now checks `_owner.Network != null || target.Network != null`,
   rejecting both directions like `EquipDragHandler` already did.
6. **Server shutdown → no crash, but players disappear** — not a bug, believed to be FishNet's
   default disconnect behaviour (no custom `OnServerConnectionState`/despawn-on-disconnect code
   exists anywhere in the project — grepped, none found). No persistence system exists yet
   (world-state save/load is unscoped), so a despawned player on disconnect is expected for now, not
   something to fix in T-045.

**Developer then spotted two gaps in the checklist/harness itself (2026-09-16), both confirmed by
re-reading `InventoryNetwork.cs`/`InventoryDemo.cs` — neither is a T-045 sync bug, both are
demo-harness/scope decisions, recorded here rather than silently resolved:**

1. **Checklist #7's "drag out, rejected" half was untestable as written — fixed.**
   `InventoryNetwork.Awake()` only did `Bag = new GridInventory(BagWidth, BagHeight)` — no seed
   item, and nothing else in the codebase wrote into `network.Bag` (grepped for
   `network.Bag`/`Network.Bag`, only reads found in `GridView`/`EquipSlotView`/`DragHandler`/
   `InventoryDemo`). Since #5 fixed the one path that used to sneak a local item in without server
   approval, there was no legitimate way left to get an item into the Networked Bag at all.
   **Fixed:** `InventoryNetwork.SeedTestItems()` (called from `Awake()`) places two throwaway
   `ItemDef` fixtures — same reasoning as `InventoryDemo.cs`'s sample items, Absolute Rule 1 is
   about game content, not test fixtures. Runs identically on the server's and the owning client's
   instance (both hit the same deterministic `Awake()`), so both start in sync with no RPC needed —
   same reasoning as the class's "no snapshot sync back" remark. Batchmode EditMode re-verified
   238/238 after this change.
2. **Equipping the backpack into the Networked Bag's own equip row does not open a "Backpack
   (opened)" panel.** `InventoryDemo.BuildEquipRow` (the local demo row) wires a `Changed` listener
   on the "back" slot that opens/closes one; `WaitForNetworkedBag`'s equip-slot loop builds the same
   `EquipSlotView`s but never wires an equivalent listener — confirmed by reading both loops side by
   side. **Decision (developer, 2026-09-16): leave as-is, out of scope for T-045.** T-045 is about
   server-authoritative sync, not UI parity across every demo panel.

T-040 through T-045 are all implemented; T-045 is committed on `feature/T-045-inventory-network`
and a PR is open (see In progress/unfinished for the link). `BACKLOG.md`'s Phase 4 is now 6/6 — the
next task to pick up is Phase 5 (survival + skills), once the developer has re-run the checklist
(item #7 both directions, now testable with the seeded items) and the PR is merged.

**Correction from an earlier reading of the developer's "keep developing" instruction** (recorded
once, still applies going forward): it does not mean stop at the first task needing manual testing
the way no-spec `T-031` was skipped — `SYS-INV-01` fully specifies T-042/T-043/T-044, so the right
move is to build the whole feature and hand the developer a manual-test checklist for the part an
EditMode test can't reach (the UI/drag behaviour itself), not to halt before writing it.

**Where to actually run this (fixed 2026-09-16):** the first version of this checklist assumed a
Canvas/EventSystem/wired `GridView`s already existed somewhere to test against — they didn't.
`Assets/Scenes/SampleScene.unity` was never touched by any of T-042/T-043/T-044, so there was no
way to reach any of this in the Editor. Fixed by adding `Scripts/UI/Inventory/InventoryDemo.cs`, a
runtime bootstrap in the same "generate everything at runtime, nothing baked" style as
`PlaceholderVisuals` — it builds its own Canvas + EventSystem + two `GridView`s (a 6×4 "player bag"
paired with an 8×6 "warehouse", `IsWarehouse` on the warehouse) + all 8 `EquipSlotView`s + a
handful of throwaway sample items (a 1×3 sword, a stack of 6 potions, a 2×2 helmet for `head`, a
2×2 backpack for `back` with a 4×4 bag), entirely in code. `GridView`/`EquipSlotView` were also
changed to build their own `_cellLayer`/`_itemLayer`/auto-sort-button when left unassigned, instead
of requiring a hand-wired prefab that doesn't exist — see Decided without a spec for why the sample
items are fabricated in C# rather than real content.

**To run it:** open `SampleScene` (or any scene), add an empty GameObject, add the
`InventoryDemo` component to it, press Play. Two grids, 8 equip slots and two "move all" buttons
appear on screen immediately — no other setup needed.

~~Automated batchmode compile verification for this file is still pending~~ **Resolved
2026-09-16 (T-045 session):** the Editor wasn't holding the project lock this time, so a real
`-batchmode -runTests -testPlatform EditMode` pass finally ran (not just a hand count) — caught a
real bug too: `Isle.UI`'s asmdef was missing a `FishNet.Runtime` reference the moment
`InventoryNetwork` became visible in its public surface (`GridView.Network`), which a hand-check
would have missed. One gotcha for next time: **don't pass `-quit` alongside `-runTests`** — Unity
quits on the initial asset-refresh/compile pass before the test run ever starts, silently
producing no results file and no error.

**Screenshot-driven fixes (2026-09-16):** the developer pressed Play and sent a screenshot with two
problems, both now fixed:

1. **Auto-Sort button looked like an unstyled white bar cutting through the warehouse grid.**
   `GridView.BuildAutoSortButton()` added an `Image` but never set its colour, so it rendered at
   Unity's default opaque white, sitting only 4px below the last cell row. Fixed: explicit dark-grey
   background colour and the gap widened to 8px.
2. **"테스트할 아이템들 어딨어 어디서봐" — no way to tell items or panels apart without hovering.**
   The only identification was `ItemTooltip`'s hover-only tooltip, and there was nothing at all
   labelling which grid was the bag vs. the warehouse. Fixed two ways: `GridView.BuildLabel` now
   draws a permanent, non-raycast-blocking name label (+ stack count) directly on every item icon in
   both `GridView.Redraw()` and `EquipSlotView.Redraw()`; and `InventoryDemo` now draws a title label
   ("Player Bag", "Warehouse (Auto-Sort)", "Backpack (opened)", each equip slot's own name) above
   every panel, with the vertical layout constants recalculated so the new titles don't crowd
   anything below them.

**Round 2 fixes (2026-09-16):** pressing Play after round 1 hit a `NullReferenceException` and
three more visual/interaction bugs, all from the same underlying two defects:

1. **Crash**: `GridView.BuildAutoSortButton()` threw a `NullReferenceException` on
   `label.text = "Auto-Sort";`, aborting `Start()` before it ever reached the "move all" buttons or
   the equip row (fully explaining why neither appeared in the screenshot — not a separate bug).
   Root cause: `Image` and `Text` (both `Graphic`-derived) were put on the same GameObject alongside
   `Button`. Every other label in the codebase (`BuildTitle`, `BuildLabel`) already put `Text` on its
   own child GameObject and worked fine — that's the pattern this was missing. Fixed in
   `BuildAutoSortButton` by extracting a shared `GridView.BuildButtonLabel` helper (a stretched,
   non-raycast child label) and reusing it from `InventoryDemo.BuildMoveAllButton` (same defect,
   just never yet exercised because `Start()` crashed first) and `ItemTooltip.EnsurePanel` (same
   defect, latent until the first hover).
2. **Grids overlapping / item sizes not reflected**: `GridView.BuildCells()`'s cell rect,
   `GridView.Redraw()`'s item-icon rect, and `EquipSlotView.Redraw()`'s icon rect never explicitly
   set `anchorMin`/`anchorMax`/`pivot`, unlike every other dynamically-built RectTransform in this
   codebase (`CreateLayer`, `BuildTitle`, the panel rect, etc.), all of which use a top-left
   `(0,1)` anchor/pivot to match this codebase's `anchoredPosition = (x*CellSizePx, -y*CellSizePx)`
   convention. Left at Unity's non-top-left default, cells and icons rendered at
   systematically wrong, size-dependent offsets from their intended grid position — this is what
   read as "inventories overlap" and "item sizes aren't reflected". Fixed by adding the explicit
   top-left anchor/pivot line to all three.
3. **Drag not moving items properly**: no separate bug found in `DragHandler`/`EquipDragHandler` or
   in `GridInventory`'s placement/move logic (both reviewed line-by-line) — the drag math itself is
   correct. This was very likely a symptom of #2: overlapping, mispositioned icons make it easy to
   grab or drop onto the wrong spot. No dedicated fix beyond #2; re-test after this round before
   assuming anything else is wrong here.

**Round 3 fixes (2026-09-16):** developer confirmed round 2 fixed the overlap and sizing, and
reported two more issues — both root-caused and fixed:

1. **Drop position didn't match where the item visually landed.** `DragHandler.TryDrop`/
   `EquipDragHandler.TryDrop` computed the drop cell from the raw pointer position
   (`eventData.position`), not from where the dragged icon itself was drawn — since the pointer can
   grab an icon anywhere over its area (not just its top-left corner) and `OnDrag` preserves that
   grab offset for the whole drag, the cell the pointer ended up over and the cell the icon visually
   snapped from were often different. Fixed both `TryDrop`s to convert the icon's own rect position
   (its pivot is the top-left corner, `GridView.Redraw`/round 2) to a screen point and feed that into
   `ScreenToCell` instead of the raw pointer position — the cell now matches what's on screen.
2. **Tooltip flickering.** `ItemTooltip.EnsurePanel`'s panel `Image` was raycast-blocking by
   Unity's default, and the panel is placed directly under the cursor on hover — so every time it
   appeared it ate the pointer, immediately firing `OnPointerExit` on the item icon underneath,
   hiding the panel, re-exposing the icon, and re-entering: an every-frame show/hide loop. Fixed by
   setting the panel's `Image.raycastTarget = false`, matching every other non-interactive visual in
   this codebase (labels already do this).

**Round 4 fixes (2026-09-16):** developer confirmed round 3 fixed the drop position and tooltip,
then reported one bug and requested one new feature:

1. **Sword didn't equip onto `main_hand`.** Not a bug in `EquipSlots`/`EquipSlotView`/
   `EquipDragHandler` — all three were checked and are correct. Root cause: `InventoryDemo`'s own
   sword fixture never set `equip_slot`, so `EquipSlots.TryEquip("main_hand", sword)` could never
   match (`item.EquipSlot != slot` always true). Fixed by adding `equipSlot: "main_hand"` to the
   sword's `Item(...)` call — a fixture data fix, not a design decision (see the existing "fabricated
   sample items" entry in Decided without a spec; the fixture already had this reasoning applied to
   its other three items).
2. **Q/E rotate, first attempt: rotate-in-place while hovering a stored item.** Implemented, then
   the developer corrected it the same day (see Round 5) — hovering a settled item and rotating it
   with nothing moving didn't read as sensible UX once tried, unlike an extraction-shooter reference
   (Escape from Tarkov) where rotation only ever happens mid-drag.

**Round 5 fixes (2026-09-16):** developer tried round 4 and asked for a different shape for the Q/E
feature, plus two more bugs:

1. **Q/E rotate moved from "hover a stored item" to "while dragging", replacing round 4's
   rotate-in-place entirely.** Removed `DragHandler`'s hover tracking (`IPointerEnterHandler`/
   `IPointerExitHandler`, `_hovering`, `Update()`, `TryRotateInPlace()`) and instead widened the
   existing R-while-dragging check in both `DragHandler.OnDrag` and `EquipDragHandler.OnDrag` to
   also accept Q or E (`kb.rKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame ||
   kb.eKey.wasPressedThisFrame`) — same toggle, three keys. This is the Tarkov-style behaviour the
   developer asked to match: rotation only exists as a live preview on the item currently being
   dragged.
2. **Tooltip rendering behind newer UI (e.g. items inside an opened backpack bag).**
   `ItemTooltip._panel` is a lazily-created singleton (`EnsurePanel`, first hover anywhere creates
   it) parented under the Canvas; anything built into the Canvas *after* that first hover (like the
   backpack's bag `GridView`, which only exists once the backpack is equipped) sits later in sibling
   order and renders on top of it by Unity UI's default "last sibling wins" stacking. Fixed by
   calling `_panel.transform.SetAsLastSibling()` every time the panel is shown (`OnPointerEnter`).
3. **Tooltip should track the cursor, anchored by its own corner, not float centred wherever it was
   first shown.** `_panel.transform.position = eventData.position` only fired once per hover
   (`OnPointerEnter`), and the panel had no anchor/pivot set, so Unity's RectTransform default put
   its *centre* at the cursor rather than a corner. Fixed both parts: `EnsurePanel` now sets
   `anchorMin = anchorMax = pivot = (0, 1)` (this codebase's usual top-left convention, matching
   every other dynamically-built rect), and a new `ItemTooltip.Update()` re-reads
   `Mouse.current.position.ReadValue()` every frame the panel is active, not just on enter, so the
   box now follows the mouse continuously with its top-left corner glued to the cursor.

**Round 6 fixes (2026-09-16):** developer tried round 5 and sent a screenshot (equip works) plus
four more reports:

1. **Unequipping a backpack via drag left its "opened" bag view on screen.** Functionally the
   unequip and the item's return to the source grid both worked — this was a UI-sync bug.
   `EquipSlotView.Changed` (the event `InventoryDemo` listens on to show/hide the opened bag grid)
   was only ever fired from `TryEquipDrop` (the equip path); `EquipDragHandler`'s unequip path
   called `_owner.Redraw()` directly and never fired `Changed` at all, so nothing ever told
   `InventoryDemo` the bag had closed. Fixed by factoring `EquipDragHandler`'s drop logic into a
   shared `UnequipInto` that fires `Changed` on every successful unequip.
2. **Shift+drag split: the remainder looked like it vanished from its original cell while
   dragging.** Root cause: there's one icon GameObject per placement, and the drag moves that exact
   icon with the pointer — so while a split is in progress, nothing else is drawn at the source
   cell until the drop finishes and `Redraw()` rebuilds it (this is why it "worked" the instant the
   mouse was released; it was purely a mid-drag rendering gap). Fixed by spawning a plain,
   non-interactive placeholder icon (`GridView.SpawnGhostIcon`) at the source cell showing the
   remainder count for the duration of the drag, destroyed in `OnEndDrag` regardless of outcome.
   **Also fixed, same report: dragging a split-off stack back onto its sibling didn't recombine
   it.** `GridInventory.TryPlace` only ever either fit into empty space or rejected on overlap —
   there was no merge path at all. Added one: if the destination position/rotation exactly matches
   an existing placement of the *same* `ItemDef` reference, `TryPlace` now merges into its count
   instead of rejecting. Reference equality (not an `Id` comparison) because defs are held by
   reference throughout this codebase (`ARCHITECTURE.md` §Patterns) and the fixture items in
   `InventoryDemo` don't set `Id` at all — two EditMode cases added
   (`TryPlace_SameItemSamePositionAndRotation_MergesCount`,
   `TryPlace_DifferentItemSamePosition_StillFails`, the latter guarding that two distinct item defs
   at the same spot still correctly reject as an overlap).
3. **Q/E rotation itself was flaky — Q never registered, E worked once and late.** Real bug, not a
   misunderstanding: the round 5 fix checked the rotate keys inside `OnDrag`, but Unity only calls
   `IDragHandler.OnDrag` on frames the pointer actually *moves* — holding the mouse still while
   tapping Q/E meant the check often never ran on the frame the key was pressed, and
   `wasPressedThisFrame` had already expired by the next `OnDrag` call. Moved the key check into a
   plain `Update()` (guarded by a new `_dragging` flag, same polling convention as
   `PlayerMovement`'s WASD) in both `DragHandler` and `EquipDragHandler`, so it runs every frame
   regardless of mouse movement. **Scope note, not silently invented:** the request describes Q
   rotating left / E rotating right "indefinitely", but `Placement.Rotated` (SYS-INV-01) is a single
   bool — only two orientations exist for a rectangle, there's no third or fourth state to cycle
   into, and a plain placeholder box has no visible "front" to tell 0°/180° or 90°/270° apart even
   if there were. Both keys now reliably toggle the same two-state flip; a real 4-orientation model
   would be a `SYS-INV-01` change, not something to invent here — flagged back to the developer
   rather than building it.
4. **Ctrl+click didn't unequip, and equipped items showed no tooltip.** Neither existed — `EquipDragHandler`
   had no `IPointerClickHandler`, and `EquipSlotView.Redraw()` never attached an `ItemTooltip`.
   Added both: `EquipDragHandler.OnPointerClick` Ctrl+click-unequips into a new
   `EquipSlotView.PairedView` (wired to the player's bag in `InventoryDemo`), reusing the same
   `UnequipInto` helper as drag-out; and the equipped icon now also gets an `ItemTooltip`, fed a
   bare `Placement` (no real grid position/rotation exists for an equipped item, so a placeholder
   one is built just to reuse the existing name/weight/size display).

**Two new `GridInventoryTests` cases added this round** (merge behaviour, point 2 above) — not yet
run through batchmode, same standing gap as every round before this one (the developer's own Editor
holds the project lock); last confirmed batchmode run was T-044's 232/232.

**Round 7 fixes (2026-09-16):** developer tried round 6, kept Q/E as-is for now (item 3 below), and
sent three more reports:

1. **Ctrl+click didn't merge onto a matching stack, only drag did.** `GridInventory.TryMoveTo`
   (backs `DragHandler.OnPointerClick`'s Ctrl+click bulk-move, and `MoveAllTo`) always called
   `FindFreePosition` and placed there — it never checked whether the destination already held the
   same item. Extracted a shared `GridInventory.FindPlacementSpot(item, rotated)`: returns an
   existing matching item's own position/rotation to merge into (via `TryPlace`'s existing merge
   path), or the first free spot otherwise. `TryMoveTo` now calls this, so Ctrl+click, "move all",
   and auto-move all merge onto a matching stack the same way an exact-cell drag already did.
   `EquipDragHandler.OnPointerClick` (Ctrl+click-unequip) had the identical gap — it also called
   `FindFreePosition` directly — so it was switched to the same shared helper. One new EditMode case
   (`TryMoveTo_MatchingItemAlreadyInDestination_MergesIntoIt`); the free-position branch was already
   covered by the pre-existing `TryMoveTo_FitsInDestination_MovesAndFreesSource`.
2. **The dragged icon rendered behind other UI panels during a drag.** Root cause: the icon stays
   parented under its own `GridView`'s item layer for the whole drag, so `SetAsLastSibling()` inside
   that layer only ever wins against its own view's siblings — Unity draws whole subtrees in
   hierarchy order, so a different, later-drawn panel (e.g. dragging from the bag over the
   warehouse) always drew its cells on top of the icon still following the pointer, regardless of
   sibling order inside the bag. Fixed by reparenting the dragged icon to the canvas root
   (`worldPositionStays: true`, so it doesn't jump) and calling `SetAsLastSibling()` there instead,
   in both `DragHandler.OnBeginDrag` and `EquipDragHandler.OnBeginDrag`. On a successful drop the
   icon is destroyed by the usual `Redraw()` regardless of its (reparented) location, so only the
   snap-back-on-failure path needs to restore the original parent — both `OnEndDrag`s now do that
   before resetting `anchoredPosition`.
3. **Q/E direction request:** developer said leave it as the current reliable two-state toggle for
   now, revisit later if needed. No change made; not re-flagging further this round.
4. **Items inside an opened backpack couldn't bulk-move or Ctrl+click.** The backpack's bag
   `GridView` (spawned in `InventoryDemo` when the backpack gets equipped) never had a `PairedView`
   set, so both `DragHandler.OnPointerClick` (Ctrl+click) and any move-all button had nowhere to
   send items. Wired it to the warehouse for now (`bagGridView.PairedView = warehouseView` —
   explicit developer instruction: "일단은 warehouse로", not a permanent decision) and added a
   "Backpack -> Warehouse" move-all button alongside the opened bag view, built and destroyed
   together with it.

**Three new `GridInventoryTests` cases added across rounds 6-7**, still not run through batchmode —
same standing gap, last confirmed batchmode run remains T-044's 232/232.

**Manual test checklist for the developer** — none of this is covered by the EditMode suite; press
Play with `InventoryDemo` in the scene (see above) and work through:

1. **Placement + snapping** — drag an item icon onto an empty cell; it should snap to that cell and
   stay there on release.
2. **Overlap rejection** — drag an item onto cells already occupied by another item; it should
   snap back to its original spot, not overlap or disappear.
3. **Rotate while dragging (R, Q or E)** — start dragging a non-square item, press `R`, `Q` or `E`;
   the icon should flip width/height while still following the pointer, any of the three keys
   toggling the same orientation. Dropping commits whatever orientation was showing.
4. **Square items don't rotate** — same test on a square item; `R`/`Q`/`E` should do nothing.
5. **Ctrl+click bulk move** — with a paired view open (e.g. crate next to the player's bag),
   Ctrl+click an item; it should jump to the first free spot in the other container without
   dragging.
6. **Move all button** — click "move all" on a container with a paired view open; every item that
   fits in the destination should move there, leftovers (if the destination is too small) stay
   behind.
7. **Auto-sort button (warehouse only)** — confirm the button only appears on a `GridView` with
   `IsWarehouse` set; clicking it repacks the warehouse's items largest-first with no gaps and no
   item lost.
8. **Shift+drag split** — hold Shift, start dragging a stack of more than 1; drop half of it into an
   empty area; the original stack should show the remainder count and a new stack with the split
   count should appear at the drop point.
9. **Split onto a full destination** — try a Shift+drag split onto cells that don't fit; the
   original stack should be untouched (no partial split, no item lost).
10. **Hover tooltip** — hover any item icon; a tooltip should appear showing the item's (raw lang
    key) name, size, weight and stack count. It should disappear when the pointer leaves the icon.
11. **Equip an item** — drag an item whose `EquipSlot` matches a slot (e.g. a sword with
    `equip_slot: "main_hand"`) onto that `EquipSlotView`; it should leave the source grid and appear
    on the slot.
12. **Equip rejects a mismatched item** — drag an item onto a slot it doesn't belong to (e.g. a
    sword onto the `head` slot); it should bounce back to its original grid position, not equip.
13. **Equip a backpack opens its bag** — equip the sample backpack into the `back` slot (it starts
    in the player bag grid, already sized 2×2 with a 4×4 bag); `InventoryDemo` listens to the
    slot's `Changed` event and should show a new 4×4 grid below the equip row, starting empty.
    Unequipping it should make that grid disappear again.
14. **Unequip** — drag the equipped icon back out of its slot onto an open grid; it should leave the
    slot and land in the grid, respecting rotation (R) the same as any other drag.
15. **Unequip blocked while the bag has items** — put an item inside an equipped backpack's bag grid,
    then try to drag the backpack itself out of the `back` slot; it should refuse (snap back), and
    the item inside the bag must still be there afterward.
16. **Sword equips** — drag the sample sword onto `main_hand`; it should now leave the bag and
    appear on the slot (round 4 fixture fix).
17. **Q/E do nothing while just hovering** — hover a non-square stored item without dragging and
    press `Q`/`E`; nothing should happen (rotation only exists mid-drag as of round 5).
18. **Tooltip stays on top of a backpack's bag grid** — equip the sample backpack (opens its own bag
    grid below the equip row), hover an item placed inside that bag; the tooltip must render above
    the bag grid, not behind it.
19. **Tooltip follows the cursor** — hover an item and move the mouse around while still hovering
    it; the tooltip's top-left corner should track the cursor continuously, not stay fixed at
    wherever the hover started.
20. **Unequip a backpack closes its bag view** — equip the backpack (opens the bag grid), then drag
    it back out of the `back` slot into any open grid; the bag grid must disappear immediately.
21. **Q/E rotate while holding the mouse still** — start dragging a non-square item, don't move the
    mouse at all, tap `Q` then `E`; both should rotate the icon immediately, every time, with no
    missed presses (round 6 fixed a real bug here — rotation used to only register on frames the
    mouse moved).
22. **Split-drag shows the remainder in place** — Shift+drag part of a stack; while still dragging
    (before releasing), the original cell should still show a placeholder icon with the remaining
    count, not look empty.
23. **Dragging a split stack back onto its sibling merges them** — after a Shift+drag split, drag
    the smaller stack back onto the leftover stack's exact cell; they should recombine into one
    stack with the summed count, not bounce back or sit stacked as two overlapping items.
24. **Ctrl+click unequips** — Ctrl+click an equipped item (no drag); it should move to the player's
    bag's first free spot and leave the slot.
25. **Equipped items show a tooltip** — hover an equipped item; the same name/weight/size tooltip
    from the grid should appear.
26. **Ctrl+click merges onto a matching stack** — put two separate stacks of the same fixture item in
    the bag and the warehouse; Ctrl+click the bag one. It should merge into the warehouse stack's
    count rather than landing in a new empty spot. Same check for Ctrl+click-unequipping an item
    that already has a matching stack sitting in the paired bag.
27. **Dragged icon renders above every panel, not just its own** — drag an item from the bag so the
    pointer passes over the warehouse (or vice versa); the dragged icon must stay visibly on top of
    the panel underneath for the whole drag, not disappear behind its cells.
28. **A failed drop still lands back in the right place visually** — drag an item somewhere invalid
    (off any grid, or a full cell) and release; it should snap back to its exact original cell, not
    end up reparented somewhere visually wrong.
29. **Backpack contents can bulk-move to the warehouse** — equip the backpack, put an item inside it,
    then both Ctrl+click that item and use the new "Backpack -> Warehouse" button; either should move
    it into the warehouse grid.

**Session note (2026-09-15):** the developer authorized working through consecutive Phase 3 tasks
in one sitting without stopping to ask after each one, batching everything into a single PR
instead of the usual one-PR-per-task — but only up to the first task that needs the developer's
own visual/manual check, or that hits a genuinely missing spec value (rule 3: "if a value is
missing, ask — do not invent one"). The same authorization was repeated for Phase 4 later the same
day, and hit its own stopping point at T-042 (see above). **T-031** ("Tilemap, Y-sort, collision, 3-step camera zoom")
hit the first kind — it's tilemap rendering/camera behavior with no spec sheet anywhere, and no way
to verify it except looking at the Editor — so the developer said to skip it and continue with the
logic-only tasks instead. The tasks after it hit the second kind, one by one — the developer
answered all four the same session, and all four are now resolved (recorded here so a later
session doesn't re-ask):

- **T-033** (SQLite chunk persistence) — **done**, see Completed. Developer picked
  `com.gilzoide.sqlite-net` and said save-file inspectability for mod debugging matters more than
  following the spec's literal "BLOB" wording, so storage is JSON TEXT (see Decided without a
  spec).
- **T-034** (`DeferredSimulation`) — developer confirmed this stays **deferred**, to be built
  together with the real Phase 4/6 systems it depends on (inventory, crops, cooking) rather than
  against invented placeholder data shapes. Not blocking Phase 3 completion; revisit once one of
  those phases lands.
- **T-035** (`IslandGenerator`) — **done**, see Completed. Developer answered the shape question
  directly: random island shape every game start (no fixed layout), a rough ellipse as the main
  axis so it never reads as a non-island blob, edges always coast, interior a patchwork/clumped mix
  of marsh and forest moving inward, with scattered ponds/rivers (marsh doubles as this — no
  fourth biome invented).
- **T-036** (landmarks) — **done**, see Completed. Developer answered the minimum-separation
  question directly: landmarks (1 shipwreck, 2 ruins, 3 springs) must be at least **2 minutes of
  walking distance** apart from each other. Converted to a tile distance using the spec's own
  stated crossing pace, not `PlayerMovement.BaseSpeed` (see Decided without a spec) — and that
  target turned out not to be reachable for every pair of 6 landmarks on a 384-tile island anyway,
  so `LandmarkPlacer` maximizes the achieved minimum separation instead (see Decided without a
  spec for the real numbers).

Phase 3 has nothing left to do except revisit T-031 (needs a spec) and T-034 (deferred to Phase
4/6) — see Blocked / needs the developer for T-031, and Phase 4 (`docs/BACKLOG.md`) for what's
next after this branch is committed and reviewed.

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

- ### 2026-09-16 — T-042/T-043/T-044 manual test harness: fabricated sample items in C#, not real content
  `InventoryDemo.cs` (see Next) needed a few concrete items to place, equip and drag for the manual
  test checklist — a sword, a stack of potions, a helmet, a backpack. The project has **zero** real
  item definitions anywhere (`StreamingAssets/definitions/items` doesn't exist yet, no `ItemDef`
  JSON has ever been written), so there was no real content to load through `DefinitionBootstrap`
  instead. Built them the same way `GridInventoryTests`/`EquipSlotsTests` already do — throwaway
  `new ItemDef { ... }` instances with placeholder names/weights, never touching the mod content
  pipeline or `StreamingAssets`. This is a test fixture, not shipped content, so Absolute Rule 1
  ("no hardcoded content") doesn't apply the way it would to a real item def — the same reasoning
  the test suite already relies on. Revisit once real item JSON exists; the demo should switch to
  loading a couple of real items by ID instead of fabricating its own.
- ### 2026-09-16 — T-042/T-043/T-044: `GridView`/`EquipSlotView` build their own child RectTransforms when unwired
  Both were originally written assuming a hand-wired prefab would supply `_cellLayer`/`_itemLayer`/
  `_autoSortButton` via the Inspector — but no such prefab exists (nothing is baked, per project
  convention), and the developer had no way to build one without instructions that didn't exist
  either. Changed `Bind()` on both to create these children itself when the field is left null,
  so either a future hand-authored prefab (Inspector wiring) or a pure runtime caller (`InventoryDemo`,
  no wiring at all) both work unchanged. No behavior change for a hand-wired prefab — this only
  fills in what would otherwise be a null reference.
- ### 2026-09-15 — T-044: `Belt` pouch's "×2" isn't representable by a single `belt` equip slot — flagged, not resolved
  `SYS-INV-01` §Containers names exactly eight equip slots including one `Belt`, but its own
  container table lists "Belt pouch | 2×2 ×2" — two pouches at once. `EquipSlots` as built holds
  **one** item per slot, so it can only ever open one 2×2 bag on `belt`, not two. Went with the
  simple single-slot model rather than inventing a resolution (a second `belt_2` slot? one item that
  represents "a pair of pouches" with a combined grid? two independent bags under one slot key?) —
  none of those is stated anywhere, and guessing wrong here would need reverting UI wiring, not just
  a number. **Needs the developer's answer** before belt pouches specifically are modeled further;
  every other container/slot in the table is unaffected.

- ### 2026-09-15 — T-044: `ItemDef.EquipSlot`/`BagGrid` added as plain fields, not a new definition type
  `SYS-INV-01` §Location doesn't list a schema file for equip data at all — only `EquipSlots.cs`
  (engine logic). The eight slot names and the bag-size table are already fully specified in
  §Containers, so exposing them as two new `ItemDef` fields is wiring up existing spec facts, not
  inventing values (same category as T-011's original field set). `EquipSlot` is a plain string, not
  an enum, matching the `SkillDef.Pool` precedent (T-018) — closed sets are a validator concern here,
  not a type-system one.

- ### 2026-09-15 — T-042/T-043: stack count lives on `Placement`, not a separate `ItemStack` type
  `SYS-INV-01` §Location lists `ItemStack.cs` as a file, and §Required UX makes "split stack"
  non-deferrable, but nothing in `ItemDef`/`Placement`/`GridInventory` modeled a quantity at all, and
  no backlog task (`T-040`..`T-045`) explicitly owns building `ItemStack.cs`. Added a `Count` field
  directly to the existing `Placement` struct (default 1, additive constructor/`TryPlace` parameter —
  every pre-existing call site is unaffected) instead of introducing a whole new type for a single
  int. Revisit if a future task needs `ItemStack` to carry more than a count (e.g. per-unit
  durability/freshness) — nothing here forecloses that, it just doesn't build it before something
  needs it.

- ### 2026-09-15 — T-043: `AutoSort` ordering — descending footprint area, first-fit
  `SYS-INV-01` §Open questions itself lists "Warehouse auto-sort ordering (by tag? size?
  frequency?)" as unresolved. Picked size-descending (largest items packed first, first-fit by scan
  order) because it's the standard bin-packing heuristic that minimizes leftover gaps, and it's the
  only one of the three options that needs no data the game doesn't already track (frequency-of-use
  isn't recorded anywhere; tag-based ordering has no defined tag priority). `AutoSort` packs into a
  scratch grid first and only commits if every item re-places, so a bad heuristic can waste space but
  can never lose an item. Flagged in case the developer wants a specific ordering rule written into
  the spec.

- ### 2026-09-15 — T-042: tooltip omits freshness/quality/enchants/crafter; item name shown as its raw lang key
  `SYS-INV-01` §Required UX asks the tooltip to show "weight/size/freshness/quality/enchants/
  crafter", but only weight and size exist on `ItemDef`/`Placement` today — spoilage-instance
  tracking, item quality, enchants and crafting attribution are all unbuilt systems with no owning
  fields anywhere yet. `ItemTooltip` shows only what actually exists (name, weight, size, stack
  count); the rest is a documented gap, not invented data. Separately, no localization/lang-table
  loader exists anywhere in the project, so the tooltip shows `ItemDef.Name`'s raw lang key (e.g.
  `"@item.raw_meat"`) rather than translated text — the same placeholder-stage honesty
  `PlaceholderIcons` already uses for missing art. Building a localization system now would be a
  second system touched in one session; left for whichever task actually owns `lang/*.json` loading.

- ### 2026-09-15 — T-042: `GridView.CellSizePx = 32` is a placeholder UI dimension
  No spec value exists for on-screen cell size — `SYS-INV-01` only defines grid *cell counts*
  (6×3, 10×6, etc.), not pixels. Reused `PlaceholderIcons.IconSizePx`'s existing 32px constant
  (ART_PIPELINE §Style: "one tile = 32px") for consistency rather than inventing an unrelated number.
  Per Absolute Rule 7, this is a placeholder dimension, not a spec value — free to change once real
  UI art/layout exists (T-160+).

- ### 2026-09-15 — T-040: no verification table for placement, tests derived from the placement rules
  `SYS-INV-01`'s only "Verification" table (±0.001 tolerance, 5 rows) is for §Weight — T-041's
  scope, not T-040's. §Placement states rules in prose ("in bounds and empty", "rotation swaps W
  and H", "AABB overlap is sufficient — no L-shapes") but gives no worked test cases. `GridInventoryTests`
  derives 7 cases directly from those rules (in-bounds, out-of-bounds, negative position, overlap,
  adjacent-non-overlap, rotation, remove) rather than inventing numbers — nothing here is a made-up
  formula value, just test coverage for prose rules that had none. Flagged in case the developer
  wants specific placement scenarios added to the spec itself later.

- ### 2026-09-15 — T-036: landmark minimum separation converted from the spec's crossing pace, not `PlayerMovement.BaseSpeed`
  The developer's answer was "≥2 minutes walking distance apart", which needs a tile-distance
  constant. Two candidate paces exist and they disagree by almost 2×:
  - `PlayerMovement.BaseSpeed = 4.2` tiles/s (T-021, `SYS-NET-01`) → **252 tiles/min**. 2 minutes
    → **504 tiles** minimum separation.
  - `SYS-WORLD-01`'s own "~384×384 tiles, roughly 3 minutes to cross on foot" → **128 tiles/min**.
    2 minutes → **256 tiles**.
  Went with the spec's pace (**256 tiles**), not `BaseSpeed`: 504 tiles is bigger than the
  island's own diagonal (~543), which makes placing all 6 landmarks (1 shipwreck, 2 ruins, 3
  springs) pairwise ≥504 apart geometrically impossible on the stated island size — a strong sign
  that pace is the wrong one for a world-generation constraint, whatever it's right for as a
  movement-tuning number. 256 tiles leaves room to actually place 6 landmarks. `BaseSpeed`'s
  "roughly 3 minutes" is descriptive prose, not a formula input elsewhere in the spec, so trusting
  it here isn't inventing a number — it's reading the same sentence the spec's own landmark section
  sits next to. Flagged here because it's still a judgment call, not a value either spec states
  directly as a tile count.

  **Second finding, discovered while implementing `LandmarkPlacer`: even 256 isn't reachable for
  every pair at once.** Six points with every pairwise distance ≥256 need more room than a
  384-tile island's ellipse gives — the best any 6 mutually-spread points can do on a disk this
  size (a regular-hexagon arrangement, the known-optimal packing for n=6) tops out at roughly the
  disk's own radius, ~155–180 tiles here depending on the seed's randomized semi-axes. This isn't a
  bug to fix, it's the geometry of asking for 6 things to be 256 apart inside something only ~384
  wide. Rather than reject-sample forever chasing a number that can't be hit, `LandmarkPlacer` uses
  greedy farthest-point placement instead — each landmark goes wherever maximizes its distance to
  every landmark already placed. Actual result for seed 42: minimum pairwise separation **~129
  tiles** (full pairwise breakdown ranges 129–309). That's a little over 1 minute of walking by the
  same pace, not 2 — worth flagging to the developer as a real gap between the requested number and
  what a 384-tile island can physically deliver for 6 landmarks, in case the answer is "make the
  island bigger" or "6 landmarks is too many" rather than "spread them as best you can."

- ### 2026-09-15 — T-033: `com.gilzoide.sqlite-net`, JSON TEXT column instead of a BLOB
  Two decisions from the developer's answers, both outside what `ADR-001`/`ARCHITECTURE.md`/
  `SYS-WORLD-01` specify:
  - **SQLite binding library: `com.gilzoide.sqlite-net`** (git package, pinned `#1.3.2`). The stack
    table only ever said "SQLite"; this was a real dependency choice (native binaries per
    platform), not a code detail — asked, developer picked this one.
  - **Chunk rows store JSON text, not a binary BLOB**, despite `SYS-WORLD-01` literally saying
    "BLOB". Developer wants save files inspectable for mod debugging; a binary blob defeats that,
    and chunks are small enough (32×32 tiles) that the JSON overhead isn't a real cost. Developer
    explicitly delegated the exact mechanism ("판단해서 무난하게") — JSON TEXT was the least
    surprising way to satisfy "readable" without inventing a whole new save-file format.

- ### 2026-09-15 — T-032: `Tile`/`WorldObject` are minimal placeholders; load/save injected as delegates, not an interface
  Two judgment calls, neither spelled out in `SYS-WORLD-01`'s one-line `Chunk` struct sketch:
  - **`Tile` holds only a `Biome` enum; `WorldObject` holds only a def ID + local position.** The
    spec names `Tile[] tiles` and `List<WorldObject> objects` as `Chunk` fields but never lists
    what either contains. `Biome` (coast/forest/marsh) is spec/GDD canon, not invented; beyond that,
    fleshing either type out is `IslandGenerator` (T-035) and whatever places world objects — both
    still blocked (see Blocked / needs the developer). Adding fields speculatively now risks
    guessing a shape those tasks would rather define themselves.
  - **`ChunkManager`'s load/save are constructor-injected `Func<Vec2Int, Chunk>`/`Action<Chunk>`
    delegates, not a formal `IChunkStore` interface.** One real implementation (the eventual SQLite
    `ChunkSerializer`, T-033) doesn't justify an interface — delegates keep `ChunkManager` fully
    unit-testable today with zero production implementations yet to satisfy.

- ### 2026-09-15 — T-030: `WorldClock` is plain, non-networked C#; branched off T-021 instead of `main`
  Two judgment calls, neither spelled out in `SYS-WORLD-01` or `BACKLOG.md`'s one-line T-030 scope:
  - **No FishNet sync yet.** Absolute Rule 2 (server authority) implies the server should eventually
    own the canonical clock and broadcast `WorldTime` to clients, but nothing yet consumes
    `WorldClock` across the network — no day/night rendering, no spawn-table queries exist. Wiring
    sync now would be speculative ahead of an actual consumer; the next task that needs `WorldTime`
    client-side should add it then.
  - **Branched `feature/T-030-world-clock` off `feature/T-021-server-movement`, not `main`.** The
    code itself has zero dependency on T-020/T-021 — this is a doc-continuity stack, not a code one.
    `main` hasn't merged PR #16/#17 yet, so its `PROJECT_STATE.md` is still pre-Phase-3; branching
    from it would build T-030's docs on stale state and likely conflict later. Started on `main`
    first, caught the staleness, deleted that branch, and redid it stacked on `feature/T-021-server-movement`
    before any commit. **Superseded same day**: once PR #16/#17 merged into `main`, the branch was
    rebuilt from `main` directly (stash, recreate, stash pop) — the stack was only ever needed while
    those PRs were open.

- ### 2026-09-13 — T-021: flat `BaseSpeed` only, no collider, reused the T-001 rig prefab, skipped an EditMode test
  Five judgment calls, none spelled out in `SYS-NET-01`'s one-line scope or `BACKLOG.md`:
  - **`weightMult`/`terrainMult`/`stanceMult` (SYS-CHAR-01 §Movement) are left out.** They need
    systems that don't exist yet — inventory weight is Phase 4, terrain is Phase 3. T-021 exists to
    prove the FishNet replicate/reconcile loop, not to ship the final movement formula; that spec
    itself is marked "needs a rewrite before it is used" pending the Phase 11 rig work anyway.
  - **No `Rigidbody2D` or collider.** There is nothing to collide with before T-031's tilemap.
    `transform.position` is the simplest thing that proves the prediction loop; swap in a physics
    body once collision exists.
  - **Reused `player_rig_placeholder.prefab` instead of a new prefab.** It already exists as "the
    box stand-in" (`BACKLOG.md` Phase 0). Adding `NetworkObject` + `PlayerMovement` to it doesn't
    touch any IK/rig internals — it sits alongside them. Avoids a second, redundant player asset.
  - **Used FishNet's built-in `PlayerSpawner`** rather than writing per-connection spawn code —
    it does exactly what was needed (spawn a prefab for each connecting client) with zero new code.
  - **No EditMode test.** The new logic is either `MonoBehaviour`/network state (untestable per
    `TESTING.md`) or a one-line diagonal-input clamp (too trivial to warrant a test on its own).
    Verification is the same live-Editor pattern as T-020 — confirmed 2026-09-15.

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
- **T-045 scoped `InventoryNetwork` authority to the player's own bag + equip slots only, not the
  warehouse/crates.** SYS-NET-01 requires server authority for inventory but doesn't say which
  containers; a crate/warehouse is a `WorldObject`, and that type is still a bare def-id + position
  placeholder with no `NetworkObject` or reach/ownership model (T-032, above). Networking that is
  Phase 6 scope once world objects have a network identity to validate reach/ownership against.
  Until then, `DragHandler`/`EquipDragHandler` reject a drop that would move a networked bag item
  into an un-networked container rather than silently falling back to a local, non-authoritative
  mutation — a deliberate limitation to call out in the manual-test checklist, not a bug.
- **`InventoryNetwork` RPCs identify "which item" by its grid position (`GridInventory.PlacementAt`),
  not by a `NamespacedId`.** Resolving an id through `DefRegistry` would need `Isle.Gameplay` to
  reference `Isle.Modding`, which the dependency direction (`UI → Gameplay → World → Networking →
  Data → Core`) doesn't allow. A position always uniquely identifies one placement within a single
  player's own bag, so nothing is lost by keying off it instead.
- **No snapshot-sync protocol for a player's own bag/slots.** Since only that same player's own
  requests can ever mutate their bag or equip slots, server and owning-client copies can't drift as
  long as every mutation goes through `InventoryNetwork` (Absolute Rule 2) — so there's nothing for
  a periodic/on-join snapshot to reconcile. Revisit if a system besides the owner ever needs to
  mutate a player's bag (e.g. a "steal" mechanic).
- **`InventoryNetwork` assumes one in-flight request at a time** (a single `Action<bool>` field, no
  request id/queue) — a solo player drives one drag/click at a time, and this is LAN/listen-server
  play (max 4 players), so an ack always lands well before the next request could be sent. Marked
  with a `ponytail:` comment; revisit with a request id if real internet play is ever added.
- **`InventoryNetwork` doesn't enforce a weight-limit hard block.** SYS-INV-01 §Weight never
  specifies one — it's a continuous movement-speed penalty (see the `WeightCalculator` entry above)
  with no consumer wired up yet, so there's no rule to enforce server-side. Reach/ownership checks
  (SYS-NET-01 §Server validation) also don't apply: both containers are the player's own body,
  always in reach, and `[ServerRpc]`'s default `RequireOwnership` covers ownership for free.

## Operational notes

- **2026-09-15 session batching:** the developer asked to skip the normal one-task-stop-and-ask
  cadence for a run of consecutive Phase 3 tasks, planning one combined PR instead of one per task
  — but only up to the first task needing the developer's own visual/manual check. T-031 hit that
  (see Blocked / needs the developer) and was skipped; T-032/T-033 continued on the same branch
  and T-035/T-036 are the same batch — keep this file's Completed section current per task so
  nothing is missing from that eventual PR's 변경점.
- **Loose Plugin DLLs with `isExplicitlyReferenced: 1` in their `.meta` are never auto-included**,
  regardless of an asmdef's `overrideReferences` setting on other asmdefs. They must be listed by
  name in the *consuming* asmdef's own `precompiledReferences` array with that asmdef's
  `overrideReferences: true`. Found this the hard way adding `ChunkSerializer.cs` (T-033) to
  `Isle.World`: it couldn't see `System.Text.Json` at all until `Isle.World.asmdef` got the same
  `overrideReferences: true` + 8-entry `precompiledReferences` list `Isle.Modding.asmdef` already
  used. Check any new asmdef that needs `System.Text.Json` against `Isle.Modding.asmdef`'s pattern
  before assuming an existing `references` entry is enough.
- **`-runTests` must not be combined with `-quit`** in a headless batchmode invocation — together
  they make Unity do a normal asset-refresh-and-quit with no test execution at all (exit 0, no
  results file, no error). Omit `-quit`; the test runner quits on its own once done. Correct form:
  `Unity -batchmode -nographics -projectPath "$(pwd)" -runTests -testPlatform EditMode -testResults
  <path> -logFile <path>`.
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

1. **T-031 scope** ("Tilemap, Y-sort, collision, 3-step camera zoom") — no spec sheet exists for
   tilemap rendering, Y-sort, collision layers, or camera zoom anywhere in `docs/specs/`.
   `SYS-WORLD-01` covers chunks/time/generation only. This is also inherently visual work (you'd
   need to look at the Editor to confirm tile layout, sort order, and zoom steps look right).
   **Developer decision 2026-09-15: skip it for now, continue with logic-only tasks instead**
   (T-032 done). Still needs (a) the developer to give the numbers/behavior directly (zoom levels,
   collision layer names) so a short spec can be written, or (b) a spec-writing session, before
   it can be picked up.

2. ~~**T-033** (SQLite chunk persistence)~~ **Resolved 2026-09-15** — developer picked
   `com.gilzoide.sqlite-net` and confirmed JSON-readable save files over a binary BLOB. Done, see
   Completed and Decided without a spec.

3. **T-034** (`DeferredSimulation`) — not blocked, **explicitly deferred**. Developer confirmed
   2026-09-15 this builds later, bundled with the real Phase 4/6 systems it depends on (inventory,
   crops, cooking) rather than against invented placeholder data shapes. Its five formulas
   (spoilage, crop growth, resource respawn, drying/smoking) all operate on data types that don't
   exist yet — item stacks (`GridInventory`, Phase 4), crop instances, resource-node spawn state,
   cooking progress (Phase 6+). Revisit once one of those phases lands; no placeholder version
   wanted in the meantime.

4. ~~**T-035** (`IslandGenerator`)~~ **Resolved 2026-09-15** — developer gave the shape rule
   directly (random per game, ellipse main axis, coast edges, marsh/forest interior patchwork,
   scattered ponds/rivers). See Next and `SYS-WORLD-01`'s Island generation section.

5. ~~**T-036** (landmarks)~~ **Resolved 2026-09-15** — developer gave the minimum separation
   directly (≥2 minutes walking). See Next and Decided without a spec for the tile-distance
   conversion.

6. **Company name.** `productName` is `ISLE`; `companyName` is still `DefaultCompany` and
   `applicationIdentifier` is `com.DefaultCompany.ISLE`. Both want the real name before anything
   ships to Steam. One-line edits in `ProjectSettings/ProjectSettings.asset`.

7. ~~**★ Skill taxonomy.**~~ **Resolved 2026-09-07** — see `SYS-SKILL-01`'s status banner and
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
| #15 | feature/T-015-hot-reload | T-015 | [PR #15](https://github.com/yhw1737/surv/pull/15) — **merged to main** |
| #16 | feature/T-020-fishnet-bootstrap | T-020 | [PR #16](https://github.com/yhw1737/surv/pull/16) — **merged to main** |
| #17 | feature/T-021-server-movement | T-021 | [PR #17](https://github.com/yhw1737/surv/pull/17) — **merged to main** |
| #18 | feature/T-030-world-clock | T-030 + T-032 + T-033 + T-035 + T-036 | [PR #18](https://github.com/yhw1737/surv/pull/18) — **merged to main** |
| #19 | feature/T-040-grid-inventory | T-040 + T-041 + T-042 + T-043 + T-044 | [PR #19](https://github.com/yhw1737/surv/pull/19) — **merged to main** |

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
