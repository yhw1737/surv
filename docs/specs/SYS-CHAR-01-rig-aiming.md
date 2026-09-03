# SYS-CHAR-01 · Rig, movement, aiming

## Purpose
Keep left/right sprites while head, torso, arms, and weapon track the mouse naturally.
**The project's top risk.** If this fails, the whole art direction changes.

## Rig
```
Root
 Hip
   Leg_L, Leg_R              movement-direction animation
   Torso                      mouse-direction twist
     Head                     look-at
     Arm_Back                 IK → support grip
     Arm_Front → WeaponSocket IK → aim point
```
Unity `com.unity.2d.animation` + `IK Manager 2D`. Arms use the **Limb solver** (2-bone). Head look-at is implemented directly — Unity 2D IK has no look-at solver.

## Angles
```
aimAngle   = angle from character to mouse world position
facingSign = +1 (right) or -1 (left)
relAngle   = angle relative to facing forward
```

| Part | Range | Smoothing |
|---|---|---|
| Head | ±`HeadMaxAngle` 70° | SmoothDamp 0.08 s |
| Torso | ±`TorsoMaxAngle` 20° | SmoothDamp 0.12 s |
| Arm_Front | unbounded (IK target) | immediate |

```
if |relAngle| > FlipThreshold(70°) held for FlipHoldTime(0.12 s):
    flip facingSign, play transition
```

**`FlipHoldTime` is the key parameter** — it stops the sprite oscillating when the mouse jitters near the threshold.

Transition: `FlipDurationSec = 0.13` (~8 frames at 60 fps). Interpolate upper-body IK to the target angle during the transition — never snap. Buffer attack inputs during the flip and fire them after.

## Movement
```
moveSpeed = BaseSpeed * weightMult (SYS-INV-01) * terrainMult * stanceMult
BaseSpeed = 4.2 tiles/s
```

| State | stanceMult |
|---|---|
| walk | 1.00 |
| sprint (Shift) | 1.65, stamina 12/s |
| crouch (C) | 0.45, noise −70% |
| dragging | `SYS-HUNT-01` |

**Legs follow movement, torso follows the mouse.** When they oppose, play the backpedal animation.

Dodge roll (Space): 0.6 s total, i-frames 0.1–0.45 s, stamina 25, disabled when overweight. Direction = movement input, or away from the aim if no input.

## Weapon grips

| Grip | Arm_Front | Arm_Back | Used by |
|---|---|---|---|
| `one_hand` | weapon socket | free (animated) | sword, axe |
| `two_hand` | weapon socket | support grip | bow, spear, rod, ladle |
| `shield` | weapon socket | shield socket | post-EA |
| `tool` | tool socket | free | pickaxe, shovel |

Read from `WeaponDef.grip`. Swapping weapons must only change IK targets.

## Network

| Item | Method |
|---|---|
| Position | server-validated + client prediction, 20 Hz |
| `aimAngle` | client → server → observers, 20 Hz, interpolated |
| `facingSign` | **reliable event, never interpolated** |

> ⚠️ Interpolating `facingSign` makes remote characters flicker left-right. It is a discrete value — send it as an event.

**Remote characters must run the same IK.** Looking fine locally and breaking in multiplayer is the classic 2D IK trap; include this in the completion criteria.

## Verification

Manual only (not unit-testable). Pair with a **blind evaluation by 5 outsiders**.

| # | Scenario | Pass condition |
|---|---|---|
| 1 | Swing the mouse rapidly left-right | no catch at the flip, no oscillation |
| 2 | Backpedal while aiming a bow | arm doesn't bend or invert |
| 3 | Micro-jitter near the 70° threshold | sprite doesn't ping-pong |
| 4 | Swap 3 weapons in sequence | grip transitions read naturally |
| 5 | **Observe a remote character at 200 ms latency** | aim accurate, no body jitter |
| 6 | Change direction mid-roll | animation doesn't break |

## Plan B
If cases 1–3 don't pass within two weeks: **8-direction sprite sheets with IK only on the forearm and weapon.** Body and legs become pre-rendered directional sprites; art volume rises but the risk disappears.

If that also fails, `docs/adr/ADR-001-engine.md` §10 review condition 1 triggers.

## Location
```
Scripts/Gameplay/Character/
  CharacterMovement.cs
  CharacterRig.cs           angles and flipping
  AimController.cs          mouse → aimAngle
  WeaponGripBinder.cs
  CharacterNetworkSync.cs
```

## Open questions
- Exact keyframes for the flip transition (artist work)
- Rig handling while swimming
- Whether crouching limits torso angle
