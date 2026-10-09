# SYS-MAP-01 · Fog of war, map, markers

Status: prototype spec (2026-10-05), from the developer's request ("fog of war 구현해줘, 미니맵 확대 및 축소,
마킹 가능하게"). Numbers marked **[invented]** are open for review.

## Fog of war

Each player remembers what they've seen. The island starts hidden; walking reveals it.

```
cell        = 2 × 2 tiles                     // fog resolution, 576 × 576 cells for a 1152-tile map (was 4 × 4 until 2026-10-06)
reveal      = every cell whose centre is within 24 tiles of the player, checked 4× a second   // 16 until 2026-10-09 (developer: wider for the tilted view)
explored    = permanent (no re-fogging), saved with the game
```

- **World:** unexplored ground is covered by a dark overlay drawn above everything else in the world, so
  creatures and nodes there can't be seen either. The edge is soft (bilinear).
- **Map and minimap:** unexplored cells draw black.
- Per player; in the prototype only the host's own fog exists (no sync — Phase 10).

## Minimap and map

| | Minimap (corner) | Map (`M`) |
|---|---|---|
| Centre | the player | the player |
| Zoom | mouse wheel over it, or `=` / `-` | mouse wheel, or `=` / `-` |
| Zoom levels (tiles across) | 64, 128, 256, 512, 1152 | 128, 256, 512, 1152 |

Both show campfires, loot piles, players and markers.

## Markers

- On the map (`M`): **left-click** places a marker; **right-click** removes the nearest one within 14 screen pixels
  (at any zoom) **[invented]**. Markers cycle through four colours **[invented]**.
- Markers show on the minimap, and in the world as a pin with the distance in tiles; one off-screen shows as
  an arrow at the screen edge.
- Saved with the game. Up to 32 markers **[invented]**; placing a 33rd removes the oldest.

## Verification

| # | Case | Expected |
|---|---|---|
| 1 | Reveal at a tile, radius 16 | cells within 16 tiles explored, a cell 24 tiles away not |
| 2 | Explored set → text → explored set | identical |
| 3 | Add a 33rd marker | the oldest is gone, 32 remain |
| 4 | Remove near a marker / far from all | removes it / removes nothing |
