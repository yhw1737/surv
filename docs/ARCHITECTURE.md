# Architecture

Where files go and what may reference what. Ignoring this fractures the codebase across sessions.

## Layers

```
UI              MonoBehaviour, presentation
Gameplay/Combat game rules
World           chunks, time, space
Networking      FishNet wrappers
Data            POCO defs — NO Unity types
Core            loading, tags, utils
```
References go downward only. `Modding` sees only `Core` and `Data` — it must not know gameplay.

## Assembly definitions

One asmdef per folder; the compiler enforces the boundaries.

| asmdef | May reference |
|---|---|
| `Isle.Core` | (nothing) |
| `Isle.Data` | Core |
| `Isle.Modding` | Core, Data |
| `Isle.Networking` | Core, Data, FishNet |
| `Isle.World` | Core, Data, Networking |
| `Isle.Gameplay` / `Isle.Combat` / `Isle.UI` | all of the above |
| `Isle.Tests.EditMode` | all of the above |

**No Unity types in `Isle.Data`.** No `Vector2`, `MonoBehaviour`, `ScriptableObject`. Pure POCOs keep EditMode tests fast, server builds light, and the data reusable in tools. Use `Vec2Int { int X, Y }` from `Isle.Core`.

## Folders

```
Assets/Scripts/
  Core/       Ids/ Tags/ Defs/ Events/ Util/
  Data/       ItemDef CreatureDef FishDef CookMethodDef CraftRecipeDef
              WeaponDef ArtifactDef EnchantDef CropDef SkillDef BuffDef
  Modding/    Defs/ (DefinitionLoader, SchemaValidator, ReferenceResolver, DefRegistry,
              DefinitionBootstrap) Editor/ (DefinitionHotReload)
              ModManifest ModLoader PatchApplier LoadOrderResolver Hooks/
  Networking/ IsleNetworkManager Authority/ Sync/
  World/      Chunks/ Time/ Spawning/ Spoilage/ Generation/
  Gameplay/   Character/ Skills/ Inventory/ Gathering/ Hunting/
              Fishing/ Cooking/ Crafting/ Buffs/
  Combat/     Weapons/ Artifacts/ Damage/ AI/
  UI/         Inventory/ Skills/ Crafting/ Cooking/ HUD/
Assets/StreamingAssets/definitions/
  items/ creatures/ fish/ crops/ cook_methods/ recipes/
  weapons/ artifacts/ enchants/ buffs/ skills/ tags/ schema/
Assets/Art/ Audio/ Prefabs/ Scenes/
Tests/EditMode/ PlayMode/
```

## Patterns

**Def vs instance.** `ItemDef` is immutable, loaded from JSON, one per game. `ItemStack` is mutable and holds `Def` by **reference, never a copy** — otherwise definition edits won't propagate and modding breaks.

```csharp
public sealed class ItemStack {
    public ItemDef Def;              // reference only
    public int Count;
    public float Freshness;          // 0..1
    public QualityTier Quality;
    public List<NamespacedId> Enchants;
    public string CrafterName;
}
```

**Lookup.** `DefRegistry.Get<ItemDef>(id)`, `DefRegistry.AllWithTag<ItemDef>("meat")`. Read-only after load; runtime mutation throws.

**Formulas are static pure functions.** Required for EditMode testing.
```csharp
public static class ButcheryCalculator {
    public static float TotalEdibleKg(float bodyWeightKg, int cookingLevel,
        float toolFactor, float damageFactor, float edibleRatio, float conditionFactor) { ... }
}
```
Anything depending on MonoBehaviour or network state is untestable and therefore not allowed for formulas.

**Event bus over direct refs.** `EventBus.Publish(new ItemCraftedEvent(...))`. These are exactly the future Lua hook points — writing them as events now means Tier 2 modding is a bolt-on later.

## Presentation boundary — art lands last

Real art arrives in Phase 11, after the solo beta and multiplayer. Everything built before it runs
on **vector-shape placeholders**: a box, a circle, a solid colour. That only stays cheap if swapping
the shape for a sprite touches nothing but the renderer.

**Gameplay reads data, never visuals.**

| Gameplay may depend on | Gameplay may never depend on |
|---|---|
| `position`, `aimAngle`, `facingSign` | a bone `Transform`, an IK target, a rig hierarchy path |
| a def's `id`, tags and numbers | a sprite, texture, atlas or prefab path |
| collider bounds from the def | the rendered size of a placeholder shape |
| an animation *event* | an animation clip length or frame count |

```csharp
// ❌ gameplay reaching into the rig — the sprite swap now breaks combat
var tip = transform.Find("Torso/Arm_Front_Upper/.../WeaponSocket").position;

// ✅ the character layer publishes data; combat consumes it
var tip = _rig.WeaponMuzzle;      // CharacterRig owns how that is computed
```

Concretely:
- Every visual is behind a component in `UI` or the owning system's presentation class. No
  `SpriteRenderer`, `Animator` or `Sprite` field outside one.
- **Never hardcode an art path.** `T-017`'s generator resolves a def's visual, falling back to a
  shape when none exists — including for mods, which will always have missing art.
- Timing that gameplay cares about lives in the def or the formula, not in a clip. An attack's
  active window is a number in JSON; the animation matches it, not the reverse.
- Placeholder dimensions are **not** spec values. If a number was eyeballed to make a shape look
  like a body, it may not leak into a formula.

This is the single thing that makes deferring art safe. `BACKLOG.md` Phase 11 depends on it.

## Network

| Target | Authority | Prediction | Rate |
|---|---|---|---|
| Position | server-validated | ✅ client | 20 Hz |
| Aim angle | client → server | — | 20 Hz |
| Inventory | **server only** | ❌ | on change |
| Skill XP | **server only** | ❌ | on change |
| Crafting/cooking | **server only** | ❌ | on change |
| Hit detection | **server only** | ❌ | immediate |
| World objects | **server only** | ❌ | AOI |

Predict movement only. Inventory prediction is the classic duplication-bug source and a solo dev cannot absorb that class of bug. Show a translucent pending state instead.

AOI: chunk-based, 3×3 chunks around each player.

## Storage

| Data | Store |
|---|---|
| Definitions | JSON, read-only |
| World state (chunks, objects) | SQLite |
| Player data | SQLite |
| Settings | JSON |

Chunks are written on unload only — never per frame.

## Deferred simulation (performance core)

```
on unload: chunk.lastSimulatedTime = WorldTime
on load:   elapsed = WorldTime - chunk.lastSimulatedTime
           simulate elapsed in one pass
```
Spoilage, crop growth, resource respawn, drying/smoking all use this. **When designing any new time-dependent system, check first that its progress is computable in a single elapsed-time pass.**

## Never

- Unity types in `Isle.Data`
- Upward references from `Core`
- Formulas inside MonoBehaviours (untestable)
- Gameplay code touching rig internals, sprites or art paths (§Presentation boundary)
- Client-predicted inventory
- Runtime mutation of definitions
- SQLite access per frame
- `Resources.Load` (use Addressables)
- More than three singletons: `DefRegistry`, `EventBus`, `WorldClock`

## New-system checklist

1. Spec sheet exists in `docs/specs/`
2. JSON definition type created first if data-driven
3. Formula extracted as a static pure function
4. EditMode test covers the spec's verification cases
5. Server authority respected
6. Deferred-simulation compatible (if time-dependent)
7. Cross-system links go through events
