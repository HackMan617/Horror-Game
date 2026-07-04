# Interior Addition — geometry & tiling guide

How the **Interior Structure** kit composes into real floors, tile by tile, on the 16 px oblique
grid — plus the expressive **haunt** controls added to the reference (`Interior Structure.dc.html`).
This is the build sheet; the per-piece art notes live in `INTERIOR_STRUCTURE.md`.

> One atlas, two palettes — `interior_structure_dusk.png` / `interior_structure_nightmare.png`,
> both **256×192**, identical rects. **Point / nearest** filter, real alpha, origin top-left.
> The nightmare texture flickers in over the dusk one on the dread flag (same rects). `-8x.png`
> files are previews — **do not ship**.

---

## The grid

Everything sits on a **16 px** cell. Multi-cell pieces are whole multiples of it:

| Footprint | Pieces |
|---|---|
| 16×16 | all wall/floor/ceiling tiles, most decor, `atticGableVent` |
| 16×32 | `atticBeamPost`, `mirror` |
| 16×48 | `supportPost` |
| 32×16 | `mountedShelf`, `coatHooks` |
| 32×32 | `deerHead` |
| 32×48 | `stairFront` |
| 48×32 | `stairDownHole` |
| 48×48 | `stairSideWood`, `stairSideCarpet`, `stairSideWorn`, `stairStone` |

**Tileable** (repeat with no seams): `concreteWall`, `concreteWallCrack`, `concreteBase`,
`basementFloor`, `atticCeilSlopeL`, `atticCeilSlopeR`, `atticBeamH`, `atticKneeWall`.

**Layer order, per storey (back → front):**
wall / ceiling tiles → floor tiles → **stairs** (on their base row) → free-standing furniture
(sorted by base-Y) → **wall decor** (over the wall) → characters → the personal-haunt overlay.

---

## Atlas rects

```js
// [x, y, w, h, frames] — frame f at [x + f*w, y]. Atlas 256×192.
const STRUCTURE = {
  // BASEMENT (tileable)
  concreteWall:  [  0,  0,16,16,1],  concreteWallCrack:[ 16,  0,16,16,1],
  concreteBase:  [ 32,  0,16,16,1],  basementFloor:    [ 48,  0,16,16,1],
  supportPost:   [  0, 32,16,48,1],
  // ATTIC / 2ND FLOOR
  atticCeilSlopeL:[64, 0,16,16,1],   atticCeilSlopeR:  [ 80,  0,16,16,1],
  atticBeamH:    [ 96,  0,16,16,1],  atticKneeWall:    [112,  0,16,16,1],
  atticGableVent:[128,  0,16,16,1],  atticBeamPost:    [ 16, 32,16,32,1],
  // WALL DECOR
  framedPortrait:[  0, 16,16,16,4],  framedLandscape:  [144,  0,16,16,1],
  wallSconce:    [ 64, 16,16,16,2],  wallClock:        [192,  0,16,16,3],
  mountedShelf:  [ 96, 16,32,16,1],  coatHooks:        [128, 16,32,16,1],
  mirror:        [160, 16,16,32,1],  deerHead:         [ 32, 32,32,32,1],
  wreath:        [160,  0,16,16,1],  calendar:         [176,  0,16,16,1],
  // STAIRS
  stairSideWood: [  0, 96,48,48,3],  stairSideCarpet:  [144, 96,48,48,1],
  stairFront:    [192, 96,32,48,1],  stairSideWorn:    [  0,144,48,48,2],
  stairStone:    [ 96,144,48,48,1],  stairDownHole:    [144,144,48,32,1],
};
```

---

## Floor geometry — the three bands

A full section is **16 cells wide**. The reference cutaway stacks three storeys in one shaft;
these are the tile recipes for each (heights in cells).

### Basement — poured concrete
```
row 0..N-2   concreteWall      × 16   (sprinkle concreteWallCrack to break the run)
row N-1      concreteBase      × 16   (cove — the course where wall meets slab)
floor        basementFloor     × 16 × (however deep the slab reads)
```
- Stand `supportPost` (16×48, 3 cells tall) at load points — one every ~6 cells.
- Drop `stairStone` against a side wall on the floor line.
- Hang `mountedShelf` / `calendar` on the wall course.

### Ground — warm plank wall
```
wall         plank fill        16 cells wide × ~5 cells tall
baseboard    dark band along the bottom cell
```
- Wall decor hangs on the upper 3–4 cells (see spacing below).
- A `stairSideWood` / `stairSideCarpet` / `stairSideWorn` flight rises to the floor above;
  a `stairFront` flight can climb the back wall.

