# Interior Furniture — living room · atlas & Unity guide

The cozy 2.5D lounge that is the **last comfort before the Nightmare Realm**. Warm woods and
soft fabric on the 16px grid, authored to sit in the existing interior rooms — and to rot when
the dream takes hold. Old-school-but-a-little-modern: inviting, then wrong.

> 16px world grid, oblique elevation, **Point / nearest** filter, real alpha. Origin top-left;
> grounded pieces sit their bottom row on the floor line. Baked per-object by `interiorgen.js`
> (palettes, proportions, the rot pass live there) — re-render, don't hand-edit.

---

## Files — one layout, three palettes

| File | Room |
|---|---|
| `interior_furniture_dusk.png`      | cool teal-grey room, warm **rust** furniture |
| `interior_furniture_lavender.png`  | faded-purple room, **dusty-rose** furniture |
| `interior_furniture_nightmare.png` | the **dream-rot** pass — flickers in on the dread flag |

All three are **256×96**, identical layout, so a palette/nightmare swap is just a different
texture sampled with the same rects. `-8x.png` variants are previews — **do not ship**.

---

## The atlas — paste-ready rects

```js
// [x, y, w, h, frames] — frame 0 rect; frames laid out horizontally (frame f at x+f*w)
const FURNITURE = {
  sofa:        [  0,  0, 48, 32, 1],
  couch:       [ 48,  0, 32, 32, 1],   // loveseat
  armchair:    [ 80,  0, 32, 32, 1],
  bookshelf:   [112,  0, 32, 32, 1],
  floorLamp:   [144,  0, 16, 32, 2],   // 0 off · 1 on
  coffeeTable: [176,  0, 32, 16, 1],
  rug:         [176, 16, 48, 16, 1],   // draw UNDER furniture
  tv:          [  0, 32, 32, 32, 4],   // 0 off · 1-2 on · 3 static
  couchDog:    [  0, 64, 48, 32, 3],   // the dog asleep on the sofa · 0-2 breathing
};
// frame N rect: [x + (N % frames)*w, y, w, h]
```

---

## Objects — frames & purpose

| Object | Cell | Frames | Purpose / what the frames are for |
|---|---|---|---|
| **sofa** | 48×32 | 1 | Plush 3-seat, the heart of the room. Seats characters; the dog sleeps here. |
| **couch** | 32×32 | 1 | Compact 2-seat loveseat. |
| **armchair** | 32×32 | 1 | A single soft chair (partner can read here). |
| **coffeeTable** | 32×16 | 1 | Low table with a book left on top. Sits on the rug. |
| **tv** | 32×32 | 4 | **Interactive.** `0` off (dark glass) · `1`,`2` on (picture shimmers between them) · `3` static. |
| **bookshelf** | 32×32 | 1 | Warm spines. In the dream a gap opens to black — something waits in it. |
| **rug** | 48×16 | 1 | Floor rug — **draw first**, under everything. |
| **floorLamp** | 16×32 | 2 | **Interactive.** `0` off · `1` on (warm pool of light; sick green when wrong). |
| **couchDog** | 48×32 | 3 | The **occupied combo**: sofa + dog curled asleep, `0-2` a gentle breathing rise. Nightmare: the dog opens one eye and watches you. |

**Looping / timing** (subtle, off-phase — like the outdoor props):

| Object | Clip | Frames | ms/frame | Loop |
|---|---|---|---|---|
| tv | on-shimmer | 1↔2 | ~285 | loop (blip to `3` static or `0` off occasionally) |
| couchDog | breathe | 0↔1 | ~600 | loop |
| floorLamp / others | — | — | — | hold a single frame |

---

## Interaction — highlight + use

The design calls for **both** a highlight on approach and a use animation on activate:

- **Highlight:** when the player is near / aiming at a piece, call `SetHighlighted(true)` — a
  warm glow tint (a code effect, no extra art). Clear it when they walk away.
- **Use:** call `Activate()`. The **TV** turns on (off → lit shimmer) and the **floor lamp**
  toggles its warm pool. Other pieces are hooks you can extend (bookshelf pull, drawer, etc.).

---

## The dream flicker — the core mechanic

Nightmare furniture doesn't hard-swap; it **flickers in** over the day sprite, more and more as
the player realizes they're still dreaming. `InteriorObject` exposes `[Range(0,1)] DreadProgress`
(feed it the same game-wide dread value as the dog, mountain, and houses):

- `0` — fully awake, the room is warm and safe.
- mid — the rot **strobes in** at random: a sofa stain blinks past, the TV snaps to static for a
  frame, the shelf gap shows for an instant. Long enough to unsettle, short enough to doubt.
- `1` — mostly the nightmare, with brief **lucid day-blinks** — you keep *almost* waking up.

Drive it room-wide from one value; every `InteriorObject` flickers on its own random phase, so
the room doesn't pulse in sync (same philosophy as the rest of the game).

---

## Unity — install

1. Copy **`InteriorAtlas.cs`** and **`InteriorObject.cs`** into `Assets/Scripts/`.
2. Import the three atlases: **Read/Write ON · Filter Point · Compression None** (Sprite Mode
   *Single* is fine — the scripts slice the raw texture, handling the top-left→Unity Y flip).
3. Per piece: a GameObject + **SpriteRenderer**, add **Interior Object**, choose the **Piece**,
   assign the room's **Day Atlas** (dusk *or* lavender) + the **Nightmare Atlas**, set
   `pixelsPerUnit` to your world scale (16).
4. Wire `DreadProgress` from your dread manager; call `SetHighlighted`/`Activate` from your
   interaction system.

```csharp
foreach (var o in room.GetComponentsInChildren<InteriorObject>())
    o.DreadProgress = DreadManager.Progress;      // the whole room flickers together

if (nearTv) { tv.SetHighlighted(true); if (used) tv.Activate(); }
```

**Layer order:** wall/window → fireplace → **rug** → furniture (sorted by base Y) → characters
(the dog/partner draw in front of the seat they occupy — or just use the `couchDog` combo).
`Interior Furniture.dc.html` is the interactive reference: palette toggle, the dream-state
slider, and the object gallery.
