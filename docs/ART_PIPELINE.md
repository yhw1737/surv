# Art pipeline and sourcing

> **The real bottleneck for a solo developer is art, not code.**
> AI writes an inventory system in a day. It does not draw 200 item icons.

> ### ⚠️ Scheduling decision, 2026-09-06 — art is the *last* of the four build stages
> Nothing in this document blocks anything in `BACKLOG.md` Phases 1–10. The game is built and
> shipped to a solo beta, then to multiplayer, on **vector-shape placeholders** — and only then
> does real art land, in Phase 11.
>
> This document is therefore a **plan for Phase 11**, not a prerequisite for coding. The two
> exceptions are §Placeholders (`T-017`, needed in Phase 1) and the palette (`T-160`, which opens
> Phase 11 before anything is bought).

## Volume estimate (ISLE Core)

| Category | Count | Difficulty | Notes |
|---|---|---|---|
| Character rig parts | 12–16 | 🔴 high | head, torso, 4 arm, 4 leg, 2 hand |
| Character animations | 12 clips | 🔴 high | idle, walk, run, roll, flip, 3 attacks, gather, fish, butcher, death |
| Equipment sprites | 40–60 | 🟠 med | 8–12 each across 5 slots |
| Item icons | **150–200** | 🟡 low | high volume |
| Weapon sprites | 20–30 | 🟠 med | |
| Creatures | 5 × 6 clips | 🔴 high | rabbit, deer, boar, crocodile, bird |
| Tilesets | 3 biomes | 🟠 med | |
| World objects | 60–80 | 🟡 low | trees, rocks, benches, structures |
| UI | 1 set | 🟠 med | grid, windows, icons, font |
| **Artifact VFX** | **3 × 3 abilities** | 🔴 highest | triple the normal budget |
| Sound | 200–300 | 🟡 low | mostly purchased |

**Drawing all of it alone takes 8–12 months** — comparable to the entire development schedule.

## Sourcing per category

**Do not draw everything yourself.**

| Category | Approach | Why |
|---|---|---|
| **Character rig** | **yourself or commission** | The face of the game; it sets the style. Spend time here |
| Character animation | yourself (rig-based) | Cutout means parts are the hard part; animation is fast |
| Equipment sprites | yourself (reuse rig parts) | ~20 min each once the rig exists |
| **Item icons** | **buy + retouch** | High volume, low individuality. Drawing these is a bad trade |
| Weapon sprites | yourself | Must align with rig sockets |
| **Creatures** | **buy or commission** | Animation volume is large — the worst thing to hand-draw |
| Tilesets | buy + retouch | Only the tone needs matching |
| World objects | buy + own mix | |
| **UI** | **buy and reassemble** | Drawing UI yourself eats a month |
| **Artifact VFX** | **yourself, top priority** | ★ The differentiator. Do not economize here |
| Sound | buy all | No reason to make it |
| Music | buy or commission | |

**Budget split if you have one:** 40% rig + animation commission, 25% creature commission, 20% asset packs (icons, tilesets, UI, sound), 15% reserve.

**No budget:** lean on buy-and-retouch, and do only the character and artifact VFX yourself.

## Style spec

Fix this first or purchased assets will never blend.

| Property | Value |
|---|---|
| Tile size | 32 × 32 px |
| Character height | ~48 px (1.5 tiles) |
| Pixel perfect | **no** — smooth sprites suit cutout rigging |
| Outline | yes, dark brown (not black) |
| Palette | fixed 32–48 colors |
| Tone | warm tropical; cool teal only at night |
| Perspective | top-down 3/4 |
| Shadow | ellipse under characters and objects |

> **Do not choose pixel art.** Running IK on a cutout rig breaks the pixel grid. Rig method and art style are a linked decision.

Define 48 colors in `Art/palette.png` and remap every asset — including purchased ones — through it. **This single step removes the patchwork feel.**

## Rig spec (matches SYS-CHAR-01)

