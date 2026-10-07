# SYS-DUNG-01 · Dungeons

Status: **design, numbers decided 2026-10-06** (Q&A with the developer) — developer direction: "던전 형태는 맵 생성 때 정해지고, 재밌게. 협동 요소는 넣되,
없으면 아예 못 하는 게 아니라 있으면 쉽게 통과." The **Decided values** table below supersedes any ❓ left in the text.

## Decided values (developer Q&A, 2026-10-06)
| Constant | Value |
|---|---|
| Floors | Tidal Grotto **1**, Rootwood Hollow **2**, Drowned Temple **3**, Clockwork Ruin **3**, the Abyss **3** |
| Floor size | **6×6 grid, 8–12 rooms** (≈ 10 minutes a floor) |
| Repopulation | floors **3 in-game days** after clearing; bosses and vaults never |
| Re-rolling | **never** — an island's dungeons are fixed |
| Soft-gate fast path | skill **Lv 10** in ★ dungeons, **Lv 20** in ★★, **Lv 30** in ★★★ and the Abyss |
| Soft-gate times | specialist **3 s**; anyone **30 s** plus the obstacle's risk (noise, wet/cold, damage…) |
| Abyss curse | per missing sigil: **+25% enemies, −15% light radius** (all four missing: ×2 enemies, 40% light) |
| Abyss pressure | stamina regen **−15% per floor** (55% on floor 3) |
| Room size | **20×20 tiles** per grid cell (decided 2026-10-07) |
| Rest rooms | **1 per floor** (decided 2026-10-07) |
| Locked doors per floor | **1 / 1 / 2** for ★ / ★★ / ★★★ and up (decided 2026-10-07) |
| Root walls / clockwork rooms / antidote | regrow **5 min** · rotate **every 30 s** · immunity **5 min** (real time) |

## Tidal Grotto values (developer Q&A, 2026-10-07, T-202)
| Constant | Value |
|---|---|
| Tide cycle | **2 high tides per in-game day** (12 h cycle) |
| Swim speed | **×0.6**; wet penalty −6 (SYS-SURV-01); two-handed items unusable while swimming |
| Flooded rooms at high tide | **about half** of the rooms; entrance, rest and boss rooms always dry; fixed per seed |
| Low-tide cache | **1 per floor**, holds **1–2 tide pearls**; reachable only at low tide |
| Hermit Colossus | **HP 600, strike 15**; resist slash ×0.5 / blunt ×1.5 (the decided resist values) |
| Boss pattern | claw sweep (hits everyone around it) + charge; below 50% HP it hides in its shell (damage greatly reduced); a blunt stun (3 hits) breaks the shell |
| Boss reward | **tide sigil ×1 + tide pearl ×3** + meat; the boss never returns |
| Grotto creatures | **cave crab** (swarm) and **moray eel** (flooded water only) — new defs |

Values in §Implementation (T-202) marked [invented] are the agent's fill-ins for what the table leaves open.

## Purpose
Hand-feel adventure spaces generated with the island: the place where artifacts, ending sigils and master
materials come from, and where each profession gets a moment to shine.

## Generation (with the island, seed-deterministic)
Every island rolls **one dungeon per biome + the Ruin + the Abyss**, entrances placed at landmarks:

| Dungeon | Entrance | Danger | Signature mechanic | Boss |
|---|---|---|---|---|
| **Tidal Grotto** | sea cave by the shipwreck | ★ | **Tides follow the in-game clock** — at high tide the lower chambers flood (swim: slow, cold, wet, two-handed items unusable); at low tide hidden passages and tidepool caches open. The same floor plays differently by time of day | **Hermit Colossus** — a giant crab in a wreck-hull shell; blunt cracks the shell, slash glances off |
| **Rootwood Hollow** | a hollow giant tree in the forest | ★★ | **A living maze** — root walls regrow ❓ minutes after being cut; fire clears them but enrages the Hollow (more spawns) | **Elder Heartwood** — weak to slash and heat |
| **Drowned Temple** | a sinkhole in the marsh | ★★★ | **Miasma and floodgates** — poison fog rooms, pressure plates that drain or flood halls | **Mire Mother** — toxic; spawns broods |
| **Clockwork Ruin** | the ruin landmark | ★★★ | **Rooms on a timer** — rotating chambers and gear-locked doors that change the route every ❓ seconds | **Sentinel** — an automaton; pierce at the joints |
| **The Abyss** | beneath the Ruin, sealed | ★★★★ | **Darkness and depth** — light radius is life; three floors down; pressure (stamina regen −❓% per floor) | **Abyss Warden** — ending boss (SYS-END-01: Keeper / Crown of the Deep) |

