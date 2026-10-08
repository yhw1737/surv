# SYS-FISH-01 · Fishing

## Purpose
Environmental variables produce a species probability table; a tension minigame turns skill into outcome.

## Species selection
```
candidates = FishDefs whose rig_allowed includes the current rig

weight  = 1.0
       *= depthMatch(depth)        // 1.0 in range, falling off outside
       *= tempMatch(waterTemp)
       *= terrainMatch(terrain)    // match 1.0, mismatch 0.15
       *= timeMatch(timeOfDay)     // match 1.0, mismatch 0.25
       *= baitAffinity(bait)       // FishDef.bait_affinity, default 0.3
       *= skillGate(fishingLevel)  // 0.05 below min_skill, else 1.0

normalize, then roulette select
```

**A probability table, not a lookup.** Even below `min_skill` there's a 5% weight — leaving room for the lucky catch.

| Rig | Req. Lv | Character |
|---|---|---|
| Handline | 0 | trash fish only |
| Rod | 5 | minigame, big fish possible |
| Trap | 12 | set and leave, crustaceans |
| Net | 20 | high volume, low quality, 2-person setup |
| Spear | 25 | single high-quality, needs underwater vision |

Traps and nets are deferred-simulation targets (`SYS-WORLD-01`).

## Individual weight
```
WeightKg ~ LogNormal(mean, sigma), clamp[min, max]
```
Individuals differ. A 3.2 kg snapper and a 0.4 kg one yield different meat and cook differently (`SYS-COOK-01` weight scaling).

Store a **server best-catch record** — it becomes a community asset.

## Tension minigame
```
tension 0.0–1.0, starts at 0.5
hold LMB  → tension += ReelRate 0.55 /s
release   → tension -= SlackRate 0.40 /s
fish resistance perturbs it further

halfWindow = FishDef.fight.tension_window * (1 + fishingLevel / MaxLevel)
safe band  = [0.5 - halfWindow, 0.5 + halfWindow]

above band → line-break meter rises
below band → hook-slip meter rises
either reaching 1.0 → failure

fish stamina drains at DrainRate 18/s only while inside the safe band
fish stamina 0 → success
```

| Pattern | Behavior |
|---|---|
| `straight` | +0.25 tension spike every 1.5 s |
| `zigzag` | ±0.18 sine, 0.8 s period |
| `dive` | −0.35 tension drop every 3 s (slack line) |

**Co-op fight:** if `WeightKg >= fight.coop_threshold_kg`, a lone player drains stamina at ×0.4. Two players restore normal rate — one reels, one gaffs on timing.

| Fishing Lv | halfWindow (window 0.22) |
|---|---|
| 0 | 0.22 |
| 25 | 0.33 |
| 50 | 0.44 |

Pinhole at Lv 0, comfortable at Lv 50.

## Verification

| # | Scenario | Expected |
|---|---|---|
| 1 | Fishing with no bait | all candidates get baitAffinity 0.3 |
| 2 | Night-only species attempted in daylight | weight ×0.25 |
| 3 | min_skill 28 species, fishing Lv 10 | weight ×0.05 (not zero) |
| 4 | Fishing Lv 50, tension_window 0.22 | halfWindow 0.44 |
| 5 | coop_threshold 3.0, 4.0 kg fish, solo | stamina drain ×0.4 |
| 6 | 100 catches of one species | weights fit the log-normal |

## Location
```
Scripts/Gameplay/Fishing/
  FishSelector.cs      ★ static pure
  WeightRoller.cs      ★ static pure
  TensionMinigame.cs   MonoBehaviour
  TrapSimulation.cs    deferred sim for traps and nets
```

## Open questions
- How depth is determined (tile property? distance from shore?)
- Underwater vision for spearing
- Second player's input scheme in a co-op fight

## Prototype scope (2026-10-03)

Implemented: **Handline only** (Lv 0, trash fish) — species selection and roulette roll, no tension
minigame. `Gameplay/Fishing/FishSelector.cs` implements §Species selection; Rod and the minigame wait on
T-081. Key `F` casts at the nearest water spot with a `fishing` block.

| Input | Source in the prototype |
|---|---|
| depth | `world_objects[].fishing.depth` (seawater 10, stream 1, standing water 2 — **[invented]**) |
| water temperature | ambient temperature (no water-temperature model exists) **[invented]** |
| terrain | the spot's `water/*` tag suffix |
| time of day | `DayPhase`, lower-cased |
| bait | none — every cast uses the 0.3 default (matches §Species selection verification 1) |
| fishing level | 0 (no fishing skill is tracked on the player yet) |

Not spec-given, invented: depth/temperature mismatch uses the same ×0.15 as terrain mismatch (no falloff
curve in the spec); cast cooldown 3 s stands in for the bite wait. Both are recorded in PROJECT_STATE.md
§Decided without a spec.

## Prototype revision (2026-10-05): cast, bite, hook

Developer correction: fishing is not instant at a one-tile spot. It happens at real water (ponds, rivers,
the sea — SYS-WORLD-03 §Water bodies):

1. **Cast** — aim at a water tile with the mouse and press `F`. The target must be water within 8 tiles
   **[invented]**. A bobber marks it.
2. **Wait** — a bite comes after 4–12 s **[invented]**. The species is rolled at the bite with §Species
   selection, using the bobber tile's depth (distance from shore) and its water body's terrain.
3. **Hook** — a "!" appears; click within 1.0 s **[invented]** or the fish gets away.
4. **Land it** — with a rod (Fishing 5, the rig table), the §Tension minigame decides the catch. Without a
   rod it's a handline: hooking is the catch, and only `handline` fish bite (trash fish, the rig table).

Walking more than 3 tiles **[invented]** from where you cast reels in and ends it.

## Rod required (2026-10-08, developer)
No fishing bare-handed: casting needs a rod (`tool/rod`); the best rod carried is put in hand automatically. Below the
rod's level requirement it still fishes as a plain line (a bite lands the fish, no tension fight).

