# CAR.md — Roadside Arrival Vehicle, Road & Sign

Handoff notes for the roadside pack: the old off-road wagon you load in beside, the road it
came up, and the sign at the verge. Same rules as the rest of the game — **16 px world grid,
integer scaling only, nearest-neighbour sampling** (`image-rendering: pixelated;` / point
sampling). All PNGs have a real alpha channel. Every sheet has a `home` file and a cold
`_nightmare` twin; **`-8x.png` files are preview blow-ups only — do not ship them.**

Regenerators live beside the art: `truckgen.js`, `roadgen.js`, `signgen.js`. Edit palette or
geometry there and re-render rather than hand-editing pixels.

---

## 1 · The vehicle — `truck_<view>.png` (+ `_nightmare`)

A rounded 1940s–50s off-road wagon: bulbous fenders, whitewalls gone to mud, cream hardtop
with snow still on it, gloss half rusted through. Drawn as **2.5D elevation** sprites (the same
low storybook angle as the player and neighbours) — *not* top-down.

**Cell 64 × 32 px** (4 × 2 world tiles). **7 frames per sheet → 448 × 32 px.** Frame N rect =
`(N*64, 0, 64, 32)`.

### Frames

| Frames | Segment | What it is |
|---|---|---|
| 0–3 | **ROLL** | wheel-spin loop. Body identical; only wheels/tread advance. Frame 0 = parked rest. |
| 4–6 | **DOOR** | near door swings open (climb-out). Back views drop the **tailgate** instead. |

```js
// pick the frame at playback:
function truckFrame(state, elapsedMs){
  if(state.door > 0) return [0,4,5,6][state.door];   // door stage 0..3 -> frame
  if(state.rolling) return Math.floor(elapsedMs/80) % 4;  // ~80 ms/frame roll loop
  return 0;                                            // parked idle
}
```
- **Roll loop:** `frameMs ≈ 80`, forward loop 0→3. Only cycle it when the truck is actually
  moving; a parked truck holds frame 0.
- **Door:** ease the `door` stage 0→3 (open) / 3→0 (close) at ~70 ms/step, then map to the
  frame with the table above. Stage 0 = shut, stage 3 = fully open / tailgate down.

### 8-way facing — five sheets, mirror three

Only five views are authored; the west-facing three are **horizontal flips** (`scale(-1,1)`).

| Facing | Sheet `view` | Flip X |
|---|---|---|
| N  | `back`    | no |
| NE | `back3q`  | no |
| E  | `side`    | no |
| SE | `front3q` | no |
| S  | `front`   | no |
| SW | `front3q` | **yes** |
| W  | `side`    | **yes** |
| NW | `back3q`  | **yes** |

File = `truck_${view}.png` (append `_nightmare` for the corrupted realm). The `side` sheet
faces **right**; flip it for the left-facing runs, exactly like the player.

### Effects driven at PLAYBACK (not baked)

Keep these in code so they read live and never shimmer in the atlas. Anchors are **local px in
the 64 × 32 cell**; when a view is flipped, mirror x → `64 - x` (and negate any x direction).

```js
// per-view anchors (local px). blink col: 'amber' front/side/3q, 'red' on the rear.
const ANCH = {
  front:  { pipe:null,    blink:{pts:[[21,20],[43,20]], col:'amber'}, head:[[24,18],[40,18]], hdir:[0,1] },
  back:   { pipe:[41,27], blink:{pts:[[23,23],[41,23]], col:'red'},   head:null,               hdir:null },
  side:   { pipe:[6,27],  blink:{pts:[[54,24]],         col:'amber'}, head:[[57,17]],          hdir:[1,0] },
  front3q:{ pipe:[12,27], blink:{pts:[[45,24]],         col:'amber'}, head:[[52,18],[46,17]],  hdir:[1,1] },
  back3q: { pipe:[46,27], blink:{pts:[[43,23],[50,23]], col:'red'},   head:null,               hdir:null },
};
```

| Effect | Recipe |
|---|---|
| **Idle rumble** | when the engine is on, jitter the whole sprite ±0.8 px (±1.3 px while rolling), refreshing the offset every ~110 ms (~60 ms rolling). |
| **Exhaust puff** | spawn a soft grey circle at `pipe`; it rises (`-y ≈ 0.045 px/ms`), drifts opposite the facing, grows, and fades over ~1.5 s. Interval ~900 ms idle, ~230 ms rolling. Views with `pipe:null` (front) emit nothing. |
| **Hazard / turn signal** | toggle a lamp at each `blink` point every ~430 ms — amber on the front/side lenses, red on the rear. |
| **Headlights** | **home only.** Draw a hot core + a soft cone from each `head` point along `hdir`; the nightmare truck's lamps are dead. |

