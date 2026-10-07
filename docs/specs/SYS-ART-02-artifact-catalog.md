# SYS-ART-02 · Artifact catalog and acquisition

Status: **design, numbers decided 2026-10-06** (Q&A with the developer) — developer direction: "유물은 던전 보상 및 희귀 확률 맵 스폰 또는 전리품. 유물 개수
늘리자 — 요리사는 국자, 뒤집개, 식칼 등 다양한 데미지·피격 판정. 다른 직업도 늘려. 무기뿐 아니라 다른 장비도 유물."
Extends SYS-ART-01; numbers inherited from it unless decided below. The **Decided values** table below supersedes any ❓ left in the text.

## Decided values (developer Q&A, 2026-10-06)
| Constant | Value |
|---|---|
| Attunement (scaling skill) | tier 1 **Lv 15**, tier 2 **Lv 25**, tier 3 **Lv 40** |
| Attuned at once | **one weapon artifact** (the hand) + **any number of gear artifacts** the slots allow |
| Landmark caches | **2–3 per island** |
| Elite drop | **2%** per elite kill |
| Duplicates | allowed — the same artifact can appear more than once on an island |

## What stays (SYS-ART-01 hard rules)
- Artifacts scale off a **production** skill, use **no combat skill** and grant **no combat XP** (Absolute Rule 5).
- Weapon `ArtifactBasePower ≤ 33` (31 by default); raw DPS 75–85% of a combat specialist; only the zone bonus
  goes past 100%. Strong on **different axes** — no power ranking.
- Abilities burn your discipline's own resources (fuel); every number lives in `artifacts/*.json`.

## What changes
| Was (SYS-ART-01) | Now |
|---|---|
| 3 artifacts | **Per profession: 4 weapons + 3 gear** (5 professions → 35), more by mods |
| Weapons only | **Gear artifacts** in head / chest / legs / feet / back / belt / off-hand slots |
| Skill Lv 40 + a quest, one per character | **Found**: dungeon boss chests (each dungeon has a pool), rare landmark caches (❓% per island), rare elite drops |
| — | **Attunement**: wielding needs the scaling skill at the artifact's tier — T1 ❓, T2 ❓, T3 ❓ (e.g. 15/25/40). Anyone can carry and trade it; only the attuned get its power. |
| — | **Not unique**: the same artifact can turn up more than once on an island (developer, 2026-10-06) — a party can carry two Ladles |

Gear artifacts obey the same spirit: their bonuses scale with the production skill and lean on that profession's
resources (dishes, bait, metal, gathered goods, runes).

## Catalog
Type/shape per SYS-COMBAT-02. ★ = already specified in SYS-ART-01. Pools: where it's found.

### Cook — Cooking
| Artifact | Slot | Type · shape | Identity | Pool |
|---|---|---|---|---|
| ★ Great Cauldron Ladle | 2H weapon | blunt · wide arc/smash, heat broth | damage + support: throw dishes to allies | Hollow, Temple |
| Searing Spatula | 1H weapon | blunt · wide arc + **flip** (knockback, briefly airborne) · heat | crowd control: flip enemies off you, sear on landing | Grotto, Ruin |
| Butcher's Cleaver | 1H weapon | slash · heavy narrow arc · **bleed** | anti-beast: bonus vs `animal`, kills raise butchered yield | Hollow |
| Chef's Knife | 1H weapon | pierce · fast **thrust** combo · crit on staggered | precision: "fillet" weak points, best after an ally's blunt stagger | Temple, Abyss |
| Hearth Apron | chest | — | heat resist; **dish buffs +❓%** while worn | Grotto |
| Toque of the Feast | head | — | dish buff **duration +❓%** for the whole party you fed | Ruin |
| Spice Belt | belt | toxic · thrown cloud | throw spice: blind/poison cloud, fuel = spice ingredients | Temple |

