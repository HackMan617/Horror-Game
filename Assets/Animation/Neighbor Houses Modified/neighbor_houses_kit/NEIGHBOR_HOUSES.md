# Neighbor Houses — spruce hamlet · Unity implementation

The three homes on the street you can visit **before** the Nightmare Realm. Cousins of the
player's warm-oak house: same 24×24 modular grammar, same home/nightmare split and animated
window/smoke/door strips — but retextured to **cool weathered spruce** (grey-brown lap siding,
~4/10 weathering), **barn-red** doors & trim, and **wood-shingle** roofs, so the block reads as
a neighborhood, not clones.

Each neighbor is its own house with a distinct **roof shape**, **trim**, and — at night — its
own kind of **wrong**:

| | Roof | Trim | Accent | Nightmare beat |
|---|---|---|---|---|
| **A** | steep gable | covered porch | barn red | door ajar to black; a figure in the one cold-lit upstairs room |
| **B** | hipped (4-slope) | low railing | red-brown | all windows black; a flashlight sweeps one; a pale face at the upstairs glass |
| **C** | saltbox (asymmetric) | shutters | oxblood | chimney dead cold; one downstairs window burns the wrong colour (sick green) |

> 16-px world, **PPU 24, Point (no) filter, Compression None**, real alpha. Swap `home ↔ nightmare`
> on your realm/dread flag, same as the dog and the mountain face.

---

## Files (per house — A shown; same for B, C)

**Tile atlas** (build houses from these, or use them for repair/variation):

| File | Size | Grid |
|---|---|---|
| `neighbor_A_tiles.png` | 192×144 | 8×6 · 24-px cells |
| `neighbor_A_tiles_nightmare.png` | 192×144 | night version |

**Assembled elevations** (drop-in whole-house sprites, like `house_side.png`):

| File | View |
|---|---|
| `neighbor_A_front.png` / `_nightmare` | front (door + windows + porch/trim) |
| `neighbor_A_side.png` / `_nightmare` | side (gable end / eaves) |
| `neighbor_A_side_mirror.png` / `_nightmare` | side, flipped |
| `neighbor_A_back.png` / `_nightmare` | back |

Elevation sizes differ per house (A ≈ 160×182, B ≈ 184×172, C ≈ 184×174) — they're authored
sprites, not tiled, so just place them by their base line.

---

## The tile atlas — rows & cells

Cell `(col*24, row*24, 24, 24)`.

**Row 0 (static):** `wallA` · `wallB` (knot) · `postV` (corner log) · `beamH` (belt) ·
`foundation` (fieldstone) · `doorTop` · `doorBottom` · `window`
**Row 1 (static):** `roofField` · `roofRakeL` · `roofRakeR` · `eave` · `chimney` · `chimneyTop`

**Rows 2–5 (animated strips)** — identical grid/timing to the player house:

| Strip | Row | Frames | ms | Home reads as | Nightmare reads as |
|---|---|---|---|---|---|
| `winSil` | 2 | 6 | 230 | a shape crosses a warm-lit window | shape crosses cold glass |
| `winCandle` | 3, cols 0–3 | 4 | 190 | lamp/candle flicker | a flashlight grazes black glass |
| `smoke` | 4 | 6 | 150 | chimney smoke rising | thin dead wisp (none for C) |
| `loosePlank` | 5, cols 0–3 | 4 | 240 | a board rattles | a shadow crosses the wall |
| `doorOpenT` | 3, cols 4–7 | 4 | 300 | door swings to a warm hall | swings to black; eyes wait |
| `doorOpenB` | 5, cols 4–7 | 4 | 300 | (bottom half of the door) | (bottom half) |

`doorOpen` plays as a ping-pong: hold-closed → open → hold → close. Suggested sequence index
(15 steps @ ~300 ms): `0 0 0 0 0 1 2 3 3 3 3 3 3 2 1`. Same wrong-timing philosophy as the rest
of the game — never a metronome.

---

## Building a house from tiles (if you don't use the elevation sprites)

Front face, per column left→right: `postV | wall/window/door | … | postV`. Stack:
foundation (bottom row) → story 1 → `beamH` belt → story 2 → eave line → roof. Place `chimney`
+ `chimneyTop` offset on the roof. Corner posts at both ends of every wall row.
Roof shape is an **assembly** choice — the same shingle/rake/eave tiles make gable, hipped, or
saltbox depending on how you lay the rake edges. (The provided elevation PNGs already bake each
house's shape; reach for tiles only for procedural/expandable buildings.)

---

## Contexts — when each state shows

- **Daytime / pre-Nightmare street:** `*_front.png` (+ sides/back for walk-around). Windows
  warm-lit, porch friendly, smoke rising. These are safe, visitable homes.
- **Nightmare Realm / night / dread beat:** swap to `*_nightmare.png`. The block goes cold and
  each house turns wrong in its own way (table above) — so re-entering the same street at night
  is unsettling *because* you recognize the houses.
- Drive the swap from the **same flag** as the dog's true form and the mountain apparition, and
  gate the animated strips (`winSil`, `winCandle`, `doorOpen`) to proximity so the wrongness
  only stirs when the player is near.

**Layer order:** sky → mountain range → **houses** (on the ground/street layer, sorted by base
Y) → props → characters.
