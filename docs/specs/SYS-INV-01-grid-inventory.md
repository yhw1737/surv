# SYS-INV-01 · Grid inventory, weight

## Purpose
Tetris-style grid placement. **Volume (cells) and weight (kg) are separate constraints** creating different kinds of pressure.

## Containers

| Type | Size | Notes |
|---|---|---|
| Base carry | 6×3 | always present |
| Straw backpack | 5×5 | back slot |
| Leather backpack | 6×7 | back slot |
| Hunter's backpack | 6×8 | back slot |
| Belt pouch | 2×2 ×2 | quick-access priority |
| Wooden crate | 10×6 | world object |
| Warehouse | 20×10 | world object |

Bags add a **separate** grid; the base grid never grows.

Equipment slots: `Head Chest Legs Feet Back Belt MainHand OffHand`

## Placement
Items have `GridSize {W,H}`. Rotation (R) swaps W and H; square items can't meaningfully rotate. Placement requires all cells in bounds and empty. AABB overlap is sufficient — there are no L-shaped items.

| Item | Grid | kg |
|---|---|---|
| stone, berry, seed | 1×1 | 0.1–0.5 |
| hatchet, dagger | 1×2 | 1.2 |
| waterskin (empty) | 1×2 | 0.5 |
| bow | 2×3 | 1.0 |
| sword, spear, fishing rod | 1×4 | 1.5–3.0 |
| fish trap | 2×2 | 2.0 |
| raw meat chunk (5 kg) | 2×2 | 5.0 |
| small carcass (rabbit) | 2×3 | 3.0 |
| dried meat ×5 | 1×1 | 0.6 |

**Drying and smoking shrink volume** — raw meat 2×2 becomes dried 1×1. This is how the cook solves expedition logistics.

Mid/large carcasses set `inventory_allowed: false`. See `SYS-HUNT-01` §4.

## Weight

```
if total <= FreeWeightKg:  speedMult = 1.0
else:
    over  = total - FreeWeightKg
    range = MaxWeightKg - FreeWeightKg
    speedMult = 1.0 - clamp(over / range, 0, 1) * MaxSpeedPenalty

if total > MaxWeightKg:
    speedMult = OverloadSpeedMult
    rolling disabled, stamina regen halted

staminaDrainMult = 1.0 + clamp((total - FreeWeightKg) / range, 0, 1) * 0.8
```

| Constant | Value |
|---|---|
| `FreeWeightKg` | 15.0 |
| `MaxWeightKg` | 45.0 |
| `MaxSpeedPenalty` | 0.50 |
| `OverloadSpeedMult` | 0.35 |

### Verification (±0.001)

| total kg | speedMult | roll |
|---|---|---|
| 10.0 | 1.000 | yes |
| 15.0 | 1.000 | yes |
| 30.0 | 0.750 | yes |
| 45.0 | 0.500 | yes |
| 50.0 | 0.350 | **no** |

## Required UX — ship from day one

| Feature | Input |
|---|---|
| Rotate | `R` while dragging |
| Bulk move | `Ctrl + click` (container ↔ bag) |
| Move all | button, container UI |
| Auto-sort | button, **warehouse only** — bags stay manual |
| Split stack | `Shift + drag` |
| Tooltip | hover: weight, size, freshness, quality, enchants, crafter |

> Grid inventory is one step from tedium. Without these six it stops being pressure and becomes labor.

## Network
**Server-only, no client prediction** (ARCHITECTURE §Network). Client sends a move request; server validates ownership, container access and reach, destination space, and weight limit; then applies and syncs. While pending, render the item translucent.

Concurrent access (two players, one crate) is serialized server-side; failures trigger a client rollback signal.

## Location
```
Scripts/Gameplay/Inventory/
  GridInventory.cs      pure C# structure — EditMode target
  ItemStack.cs
  EquipSlots.cs
  WeightCalculator.cs   ★ static pure
  InventoryNetwork.cs   server authority
Scripts/UI/Inventory/{GridView,DragHandler,ItemTooltip}.cs
```

## Open questions
- Warehouse auto-sort ordering (by tag? size? frequency?)
- Dropped-item representation (individual objects vs a pile)
- Death drop rules (everything? equipped excluded?)
