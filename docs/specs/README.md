# System specs

**Implement from these, not from the GDD.** The GDD holds "why"; these hold "how exactly".

| ID | System | Depends on |
|---|---|---|
| [SYS-CORE-01](SYS-CORE-01-definitions.md) | definition loading, tags, IDs | — |
| [SYS-NET-01](SYS-NET-01-authority.md) | network authority | CORE |
| [SYS-CHAR-01](SYS-CHAR-01-rig-aiming.md) | rig, movement, aiming | NET |
| [SYS-SURV-01](SYS-SURV-01-vitals.md) | survival gauges | CHAR |
| [SYS-WORLD-01](SYS-WORLD-01-chunks-time.md) | chunks, time, deferred sim | CORE |
| [SYS-INV-01](SYS-INV-01-grid-inventory.md) | grid inventory, weight | CORE, NET |
| [SYS-SKILL-01](SYS-SKILL-01-skills-focus.md) | skills, focus, rust | CORE |
| [SYS-BUFF-01](SYS-BUFF-01-buffs.md) | buffs (draft — schema only) | CORE |
| [SYS-HUNT-01](SYS-HUNT-01-butchery.md) | hunting, butchery, carrying | SKILL, INV |
| [SYS-FISH-01](SYS-FISH-01-fishing.md) | fishing | SKILL, WORLD |
| [SYS-COOK-01](SYS-COOK-01-cooking.md) | cooking, tag combination, buffs | SKILL, CORE, BUFF |
| [SYS-CRAFT-01](SYS-CRAFT-01-crafting.md) | crafting, quality, enchanting | SKILL, INV |
| [SYS-COMBAT-01](SYS-COMBAT-01-combat.md) | combat, weapons, hit detection | SKILL, NET |
| [SYS-ART-01](SYS-ART-01-artifacts.md) | artifacts (3) | COMBAT, SKILL |

## Spec format

```markdown
# SYS-XXX-NN · Title
## Purpose        one or two sentences
## I/O            parameters, types, ranges, outputs
## Formula        exact math, named constants
## Constants      name = value, tunable flag
## Edge cases     clamps, zero handling, exceptions
## Verification   input → expected output, portable to EditMode tests
## Location       folder and class names
## Open questions what is NOT decided — ask, don't invent
```

## Rules

1. **Every number has a name.** No magic numbers: `CrossPoolFactor = 0.35`, not `0.35`
2. **Verification cases are mandatory.** Without them a solo dev cannot check AI-written code
3. **Never leave "Open questions" empty.** Recording what is undecided is what stops values being invented
4. All numbers are tuning placeholders — but changing one means **editing this doc first** (see `TESTING.md` §6)
