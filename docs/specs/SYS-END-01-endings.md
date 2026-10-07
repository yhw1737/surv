# SYS-END-01 · Multiple endings

Status: **design, numbers decided 2026-10-06** (Q&A with the developer) — developer direction: "다중 엔딩으로 하자. 섬 탈출, 비밀 풀기(보스 처치) 등 여러
경우의 수 (림월드처럼)." The **Decided values** table below supersedes any ❓ left in the text.

**Decided by the developer (2026-10-06, second pass):**
- **One ending per island.** Once an island reaches an ending, no further ending can be reached on it.
- **Shared ending.** When an ending happens, **everyone on that island sees the ending sequence and is credited with
  the same ending** — whoever triggered it.
- **Presentation:** a **short scripted sequence**, then the **run summary as text**.
Supersedes GDD §World "Goals are achievements, not bosses" (see GDD scope note, 2026-10-06).

## Decided values (developer Q&A, 2026-10-06)
| Constant | Value |
|---|---|
| The Signal | keep the beacon lit **5 nights** |
| Home | self-sufficiency **20 days** (the GDD's original goal) |
| Crown of the Deep | survive the backlash **3 days** |
| Unrest | **= progress % of the most advanced ending** (0–100); storm frequency **×(1 + Unrest/100)** → ×2 at the top; creature aggression **+50% × Unrest/100** |
| Adrift odds | base **40%**, **+30%** with enough food and water aboard, **+20%** in clear weather (max 90%); failure is its own ending ("lost at sea") |
| After an ending | the island stays playable as a **sandbox** (no further endings); escape endings offer "stay a while longer" |
| Absent members | see the ending sequence on their **next join** and are credited the same ending |
| Sequence length | **20–30 s**, skippable, then the run summary text |

## Purpose
Give a survival sandbox a reason to push forward without forcing one path. Like RimWorld's ship launch, archonexus
or royal ascent: **several long projects, each a way to finish**, chosen by the players from what the island reveals.
An ending is never required — the sandbox stays playable forever.

## Principles
1. **Projects, not quests.** Each ending is a chain of things to build, find or defeat. No dialogue trees.
2. **Discovered in the world.** Players learn an ending exists from **journal pages** found in landmarks, dungeons
   and rare loot. A page names one step; a full set describes the path. Nothing is explained up front.
3. **The island pushes back.** Progress toward any ending raises **Unrest**; Unrest raises storm frequency, creature
   aggression and dungeon activity. The climax of each ending is a defend/survive/fight event.
4. **Co-op makes it easier, never possible-only-with.** Every requirement has a specialist fast path and a slow
   path anyone can take (same rule as dungeon soft gates, SYS-DUNG-01).
5. **No meta power.** Endings unlock entries in the profile's ending gallery and cosmetics only — never stats.

## The endings

| # | Ending | Path in one line | Climax | Tone |
|---|---|---|---|---|
| 1 | **Adrift** — raft escape | Lash a raft from shipwreck salvage, stock it, push off in calm weather | Launch during a storm window; provisions and weather roll the outcome (rescued / lost at sea) | risky, early, bittersweet |
| 2 | **The Signal** — lighthouse rescue | Restore the ruin's lighthouse: clockwork gears (Clockwork Ruin), a lens (master-quality craft), lamp oil (cook: rendered fat/fish oil) | Keep the beacon lit for ❓ nights while Unrest peaks — tide beasts and storms assault the lighthouse | defensive siege |
| 3 | **Shipwright** — seaworthy ship | Rebuild the wrecked ship: master planks, sails, rigging, and the **Tide Compass** (artifact-tier item, Tidal Grotto) to cross the storm wall | Sail through the storm wall: a timed run on deck against the sea | long build, triumphant |
| 4 | **Keeper** — seal the Abyss | Gather sigils from the dungeons, descend the Abyss, defeat the **Abyss Warden**, seal the Heart | The Warden fight, then hold the seal while the Abyss collapses upward | heroic, "secret solved" |
| 5 | **Crown of the Deep** — claim the Abyss | Same descent; at the Heart, **take it** instead of sealing | The island turns on you: survive the backlash ❓ days | dark |
| 6 | **Home** — the settlers | Stay: self-sufficiency ❓ days, every profession mastered within the party, a master-tier item, a Great Feast | The Founding: host a feast for ❓ guests' worth of dishes while a final storm hits | peaceful |
| 7 | **Leviathan** (hidden) | Find the trench, bait it with the Abyss-Caller's Rod, catch and ride the Leviathan out | A tension fight far longer than any fish (SYS-FISH-01 minigame, boss scale) | secret, joyful |

Endings 4 and 5 share the descent and branch at the Heart (a real choice — and, like every ending, final for the island).
The GDD's old achievement goals (self-sufficiency, artifact, master item) become steps of **Home** and gates elsewhere.

## Flow
```
discover pages → unlock the project in the journal → gather / build / defeat steps → Unrest rises
→ climax event → ending scene + run summary → profile: ending unlocked → "continue on this island?"
```
- **Everyone on the island** watches the sequence together and gets the ending in their profile — the escape
  endings take the whole party along, the in-place endings end it for everyone at once.
- The island is then **ended**: no ending can be reached on it again. ❓ Whether an ended island stays playable as a
  sandbox afterwards (Keeper/Home calm the island; Crown of the Deep would continue as **Endless Night** — permanent
  high Unrest, Abyss creatures on the surface) or closes for good.

## Unrest
```
Unrest = Σ ending-step weights completed ❓   (0..100)
Effects scale by band: 0–25 none · 25–50 storms +❓% · 50–75 creature aggression ❓ · 75–100 dungeon surges ❓
```
Climax events read Unrest for their intensity. Values open.

## Presentation
1. **Short scripted sequence** (❓ ~20–40 s): the ending's moment played with the game's own figures and props — the
   raft pushing off, the ship clearing the storm wall, the Heart sealing — camera moves, a few captions.
2. **Run summary as text** (below), then back to the main menu.

## Run summary (ending screen)
Days survived, deaths and rescues, dishes cooked, biggest catch, artifacts found, dungeons cleared, per-player
profession and top skill. Shown on every ending.

## I/O
- `definitions/endings/*.json` — one def per ending: id, name/desc keys, steps (each: kind `craft|place|defeat|
  deliver|survive|catch`, target id/tag, count), climax event id, outcome rolls, continue mode. **Mods can add endings.**
- `definitions/lore/*.json` — journal pages: id, text key, which ending step it reveals, where it can spawn.
- Save: per-world ending progress + Unrest; per-profile unlocked endings (separate file).

## Open questions (developer)
- What "enough food and water" means for Adrift — set with the raft recipe (T-208).
- Shot list of each ending sequence — written when each ending is built.

## Verification (to write as tests when built)
| # | Case | Expected |
|---|---|---|
| 1 | Every ending's step chain | reachable from a fresh island on a fixed seed (no step needs an item that can't spawn) |
| 2 | Solo, no specialist | every ending completable via slow paths |
| 3 | Any ending reached | the island is marked ended; no other ending's final step can complete |
| 4 | Ending with 4 players connected | all 4 see the sequence and get the same ending in their profiles |
