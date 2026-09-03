# Art pipeline and sourcing

> **The real bottleneck for a solo developer is art, not code.**
> AI writes an inventory system in a day. It does not draw 200 item icons.
> Decide this before you start coding.

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

## Placeholders ★

**Never stall coding waiting on art.**

| Target | Placeholder |
|---|---|
| Item icons | solid rectangle + first letter |
| Creatures | colored circle |
| Tiles | solid color |
| VFX | default particles |

Put a generator script in `Art/Placeholder/`. When a definition has no icon, fall back automatically.

**Real art is only needed at the gates** — G1 (rig) and G3 (artifacts).

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

```
1  Fix the palette                      half a day
2  Character rig PSD                    required before G1
3  Placeholder generator script         half a day
4  --- start coding here ---
5  Tilesets + basic objects             around Phase 2
6  Item icons (buy + retouch)           alongside Phases 3–5
7  Five creatures                       before Phase 7
8  Artifact VFX                         Phase 8, top priority
9  UI polish                            Phase 9
```

**Stop at step 4 and start coding.** Trying to finish the art first burns six months.

## Open questions
- Commission or draw the character rig (depends on budget)
- Candidate asset packs
- Music direction (ambient? acoustic?)
- Concrete visual concept for artifact VFX
