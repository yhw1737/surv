# SYS-INV-02 · List inventory (supersedes SYS-INV-01 §Containers, §Placement, §Required UX)

## Purpose
Carrying is about **weight, not shapes**. Every container — your pockets, a backpack, a crate, a campfire's fuel — holds
a list of items up to a weight capacity, the way Project Zomboid does it. No grid, no rotating, no Tetris.

> **Developer decision 2026-10-13:** replace the Tarkov-style grid with a Zomboid-style list, a major overhaul.
> SYS-INV-01 §Weight (encumbrance: free 15 kg, max 45 kg, speed curve) stays as it is. Capacities below are [proposed].
> **2026-10-14:** carried capacities raised (developer: "raise the cap a little") — pockets 8→12, pouch 2→3, backpacks
> 10/15/20→15/20/25 kg.

## Containers

| Container | Capacity | Where |
|---|---|---|
| Pockets (base carry) | 12 kg | always |
| Belt pouch | 3 kg | belt slot |
| Straw backpack | 15 kg | back slot |
| Leather backpack | 20 kg | back slot |
| Hunter's backpack | 25 kg | back slot |
| Wooden crate | 50 kg | world object |
| Warehouse | 150 kg | world object |
| Campfire (fuel) | 8 kg | world object, fuel only |

An item fits when the container's contents plus it weigh no more than its capacity. Items of one kind stack (up to the
item's `stack`); gear with wear, quality or enchants stays one per entry. A container's order is the order things went
in. Bags declare `bag_capacity_kg`, storage `storage_capacity_kg` (definitions); the old `bag_grid` / `storage` sizes are
only a fallback for mods that haven't moved yet.

**Too heavy to carry.** A crafted item that fits nowhere drops at your feet. Kits heavier than your bags (furnace 14,
warehouse 15, anvil 16 kg) are set up straight from the ground: placing takes the kit from your bags, else from the pile
within reach; the ground list's right-click menu offers "Place". E over a pile you can't take anything from falls
through to whatever else is in reach (a station, water).

Entries keep a key (`Vec2Int`, first column only) so requests, saves and links can still name "that stack"; it is an
id, not a place.

## Screen (Tab)
- **Left: you.** A row of equipment slots on top; under it a tab per carried container (pockets, each worn bag) showing
  `used / capacity kg`; the selected container's list below.
- **Right: around you.** Tabs for the ground at your feet, each crate within reach, and a campfire you opened with E;
  the selected one's list.
- **A row:** icon, name (identification rules apply, SYS-FORAGE-01), ×count, weight. Hover for the tooltip.
- **Moving:** drag a row onto the other list, onto a tab, onto an equipment slot (wear/wield) or out of an equipment slot
  onto a list. Shift+drag moves one. Double-click or Ctrl+click sends the whole stack to the other side's selected
  container. Right-click: the item's menu (eat, drink, wear, hold, place, plant, tend, move to each container, drop,
  put on the fire / take out).
- Dragging a row off both panels drops it on the ground.
- **Freshness (T-307).** A row of food that spoils carries a bar under its name: full and green when fresh, shrinking
  right to left and turning yellow then red as it spoils, with the percentage beside it (carried containers, crates,
  the fire; not the ground).

## Server
Every move is a request; the server checks capacity and moves what fits (`InventoryNetwork.RequestTransfer`):
`(from container, entry key, to container, count)`. Ground, crate and fire moves use their existing requests.

## Verification

| # | Case | Expected |
|---|---|---|
| 1 | 11.5 kg in pockets, add a 1 kg item | refused (12 kg) |
| 2 | Same, add 0.5 kg | fits, 12.0 / 12 kg |
| 3 | Two stacks of wood moved into a bag that has wood | merge into one entry |
| 4 | Drag a row from pockets to the backpack tab | it moves |
| 5 | Shift+drag a stack of 5 | 1 moves, 4 stay |
| 6 | A furnace kit on the ground, place it | the furnace is built, the kit leaves the pile |

## Location
```
Gameplay/Inventory/GridInventory.cs   list mode (capacity, keys) beside the old grid mode
UI/Inventory/InventoryScreen.cs       the two-panel list screen
UI/Inventory/ItemListView.cs          one container's rows; ItemRow (drag, click, menu)
```

## Open questions
- Bags reducing the weight of what they carry (Zomboid's weight reduction).
- Sorting and filtering a long list.
