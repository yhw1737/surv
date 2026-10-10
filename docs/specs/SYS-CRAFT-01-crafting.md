# SYS-CRAFT-01 · Crafting, quality, enchanting

## Purpose
Crafting skill sets the **quality tier**, and quality sets the **enchant slot count**.
Higher benches need several skills, but **nearby allies contribute**, driving cooperation.

> **2026-09-07 developer decision.** Enchanter is a profession distinct from Blacksmith — the
> smith (Crafting) builds the item and its slot count; the enchanter (**Enchanting**,
> `isle:enchanting`) fills those slots and also brews (no spec yet, see BACKLOG T-107). §Enchanting
> below now gates on Enchanting, not Crafting — the callout under §Quality already named this
> split before either skill existed to back it.

## Quality

| Tier | Power | Slots | Durability |
|---|---|---|---|
| Crude | 0.70 | 0 | 0.60 |
| Common | 1.00 | 1 | 1.00 |
| Fine | 1.15 | 2 | 1.30 |
| Superior | 1.30 | 3 | 1.70 |
| Master | 1.50 | 4 | 2.20 |

> **Slot count is why the smith exists.** However good the enchanter, without a Master item you cannot safely fit four.

```
score = 0.20
      + 0.45 * (craftingLevel / 50)
      + 0.15 * materialPurity     // 0..1, mean quality of materials
      + 0.10 * stationTier        // 0..1
      + 0.10 * minigameScore      // 0..1
score = clamp(score, 0, 1)

Crude < 0.30 <= Common < 0.55 <= Fine < 0.75 <= Superior < 0.90 <= Master
```

### Verification (±0.005)

| # | Lv | Material | Station | Minigame | score | Tier |
|---|---|---|---|---|---|---|
| 1 | 5 | 0.2 | 0.0 | 0.3 | 0.305 | Common |
| 2 | 30 | 0.5 | 0.5 | 0.5 | 0.645 | Fine |
| 3 | 50 | 1.0 | 1.0 | 1.0 | 1.000 | Master |
| 4 | 50 | 0.5 | 0.5 | 0.5 | 0.825 | Superior |
| 5 | 1 | 0.0 | 0.0 | 0.0 | 0.209 | Crude |

## Forging minigame
```
heat gauge 0..1, target band [0.55, 0.75]
right-click bellows raises heat; heat falls over time
left-click hammers; a strike inside the band succeeds

successWindow = BaseWindow(0.10) * (1 + craftingLevel / 50)
strikes needed = 3..8 per recipe
minigameScore  = successes / required
```
**Co-op forging:** two players (one bellows, one hammer) halve heat decay and add +0.08 to `minigameScore`.

### Implementation (T-091, 2026-10-10)
- Runs when a recipe names `"minigame": "isle:forging"` **and** it is made at a station tagged `forge` (the anvil) —
  so a copper sword is forged, a stone hatchet (no station) is made at once. Strikes come from the recipe's
  `forge_strikes` (clamped to 3..8).
- **Window:** centred on the target band, half-width = `successWindow` — at Lv 0 that is exactly [0.55, 0.75], at
  Lv 50 [0.45, 0.85].
- [invented]: bellows +0.5 heat/s, decay 0.2/s (co-op 0.1/s), one strike per 0.4 s, heat starts cold (0); strikes
  per recipe — knife 3; hatchet, pickaxe, spear, boots 4; helmet, mace 5; greaves, sword 6; breastplate 8. Arrows
  aren't forged.
- After the last strike the craft is checked again and only then paid for; the score replaces the 0.5 stand-in in
  §Quality. Walking away (0.3 tiles) or E cancels at no cost. The craft and cook windows close while forging (the
  mouse is the bellows and the hammer).
- Server-side state machine (`ForgingMinigame`); the client sends only "bellows held" and "strike".
- **Not yet:** co-op forging (the halved decay and +0.08 are in `ForgingMinigame` but no second player can join a
  forge yet — Phase 10).

