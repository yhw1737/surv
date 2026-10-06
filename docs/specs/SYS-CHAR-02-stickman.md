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

## Revision 2026-10-06 — Fancy Pants look, aim, scale (developer feedback)

"좀보이드/돈스타브처럼 캐릭터는 화면에 작게, 나무 같은 큰 물체는 크게" · "팬시 팬츠를 벤치마킹하라 했는데 일반 졸라맨 같고,
얼굴이 있고, 얼굴이 너무 크다" · "무기를 마우스 방향으로 — 창끝이 마우스를 향하게".

| What | Now | Was |
|---|---|---|
| Head | **white disk r 0.15 in a near-black ring 0.042 — no face, no hair** (second feedback round). It looks at the aim: the neck bends toward the mouse (head direction = 0.6·up + aim, spring-smoothed) and the upper body leans back 0.18 rad per rad of upward aim | white disk r 0.30 with eyes, mouth |
| Skeleton | hip 0.74, torso 0.44, neck→head 0.19, thigh/shin 0.38, upper/fore arm 0.29 | 0.64 / 0.40 / 0.32 / 0.33 / 0.26 |
| Limb stroke | 0.068 | 0.085 |
| Gait | walk: foot down 50% of the cycle, half-span 0.38; run (blends in from 4.6 to 6.6 tiles/s): foot down 32%, half-span 0.47, a flight phase, hips sink 0.09, knees lift to 0.32 — the stride itself lengthens (stride = 2·half-span ÷ stance share) instead of the feet just cycling faster | fixed stride 1.1 |
| Arms | swing 0.7 → 1.4 rad with running, reach 0.56 → 0.42 (bent, pumping elbows) | 0.6 rad, straight |
| Lean | 0.075 rad per tile/s, max 0.42 | 0.035, max 0.22 |
| `pants` wear | flared trousers: 0.045 at the hip, 0.055 at the knee, 0.115 at the ankle (half-widths) | straight thick strokes |
| Aim | with an item in the main hand and the local player's mouse: hand reaches along the aim, the item points at it (spear tip at the mouse), the body faces the mouse; swings rotate their arc by the aim, the bow and the guard face it | swings/draw/guard always horizontal |
| Camera | half-height 8.5 tiles (character ≈ 1/11 of the screen height) | 4.95 |
| Standing objects | sprite pivot at the foot, sort by it; tree 3.6, palm 3.4, rock 1.15, bush 1.1, grass 0.9 tiles | centred pivot; 1.4 / 1.4 / 0.9 / 0.9 / 0.8 |
| Figures | mesh bounds centred on the feet so the custom-axis sort compares feet with feet | bounds centre ≈ waist |

All **[invented]** presentation values. Aim is presentation only and local-only (remote players have no aim yet);
melee hits still pick the nearest creature in reach — a forward cone along the aim is SYS-COMBAT-01's T-113 hit
detection, not built here. New verification: 13 held item points along the aim (±0.05 rad); 14 head ≤ 25% of height.

**Roll (same round).** "구르기는 이것보단 빠르게, 중간에 경로를 바꿀 수 있어야": roll speed 2.6× base (was the sprint
1.65×), still 0.6 s with SYS-CHAR-01's i-frames; held direction keys steer it 35% of the way per tick
(`MovementCalculator.SteerRoll` — SYS-CHAR-01 verification 6 "change direction mid-roll"). Both **[invented]**.

**World objects (same round).** `ShapeLibrary` now draws every placeholder in the cartoon ink style: a near-black line
around each primitive (5/256 of the canvas), highlights un-inked; tree (flared trunk, cloud canopy with a dark
underside), palm (segmented leaning trunk, drooping fronds), rock (faceted with a lit top), bush, grass redrawn.