### Angler — Fishing
| Artifact | Slot | Type · shape | Identity | Pool |
|---|---|---|---|---|
| ★ Abyss-Caller's Rod | 2H weapon | pierce · line, pull · grapple | mobility/control near water | Grotto, Abyss |
| Leviathan Harpoon | 2H weapon | pierce · **projectile on a line** (thrown, reeled back) | long-range spike, pins targets | Abyss |
| Tidecaller Net | off-hand | — · cast **area entangle** | control: snare a group, bonus vs swimmers | Grotto |
| Hookblade | 1H weapon | slash · short arc + **pull** | duelist: drag one target into your reach | Temple |
| Tidewalker Boots | feet | — | walk on shallow water, swim speed +❓% — Grotto's tides become a road | Grotto |
| Diver's Helm | head | — | breathe in flooded rooms, see further in darkness | Temple, Abyss |
| Creel of Plenty | back | — | bag: fish never spoil inside, bait refills slowly | Grotto |

### Blacksmith — Crafting
| Artifact | Slot | Type · shape | Identity | Pool |
|---|---|---|---|---|
| ★ Unbroken Anvil Hammer | 2H weapon | blunt · slow smash, armor break, heat | tank: breaks armor, field repair | Ruin |
| Forgefist Gauntlets | 1H weapon (both hands) | blunt · fast punches · heat builds | brawler: heat stacks into a burst | Ruin, Abyss |
| Titan's Tongs | 1H weapon | blunt · **grab** (disarm / hold a small foe) | utility: pull shields, hold a target for allies | Ruin |
| Rivet Driver | 1H weapon | pierce · thrust · **armor shred** | anti-armor: each hit lowers pierce armor | Temple |
| Forgeplate Cuirass | chest | — | heavy armor that **self-repairs** with metal fuel | Ruin |
| Master's Goggles | head | — | reveal weak points and durability; craft quality +❓ | Ruin |
| Ever-Sharp Whetstone | belt | — | field-sharpen allies' blades: slash +❓% for ❓ s | Hollow |

### Farmer / Gatherer — Gathering
| Artifact | Slot | Type · shape | Identity | Pool |
|---|---|---|---|---|
| Woodsman's Greataxe | 2H weapon | slash · heavy arc · **cleave** (hits all in arc) | wrecker: bonus vs wood/plant foes, fells trees in one blow | Hollow |
| Prospector's Pick | 1H weapon | pierce · smash · **shell break** | anti-shell: cracks crabs and stone, ore drops on kills | Grotto, Abyss |
| Reaper's Sickle | 1H weapon | slash · **sweep** 360° | swarm-clearer; harvests plants it hits | Hollow, Temple |
| Thornwhip | 1H weapon | slash · line · **toxic** thorns | ranged-ish control, roots the target briefly | Temple |
| Forager's Basket | back | — | bag: gather speed +❓%, extra yield chance | Hollow |
| Rootbound Boots | feet | — | no slow on rough ground; **can't be knocked back** while planted | Hollow |
| Wayfinder's Hat | head | — | shows resource clusters and dungeon entrances on the map | Grotto |

### Enchanter — Enchanting
*(production scaling; the Enchanter's `isle:magic` combat skill is a separate path — artifacts never use it)*
| Artifact | Slot | Type · shape | Identity | Pool |
|---|---|---|---|---|
| Runequill | 1H weapon | pierce · **projectile** glyphs | writes glyphs that fire on their own; fuel = runes | Ruin |
| Censer of Embers | 1H weapon | heat · **aura** | burns foes around you, warms allies (SYS-SURV-01 temperature) | Abyss |
| Mirror Ward | off-hand | — | reflects one projectile per ❓ s | Ruin |
| Glyphbound Tome | off-hand | — | stores one enchant to apply in the field | Temple |
| Starweave Mantle | chest | — | toxic/heat resist; enchant power +❓% | Abyss |
| Circlet of Insight | head | — | reveals hidden doors and rune solutions (dungeon soft gates) | Ruin |
| Glyph Pouch | belt | — | carry runes; one free rune per ❓ min | Temple |

## Balance notes
- Weapons per profession cover different shapes and types, so a Cook can pick *how* to fight (control with the
  Spatula, precision with the Knife) — not *how hard*.
- Gear artifacts never add raw damage; they bend survival, mobility and team support around the profession.
- The ★ three keep their SYS-ART-01 abilities and verification cases unchanged.

## Open questions (developer)
- Every ability number (cooldowns, fuel, radii, buff percentages) — asked per artifact right before it is built.
- Which creatures count as elites (decided with the dungeon creature defs).

## Build order
Same gate as SYS-ART-01: **build the Ladle first and playtest it** (G3). Then one weapon per profession, then gear.