### `_nightmare` twin

Same silhouette and frames, palette pushed **cold + drained**: dead grey headlights (no glow),
deeper rust, grime crawls higher, glass goes near-black. As with the dog, the real dread is the
**idle timing** — stutter the rumble / hold the loop on wrong frames at playback; don't bake it.

---

## 2 · The road — `road_tiles.png` (+ `_nightmare`)

Two surfaces on one **16 × 16** sheet, **128 × 32 px, 8 × 2**. Both are **seamless in the
vertical (run) direction** so the road can stretch to the horizon. Opaque tiles that drop
straight onto the grass field; **edge tiles feather a gravel shoulder + a few grass blades** so
the road melts into the field. Rotate 90° in-engine for a crossing (horizontal) road.

Rect = `(col*16, row*16, 16, 16)`.

**Row 0 — ASPHALT** (the paved stretch nearest the house):

| col | name | notes |
|---|---|---|
| 0 | `asphaltPlain` | seamless lane base |
| 1 | `asphaltDash` | centre tile with the dashed yellow line |
| 2 | `asphaltEdgeL` | left white shoulder line + gravel + grass fringe |
| 3 | `asphaltEdgeR` | right edge (mirror) |
| 4 | `asphaltCrack` | worn / cracked variant |
| 5 | `asphaltPatch` | tar-patch pothole |
| 6 | `asphaltSnow` | snow-dusted |
| 7 | `transition` | top half asphalt, bottom half dirt — where the pavement ends |

**Row 1 — DIRT / GRAVEL** (further up the mountain):

| col | name | notes |
|---|---|---|
| 0 | `dirtPlain` | seamless dirt base |
| 1 | `dirtRut` | two wheel ruts down the middle |
| 2 | `dirtEdgeL` | dirt feathering to grass (left) |
| 3 | `dirtEdgeR` | right edge |
| 4 | `gravel` | loose gravel variant |
| 5 | `mud` | a muddy puddle |
| 6 | `dirtSnow` | snow patches |
| 7 | `rocks` | embedded rocks / debris |

Lay a route: centre with `asphaltDash`, flank with `asphaltPlain`, cap the sides with
`asphaltEdgeL/R`; drop in `transition` where it turns to dirt, then continue with the row-1
tiles. Sprinkle `crack`/`patch`/`snow` sparsely so no stretch reads uniform.

---

## 3 · The sign — `road_sign.png` (+ `_nightmare`)

A weathered wooden directional post: a leaning split-log post with two hand-painted plank
arrows (top → **TOWN**, lower → **MILL RD**), a rusted nail per plank, snow on the top edges,
dirt piled at the base. **Single sprite, 32 × 48 cell, 2 idle frames → 64 × 48 px** (a slow
creak/sway; the boards rock a hair). Frame rect = `(f*32, 0, 32, 48)`, `frameMs ≈ 900`, loop.

Place with the post base on the ground row at the road's shoulder. **Nightmare twin:** paint
drained, wood split, and the top arrow **turns to point the wrong way** with the lettering
rotted out — swap it in with the daytime sign, don't animate the flip.

---

## Quick timing reference

| Sheet | Clip | Frames | ms/frame | Loop |
|---|---|---|---|---|
| truck | wheel roll | 4 (0–3) | 80 | loop (rolling only) |
| truck | door / tailgate | 3 (4–6) | ~70/step | one-shot open ↔ close |
| road | (all static) | 1 | — | — |
| sign | creak sway | 2 | 900 | loop |

## File inventory (ship these)

```
truck_front.png   truck_front_nightmare.png
truck_back.png    truck_back_nightmare.png
truck_side.png    truck_side_nightmare.png       (faces right; flip for W)
truck_front3q.png truck_front3q_nightmare.png    (faces down-right; flip for SW)
truck_back3q.png  truck_back3q_nightmare.png     (faces up-right; flip for NW)
road_tiles.png    road_tiles_nightmare.png
road_sign.png     road_sign_nightmare.png
```
Live viewer with every facing, the animations and the composed scene: `Roadside Arrival.dc.html`.
