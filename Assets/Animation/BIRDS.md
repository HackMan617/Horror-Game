# Birds — exterior flock, geometry & spawning

Distant flying birds for the exterior scenes. Deliberately undetailed — from the ground they're
always far off, so each one is just a **silhouette**: two wing strokes meeting at a body dot,
flapping through a simple 4-frame cycle. Baked by `birdgen.js` — re-render, don't hand-edit.

> 1px-line silhouette art, **not** on the 16px furniture grid — birds are small and free-floating,
> not tile-anchored. Point / nearest filter, real alpha (each size has its own base opacity so
> farther birds read fainter). Single neutral palette — silhouettes work over any sky/room palette,
> so there's no dusk/lavender/nightmare variant.

---

## Atlas — paste-ready rects (64×24)

```js
// [x, y, w, h, frames] — frame 0 rect; frames laid out horizontally (frame f at x+f*w)
const BIRDS = {
  birdFar:  [0,  0,  8,  6, 4],   // deep background — smallest, faintest
  birdMid:  [0,  6, 12,  8, 4],   // mid distance
  birdNear: [0, 14, 16, 10, 4],   // closest — still just a silhouette, no detail
};
// frame N rect: [x + (N % frames)*w, y, w, h]
```

`birds_flock.png` is the shipped sheet. `-12x.png` is a preview only — **do not ship**.

---

## The flap cycle

Every size shares the same 4-frame loop, just scaled:

| Frame | Pose | Read |
|---|---|---|
| 0 | wings **UP** | tips lifted above the body |
| 1 | **LEVEL** | wings roughly flat |
| 2 | wings **DOWN** | tips dropped below the body |
| 3 | **LEVEL** | flat again, loops back to frame 0 |

Loop forward at a steady rate (~6–8 fps reads as a lazy soar; push to ~12 fps for a hurried
flock). No nightmare / alt state — a single-use ambient loop.

---

## Placement — random spawn, exterior only

These are **ambient background elements**, not gameplay objects:

- Spawn **2–5** at a time, staggered on a random timer (every few seconds, not on a fixed grid).
- Randomize **size** (far/mid/near — pick one per bird, weighted toward `birdFar`/`birdMid` so the
  sky doesn't feel crowded with close ones), **height band**, **direction** (mirror the sprite
  horizontally for left↔right flight), and **speed** (`birdNear` fastest/most parallax, `birdFar`
  slowest — reinforces the depth read).
- Cull once a bird drifts off either edge of the screen; respawn on the opposite side or after a
  fresh random delay.
- Draw **above the room/exterior background, below any foreground silhouettes** (trees, roofline)
  so they read as sky, not props.

`Birds.dc.html` is the live reference — a looping demo sky with random spawns plus the three
size/flap swatches.
