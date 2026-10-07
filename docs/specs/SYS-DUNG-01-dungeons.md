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
| Root walls / clockwork rooms / antidote | regrow **5 min** · rotate **every 30 s** · immunity **5 min** (real time) |

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
- Rooms are **templates from JSON** (`definitions/dungeons/rooms/*.json`): tile layout, tags (`combat trap puzzle
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
loot tables, artifact pool, sigil, materials, signature mechanic params). `definitions/dungeons/rooms/*.json` — room
templates. Save: per-world dungeon state.

## Open questions (developer)
- Underground layer as a separate scene vs a far-away region of the same world — an implementation choice, decided
  when T-201 starts (a region is simpler for FishNet).

## Verification (to write as tests when built)
| # | Case | Expected |
|---|---|---|
| 1 | 1,000 seeds | every floor connected; every key placed before its lock; boss reachable |
| 2 | Same seed twice | identical floors |
| 3 | Each soft gate, solo, no specialist | passable via the slow path |
| 4 | Abyss with 0 sigils | gate opens, 4 curses active |
