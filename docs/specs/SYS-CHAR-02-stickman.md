# SYS-CHAR-02 · Cartoon stick-figure character, vector rendering, animation

Status: **2026-10-06, developer art direction** — "이미지·디자인에 관한 모든 걸 개편. 매우 부드러운 애니메이션이 기본,
핵심은 카툰 그래픽. 졸라맨 카툰, 고해상도·고프레임. 팬시 팬츠 어드벤처의 그래픽을 벤치마킹. 모든 장비는 이 졸라맨 위에
그려질 수 있어야 해." This brings the art stage forward (ART_PIPELINE's "art last" schedule is overridden by the
developer) and replaces `PlayerVisual`'s three circles and SYS-CHAR-01's retired rig. Numbers marked **[invented]**
are presentation values open for tuning.

**Benchmark, not copy.** The Fancy Pants Adventure is the reference for *feel* — thick, slightly loose black line
work, a round expressive head, exaggerated fluid motion, bright flat colours. Every shape here is drawn by our own
code; no asset, pose or frame from that game is reproduced.

## Look

| Property | Value |
|---|---|
| Line work | solid near-black strokes (#16130F), round caps and joins |
| Limb stroke | 0.085 tiles wide **[invented]** |
| Head | white disk, 0.30-tile radius, 0.05-tile outline; two dot eyes and a mouth line; three hair spikes |
| Height | ~1.5 tiles head to toe (ART_PIPELINE §Style) |
| Shadow | soft ellipse under the feet |
| Perspective | top-down 3/4 (ART_PIPELINE): drawn side-on, facing left or right |
| Resolution | **vector** — meshes rebuilt every frame, edges anti-aliased in the shader with screen-space derivatives, so they are crisp at any zoom |

## Rendering

`VectorMesh` collects primitives into one mesh per object per frame, drawn in painter's order:

| Primitive | Use |
|---|---|
| stroke (segment, width) | limbs, weapon shafts, fishing line |
| disk (centre, radius) | head, joints, eyes, round tips |
| polygon (convex, filled) | clothing, hats, blades, bags — always followed by its outline strokes so the fill edge is covered by an anti-aliased line |

Shader `Isle/Vector` (`Assets/Resources/IsleVector.shader`): alpha-blended; a per-vertex distance value is smoothed
with `fwidth` into a ~1.5-pixel edge. Pass `Universal2D` is lit by the URP 2D lights (day/night, torches, campfires)
like every sprite; pass `UniversalForward` is the unlit fallback. Vertex colours are sRGB and converted to linear in
the shader (the project is in linear colour space).

**Sorting:** the URP 2D renderer sorts by a custom axis (0, 1, 0): lower on screen draws in front, the 3/4-view rule.

## Skeleton

Joints are computed every frame; nothing is baked.

```
hip       (0, 0.64)   torso 0.40 up   neck → head centre 0.32 above the neck
legs      thigh 0.33, shin 0.33   — two-bone IK from the hip to the foot target
arms      upper 0.26, fore 0.26   — two-bone IK from the shoulder to the hand target
```
(tiles, origin at the feet) **[invented]**

## Animation

All of it procedural and frame-rate independent; every target value is chased with a critically damped spring,
so poses blend instead of snapping.

| State | Source (gameplay data only — Absolute Rule 7) | Motion |
|---|---|---|
| idle | not moving | breathing bob, occasional arm sway |
| walk / run | position delta per frame (speed) | gait phase advances with distance; feet trace arcs, opposite arm swing, torso leans into speed, head bob, a small squash on each foot plant |
| attack | `GameFeed.PlayerSwing` | wind-up, fast strike arc with the held item, follow-through |
| gather | `PlayerInteraction.Gathering` | looping chop/pick toward the node |
| cast / wait / bite / reel | `PlayerInteraction.Cast`, `.Fight` | cast arc, rod held out with a line to the bobber, a jerk on the bite, cranking while reeling |
| bow draw | `PlayerInteraction.DrawStartedAt` | arm back, bow forward |
| roll | `PlayerMovement.IsRolling` | body tucks into a ball and spins |
| dead | `DeathHandler.IsDead` | topples and lies flat, eyes as crosses |

Facing follows horizontal movement, or the aim when acting; turning is a quick squash through the middle
(no instant flip) **[invented]**.

**Smooth position:** player movement runs on 30 Hz network ticks; the figure is drawn at a position
interpolated between the last two ticks, so it moves every rendered frame. Target frame rate: the display's
refresh rate (vSync), falling back to 120.

## Equipment on the figure

Any item can be drawn on the figure through data — the def says *what* to draw, the figure says *where*:

```json
"wear": { "style": "pants", "color": "#3D6FB6" }
"hold": { "style": "spear", "length": 1.2, "color": "#8A6A48", "tip": "#9FA4AA" }
```

| `wear.style` | Slot it suits | Drawn as |
|---|---|---|
| `cap`, `hood` | head | shape over the head |
| `shirt`, `cloak` | chest | torso fill (cloak flaps behind, lagging the motion) |
| `pants` | legs | thick fills along both legs |
| `boots` | feet | rounded shoes on the feet |
| `backpack`, `pouch` | back / belt | bag behind the torso / on the hip |

| `hold.style` | Drawn as |
|---|---|
| `spear`, `hatchet`, `pickaxe`, `rod`, `torch`, `bow`, `sword` | held in the hand, pointing along the forearm |

An item without a `wear`/`hold` block is simply not drawn. Unknown styles fall back to a generic shape in
the item's colour. Main hand is the front hand; off hand the back hand — a held torch is carried raised and forward
whenever the action leaves that hand free. Draw order: shadow → back items (cloak, backpack) → back arm + off-hand
item → back leg → torso + torso wear → front leg → belt → head + head wear → front arm → main-hand item.
Back limbs are drawn in a lighter ink (#4A433C) for depth.

## Verification

| # | Case | Expected |
|---|---|---|
| 1 | Two-bone IK, target within reach | upper + lower lengths preserved; end effector on the target |
| 2 | Two-bone IK, target out of reach | limb straight, pointing at the target |
| 3 | Critically damped spring, many steps | converges to the target without overshoot |
| 4 | Tick interpolation half-way between ticks | position half-way between the two tick positions |
| 5 | Gait phase | advances by distance / stride length; stays in [0, 1) |
| 6 | Vector mesh with N strokes / disks | 4 vertices and 2 triangles per primitive (quads) |
| 7 | Animator, idle | feet on the ground, head above neck above hip, no rotation |
| 8 | Animator, any pose | thigh, upper-arm and forearm lengths preserved |
| 9 | Animator, walking | never both feet off the ground; each foot lifts |
| 10 | Animator, facing left | facing scale settles at −1 |
| 11 | Animator, dead | figure rotated 90°, lying flat |
| 12 | Animator, half-way through a roll | rotated half a turn |

Visual check (manual, not automated): poses rendered to PNG through the shader — idle, walk cycle, swing, gather,
cast, reel, bow draw, dead, with every `wear`/`hold` style used by the starter items.
