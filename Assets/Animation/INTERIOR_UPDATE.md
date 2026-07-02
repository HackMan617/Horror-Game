# Interior Furniture — geometry & facings

The cozy 2.5D living room, the last comfort before the Nightmare Realm. This note covers the
**interior geometry**: which facings each piece ships with, the full atlas rects, and how to place
and sort them in an oblique room. Art is baked by `interiorgen.js` — re-render, don't hand-edit.

> 16px world grid · oblique elevation · **Point / nearest** filter · real alpha.
> Origin top-left; a grounded piece rests its **bottom row on the floor line**.

---

## Facings — solid pieces now turn

The **solid** pieces carry three facings so they can sit against any wall of the room. Thin /
decorative pieces stay a single sprite.

| Piece | FRONT | BACK | SIDE | Notes |
|---|:---:|:---:|:---:|:---:|
| **sofa** | ✔ 48×32 | ✔ 48×32 | ✔ 30×32 | 3-seat |
| **loveseat** (`couch`) | ✔ 32×32 | ✔ 32×32 | ✔ 30×32 | 2-seat, same profile as sofa |
| **armchair** | ✔ 32×32 | ✔ 32×32 | ✔ 30×32 | single seat |
| **bookshelf** | ✔ 32×32 | ✔ 32×32 | ✔ 18×32 | back = plank board, side = shelf edges |
| **tv + stand** | ✔ 32×32 · 4f | ✔ 32×32 | ✔ 26×32 | front is the interactive clip |
| floorLamp | ✔ 16×32 · 2f | — | — | one-facing pole sprite |
| couchDog | ✔ 48×32 · 3f | — | — | occupied combo (sofa + dog) |
| coffeeTable | ✔ 32×16 | — | — | symmetric; no back/side needed |
| rug | ✔ 48×16 | — | — | flat on the floor |

**Facing convention** (oblique):
- **FRONT** faces down-room, toward the camera. Use it for a piece against the **back/far wall**.
- **BACK** is seen from behind — a plain cushioned/plank rear. Use it for a piece against the
  **near wall** (between camera and the piece) or floated with its back to the player.
- **SIDE** is the profile: **arm / screen-glass to the LEFT, backrest / CRT-neck to the RIGHT**.
  Use it against a **left or right wall**. Flip horizontally in engine for the opposite wall.

The SIDE sprites are narrower than the front (a sofa seen edge-on is only ~30px of seat depth),
so budget the shallower footprint when you snap a piece into a corner.

---

## Atlas — paste-ready rects (256×128)

```js
// [x, y, w, h, frames] — frame 0 rect; frames laid out horizontally (frame f at x + f*w)
const FURNITURE = {
  // row 0 — sofa trio · loveseat trio
  sofa:          [  0,   0, 48, 32, 1],
  sofaBack:      [ 48,   0, 48, 32, 1],
  sofaSide:      [ 96,   0, 30, 32, 1],
  couch:         [126,   0, 32, 32, 1],   // loveseat
  couchBack:     [158,   0, 32, 32, 1],
  couchSide:     [190,   0, 30, 32, 1],
  // row 1 — armchair trio · bookshelf trio · lamp
  armchair:      [  0,  32, 32, 32, 1],
  armchairBack:  [ 32,  32, 32, 32, 1],
  armchairSide:  [ 64,  32, 30, 32, 1],
  bookshelf:     [ 94,  32, 32, 32, 1],
  bookshelfBack: [126,  32, 32, 32, 1],
  bookshelfSide: [158,  32, 18, 32, 1],
  floorLamp:     [176,  32, 16, 32, 2],   // 0 off · 1 on
  // row 2 — tv trio (front is the 4-frame clip)
  tv:            [  0,  64, 32, 32, 4],   // 0 off · 1-2 on · 3 static
  tvBack:        [128,  64, 32, 32, 1],
  tvSide:        [160,  64, 26, 32, 1],
  // row 3 — combo · table · rug
  couchDog:      [  0,  96, 48, 32, 3],   // dog asleep on the sofa · 0-2 breathing
  coffeeTable:   [144,  96, 32, 16, 1],
  rug:           [144, 112, 48, 16, 1],   // draw UNDER furniture
};
// frame N rect: [x + (N % frames)*w, y, w, h]
```

Three palettes share this **identical** layout — `interior_furniture_{dusk,lavender,nightmare}.png`,
all **256×128**. A palette / nightmare swap is the same rects on a different texture. `-8x.png`
files are previews only — **do not ship**.

---

## Placement & sort order

Layer order per room:

```
wall / window  →  fireplace glow  →  rug  →  furniture (sorted by base Y)  →  characters
```

- **Rug first**, flat on the floor, under everything.
- **Furniture sorted by base Y** (the world-Y of its bottom row): a piece with a lower base draws
  in front of one behind it. For each piece pick the facing that points **away from the wall it
  sits against** (back wall → FRONT, near wall → BACK, side walls → SIDE, mirrored as needed).
- **Characters** draw after the seat they occupy — or just use the `couchDog` combo, which bakes
  the dog into the sofa's FRONT footprint.

Anchoring: a piece's origin is its **top-left**; ground it by aligning its bottom row to the floor
line. Feet/skirt already sit on the bottom rows of each sprite, so no per-piece offset is needed.

---

## The dream flicker

Nightmare furniture doesn't hard-swap — it **flickers in** over the day sprite on the dread flag,
more and more as the player realizes they never woke up (`0` awake → mid strobes → `1` mostly
dreaming with brief lucid day-blinks). Every facing has its own rot pass: back slabs stain and
sag, the bookshelf back gapes to a black hole with an eye, the TV back's vents go dead-dark, the
CRT-side neck rots. Drive one room-wide `[0,1]` value; each object flickers on its own phase.

`Interior Furniture.dc.html` is the live reference — palette toggle, dream-state slider, and the
grouped FRONT / BACK / SIDE gallery. Unity install lives in
`interior_furniture_kit/INTERIOR_FURNITURE.md` (`InteriorAtlas.cs` + `InteriorObject.cs`).
