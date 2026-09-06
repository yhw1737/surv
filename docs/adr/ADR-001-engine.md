# ADR-001 · Engine selection

Status: **accepted** · Project: ISLE, solo dev + AI · Basis date: 2026-09-01

## 2026-09-04 · Requirement A deferred — the decision still holds

The developer reprioritised: placeholder shapes, systems first, character rigging to Phase 11
(`BACKLOG.md`). **Requirement A — the ★★★★★ item this ADR was largely argued on — is no longer
what the next six months are spent on.**

That does not reopen the engine choice, for two reasons:

1. **Unity also won B and E,** and B (server-authoritative networking, FishNet) is now the
   *top* priority — T-020–T-025. The new plan leans harder on the requirement Unity won on
   merit, not less.
2. **A is deferred, not cancelled.** Phase 11 still runs the rig, and Godot's negative-scale
   2D IK problem would still be waiting there.

Review conditions are unchanged, except that condition 1 ("the rig prototype doesn't produce
acceptable results in two weeks") now fires in Phase 11 rather than Phase 0. **The cost of that
is real and accepted:** a G1 failure arrives with mechanics already built on top, instead of
before them.

## Decision

> **Unity 6 LTS + Mono scripting backend + FishNet (free).**

Godot 4.6 is attractive on licensing and mod loading, but **it does not support this project's top risk item: a 2D bone rig with mouse-tracking IK.** Godot 4.6 (January 2026) restored IK for 3D only; the 2D `SkeletonModification2D` family still carries the experimental marker meaning it may change or be removed.

You cannot build the core character presentation of an 18-month project on an API the engine says it might delete.

## Weighted requirements

| # | Requirement | Source | Weight |
|---|---|---|---|
| A | **2D cutout rig + runtime IK** (mouse-tracking torso/arms, left-right flip) | SYS-CHAR-01, top risk | ★★★★★ |
| B | **Server-authoritative persistent world** (listen server, large state) | SYS-NET-01 | ★★★★★ |
| C | **3-tier modding + Steam Workshop** | SCHEMA.md, longevity | ★★★★☆ |
| D | Large data-driven content volume | throughout | ★★★☆☆ |
| E | **Ecosystem size** (assets, references, AI code-generation quality) | solo dev | ★★★★☆ |
| F | License cost and risk | — | ★★★☆☆ |
| G | 2D VFX expressiveness | SYS-ART-01 | ★★★☆☆ |

**A and B outweigh everything else combined.** A failing changes the entire art pipeline; B failing means the genre doesn't work.

## Candidates eliminated

| Candidate | Reason |
|---|---|
| Unreal 5 | Weak 2D workflow; C++ modding conflicts with the Workshop strategy; too heavy for one person |
| MonoGame / FNA | No editor or toolchain — a solo dev cannot build tools first |
| Bevy (Rust) | Immature ecosystem; too few references when stuck |
| Custom engine | Impossible solo in 18 months |

## Unity 6 vs Godot 4.6

| Area | Unity 6 | Godot 4.6 | Winner |
|---|---|---|---|
| **A. 2D rig + IK** | 2D Animation package ships IK Manager 2D with Chain (CCD), Chain (FABRIK), and Limb solvers; maintained through August 2026 | IK returned in 4.6 but **3D only**; 2D still experimental | **Unity (decisive)** |
| **B. Networking** | FishNet 4.7.2R (April 2026), MIT, free, no CCU caps, authoritative dedicated-server design, self-hosting allowed | Built-in MultiplayerSynchronizer works but has few large persistent-world precedents | **Unity** |
| **C. Mod loading** | Must be built (Mono assembly loading + Lua sandbox) | `load_resource_pack()` PCK loading is a built-in engine feature | **Godot** |
| **C′. Modding track record** | RimWorld, Valheim, Subnautica, Cities Skylines, 7 Days to Die — **nearly every major moddable survival/colony game is Unity Mono** | No large modding ecosystem precedent | **Unity** |
| **D. Data-driven** | ScriptableObject + Addressables | Resource + PCK | tie |
| **E. Ecosystem** | Vast asset store; abundant AI training data | Fewer assets and references | **Unity (decisive)** |
| **F. License** | Personal free below $200,000 total finances; Pro above that (~$2,310/seat/year, +5% from 12 Jan 2026) | MIT, free forever | **Godot** |
| **G. 2D VFX** | VFX Graph, Shader Graph, URP 2D lights and normal maps | Strong renderer, concise shader language | tie |

**Unity takes A, B, and E — the three irreversible items.**

## Decisive reasoning

**1. Godot's 2D IK fails this specific requirement.** Not merely because it's experimental. SYS-CHAR-01 drives `Torso → Arm_Front → WeaponSocket` by IK *while* flipping the whole sprite past 70°, and the standard flip implementation sets parent X scale to −1. Godot has a reported issue where **negative parent X scale breaks all SkeletonModification2D IK targeting** — precisely the intersection of this game's two core requirements. Workarounds exist (mirror bone rotations instead, or write the solver in GDScript), but that means **carrying the top risk item outside the engine.** Unity's package handles it.

**2. Modding is a Unity strength, not a weakness.** Godot's PCK loading is more elegant on paper, but the survival/colony games with exploded Workshop ecosystems are overwhelmingly Unity Mono. That isn't coincidence: C# plus the Mono runtime plus reflection plus Harmony patching is exceptionally modding-friendly, and the modder population skews heavily C#. **This advantage is conditional on the Mono decision below.**

**3. Solo development makes ecosystem size into velocity.** With no one to ask, the volume of references, assets, tutorials, and answers *is* your development speed. Asset purchases directly attack the art bottleneck (`ART_PIPELINE.md`). And Unity C# dominates AI training data — **which matters directly when AI writes most of the code.**

## Stack

| Layer | Choice | Note |
|---|---|---|
| Engine | Unity 6 LTS | version pinned |
| **Scripting backend** | **Mono** | ★ prerequisite for modding |
| Render | URP + 2D Renderer | 2D lights, normal maps |
| Rig | `com.unity.2d.animation` | Limb solver = arms, look-at = head |
| Sprites | PSD Importer | artist layers straight into the rig |
| Networking | FishNet 4.7.x free | MIT, unlimited CCU |
| Transport | FishNet Steam (invites) + Tugboat (direct) | free plugins |
| Steam | Steamworks.NET | Workshop, invites, achievements |
| Modding T1 | JSON + JSON Schema | in-game editor, built in-house |
| Modding T2 | MoonSharp | pure C# Lua, easy sandboxing |
| Modding T3 | Mono assembly loading + Harmony | server opt-in only |
| Assets | Addressables | same path as mod bundles |
| Storage | SQLite (world) + JSON (definitions) | |
| VCS | Git + Git LFS | |
| CI | GitHub Actions + GameCI | Linux server builds |

## Decisions that must be locked now

**1. ★ Pin the Mono backend**
```
Player Settings → Configuration → Scripting Backend = Mono
```
**IL2CPP makes Tier 3 C# modding effectively impossible** — it AOT-compiles IL to C++, blocking runtime assembly loading. This is exactly why RimWorld and Valheim ship Mono. Trade-off: somewhat lower runtime performance and weaker obfuscation. With no PvP, both are acceptable. Lock this at project init, document it, and assert it in the build script.

**2. Pin the Unity version**
Pick one Unity 6 LTS minor and allow at most one major upgrade before EA. Do not chase releases: 2D Animation package upgrades have been reported to break existing bones and IK Manager 2D rigs, and the character rig is this game's heart. Upgrades go on a branch and merge only after rig regression tests pass. (Unity 6.3 drops Havok Physics from Pro/Enterprise — irrelevant for a 2D project.)

**3. Build the mod API from day one**
Author all internal content in the same JSON schema mods use. Not one hardcoded path. Design Addressables groups identically to mod asset bundles — once they diverge they cannot be merged. **This is how we offset the one category (C) where Unity loses: the engine doesn't provide it, so we build it now rather than in six months.**

**4. Encode the authority model in conventions**
All state changes server-side; clients predict movement only. Apply FishNet's collider-rollback lag compensation to **melee only**; resolve ranged server-side (SYS-NET-01).

## Cost

| Tier | Eligibility | Price |
|---|---|---|
| Personal | total finances ≤ $200,000 (trailing 12 months) | free |
| Pro | $200,001 – $24,999,999 | ~$2,310/seat/year |
| Enterprise | > $25,000,000 | negotiated |

The 2023 install-based Runtime Fee was withdrawn in September 2024; Unity returned to seat subscriptions. No per-install billing risk today.

⚠️ **Trap:** eligibility counts revenue **plus funding**, and above $200,000 you cannot use Personal even for internal prototyping. Investment or grant money crossing ~$200K forces Pro at zero revenue.

**Solo makes this nearly a non-issue:** free before launch, and one seat (~$2,310/year) once revenue passes $200K — at which point you've succeeded. Budget 5–8% annual increases; Unity has stated a yearly adjustment cadence.

## What we take on by choosing Unity

| Burden | Mitigation |
|---|---|
| **Must build the mod loader** (Godot has PCK built in) | Ship the Tier 1 loader early (T-130); dogfood from day one |
| **Mono = slower runtime, easier to reverse** | No PvP; performance comes from deferred chunk simulation |
| License tier changes | Budget 5–8%/year; re-check terms at renewal |
| Trust after the 2023 Runtime Fee episode | Withdrawn, but **no source access remains a structural risk** — see review conditions |
| Heavier server builds than Godot | Dedicated Server module, headless; document community server deployment |

And more importantly: **the engine solves maybe 30% of this game.** Grid inventory, tag cooking, enchant probabilities, deferred chunk simulation, and the mod API are all ours. Engine choice decides where that 30% comes from; it does not reduce the workload.

## Review conditions

Reconsider **only at the end of Phase 0**, never after.

1. **The rig prototype (T-001–T-006) doesn't produce acceptable results in two weeks** — the engine failed the core requirement, so the premise collapses
2. Godot promotes 2D IK to non-experimental in 4.7+ **and** resolves the negative-scale flip issue
3. Unity reintroduces install- or revenue-based billing, or lowers Personal/Pro eligibility
4. FishNet goes unmaintained for 6+ months → **swap the networking library** (Mirror or Netcode for GameObjects), not the engine

**After Phase 1 begins, the engine does not change for any reason.** Writing that down is part of this ADR's purpose.

## First action

`docs/BACKLOG.md` Phase 0 (T-000 – T-006) and `docs/specs/SYS-CHAR-01-rig-aiming.md` §Verification.

Pass criteria: swinging the mouse rapidly produces no catch at the flip; aiming a bow while backpedaling doesn't bend the arm; the same holds for remote characters at 200 ms latency.

On failure, try SYS-CHAR-01's plan B (8-direction sprites, forearm IK only) first. If that also fails, review condition 1 triggers.

---

*License and package details reflect publicly available information as of 2026-09-01. Unity pricing adjusts annually — verify before contracting.*
