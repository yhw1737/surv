# SYS-WORLD-01 · Chunks, time, deferred simulation

## Purpose
Manage the world in chunks and **never simulate where no player is**. This is the performance core.

## Chunks

| Property | Value |
|---|---|
| Size | 32 × 32 tiles |
| Load radius | 3×3 around each player |
| Save trigger | on unload only |
| Store | SQLite, one row per chunk (BLOB) |

```
Chunk { Tile[] tiles; List<WorldObject> objects; long lastSimulatedTime; }
```

## Time
```
1 in-game day = 1440 in-game minutes = 20 real minutes
1 real second = 1.2 in-game minutes
WorldTime : long (accumulated in-game minutes)
```

| Phase | In-game clock |
|---|---|
| dawn | 300–420 (05:00–07:00) |
| day | 420–1020 |
| dusk | 1020–1140 (17:00–19:00) |
| night | 1140–300 |

Night reduces vision radius (light-dependent) and changes some spawn tables. **No seasons in Core.**

## Deferred simulation ★
```
on unload: chunk.lastSimulatedTime = WorldTime
on load:   elapsed = WorldTime - chunk.lastSimulatedTime
           SimulateElapsed(chunk, elapsed)
           chunk.lastSimulatedTime = WorldTime
```

| System | Computation |
|---|---|
| Item spoilage | `freshness -= elapsed * rate * tempFactor` |
| Carcass spoilage | same (`SYS-HUNT-01`) |
| Crop growth | `growth += elapsed / growthMinutes`, capped at 1.0 |
| Resource respawn | roll respawns for `elapsed / respawnMinutes` |
| Drying / smoking | `progress += elapsed / durationMinutes` |

**Design constraint:** any new system's time progression must be computable in a single elapsed-time pass.
- ✅ works: spoilage, growth, respawn, cooking progress
- ❌ doesn't: anything needing per-tick interaction (AI wandering, predation)

Anything in the second category is **regenerated from spawn rules on chunk load** rather than persisted. Animals don't remember positions.

Check this constraint **before** designing any new time-dependent system.

## Island generation
Procedural plus hand-placed landmarks, seeded and deterministic (same seed → same island). Three biomes: coast, forest, marsh. Landmarks: 1 shipwreck, 2 ruins, 3 freshwater springs, positions from a definition file.

**Fully random worlds aren't memorable.** Landmarks are what drive settlement choice and mental mapping.

Island size ~384 × 384 tiles (12×12 chunks), roughly 3 minutes to cross on foot.

## Verification

| # | Scenario | Expected |
|---|---|---|
| 1 | Unload, wait 6 in-game hours, reload | spoilage advanced by 6 hours |
| 2 | Generate twice with the same seed | identical island |
| 3 | Player moves outside the 3×3 | old chunks unload and save |
| 4 | Query fish table at in-game 1200 | dusk species returned |
| 5 | Unload mid-growth, reload after growth time | crop fully grown |

## Location
```
Scripts/World/
  Chunks/{Chunk,ChunkManager,ChunkSerializer}.cs
  Chunks/DeferredSimulation.cs   ★ static pure — EditMode target
  Time/WorldClock.cs
  Generation/IslandGenerator.cs
  Spawning/
```

## Open questions
- Chunk serialization format
- Landmark placement algorithm (minimum separation)
- Concrete night vision radius