```
Character.psd            layer names become bone names — typos break the rig
├─ Head
├─ Torso
├─ Arm_Front_Upper / Arm_Front_Lower / Hand_Front
├─ Arm_Back_Upper  / Arm_Back_Lower  / Hand_Back
├─ Leg_Front_Upper / Leg_Front_Lower / Foot_Front
└─ Leg_Back_Upper  / Leg_Back_Lower  / Foot_Back
```
Draw joints with generous overlap so rotation never opens a gap. Equipment uses the same part structure for swap rendering.

## Naming

```
Art/Characters/  player_rig.psd, player_equipment_{slot}_{name}.png
Art/Items/       icon_{namespace}_{item_id}.png    ← match definition IDs
Art/Creatures/   {creature_id}_{clip}.png
Art/Tiles/       tileset_{biome}.png
Art/Objects/     obj_{name}.png
Art/VFX/         vfx_{artifact_id}_{ability}.png
Art/UI/
```
Icon filenames match definition IDs so binding can be automatic — and modders follow the same rule.

## Placeholders ★ — what the game actually runs on until Phase 11

**Never stall coding waiting on art.** This is not a stopgap; it is stage 1 of the build order.

Placeholders are **vector shapes**: flat, untextured, generated at runtime. A box, a circle, a
solid fill. No pixel work, nothing hand-drawn, nothing to keep in sync with a definition.

| Target | Placeholder shape |
|---|---|
| Item icons | solid rounded rectangle, tag-derived colour, first letter |
| Characters | the `T-001` capsule rig (already built) |
| Creatures | coloured circle, radius from body weight |
| Weapons / tools | solid rectangle at the grip socket |
| Tiles | solid colour per terrain |
| World objects | rectangle or circle by footprint |
| VFX | default particles |

`T-017` puts the generator in `Art/Placeholder/`. **A definition with no art falls back
automatically** — that rule also covers mods, which will always ship with missing art.

Two constraints that keep the eventual swap cheap:
- Placeholder dimensions are **not** spec values. Nothing in a formula may read them
  (`ARCHITECTURE.md` §Presentation boundary).
- Colours come from tags, not from IDs. A new ingredient gets a sensible colour with no C# edit,
  the same way Absolute Rule 4 wants everything else to work.

## Sound

| Category | Count | Source |
|---|---|---|
| UI | 15 | buy |
| Footsteps (per terrain) | 20 | buy |
| Tools / gathering | 30 | buy |
| Combat | 40 | buy |
| Creatures | 30 | buy |
| Ambience | 10 | buy |
| **Artifacts** | **15** | buy + edit |
| Music | 6–8 tracks | buy or commission |

Royalty-free with commercial rights only. Record every license in `Art/LICENSES.md` — you will need it for Steam review or a dispute.

## Order of work

Reordered 2026-09-06 to match the four-stage build order.

```
--- during Phase 1 -------------------------------------------------
1  Placeholder shape generator    T-017     half a day
--- then code, all the way through the solo beta and multiplayer ----
   (Phases 2–10: no art work at all)
--- Phase 11, in this order -----------------------------------------
2  Fix the 48-colour palette      T-160     half a day
3  Character rig PSD              T-003–6   required before G1
4  Item icons (buy + retouch)     T-161     the long pole, 150–200
5  Five creatures                 T-162     buy or commission
6  Tilesets + world objects       T-163
7  UI pass                        T-164
--- Phase 12 --------------------------------------------------------
8  Artifact VFX                   T-123     top priority, triple budget
```

**Step 1 is the only art task before Phase 11.** Everything else waits. Trying to finish art
first burns six months, and doing it before the systems are proven means redrawing whatever the
design changes.

**Real art is only needed at G1 (rig, Phase 11) and G3 (artifacts, Phase 12).** No gate before
those can fail for want of a sprite.

## Open questions
- Commission or draw the character rig (depends on budget)
- Candidate asset packs
- Music direction (ambient? acoustic?)
- Concrete visual concept for artifact VFX
