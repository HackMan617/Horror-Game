# Clouds — atmosphere sheet, geometry & spawning

Ambient background clouds for the exterior sky. Five silhouettes across a small→large range,
each a lumpy union of puffs, two-tone shaded (a pale base + one mauve-dusk shadow wrapped around
the underside — light reads as coming from above/behind). A 2-frame shimmer per cloud: the
shadow line breathes by 1–2px and a single moonlit fleck blinks on the top surface. Baked by
`cloudgen.js` — re-render, don't hand-edit.

> Point / nearest filter, real alpha (each depth layer has its own base opacity — farther clouds
> read hazier/fainter, same trick as the birds sheet). One neutral dusk/night palette — no
> separate variant per time of day; it already reads against both the dusk and night keyframes
> in `SKY_README.md`.

---

## Atlas — paste-ready rects (120×54)

```js
// [x, y, w, h, frames] — frame 0 rect; frame 1 sits immediately to the right (x + w)
const CLOUDS = {
  cloudWisp:  [0,  0, 14,  5, 2],   // far  — thin scrap, deep background
  cloudSmall: [0,  5, 20,  8, 2],   // far  — small puff
  cloudMed:   [0, 13, 30, 11, 2],   // mid  — medium puff
  cloudLarge: [0, 24, 42, 14, 2],   // mid  — wide puffy cloud
  cloudHero:  [0, 38, 60, 16, 2],   // near — big hero cloud, closest
};
// frame N rect: [x + N*w, y, w, h]
```

`clouds_atmo.png` is the shipped sheet. `-8x.png` is a preview only — **do not ship**.

---

## Depth layers — 3 parallax bands

| Layer | Variants | Speed | Alpha | Shadow contrast |
|---|---|---|---|---|
| far  | `cloudWisp`, `cloudSmall` | slowest | 0.50 / 0.62 | soft, hazy |
| mid  | `cloudMed`, `cloudLarge`  | medium  | 0.80 / 0.88 | moderate |
| near | `cloudHero`               | fastest | 0.96        | deepest, crispest |

Bigger reads as closer — pick larger variants for the near band, same depth cue as the birds
sheet's size tiers. Vary each band's drift speed (near fastest) for a proper parallax read.

---

## The shimmer (2 frames)

| Frame | Read |
|---|---|
| 0 | shadow line at rest, moonlit fleck **on** |
| 1 | shadow line eased by 1–2px, fleck **off** |

Loop slow — clouds are atmosphere, not characters. ~1 fps (roughly 0.6–1s per frame) reads as a
gentle drift/shimmer rather than a flap. Don't sync all clouds to the same phase; offset each
instance's timer so the sky doesn't pulse in unison.

---

## Placement — random spawn, exterior only

- Spawn **4–8** at a time across the three depth bands, weighted toward `far`/`mid` so the sky
  doesn't feel crowded with hero clouds.
- Randomize **height band** (upper ⅔ of sky, above the horizon glow), **horizontal start**, and
  **drift speed** per band (near fastest, far slowest — same parallax rule as birds).
- Drift in one direction per cloud (occasional gentle horizontal-flip for variety, no wing-style
  mirroring logic needed).
- Cull once a cloud drifts off either edge; respawn on the opposite side after a random delay.
- Draw **behind the moon/sun and above the gradient/stars** — clouds sit in front of the sky
  backdrop but never occlude gameplay.

`Clouds.dc.html` is the live reference — a looping dusk-sky demo with parallax spawns plus the
five variant swatches.
