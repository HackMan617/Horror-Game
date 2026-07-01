# Sprite & Tile Assets — Animation & Atlas Reference

Handoff notes for wiring these sheets into the game. Every sheet is hand-authored
pixel art on a **16 px world grid**. This doc focuses on **animation frames**: which
cells are frames, how many, in what order, and at what speed.

---

## Global conventions

| Thing | Value |
|---|---|
| World tile size | **16 × 16 px** |
| Character / big-prop cells | multiples of 16 (dog 32×32, hero trees 32×48) |
| Scaling | integer only (2×, 3×, 4×). Never fractional — it shimmers. |
| Filtering | nearest-neighbour. In CSS: `image-rendering: pixelated;` In engines: point/nearest sampling. |
| Sprite origin | **top-left** of the cell. For grounded props, place so the sprite's bottom row sits on the ground line. |
| Transparency | PNGs have a real alpha channel. Draw terrain first, then overlays/props on top. |
| Frame math | `frame = Math.floor(elapsedMs / frameMs) % frameCount` (unless noted). |

All sheets share one warm "home" palette. The nightmare dog and the eerie props are
the same palette pushed cold / desaturated — not a different set of hues.

---

## File inventory

| File | Size | Cell | Grid | Animated? |
|---|---|---|---|---|
| `grass_tiles.png` | 128×48 | 16×16 | 8×3 | yes — 4 animated strips |
| `path_cobble.png` | 128×32 | 16×16 | 8×2 | yes — puddle (4f) |
| `path_flag.png` | 128×32 | 16×16 | 8×2 | yes — puddle (4f) |
| `path_brick.png` | 128×32 | 16×16 | 8×2 | yes — puddle (4f) |
| `props_autumn.png` | 128×96 | mixed | atlas | yes — tree, crow, leaves |
| `dog_cream_nightmare.png` (+ `chocolate`, `apricot`) | 192×128 | 32×32 | 6×4 | yes — per-row, corrupted timing |

`-8x.png` variants are preview blow-ups only — **do not ship them**, use the base files.

---

## 1 · `grass_tiles.png` — terrain (16×16, seamless all sides)

Coordinates are **(col,row)**, 0-indexed. Pixel rect = `(col*16, row*16, 16, 16)`.

**Row 0 — static tiles:** `grass`(0,0) `grass2`(1,0) `grass3`(2,0) `grassPebble`(3,0)
`halfDirt`(4,0) `dirtPath`(5,0) `dirtPath2`(6,0) `dirtPatch`(7,0).
Every grass tile is grass on top with a thin dirt strip along the bottom — they tile
seamlessly in all directions, so fill a field by repeating any mix of the four grass
variants.

**Rows 1–2 — animated accents (4 frames each, loop):** sprinkle these sparsely over
the plain grass base.

| Name | Frames (col,row) | frameMs | Loop | Reads as |
|---|---|---|---|---|
| `flowerBob` | (0,1)→(3,1) | **340** | forward loop | gold flower nodding |
| `tuftBob` | (4,1)→(7,1) | **280** | forward loop | grass tuft swaying |
| `dirtShimmer` | (0,2)→(3,2) | **200** | forward loop | dust settling on soil |
| `flowerDaisy` | (4,2)→(7,2) | **360** | forward loop | white daisy, off-phase |

Deliberately **different speeds** so a field never pulses in sync. Frame 0 of each is
also a valid static tile if you don't want motion.

---

## 2 · `path_cobble.png` (and `path_flag` / `path_brick`) — pathway overlay

**Transparent overlay tiles** — draw them *on top of* the grass field; the stone core +
gravel shoulder are opaque, the rest is alpha. Straights are **seamless in their run
direction**. All three material files share the identical layout below.

**Row 0 — routing (static):**

| Cell (col,row) | Name | Connects |
|---|---|---|
| (0,0) | `straightV` | N–S |
| (1,0) | `straightH` | E–W |
| (2,0) | `elbowNE` | N+E |
| (3,0) | `elbowNW` | N+W |
| (4,0) | `elbowSE` | S+E |
| (5,0) | `elbowSW` | S+W |
| (6,0) | `threshold` | S only (meets the house) |
| (7,0) | `full` | 4-way fill / plaza |

**Row 1 — accents:** `cross`(4,1) `teeSEW`(5,1) `capN`(6,1, start cap) `fullMoss`(7,1).

**Animated — `puddleV` (4 frames):**

| Frames (col,row) | frameMs | Loop | Notes |
|---|---|---|---|
| (0,1)→(3,1) | **170** | forward loop | a `straightV` with a shallow puddle; a specular glint travels across the 4 frames |

Drop `puddleV` in place of an occasional `straightV` on a vertical run. (There's no
horizontal puddle — rotate in-engine if you need one.)

To lay a bending route: pick the tile whose open sides match the neighbours (this is a
simple "line" autotile — 2 straights + 4 elbows + caps/junctions).

---

## 3 · `props_autumn.png` — outdoor props (transparent atlas)

Mixed cell sizes — use the rects below. Origin top-left; grounded props already have
their base at the bottom of the cell. Contact shadows are **not** baked in — draw a soft
dark ellipse under big props yourself. Paste-ready atlas:

