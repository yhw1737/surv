# Testing

**How a solo developer substitutes for QA.** You must be able to verify AI-written code alone.

## Three layers

| Layer | Covers | Automation | Frequency |
|---|---|---|---|
| **EditMode** | formulas, calculations | full | every commit |
| **PlayMode** | integration, network | partial | task completion |
| **Manual play** | fun, feel | none | weekly + gates |

## EditMode — most important

**Every spec's verification cases become tests verbatim.** A spec with verification cases and no matching test means the implementation is incomplete.

```csharp
[TestCase(62f, 5,  0.4f,  0.95f, 0.45f, 1.1f, 10.0f)]
[TestCase(62f, 30, 1.0f,  0.95f, 0.45f, 1.1f, 21.5f)]
[TestCase(62f, 50, 1.15f, 1.0f,  0.45f, 1.3f, 40.1f)]
[TestCase(62f, 30, 1.0f,  0.60f, 0.45f, 1.1f, 16.9f)]
public void TotalEdibleKg_MatchesSpec(float w, int lv, float tool,
    float dmg, float ratio, float cond, float expected)
{
    Assert.AreEqual(expected,
        ButcheryCalculator.TotalEdibleKg(w, lv, tool, dmg, ratio, cond), 0.15f);
}
```

This requires formulas to be **static pure functions** (ARCHITECTURE §Patterns). Anything depending on MonoBehaviour or network state is untestable, which means unverifiable.

### Required coverage

| Target | Spec | Cases |
|---|---|---|
| `FocusCalculator` | SYS-SKILL-01 | 9 |
| `WeightCalculator` | SYS-INV-01 | 5 |
| `ButcheryCalculator` | SYS-HUNT-01 | 5 |
| `PowerCalculator` | SYS-COMBAT-01 | 7 |
| `ArtifactPowerCalculator` | SYS-ART-01 | 6 |
| `QualityCalculator` | SYS-CRAFT-01 | 5 |
| `CookingResolver` | SYS-COOK-01 | 9 |
| `VitalsCalculator` | SYS-SURV-01 | 6 |
| `TagRegistry` | SYS-CORE-01 | 8 |
| `FishSelector` | SYS-FISH-01 | 6 |

**66 tests total.** This is the balancing safety net — change a number, update the test alongside it.

## PlayMode — the co-op problem

**A co-op game tested by one person.** Three tools solve it; build them at T-023.

**Two-client launch script** (`Tools/run_two_clients.sh|bat`) — build hosts a world, editor auto-connects, windows tile side by side. If it takes manual rebuilding and connecting every time, you will simply stop testing multiplayer.

**Dummy client** — headless bot performing random movement, gathering, and inventory operations. Used for 4-player load and for duplication detection (assert total item count).

**Latency simulator** — FishNet's built-in delay/loss injection. **Default your test environment to 100 ms.** Testing only at 0 ms means finding the problems after release.

### Required scenarios

| # | Scenario | Expected |
|---|---|---|
| 1 | Two clients grab one item simultaneously | no duplication |
| 2 | Forged craft request | server rejects |
| 3 | Movement at 200 ms | no rubber-banding |
| 4 | Remote aim at 200 ms | no jitter |
| 5 | Chunk unload then reload | state preserved |
| 6 | Host quits | world saved |

## Manual play

**Weekly, 30 minutes, from a fresh start.** A solo developer who stops playing their own game is the biggest risk. Put it on the calendar.

Ask: is it fun, what is irritating, what do I want to do next?

### Gate playtests

| Gate | Participants | Question |
|---|---|---|
| **G1** rig | 5, blind | "Does this character's movement look off?" |
| **G2** roles | 5 pairs, 90 min | "Did the two of you naturally do different things?" |
| **G3** artifacts | 5 groups of 3 | "Did the cook feel like a spectator?" / "Did it feel spectacular?" |
| **G4** final | 5 groups + 2 modders | everything + "Could you build a mod?" |

**G1 and G3 send you backward.** Do not continue past a failed gate.

**Show the game to outsiders at least every three months**, gate or no gate. Alone, you find out it isn't fun six months late.

## Regression

| When | Run |
|---|---|
| every commit | full EditMode (seconds) |
| task completion | EditMode + relevant PlayMode |
| weekly | everything + manual play |

CI (GitHub Actions + GameCI) runs EditMode automatically. Failures don't merge.

## Changing balance numbers

1. **Edit the spec sheet first** — not the code
2. Recompute the verification cases' expected values
3. Update the tests
4. Update the code constants
5. Confirm tests pass

**Skip this order and spec and code diverge — at which point the specs are worthless.**
