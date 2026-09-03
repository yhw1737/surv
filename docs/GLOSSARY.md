# Glossary

Synonyms fracture the codebase across sessions. Use only these names.

**Identifiers are always English.** Korean appears only in `lang/ko.json` and in conversation with the developer (CLAUDE.md §Language). Never name a class, field, or definition ID in Korean.

## Skills

| Concept | Code | ID | Banned |
|---|---|---|---|
| Production pool | `SkillPool.Production` | — | Crafting Pool, Life Skill |
| Combat pool | `SkillPool.Combat` | — | Battle, War |
| Gathering | `Gathering` | `isle:gathering` | Foraging, Harvesting |
| Hunting/butchery | `Hunting` | `isle:hunting` | Butchery (not separate) |
| Fishing | `Fishing` | `isle:fishing` | Angling |
| Cooking | `Cooking` | `isle:cooking` | Culinary |
| Crafting | `Crafting` | `isle:crafting` | Smithing, Building |
| Melee | `Melee` | `isle:melee` | Blade, Sword |
| Ranged | `Ranged` | `isle:ranged` | Archery, Bow |
| **Focus** | `Focus` | — | Concentration, Specialization |
| XP multiplier | `FocusMultiplier` | — | XpBonus |
| Band weight | `InterferenceWeight` (w) | — | Penalty |
| Cross-pool factor | `CrossPoolFactor` (c) | — | |
| Highest level reached | `PeakLevel` | — | MaxLevel (that's the cap) |
| Decay state | **`Rust`** | — | Decay, Rot, Atrophy |

## Items / inventory

| Concept | Code | Banned |
|---|---|---|
| Item definition (immutable) | `ItemDef` | ItemData, ItemTemplate |
| Item instance (mutable) | `ItemStack` | ItemInstance |
| Grid container | `GridInventory` | Container (different use) |
| Grid footprint | `GridSize { W, H }` | Dimensions |
| Placement | `GridPos { X, Y }` | Cell, Slot |
| Rotated flag | `IsRotated` | Orientation |
| Volume | grid cells; no separate field | Volume |
| Weight | `WeightKg` (float, kg) | Mass |
| Soft weight limit | `FreeWeightKg` | SoftCap |
| Hard weight limit | `MaxWeightKg` | HardCap |
| Equipment slot | `EquipSlot` | Gear |

## Creatures / butchery

| Concept | Code |
|---|---|
| Creature definition | `CreatureDef` |
| Individual weight | `BodyWeightKg` |
| Edible ratio | `EdibleRatio` |
| Condition factor | `ConditionFactor` |
| Butcher quality | `ButcherQuality` |
| Corpse damage factor | `DamageFactor` |
| Corpse | `Carcass` — world object, not an item |
| Two-player carry | `CoopCarry` |

## Cooking

| Concept | Code | Banned |
|---|---|---|
| Cook method (technique) | `CookMethod` | Recipe (that's crafting) |
| Cooked result | `Dish` | Meal, Food |
| Tag reaction | `TagReaction` | Rule |
| Master-cook tag | `CareTag` | Quality (that's the tier) |
| Repeat-eating decay | `SatietyFatigue` | Boredom |

## Crafting / quality

| Concept | Code |
|---|---|
| Craft recipe | `CraftRecipe` |
| Quality tier | `QualityTier` (Crude/Common/Fine/Superior/Master) |
| Enchant slot count | `EnchantSlots` |
| Nearby-ally assist | `AdjacentAssist` |

## Combat

| Concept | Code | Banned |
|---|---|---|
| Weapon base power | `BasePower` | Damage (that's final) |
| Final power | `FinalPower` | |
| Artifact | `Artifact` | Relic, Legendary |
| Scaling skill | `ScalingSkill` | — |
| Zone bonus | `ZoneBonus` | TerrainBonus |
| Artifact fuel | `ArtifactFuel` | Ammo |

## World

| Concept | Code |
|---|---|
| Chunk | `Chunk` (32×32 tiles) |
| In-game time | `WorldTime` (long, in-game minutes) |
| Day length | `DayLengthMinutes` = 1440 in-game min |
| Deferred sim | `DeferredSimulation` |
| Spoilage | `Spoilage` |

## Units

| Quantity | Unit | Type |
|---|---|---|
| Weight | kilograms | `float` |
| Distance | Unity units = 1 tile | `float` |
| Game time | **in-game minutes** | `long` |
| Real time | seconds | `float` |
| Gauges | 0.0–100.0 | `float` |
| Probability | 0.0–1.0 | `float` |
| Multiplier | 1.0 = no change | `float` |

**1 in-game day = 20 real minutes = 1440 in-game minutes.** 1 real second = 1.2 in-game minutes.

## ID namespaces

Base content `isle:`, mods `{mod_id}:`. **Unnamespaced IDs are a load error.** No exceptions.

## Display names

Never put player-facing text in a definition value. Use an `@key` reference:

```json
{ "id": "isle:raw_meat", "name": "@item.raw_meat" }
```
```json
// lang/ko.json          // lang/en.json
{ "item.raw_meat": "생고기" }   { "item.raw_meat": "Raw meat" }
```

Korean is the primary shipping language; English is required for Workshop reach. Both files stay in sync — a missing key falls back to English, then to the raw `@key`.