```js
// [x, y, w, h, frames]  — pixel rect of frame 0; frames laid out horizontally
const PROPS = {
  bareTree:   [  0,  0, 32, 48, 3],  // animated — sway
  hollowTree: [ 96,  0, 32, 48, 1],  // static (see "cold eyes" note)
  fallenLog:  [  0, 48, 32, 16, 1],
  woodpile:   [ 32, 48, 32, 16, 1],
  bench:      [ 64, 48, 32, 16, 1],
  fence:      [ 96, 48, 16, 16, 1],  // tiles horizontally
  gate:       [112, 48, 16, 16, 1],
  mushHomey:  [  0, 64, 16, 16, 1],  // cozy red toadstools
  mushSickly: [ 16, 64, 16, 16, 1],  // pale, unsettling
  planks:     [ 32, 64, 16, 16, 1],
  acorns:     [ 48, 64, 16, 16, 1],  // acorns + pinecone
  crow:       [ 64, 64, 16, 16, 2],  // animated — blink
  rock:       [ 96, 64, 16, 16, 1],
  leaves:     [  0, 80, 16, 16, 4],  // animated — skitter
};
// frame N rect: [x + (N % frames)*w, y, w, h]
```

**Animated props:**

| Name | Frames | frameMs | Loop | Behaviour |
|---|---|---|---|---|
| `bareTree` | 3 | **240** | forward loop (0→1→2→0) | crown creaks: frame 0 = neutral, 1 = leans right, 2 = leans left. Roots/trunk stay put. |
| `crow` | 2 | **event** | mostly frame 0 | "too still": hold frame 0 (eye open); show frame 1 (blink) for ~**140 ms** every ~**3400 ms**. |
| `leaves` | 4 | **190** | forward loop | a few leaves skitter across the tile. |

```js
// crow blink (not a simple modulo):
crowFrame = (elapsedMs % 3400) < 140 ? 1 : 0;
```

**`hollowTree` "cold eyes":** the two pale eye-pixels deep in the hollow are a *code
effect*, not a frame. If you want them to breathe, draw two 1px `#aeb8b8` dots at local
(14,29) and (17,30) inside the cell with alpha oscillating `0.42 + 0.30*sin(t/430)`.
Otherwise the static sprite already has faint eyes.

**Placement notes:** hero trees are 2×3 tiles (32×48) — reserve that footprint and put
the trunk base on the ground row. `fence` repeats horizontally into a run; cap it with
`gate`. Perch the `crow` on a branch or fence rail, not floating.

---

## 4 · `dog_*_nightmare.png` — companion (32×32, 6×4) — the special one

Cell = 32×32, `(col*32, row*32, 32, 32)`. Three coat files: `dog_cream_nightmare.png`,
`dog_chocolate_nightmare.png`, `dog_apricot_nightmare.png` (drop the `_nightmare` for the
non-corrupted overworld coats). Each **row is an animation, 6 frames** (cols 0–5):

| Row | State | Frames |
|---|---|---|
| 0 | idle / pant | 6 |
| 1 | walk | 6 |
| 2 | walk (secondary / return) | 6 |
| 3 | eager greet | 6 |

⚠️ **The frames are a normal, loving dog. The horror is the _timing_, driven at
playback — do not bake it into the art.** Instead of a fixed `frameMs`, advance with
these per-state rules (ms), so the loop juxdders and skips:

```js
// call when it's time to pick the next frame delay:
function nextDelay(row, rnd = Math.random) {
  const r = rnd();
  if (row === 0) {                    // stutter pant
    if (r < 0.20) return 340 + rnd()*120;         // long, wrong stare (hold frame)
    if (r > 0.90) { frame = (frame+2)%6; return 50; } // skip-judder
    frame = (frame+1)%6; return 70 + rnd()*120;
  }
  if (row === 1) {                    // too-fast wag
    frame = (frame+1)%6; return r < 0.28 ? 40 : 96 + rnd()*44; // bursts
  }
  // row 3 — eager greet: repeats + skips
  if (r < 0.30) return 58;                          // judder (repeat same frame)
  if (r > 0.86) { frame = (frame+2)%6; return 54; } // skip
  frame = (frame+1)%6; return 80 + rnd()*70;
}
```

Plus an occasional **lurch toward camera**: every **3200–6800 ms**, over **430 ms**,
scale the sprite up ~+35% and nudge it +14px toward the viewer with a `sin` ease, adding
a 2–3px per-frame tremble. That's a transform, not a frame change.

---

## Quick animation-timing reference

| Sheet | Clip | Frames | ms/frame | Loop |
|---|---|---|---|---|
| grass | flowerBob | 4 | 340 | loop |
| grass | tuftBob | 4 | 280 | loop |
| grass | dirtShimmer | 4 | 200 | loop |
| grass | flowerDaisy | 4 | 360 | loop |
| path  | puddleV | 4 | 170 | loop |
| props | bareTree | 3 | 240 | loop |
| props | crow | 2 | blink 140 / 3400 | event |
| props | leaves | 4 | 190 | loop |
| dog   | pant / walk / greet | 6 each | variable (see rules) | corrupted |

Regenerators (Node/canvas-style scripts that emit these PNGs) live alongside the art:
`grassgen.js`, `pathgen.js`, `propsgen.js`, `nightmaredoggen.js` — edit palette or add
tiles there and re-render rather than hand-editing pixels.
