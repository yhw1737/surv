# SYS-WORLD-02 · Weather

> ⚠️ **Every number in this spec is a first pass, not a balanced value.** SYS-SURV-01 had a
> developer-supplied table to implement against; weather had none (BACKLOG T-051's own note: "no
> spec, no numbers anywhere"). These numbers are chosen to be internally consistent with
> SYS-SURV-01's existing thresholds and are flagged in PROJECT_STATE.md §Decided without a spec —
> replace freely once the day/night loop has been played.

## Purpose

Feeds `ambientTemp` and rain/wetness into SYS-SURV-01's temperature formula, which until now had
no real driver (`Vitals.AmbientTemp` defaulted to a constant, `WetPenalty` had no setter caller).
Driven off `WorldClock`'s existing day phase plus `Season`, with three precipitation states
(`Clear`/`Rain`/`Snow`) and an independent hot/cold event axis (`ColdSnap`/`HeatWave`) layered on
top — see §Precipitation and §Temperature events. Snow, cold snaps and heat waves were added at
the developer's explicit request (2026-09-17), which supersedes this spec's original "two states
only, nothing else is in scope" framing (Absolute Rule 6 no longer blocks this — the request itself
is the scope).

## Ambient temperature by day phase

`WorldClock.DayPhase` (SYS-WORLD-01) already exists; this table is the only new input.

| Phase | Hours | Ambient °(arbitrary unit, same scale as SYS-SURV-01) |
|---|---|---|
| Dawn | 05:00–07:00 | 31 |
| Day | 07:00–17:00 | 37 |
| Dusk | 17:00–19:00 | 31 |
| Night | 19:00–05:00 | 24 |

```
BaseAmbientTemp(phase) =
  Day              → 37
  Dawn or Dusk     → 31
  Night            → 24

AmbientTemp(phase, isRaining) = BaseAmbientTemp(phase) - (isRaining ? 5 : 0)
```

A discrete step per phase, not a continuous curve — `ApproachTemperature`'s existing 2.0/in-game-minute
cap (SYS-SURV-01) already smooths every transition over a few in-game minutes, so a sinusoidal
ambient function would cost real complexity for no felt difference. Each phase is long enough
(Dawn/Dusk 120 in-game minutes, Day/Night 600) that the step settles well before the next one.

**Day (37) matches `Vitals.ComfortableTemperature` exactly** — a player with zero clothing, no
fire, dry, standing in daylight sits at the comfortable-band midpoint and takes no drain. This is
deliberate: the solo-beta milestone (Phase 9) needs the loop bug-free before it needs it hard, and
punishing a player for existing during the day would make every other bug harder to isolate.

**Night (24) is below the −0.5 HP/s threshold (28)** — sustained, unsheltered night exposure is
lethal over a few real minutes, by design: it is the reason a campfire needs to exist at all. The
hypothermia warning band (<33) triggers well before that, during the dusk transition, giving a
player time to act. This is the one number in this table most likely to need retuning once it's
actually played — see Open questions.

## Season

Four fixed seasons, cycling in calendar order — `Spring → Summer → Autumn → Winter → Spring …` —
purely a function of elapsed in-game days (`WorldClock.TotalMinutes`), no new state to hold or
randomize. An additive offset on top of the day-phase table above, not a second table: keeps every
existing number meaningful (Spring/Autumn are the unmodified baseline the day-phase table was
tuned against) instead of needing four full day-phase tables.