### Attic — beamed gable
```
ridge        atticBeamH        × 16   (rafter run across the top)
slopes       atticCeilSlopeR   × 2    at the left, raking down-left
             atticCeilSlopeL   × 2    at the right, raking down-right
peak         atticGableVent    × 1    centred; atticBeamPost (king-post) below it
knee walls   atticKneeWall     × 2    at each low side where slope meets floor
```
- Butt **SlopeR (left) + SlopeL (right)** into a symmetric gable; widen by adding slope tiles
  before the peak.
- **`atticBeamH` does double duty:** one joist run reads as the attic floor **and** the ceiling
  of the storey below — share it between bands.

---

## Stairs — placement geometry

- All side flights (`stairSideWood`, `stairSideCarpet`, `stairSideWorn`, `stairStone`) are drawn
  **ascending left → right**; mirror in-engine for the other hand.
- Sit each stair on its **bottom row** on the floor line. Draw a soft contact-shadow ellipse
  under it (not baked into the sprite).
- To connect storeys, cut a **`stairDownHole`** (48×32) into the floor of the **upper** storey,
  directly **above** the flight on the lower one — a character passes through the opening.
- `stairFront` (32×48) climbs **away up the back wall** — use it for a flight seen head-on.

---

## Wall-decor hanging

Hang over the finished wall layer, on the upper cells so a standing character passes beneath.
Keep ~2 cells of clear wall between neighbours so the row reads. Suggested run across a ground
wall (16 cells): `framedPortrait · wallClock · wallSconce · deerHead · wallSconce · mirror ·
coatHooks`. Animated / interactive frame counts:

| Piece | Frames | Behaviour |
|---|---|---|
| `framedPortrait` | 4 | eyes dart L/C/R on `0/1/2`; `3` = the LUNGE (nightmare only, rare single frame) |
| `wallClock` | 3 | pendulum swing L–C–R (`0/1/2`) |
| `wallSconce` | 2 | `0` off · `1` lit (interactive; sick-green when wrong) |
| `stairSideWood` | 3 | dust drift + a middle tread sags on frame 2 |
| `stairSideWorn` | 2 | upper steps flex 1 px — a slow creak |

---

## Addition — the expressive haunt controls

The reference now exposes three **Tweaks** (props on the DC) that reshape the *whole* nightmare
rather than nudging single pixels. They drive the runtime grade + behaviour over the baked
nightmare atlas; treat them as art-direction the engine can pick per player / per run.

| Tweak | Type | What it reshapes |
|---|---|---|
| **hauntTone** | enum · `Sick Green` · `Blood Rite` · `Ash & Ember` · `Drowned Blue` · `Bruise Violet` | The dream's colour grade — a global tint + vignette colour + the WRONG/DREAMING tag colours. |
| **menace** | range `0–1` | One relentlessness dial: flicker speed, portrait-lunge frequency, vignette depth, and a per-frame screen-shake all ramp together. |
| **presence** | enum · `Absent` · `Watchers` · `Reaching Hands` · `Your Double` · `Strung Up` | **How the house reflects the player.** Overlays that strobe in on nightmare frames: eyes bleed open across the decor; drained clawed hands reach from the mirror / floor-hole / pegs; **your own drained double** surfaces in the mirror & portrait (in the player's shirt-red); or hung marionette forms fill the openings. |

**Wiring:** `hauntTone` / `presence` are enum strings; `menace` is `0–1`. Read with a fallback
(`this.props.x ?? default`). `presence` is gated to nightmare frames so it flickers in with the
rot, and its `Your Double` / `Reaching Hands` / `Strung Up` forms are the interior echo of the
**nightmare-player** sheets — the same "it's wearing you" haunt, bleeding into the room.

---

## Files

- `INTERIOR_ADDITION.md` — this build sheet.
- `INTERIOR_STRUCTURE.md` — per-piece art notes & timing.
- `interior_structure_dusk.png` / `interior_structure_nightmare.png` — the shipped **256×192**
  atlases (same rects; nightmare flickers in on the dread flag).
- `interiorstructgen.js` — the generator (palettes, geometry, rot pass). Re-render, don't hand-edit.
- `Interior Structure.dc.html` — the interactive reference: three-floor cutaway, dream-state
  slider, labelled kit, and the haunt Tweaks.