## Adjacent assist ★
```
effectiveLevel = max(own, own + (bestNearbyLevel - own) * AdjacentAssistRatio)
AdjacentAssistRatio = 0.60, AdjacentRadius = 3.0 tiles
```

**Applies to skill requirements only — quality uses your own level.**
So: build it together and it gets built, but making it *well* still takes personal skill.

Example: a Crafting 20 player attempting a Lv 40 recipe. A Lv 40 neighbor gives `20 + 20×0.6 = 32` — still short. A Lv 50 neighbor gives `38` — still short. **You need Crafting 30 yourself** to reach 42 with a Lv 50 helper.

Set `allow_adjacent_assist: false` on top-tier recipes to disable this.

## Enchanting (Core: slots only)
```
successRate = EnchantBaseRate(0.60) + enchantingLevel * EnchantSkillRate(0.008)
Lv 50 → 100%
On failure only the catalyst is lost.
★ Within the slot count, the item can never be destroyed.
```
`enchantingLevel` is the **Enchanting** skill (`EnchantDef.enchanting_skill_required`), separate
from the Crafting level that set the item's slot count. Overload enchanting (exceeding slots) is
post-EA; the formula is preserved in `docs/BACKLOG.md`. Core implements `EnchantDef` and the slot
system with 3–4 enchant types.

**Design intent:** enchanting raises the **late-game power ceiling** (GDD §Scope) — the solo beta
loop must be completable on unenchanted Common gear. It is the payoff for reaching a smith who can
build slots and an enchanter who can fill them, not a requirement to reach either.

## Crafter mark
`ItemStack.CrafterName` set on creation, shown in tooltips. Master tier gets a distinguishing visual effect.

## Repair
```
newMaxDurability = previousMaxDurability * RepairDecay(0.92)
restore up to the new maximum
```
The ceiling erodes with each repair — **demand for the crafter persists.**

## Location
```
Scripts/Gameplay/Crafting/
  QualityCalculator.cs   ★ static pure
  AdjacentAssist.cs      ★ static pure
  ForgingMinigame.cs
  EnchantSystem.cs
  RepairSystem.cs
```

## Implementation (T-090, 2026-10-09)

The open questions below were filled in [invented] to unblock quality; they are listed in PROJECT_STATE §Decided
without a spec and can be changed in one place each.

| Input | Rule |
|---|---|
| `craftingLevel` | the crafter's own level in the recipe's primary skill (no adjacent assist) |
| `materialPurity` | mean over every consumed unit: Crude 0, Common 0.25, Fine 0.5, Superior 0.75, Master 1; an input without a quality (raw materials) counts **0.5** |
| `stationTier` | the recipe's station's `station_tier` (world-object data): workbench 0.25, furnace 0.5, anvil 0.5; no station 0 |
| `minigameScore` | **0.5** until the forging minigame (T-091) exists |

- Only gear gets a tier (items with `durability`; they never stack). Its max durability is
  `round(base × durability mult)`; the tier rides on the item's wear record (slot, bag, pile, save) and survives repair.
- `Power` scales a melee weapon's and a bow's damage, a tool's harvest speed, and each armor piece's protection.
  Items with no tier (start gear, salvage, anything crafted before T-090) count as Common.
- With these defaults a new survivor makes Common gear by hand (score ≈ 0.33); Crude needs poor inputs. Lv 30 at an
  anvil makes Fine; Lv 50 makes Superior; Master needs the minigame or Master inputs.
- Enchant slots follow the tier table but nothing fills them yet (T-106).

## Open questions
- ~~How `stationTier` is derived~~ — [invented] above
- ~~How `materialPurity` is computed~~ — [invented] above
- The Core enchant type list
- Whether Enchanting gets its own adjacent-assist / minigame, or reuses this sheet's — not decided, tracked at BACKLOG T-106
