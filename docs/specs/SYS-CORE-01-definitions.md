# SYS-CORE-01 · Definition loading, tags, IDs

## Purpose
Load all content from JSON. **Without this everything else gets hardcoded.** Build it first.

## NamespacedId
```
{namespace}:{name}   both [a-z0-9_]+
isle:raw_meat, coolmod:lanternfish
```
Unnamespaced IDs are a **load error** — never auto-corrected. Implement as a readonly struct with a precomputed hash; compare hashes, not strings.

## Tags

Hierarchical, slash-separated, max depth 3. `fish/saltwater` implies `fish`.

`HasTag(item, "fish")` must return **true** for an item tagged `fish/saltwater`.

Flatten all tags at load and cache: `"fish/saltwater"` stores `{ "fish/saltwater", "fish" }`. Never parse strings at runtime — cooking calls this on every combination. Use `HashSet<int>` over interned tag IDs.

Base tag list: `docs/modding/SCHEMA.md` §4. It must exist in `tags/isle_core.json`.

## Load pipeline

```
1  scan mods         collect mod.json (isle itself is a mod)
2  resolve deps      cycle + missing checks → abort with a clear error
3  order             topological sort over dependencies + load_after
4  load defs         parse definitions/ in order
5  validate schema   report ALL failures, don't stop at the first
6  apply patches     in order, detect conflicts
7  resolve refs      ★ verify every referenced id exists
8  flatten tags      §Tags
9  bind assets       register with Addressables
10 freeze registry   read-only from here
```

**Step 7 matters most.** A typo like `"isle:steel_ingott"` must fail at load, not mid-game.

Required error format:
```
[coolmod] definitions/recipes/harpoon.json:14
  Unknown item ID: "isle:steel_ingott"
  Did you mean "isle:steel_ingot"?
```
Suggest candidates within Levenshtein distance 2. **A modding ecosystem's perceived quality tracks its error messages.**

## DefRegistry
```csharp
DefRegistry.Get<ItemDef>(id);              // throws if missing
DefRegistry.TryGet<ItemDef>(id, out def);
DefRegistry.AllWithTag<ItemDef>("meat");   // O(1), indexed at load
DefRegistry.All<CookMethodDef>();
```
Read-only after load; mutation throws. Singleton in `Isle.Core`.

## Definition locations
```
Assets/StreamingAssets/definitions/    base content
{gameDir}/Mods/{modid}/definitions/    local mods
{steamWorkshop}/{workshopId}/          Workshop mods
```
**Base content uses the exact mod format.** Dogfooding is the only way to verify the modding path works.

## Serialization
`System.Text.Json` (not Newtonsoft — keeps the server build light). Configure `snake_case` ↔ `PascalCase`. Unknown fields: **warn and ignore** (forward compatibility). Missing required fields: **error**.

## Hot reload
`F5` in the editor reloads all definitions without restarting. This is the single biggest iteration-speed lever for a solo developer — build it early. Runtime objects hold `ItemDef` by reference, so reload propagates automatically.

## Verification

| # | Input | Expected |
|---|---|---|
| 1 | `"raw_meat"` (no namespace) | load error |
| 2 | item tagged `["fish/saltwater"]`, `HasTag("fish")` | true |
| 3 | item tagged `["fish"]`, `HasTag("fish/saltwater")` | false |
| 4 | reference to nonexistent ID | load error + suggestion |
| 5 | two mods with circular dependency | rejected, cycle shown |
| 6 | unknown JSON field | warning only, load succeeds |
| 7 | missing required field | error with file and line |
| 8 | two mods `replace` the same path | conflict warning |

## Location
```
Scripts/Core/Ids/NamespacedId.cs
Scripts/Core/Tags/TagRegistry.cs
Scripts/Core/Defs/{DefinitionLoader,SchemaValidator,ReferenceResolver,DefRegistry,LoadErrorReporter}.cs
Scripts/Modding/{ModManifest,ModLoader,LoadOrderResolver,PatchApplier}.cs
```

## Open questions
- Hand-write JSON Schema files or generate from `Data` classes? (generation preferred)
- Steam Workshop path detection via Steamworks API
- Handling in-flight crafting/cooking during hot reload
