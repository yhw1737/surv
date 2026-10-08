# SYS-START-01 · Shipwreck start and survivors

Status: **decided 2026-10-08** (Q&A with the developer). Values marked [invented] are the agent's fill-ins, listed in
`docs/PROJECT_STATE.md` §Decided without a spec.

## Premise
A cruise ship goes down in a storm. The player is one of its passengers and wakes on a remote island's beach, soaked,
with wreckage scattered along the shore. Each player is a different passenger.

## Decided values (developer Q&A, 2026-10-08)
| Constant | Value |
|---|---|
| Start | **always the coast**, on a beach next to the open sea |
| Start state | **wet** (SYS-SURV-01 wet penalty −6, drying over 20 in-game minutes) |
| Who | **one random passenger shown at a time, rerolled as often as you like** |
| Clothes | **rolled per passenger**; a T-shirt and shorts is the free baseline; better clothes **cost trait points, at most 2** (only gear that matters early — a padded coat, a bulletproof vest — costs anything; it wears out anyway); less (swimwear) gives a point |
| Trait count | **3–6 per passenger**, background included |
| Clashes | contradictory traits never come together (a soldier is never frail) — `excludes`, checked both ways |
| Trait style | broad character traits (great memory, industrious, brawler…), **not** tiny skill bonuses |
| Jobs | **no job pick**: random traits + random skills; a *background* trait (a past job) raises its skill |
| Traits | about **20** traits; base **0 points** — good traits cost points, bad traits give them; a rolled passenger balances to about 0 |
| Editing | **none** — the roll is who you play |
| Skills | **mostly 0–3**, a background adds **+5** to its skill |
| Belongings | wreckage on the beach + **random belongings** |

## Rolling a passenger
1. Name from the scenario's list; an outfit, weighted.
2. Background with chance **0.7** [invented]: one of 8, +5 to its skill.
3. A target count of 3–6 traits; then repeatedly: if points are owed, a bad trait that pays them (overshooting by at
   most one); otherwise a good or even trait; the last slot takes a small trait so the books stay balanced. Points =
   traits + outfit, ending at **0 or −1**. Never two traits from one `group`, never an `excludes` pair. A draw that
   can't balance is drawn again.
4. Every skill 0 … 3 (uniform) [invented distribution], then the background's +5.

Traits are permanent effects of the same types buffs use (`carry_capacity_mult`, `move_speed_mult`,
`stamina_max_mult`, `stamina_regen_mult`, `xp_mult`, `hunger_drain_mult`, `thirst_drain_mult`,
`hypothermia_threshold_shift`, `damage_resist`, `gather_speed_mult`, `damage_taken_mult`, `aim_sway_mult`,
`buff_duration_mult`, `melee_power_mult`, `ranged_power_mult`, `sprint_cost_mult`).

## Outfits [invented]
| Outfit | Worn | Points | Weight |
|---|---|---|---|
| T-shirt and shorts | cloth T-shirt, cloth shorts | 0 | 4 |
| Swimwear | cloth shorts | **+1** | 1 |
| Casual | cloth T-shirt, cloth pants, cloth shoes | 0 | 3 |
| Dinner clothes | cloth shirt, cloth pants, leather shoes | 0 | 2 |
| Leather jacket | cloth T-shirt, leather jacket, cloth pants | −1 | 1.5 |
| Deckhand | cloth T-shirt, cloth pants, leather boots, cloth cap | −1 | 1 |
| Padded coat | cloth T-shirt, cloth parka, cloth pants, cloth shoes | −2 | 1 |
| Security guard | cloth shirt, **bulletproof vest**, cloth pants, leather boots | −2 | 0.5 |

The bulletproof vest is salvage (armor slash 10 / pierce 14 / blunt 5 / heat 3, durability 200): it can't be made,
so it can't be repaired.