| Season | Days | Temp offset |
|---|---|---|
| Spring | 0–14 | +0 |
| Summer | 15–29 | +5 (this map's roll; see §Season temperature pool) |
| Autumn | 30–44 | +0 |
| Winter | 45–59 | −8 (this map's roll; see §Season temperature pool) |

```
SeasonAt(totalMinutes) = (totalMinutes / MinutesPerDay / DaysPerSeason) % 4   // 0=Spring..3=Winter
AmbientTemp(phase, isRaining, season) =
  BaseAmbientTemp(phase) + SeasonTempOffset(season) - (isRaining ? 5 : 0)
```

`DaysPerSeason = 15` (60-day year) — matches RimWorld's quadrum length (also 15 in-game days per
season), picked over a shorter cycle at the developer's request. At SYS-WORLD-01's 20 real
minutes/in-game day, that's ~5 real hours per season and ~20 real hours per year — spans several
sessions rather than being observable in one sitting; see Open questions.

`season` defaults to `Spring` (offset 0) on `AmbientTemp`'s signature so every pre-season call site
and verification case above keeps meaning exactly what it meant before this section existed.

**Winter (−8) is deliberately the harshest number in this spec.** Winter night, raining, stacks
all three penalties: 24 − 8 − 5 = 11 — the coldest temperature reachable in the game, far below
the −0.5 HP/s threshold (28). This is intentional (a "worst night of the year" should exist), but
it compounds the Night=24 concern already flagged below — see Open questions.

### Season temperature pool

Added at the developer's request (2026-09-17): Summer and Winter's offset is no longer the same
fixed number on every map. Instead, each is rolled once from a range and then held fixed for the
rest of that session — "map generation" doesn't have a live runtime hook yet (see §Location), so
the roll happens in `WeatherController.Awake()`, which already runs exactly once per server
session. Spring/Autumn are not randomized — the developer's examples only covered Summer/Winter,
and the spec already treats Spring/Autumn as the unmodified baseline (Absolute Rule 3: no value
invented beyond what was asked for).

| Season | Range | Old fixed endpoint | New endpoint |
|---|---|---|---|
| Summer | +5 to +10 | +5 (`SummerTempOffset`) | +10 (`SummerTempOffsetMax`) |
| Winter | −12 to −8 | −8 (`WinterTempOffset`) | −12 (`WinterTempOffsetMin`) |

```
RollSeasonTempOffset(season, t) =   // t: caller-supplied [0,1) sample, rolled once at Awake
  Summer → lerp(SummerTempOffset, SummerTempOffsetMax, t)   // 5..10
  Winter → lerp(WinterTempOffsetMin, WinterTempOffset, t)   // -12..-8
  Spring, Autumn → SeasonTempOffset(season)                 // unchanged, t ignored
```

The old constants (`SummerTempOffset = 5`, `WinterTempOffset = -8`) are untouched and become one
endpoint of their own range — every existing test/verification row that used them as a fixed value
still holds, since they're still valid (if now merely the mildest possible) rolls.

## Precipitation

Three states — `Clear`, `Rain`, `Snow` — Winter alternates `Clear`/`Snow` instead of `Clear`/`Rain`;
every other season is unchanged. Snow added at the developer's request (2026-09-17), superseding
this spec's original "no third state" framing.

| | Duration (in-game minutes) | Real-time equivalent |
|---|---|---|
| Clear | 180–480, uniform | 2.5–6.7 min |
| Rain | 20–60, uniform | 16.7–50 s |
| Snow | 20–60, uniform (same range as Rain) | 16.7–50 s |

```
NextState(current, season) =
  current != Clear → Clear
  current == Clear → (season == Winter ? Snow : Rain)
NextDurationMinutes(state, t) =                         // t: caller-supplied [0,1) sample
  Clear        → lerp(180, 480, t)
  Rain or Snow → lerp(20, 60, t)
```

`season` defaults to `Spring` on `NextState`'s signature, so every pre-Snow call site still
alternates `Clear`/`Rain` exactly as before. Snow reuses Rain's duration range rather than
inventing a separate one — no spec basis yet for snow lasting longer/shorter than rain; flagged in
PROJECT_STATE.md §Decided without a spec as a deliberate simplification, first thing to revisit
once winter has actually been played.

`t` is a parameter, not drawn internally, so the duration formula stays pure and testable —
mirrors `VitalsCalculator`'s split between pure formula and the impure `Vitals` that calls it.
The actual `System.Random` draw belongs to the impure controller, not this formula.

While precipitating (Rain or Snow): `AmbientTemp` drops 5 (`PrecipitationTempPenalty`, renamed
from `RainTempPenalty` now that it applies to Snow too) and every player outdoors is wet
(`Vitals.WetPenalty` pinned at its −6 magnitude for the duration, same as `SetWet()`, decaying
normally via `DecayWetPenalty` only once precipitation stops) — snow "wetting" a player the same
as rain is a simplification (real snow doesn't soak you the way rain does), but no separate
frost/cold-dry mechanic exists to reach for instead, and inventing one would be new scope beyond
what was asked (Absolute Rule 6). No indoor/roofed shelter concept exists yet to exempt anyone
either way (Open questions).

## Temperature events

Independent of precipitation — a `ColdSnap` or `HeatWave` can be active regardless of whether it's
`Clear`/`Rain`/`Snow` at the same time, same as RimWorld's actual mechanic (a single combined enum
couldn't represent "raining during a heat wave"). Added at the developer's request (2026-09-17).
Gated by season, same shape as precipitation: Summer alternates `None`/`HeatWave`, Winter
alternates `None`/`ColdSnap`, Spring/Autumn always resolve to `None`. Runs on its own independent
timer in `WeatherController`, parallel to (not combined with) the precipitation timer.

| | Duration (in-game minutes) | Real-time equivalent | Temp offset |
|---|---|---|---|
| None (calm) | 720–2160, uniform | 6–18 min | +0 |
| HeatWave (Summer only) | 120–300, uniform | 1–2.5 min | +10 |
| ColdSnap (Winter only) | 120–300, uniform | 1–2.5 min | −10 |

```
NextTemperatureEvent(current, season) =
  current != None → None
  current == None → (season == Summer ? HeatWave : season == Winter ? ColdSnap : None)
NextTemperatureEventDurationMinutes(state, t) =        // t: caller-supplied [0,1) sample
  None                  → lerp(720, 2160, t)
  HeatWave or ColdSnap  → lerp(120, 300, t)
TemperatureEventOffset(tempEvent) =
  None → 0, HeatWave → +10, ColdSnap → -10
```

Calm duration (720–2160) is deliberately much longer than an active event (120–300) so a heat
wave/cold snap reads as a rare event, not as "half of every summer/winter" — both the calm range
and the ±10 magnitude are invented (no spec, no reference numbers existed for this), flagged in
PROJECT_STATE.md §Decided without a spec, first thing to retune once a summer/winter has actually
been played through.

The resolved temperature composes as:

```
AmbientTemp(phase, state, seasonTempOffset, tempEvent) =
  BaseAmbientTemp(phase) + seasonTempOffset + TemperatureEventOffset(tempEvent)
    - (state != Clear ? PrecipitationTempPenalty : 0)
```

— a second `AmbientTemp` overload alongside the original `(phase, isRaining, season)` one, which is
left untouched (still resolves `SeasonTempOffset`'s fixed constant internally, has no way to take a
`TemperatureEvent`, and every pre-existing call site/test keeps compiling and passing unchanged).

## Verification

| # | Scenario | Expected |
|---|---|---|
| 1 | `BaseAmbientTemp(Day)` | 37 |
| 2 | `BaseAmbientTemp(Dawn)`, `BaseAmbientTemp(Dusk)` | 31, 31 |
| 3 | `BaseAmbientTemp(Night)` | 24 |
| 4 | `AmbientTemp(Day, isRaining: true)` | 32 |
| 5 | `AmbientTemp(Night, isRaining: false)` | 24 |
| 6 | `NextState(Clear)`, `NextState(Rain)` | Rain, Clear |
| 7 | `NextDurationMinutes(Clear, t=0)`, `(Clear, t=1)` | 180, 480 |
| 8 | `NextDurationMinutes(Rain, t=0)`, `(Rain, t=1)` | 20, 60 |
| 9 | `SeasonAt` at day 0, 15, 30, 45, 60 | Spring, Summer, Autumn, Winter, Spring (wraps) |
| 10 | `AmbientTemp(Day, false, Summer)` | 42 |
| 11 | `AmbientTemp(Night, true, Winter)` | 11 |
| 12 | `RollSeasonTempOffset(Summer, t=0)`, `(Summer, t=1)` | 5, 10 |
| 13 | `RollSeasonTempOffset(Winter, t=0)`, `(Winter, t=1)` | −12, −8 |
| 14 | `NextState(Clear, Winter)`, `NextState(Snow, Winter)` | Snow, Clear |
| 15 | `NextDurationMinutes(Snow, t=0)`, `(Snow, t=1)` | 20, 60 |
| 16 | `NextTemperatureEvent(None, Summer)`, `(None, Winter)` | HeatWave, ColdSnap |
| 17 | `NextTemperatureEventDurationMinutes(None, t=0)`, `(None, t=1)` | 720, 2160 |
| 18 | `NextTemperatureEventDurationMinutes(HeatWave, t=0)`, `(HeatWave, t=1)` | 120, 300 |
| 19 | `AmbientTemp(Day, Clear, seasonTempOffset: 10, HeatWave)` | 57 |
| 20 | `AmbientTemp(Night, Snow, seasonTempOffset: -12, None)` | 7 |

## Location

```
Assets/Scripts/World/Time/WorldTime.cs        — live WorldClock holder (T-030 gap, see below)
Assets/Scripts/World/Weather/
  WeatherCalculator.cs  ★ pure — this spec's formulas, EditMode-tested
  WeatherController.cs    impure — server NetworkBehaviour, owns the Random + duration countdowns
```

`WeatherController` reads `WorldTime.Instance.Clock.Phase`; `Vitals` reads
`WeatherController.Instance.AmbientTemp`/`IsPrecipitating` each tick — a pull, not a push, same
reasoning as the campfire-distance query (see the world-object-interaction work landing alongside
this spec, PROJECT_STATE.md). `IsRaining`/`IsSnowing` also exist on the controller for callers that
need to distinguish the two; `Vitals.WetPenalty` doesn't, so it uses the broader `IsPrecipitating`.

`WeatherController` now owns two independent duration countdowns (precipitation, temperature
event) instead of one, each with its own `_remainingMinutes` field and `Advance` loop — see the
class itself. It also rolls and holds this map's four `SeasonTempOffset` values (one per `Season`)
once in `Awake()`, since no live map-generation bootstrap exists yet to roll them at
(`Assets/Scripts/World/Generation/IslandGenerator.cs`/`LandmarkPlacer.cs` are pure, seeded,
deterministic — but nothing in the running game ever constructs one; they're exercised only by
their own EditMode tests). `Awake()` running exactly once per server session is the closest
existing equivalent to "when the map is generated" — flagged in PROJECT_STATE.md §Decided without
a spec as an interpretation choice, revisit if/when a real map-gen bootstrap gets built.

## Open questions

- **Is Night=24 too harsh for solo beta?** First playtest data needed — if a completely
  unsheltered new player can die to the first night before finding a campfire recipe, that's a
  softer number (e.g. 28–30) or a slower approach rate at night specifically, not a design goal.
- **Winter night rain (11) may be too harsh on top of that.** Same concern, compounded — if
  solo beta needs Night retuned, Winter's offset needs re-checking against whatever the new
  baseline is, not just left at −8.
- **Is a ~20-real-hour year the right pace?** `DaysPerSeason=15` matches RimWorld's quadrum
  length at the developer's request, but RimWorld's day is much shorter in real time than
  SYS-WORLD-01's 20-real-minute day — the two aren't running on the same clock, so borrowing one
  number without the other is untested against feel. First playtest data needed.
- **No indoor/roofed shelter exists.** Rain/Snow wets everyone outdoors uniformly; a future
  building system could add a "stays dry" volume. Out of scope here (no such system exists to
  hook into).
- **ColdSnap/HeatWave's calm/event durations and ±10 magnitude are unverified.** No reference
  numbers existed anywhere for this mechanic; needs a full summer and winter played through
  before treating them as final. See §Temperature events.
- **Snow reuses Rain's WetPenalty and duration range as a stand-in.** Both are simplifications —
  snow "wetting" a player like rain, and lasting exactly as long as rain would — revisit once
  there's a reason to differentiate (e.g. a frost/cold-dry mechanic, or a snow-specific duration).
- **The season temperature pool rolls at `WeatherController.Awake()`, not at "map generation".**
  No live map-gen runtime exists to hook into yet — see §Location. If/when one is built, the roll
  should move there so it can be seeded consistently with the rest of the map instead of a fresh
  `System.Random()`.
- ~~No seasons.~~ **Resolved 2026-09-16** — see §Season above. `DaysPerSeason=3` and the four
  offsets are invented (no spec existed), flagged in PROJECT_STATE.md §Decided without a spec;
  first thing to retune once a year has actually been played through.
- ~~Where does `WorldClock` get a live instance?~~ T-030 shipped the type but nothing ever
  instantiated/ticked it (`Vitals` only used the static `MinutesPerRealSecond` constant). Resolved
  here: `WorldTime : NetworkBehaviour` is the minimal server-side holder, still with no client sync
  (T-030's own note) — nothing client-facing reads time yet.
- ~~No snow, cold snaps, or heat waves.~~ **Resolved 2026-09-17** — see §Precipitation and
  §Temperature events above.
- ~~Season temperature offsets are fixed per season, same every map.~~ **Resolved 2026-09-17** —
  see §Season temperature pool above.
