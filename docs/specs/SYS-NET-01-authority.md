# SYS-NET-01 · Network authority

## Purpose
Server-authoritative state. **Duplication bugs are fatal for a solo dev, so prediction is minimized.**

## Structure
Listen server (host also plays) for Core. Dedicated servers post-EA — write everything behind `IsServer` branches so splitting later is mechanical. FishNet free tier. Transports: Steam (friend invites) + Tugboat (LAN/direct). Max 4 players.

## Authority

| Target | Authority | Prediction | Rate |
|---|---|---|---|
| Position | server-validated | ✅ | 20 Hz |
| Aim angle | client → server | — | 20 Hz, interpolated |
| `facingSign` | client → server | — | **event, never interpolated** |
| Inventory | **server only** | ❌ | on change |
| Skill XP | **server only** | ❌ | on change |
| Crafting/cooking | **server only** | ❌ | on change |
| Melee hits | server + lag compensation | ❌ | immediate |
| Ranged hits | **server, no lag comp** | ❌ | immediate |
| Vitals | **server only** | ❌ | in-game minute |
| World objects | **server only** | ❌ | AOI |

**Why no inventory prediction:** it is the classic item-duplication source and a solo developer cannot absorb that bug class. Show a translucent pending item instead.

**Why no ranged lag comp:** it rewards high ping. There is no PvP, so server-side resolution is sufficient.

**Why `facingSign` is an event:** interpolating a discrete value makes remote characters jitter left-right.

## AOI
Chunk-based, 3×3 chunks around each player (chunks are 32×32 tiles → 96×96 tile radius).

## Server validation

| Request | Validate |
|---|---|
| Move item | ownership, reach distance, destination space, weight limit |
| Craft | materials held, skill requirement, station proximity, adjacent assist |
| Cook | ingredients held, method unlocked, station proximity |
| Attack | cooldown, stamina, weapon equipped, range |
| Gather | target exists, distance, tool requirement |
| Artifact ability | cooldown, fuel held, skill requirement |

**Never trust client-supplied values.** Requests carry intent only; the server computes results.

## Solo testing rig ★
A co-op game tested by one person. Build these early (T-023), see `docs/TESTING.md`.
1. **Two-client launch script** — build + editor, auto-connect, side-by-side
2. **Dummy client** — headless bot performing random actions
3. **Latency simulator** — FishNet's built-in delay/loss injection

## Verification

| # | Scenario | Expected |
|---|---|---|
| 1 | Two clients grab the same item simultaneously | one succeeds, no duplication |
| 2 | Forged craft request | server rejects |
| 3 | Movement at 200 ms latency | local character doesn't rubber-band |
| 4 | Remote aim at 200 ms | direction correct, no jitter |
| 5 | Host leaves | session ends, world saved |
| 6 | Object outside AOI | not spawned on client |

## Location
```
Scripts/Networking/{IsleNetworkManager}.cs
Scripts/Networking/Authority/RequestValidator.cs
Scripts/Networking/Sync/AoiManager.cs
Scripts/Networking/Debug/LatencySimulator.cs
```

## Open questions
- Host migration (recommend: unsupported in Core)
- How much character state to restore on reconnect