## Traits [invented]
| Good (cost) | Effect | Bad (gives) | Effect |
|---|---|---|---|
| Strong (3) | carry ×1.25 | Weak (3) | carry ×0.8 |
| Athletic (4) | stamina ×1.2, speed ×1.05 | Out of shape (3) | stamina ×0.8 |
| Fast learner (4) | XP ×1.2 | Slow learner (4) | XP ×0.8 |
| Great memory (3) | XP ×1.15 | | |
| Light eater (2) | hunger ×0.75 | Hearty appetite (2) | hunger ×1.3 |
| Camel (2) | thirst ×0.75 | Always thirsty (2) | thirst ×1.3 |
| Cold-tolerant (2) | hypothermia −2° | Feels the cold (2) | hypothermia +2° |
| Iron stomach (2) | poison −30% | Weak stomach (2) | poison +30% |
| Nimble hands (3) | harvest ×1.2 | Clumsy (2) | harvest ×0.8 |
| Industrious (3) | harvest ×1.25 | Lazy (3) | harvest ×0.8 |
| Tough (4) | damage taken ×0.85 | Fragile (4) | damage taken ×1.2 |
| Thick skin (2) | slash/pierce −15% | | |
| Eagle eye (2) | aim sway ×0.7 | Short-sighted (2) | aim sway ×1.4 |
| Quick recovery (2) | stamina regen ×1.25 | Delicate (2) | stamina regen ×0.8 |
| Jogger (3) | speed ×1.1 | Sluggish (3) | speed ×0.92 |
| Sprinter (2) | sprint cost ×0.7 | Asthmatic (2) | sprint cost ×1.4 |
| Sharpshooter (3) | ranged power ×1.2 | | |

Mixed and small: Brawler (+1: melee ×1.2, aim sway ×1.5), Ascetic (+1: hunger ×0.85, dish effects ×0.7), Gourmand
(−1: hunger ×1.4, dish effects ×1.3), Hardy (+1: hypothermia −1°), Steady hands (+1: aim ×0.85), Big-boned (0: carry
×1.1, speed ×0.97), Skinny (−1: carry ×0.9, speed ×1.03), Sweaty (−1: thirst ×1.15), Picky eater (−1: hunger ×1.1),
Restless (−1: stamina regen ×0.9).

Backgrounds (cost 2 each, +5): ship's cook (cooking), fisherman (fishing), forager (gathering), carpenter (crafting),
soldier (melee), archer (ranged), scholar (enchanting), stage magician (magic).

Exclusions: soldier ✗ weak, out of shape, fragile, sluggish, lazy · archer ✗ short-sighted · forager ✗ clumsy, lazy ·
carpenter, magician ✗ clumsy · scholar ✗ slow learner, brawler · athletic ✗ asthmatic, sluggish, delicate · jogger ✗
out of shape, asthmatic · great memory ✗ slow learner · sprinter ✗ out of shape · thick skin ✗ fragile · gourmand ✗
light eater · ascetic ✗ hearty appetite · restless ✗ quick recovery · hardy ✗ feels the cold · steady hands ✗
short-sighted · sweaty ✗ camel · picky eater ✗ light eater.

## The shipwreck scenario [invented]
- Start tile: walk out from the island's middle in a seed-chosen direction to the first open sea, step 2 tiles back;
  it must be a dry coast tile (other directions in 15° steps otherwise). Respawn point starts there too.
- Worn: the rolled outfit (§Outfits).
- Belongings: 1–3 different picks from chocolate ×2, bottled water, cloth ×2, fibre ×3, torch, cloth cap, steel knife
  (weights 3/3/2/1/1/1/0.5).
- Wreckage: 4–6 piles within 10 tiles on dry ground, each wood 1–3, cloth 0–2, fibre 0–2.

All of it lives in `definitions/scenarios/shipwreck.json` and `definitions/traits/*.json`.

## Verification
| # | Case | Expected |
|---|---|---|
| 1 | 1,000 rolls | points (with the outfit) 0 or −1; 3–6 traits; no group twice; no excluded pair; ≤ 1 background; outfit ≤ 2 points; skills in range; about 70% have a background |
| 2 | Same seed | same passenger |
| 3 | Every trait effect | a type the game reads |
| 4 | New island | on a coast tile within 4 of the sea, wet, wearing the rolled outfit, ≥ 4 wreckage piles |
| 5 | Save, back to menu, continue | same name and traits |