**The Abyss gate is soft-gated too:** it opens with any number of the four **sigils** (one per other dungeon's boss);
each missing sigil adds a **curse** to the descent (more enemies / darker / less loot ❓). All four make it fair;
fewer make it a dare.

### Floor layout
- Each dungeon has ❓ floors (Abyss: 3). A floor is a **room graph on a grid** (❓ e.g. 6×6 cells, 8–14 rooms).
- Algorithm: random walk carves the **main path** entrance → stairs/boss; side branches hang off it; a
  **lock-and-key pass** places each key on a branch *before* its door on the main path (always solvable); one
  treasure vault per floor behind a soft gate; one rest room (safe, campfire spot) every ❓ rooms.
- Rooms are **templates from JSON** (`definitions/dungeon_rooms/*.json`): tile layout, tags (`combat trap puzzle
  treasure rest soft_gate boss`), spawn points, allowed dungeons; rotated/mirrored for variety. **Mods add rooms.**
- Same seed → same dungeons (save stores only cleared state, opened doors, looted chests).

### World integration
- Floors live in a separate **underground layer** (own tile map, own fog of war, own minimap page); entering is a
  transition at the entrance. Time keeps running: hunger, thirst, torches burn down — expeditions need packing.
- Dungeon creatures never leave their dungeon. Floors repopulate after ❓ in-game days; **bosses and vaults never
  reset**.
- Dying underground leaves your body where you fell — an ally carrying it out reduces the death penalty (GDD
  §Multiplayer rescue rule).

## Soft gates — co-op makes it easy, not possible
Every obstacle has a **specialist fast path** and a **slow path anyone can take**:

| Obstacle | Fast path (skill ❓) | Anyone |
|---|---|---|
| Rusted portcullis | Blacksmith (Crafting): pries it in ❓ s, quietly | Bash it ❓ s — loud, wakes the room |
| Flooded channel | Angler (Fishing + rod): casts a line across, team crosses dry | Swim — wet, cold, slow, two-handed items stowed |
| Miasma hall | Cook: an `antidote`-tagged dish → immune ❓ min | Hold breath / take toxic damage over time |
| Root wall | Gatherer (Gathering + axe): chops in ❓ s | Hack ❓ s, or burn (enrages the Hollow) |
| Rune door | Enchanter (Enchanting): reads it — opens and reveals the floor map | Solve the wall-glyph pattern puzzle |
| Beast den | Hunter (combat): clear it | Cook's bait dish lures the pack away; sneak past |
| Pitch-dark shaft | Anyone with a strong light (torch/lantern artifacts) | Feel along — slow, ambush risk |

A full party breezes through; a solo player pays in time, risk and resources.

## Rewards
- Treasure rooms roll **per-floor loot tables** (deeper = better).
- **Boss chest:** a guaranteed roll on the dungeon's **artifact pool** (SYS-ART-02) + its **sigil** + materials.
- **Dungeon materials** for master-tier crafts and ending steps: tide pearl (Grotto), heartwood (Hollow), temple jade
  (Temple), clockwork gear (Ruin), abyssal ore (Abyss).
- **Journal pages** (SYS-END-01) are placed on floors; some only in vaults.

## Creatures (new defs, numbers ❓)
Grotto: cave crab swarm, moray eel · Hollow: root sprite, web spider · Temple: bog drowned, temple snake ·
Ruin: clockwork beetle, spark wisp · Abyss: lantern angler, abyss eel, hollow knight. All use SYS-COMBAT-02 damage
types and resistances, so weapon choice matters (crabs and automatons resist slash, plants burn, etc.).

## I/O
`definitions/dungeons/*.json` — dungeon defs (biome, entrance landmark, floors, room tags, creature tables, boss,
loot tables, artifact pool, sigil, materials, signature mechanic params). `definitions/dungeon_rooms/*.json` — room
templates (a sibling directory: the loader reads each directory recursively). Save: per-world dungeon state.

## Open questions (developer)
- ~~Underground layer: separate scene vs far region~~ — **decided at T-201: a far region of the same world** (see
  §Implementation).

## Verification (to write as tests when built)
| # | Case | Expected |
|---|---|---|
| 1 | 1,000 seeds | every floor connected; every key placed before its lock; boss reachable |
| 2 | Same seed twice | identical floors |
| 3 | Each soft gate, solo, no specialist | passable via the slow path |
| 4 | Abyss with 0 sigils | gate opens, 4 curses active |

## Implementation (T-201, 2026-10-07)

| Piece | Where | Notes |
|---|---|---|
| Floor graph | `World/Generation/DungeonGenerator.cs` | Self-avoiding walk for the main path; locks on main-path doors (never the first); each key on a branch off a main room at or before its lock; rest room (boss floor: off the room before the boss); one soft-gated vault; combat rooms fill to 8–12. Retries up to 200 layouts per seed |
| Tiles | `World/Generation/DungeonTiles.cs` | 120×120 tiles per floor (6×6 cells × 20). Templates picked by room-kind tag, rotated/mirrored; 3-tile doorways with a 4-tile corridor each way, plus a carved line to each room centre so no template seals a door. Built-in fallback template when JSON has none |
| Runtime | `Gameplay/Dungeons/DungeonDirector.cs` | Server-side. Places one entrance per dungeon def (biome match, spread out, seed-deterministic), builds each floor on first entry, runs stairs/exit portals, keys → locked doors, the soft gate, creature spawns |
| Underground layer | world region | Floor *f* of site *s* sits at world (2000 + 200·s, 2000 + 200·f). `IslandWorld.ExtraWalkable` asks the director first; outside a floor the island rules apply |
| Content | `definitions/dungeons/`, `definitions/dungeon_rooms/` | 4 dungeon defs (Abyss is T-204), 10 room templates |

**Soft gate:** E at the gate starts clearing — 3 s if the player's `gate.skill` level ≥ 10/20/30 (by danger),
otherwise 30 s and every dungeon creature within 25 tiles is woken (prey flees). Moving more than 2 tiles cancels.

**Not yet (later tasks):** bosses (boss room ends in an exit portal to the surface until T-202), signature mechanics,
loot in chests (T-205), save of dungeon state (floors regenerate identically from the seed; opened doors and taken
keys are lost on reload), repopulation, own fog/minimap page, dying-underground rules. Dungeon creatures are existing
creatures as placeholders (`creatures` list per def) until the new defs are written.

## Implementation (T-202, 2026-10-07) — Tidal Grotto

| Piece | Where | Notes |
|---|---|---|
| Tides | `World/Generation/Tides.cs` | `Level = cos(2π(hour − high_at_hour) / cycle_hours)`; high while ≥ 0. `FloodedRooms` shuffles the eligible rooms per seed and floods ⌊eligible × share⌋; `CacheRoom` picks one of them |
| Tide runtime | `DungeonDirector` | Water overlay per floor shown at high tide; `IsWater` drives swimming (`PlayerMovement.TerrainSpeed` ×0.6, `Vitals.SetWet` each frame in water, two-handed weapons and casting refused) and water creatures; a notice when the tide turns while someone is inside |
| Low-tide cache | `DungeonDirector` | One per floor in a flooded room; hidden and unusable at high tide; gives 1–2 tide pearls (to the bag, or a loot pile if full) |
| Boss | `BossShell.cs`, `CreatureDirector` | `boss.sweep_radius_tiles`: the strike hits every player in range. `boss.shell`: hide below the threshold, every N s for M s, damage ×mult, broken by a stun. `boss.drops` land as a loot pile. The boss room's exit portal appears only after the boss dies |
| Creatures | `creatures/cave_crab.json`, `moray_eel.json`, `hermit_colossus.json` | `group_size` spawns a swarm per mark; `habitat: water` moves only on flooded tiles and hides (untouchable) when they drain |
| Items | `items/tide_pearl.json`, `tide_sigil.json` | New icon shapes `pearl`, `sigil` |

**[invented] values** (the table above fixes the rest): high-tide peak at **06:00** (so high 03–09 and 15–21);
cache sits nearest the room centre + (4, −4); swarm members 0.8 tiles off their mark; water creatures alternate
with land creatures on flooded rooms' spawn marks.
Shell **every 15 s for 6 s, damage ×0.2**; sweep radius **1.8**; boss body radius 1.1, vision 10, chase 2.8, strike
interval 2.2 s, wind-up 0.7 s, charge 4 tiles at 9 tiles/s, strike type slash; 4 meat. Cave crab 1.0 kg × 6 HP/kg,
strike 3 slash, swarm of 3, resists like the crab. Moray eel 8 kg × 4 HP/kg, strike 8 pierce, chase 4.5,
resist pierce ×1.5.

**Not yet:** the angler's line across the flooded channel (the generic soft gate stands in), boss health bar, eels
attacking from water onto land, sigil use (T-204), dungeon save state.

