# SYS-DIFF-01 · World difficulty

## Purpose
One setting, picked when a world is created, scales how hard surviving is. Four tiers; the fourth is a sandbox for
building and testing with nothing at stake.

> **Developer decisions.** 2026-09-10: tiers scale hunger/thirst drain and enemy attack power; enemy HP is the same at
> every tier. 2026-10-12: the tiers are **Easy / Normal / Hard / Unlimited** (하 / 중 / 상 / 무제한) at **50% / 100% /
> 125%**, and Unlimited is a creative mode. (The 2026-09-10 tiers — Peaceful / Easy / Normal / Hard at 200% — are
> replaced.)

## Tiers

| Tier | ID | DifficultyMult |
|---|---|---|
| Easy | `easy` | 0.50 |
| Normal | `normal` | 1.00 |
| Hard | `hard` | 1.25 |
| Unlimited | `unlimited` | — (creative, below) |

```
hungerDrain = (SYS-SURV-01 hungerDrain) × DifficultyMult
thirstDrain = (SYS-SURV-01 thirstDrain) × DifficultyMult
creatureDamageToPlayer = (creature attack damage) × DifficultyMult      // before block/parry and armor
```

Unchanged at every tier: creature HP, player damage, XP, yields, spoilage, crafting costs. Environmental hazards
that aren't a creature's attack (dungeon miasma, temperature) are not scaled.

## Unlimited (creative)
- The survivor can't be hurt and doesn't need anything: health, hunger, thirst and stamina stay full; no damage of any
  kind lands; nothing kills them.
- **I** opens the item palette: every item in the definitions, searchable; a click takes one, Shift+click a full stack.
  Server-side, and refused unless the world is Unlimited.
- Everything else plays as normal (creatures, skills, recipes, stations), so systems can be tried out.

## Setting it
- Picked on the New game page (default Normal) and saved with the world (save version 10). Older saves load as Normal.
- Fixed for the world's life in the prototype; shown on the load list, the pause menu and the debug screen.

## Verification

| # | Case | Expected |
|---|---|---|
| 1 | Normal, idle hunger drain | SYS-SURV-01 rate × 1.00 |
| 2 | Easy, idle hunger drain | × 0.50 |
| 3 | Hard, creature strike of 20 | 25 before armor |
| 4 | Unlimited, a strike, a day without food | health and hunger still full |
| 5 | Item palette outside Unlimited | refused, nothing given |

## Location
```
Scripts/Gameplay/World/Difficulty.cs   static: tier, multiplier, creative flag
```

## Open questions
- Bosses getting faster patterns on higher tiers (2026-09-10: "may") — not built.
- Changing difficulty after creation.
