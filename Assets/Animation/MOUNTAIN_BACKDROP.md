# Mountain Backdrop — “The Far Range” · Unity implementation

A **background-only** dusk range for the exterior. No collision, no gameplay — it sits
behind the play-field and reads as the wall of the world seen from far off. Big **dirt**
mountains up front dissolve, ridge by ridge, into pale **snow** peaks against a sunset.
Every plane is a **seamless horizontally-tiling strip** on its own parallax rate. Fog
pools in the valleys. A lone **spruce line** holds the foreground. It’s wholesome from a
distance — until the **face surfaces** on the central peak, or the **wanderer** crosses a
near slope.

> Same authoring rules as the rest of the project: **16 px world grid**, integer scaling,
> **point / nearest** filtering, real alpha. Import every PNG with **Pixels-Per-Unit = 16**,
> Filter = **Point (no filter)**, Compression = **None**, Wrap = **Repeat** for the ridge
> strips (Clamp for the sprites). The regenerator is `rangegen.js` — edit palette / ridges
> there and re-export rather than hand-editing pixels.

---

## File inventory

| File | Size | Role | Wrap |
|---|---|---|---|
| `range_sky.png` | 380×240 | dusk gradient + stars + sun-glow (static backdrop) | Clamp |
| `range_L0_snowfar.png` | 480×114 | plane 0 — farthest snow peaks, warm-lit caps | Repeat |
| `range_hero_peak.png` | 120×104 | the central dominant peak (the face lives here) | Clamp |
| `range_L1_snowrock.png` | 480×96 | plane 1 — snow / rock | Repeat |
| `range_L2_purple.png` | 480×79 | plane 2 — purple ridge | Repeat |
| `range_L3_dirtridge.png` | 480×58 | plane 3 — dirt ridge (transition) | Repeat |
| `range_L4_neardirt.png` | 480×31 | plane 4 — near dirt, grainy earth | Repeat |
| `range_trees.png` | 480×38 | foreground spruce line (near-black) | Repeat |
| `figure_wanderer.png` | 64×20 | the wanderer — **4 frames**, 16×20 cells | Clamp |
| `mountain_face.png` | 28×28 | the face apparition (full intensity; engine fades it) | Clamp |
| `mountain_face_stages.png` | 168×28 | reference: 6 reveal stages (do not ship) | — |

`figure_wanderer-8x.png` / `mountain_face_stages-6x.png` are preview blow-ups — **do not ship**.

---

## Scene assembly — depth stack

All ridge strips share one **baseline** (the world ground line). Give each strip a
**bottom-centre pivot** and drop it on the baseline; its transparent top makes the
silhouette. The strips tile horizontally forever. Draw **far → near**; the hero peak sits
*between* plane 0 and plane 1; the figure rides just behind the spruce line.

```
        ┌──────────────────────────────────────────────┐   sort
  sky   │  range_sky.png            (static, factor 0)  │    0
  L0    │  range_L0_snowfar   top y126   ×0.13          │   10
  hero  │  range_hero_peak    top y78    ×0.27  (centre)│   15
  face  │   └ mountain_face   (child of hero, gated)    │   16
  L1    │  range_L1_snowrock  top y144   ×0.21          │   20
    ~ fog band  scene-y 184 (warm, α .16, drifts) ~     │   25
  L2    │  range_L2_purple    top y161   ×0.33          │   30
    ~ fog band  scene-y 206 (cool, α .20, drifts) ~     │   35
  L3    │  range_L3_dirtridge top y182   ×0.47          │   40
    ~ fog band  scene-y 228 (cool, α .24, drifts) ~     │   45
  L4    │  range_L4_neardirt  top y209   ×0.70          │   50
  fig   │  figure_wanderer    (near slope ~y205)        │   55
    ~ haze band scene-y 208–234 (cool, α .34) ~         │   58
  trees │  range_trees        top y202   ×1.00          │   60
        └──────────────────────────────────────────────┘  bottom = y240
```

**Parallax** — as the camera pans by Δx, shift each plane’s texture **left by `factor · Δx`**
(far planes barely move; the spruce line moves most). Factors are relative to the nearest
plane; scale them to taste. `MountainBackdrop.cs` does this via `material.mainTextureOffset`.

| Plane | factor |
|---|---|
| sky | 0.00 (pinned) |
| L0 snow far | 0.13 |
| L1 snow / rock | 0.21 |
| hero peak | 0.27 |
| L2 purple | 0.33 |
| L3 dirt | 0.47 |
| L4 near dirt | 0.70 |
| spruce line | 1.00 |

> Positions above are in the **380×240 authoring frame** (y measured down from the top,
> baseline at y240). At PPU 16 that frame is 23.75×15 units — scale the whole rig up to
> fill your camera’s vertical view; the strips still tile horizontally.

---

## Sky & fog (code effects)

- **Sky** — ship `range_sky.png` as a static quad behind everything, **or** rebuild it as a
  Unity vertical gradient for crisp infinite height. Stops, top → horizon:
  `#141e46 · #232653 · #3d335e · #5c3f61 · #8a4a5c · #c65f38 · #ef9f55`, with a warm
  **sun-glow** radial (`#f8cf83` core) low-centre behind the peaks, and sparse 1-px stars in
  the upper third.
- **Fog** — soft horizontal gradient bands in the valleys (see stack), each **drifting**
  sideways slowly and independently. Cheapest in Unity: thin semi-transparent quads with a
  scrolling material, or a soft-noise fog shader. Warm tone `#d9b8a0` up high, cool
  `#a89fb4` toward the front. Keep it subtle — it should *breathe*, not curtain.

---

## The wanderer  ·  `figure_wanderer.png`

![wanderer frames](figure_wanderer-8x.png)

Four frames, **16×20 px** cells (the hunched figure is drawn at a 1-px inset). It is a
**normal detail that behaves wrong** — the dread is in the timing, not the art.

| property | value |
|---|---|
| frames | 4, loop |
| frame time | ~**170 ms** |
| appears | every **12–22 s** (random) |
| behaviour | fades in, creeps a near slope (~scene-y 205) over ~**7 s**, fades out |
| tint | near-black `#0c0a12` |

Place two empty markers on a near slope (`figureSlopeStart` / `figureSlopeEnd`) and let it
lerp between them. It can run **from the start** — it’s a background unease, not a scare.

---

## The face  ·  `mountain_face.png`  ·  **off at start, surfaces with progress**

![face reveal stages](mountain_face_stages-6x.png)

A gaunt, frowning face buried in the central peak — brow ridge, slanted sockets with cold
pin-point eyes, a nose gash, a grim mouth. It is **not baked into the rock**: ship the peak
clean (`range_hero_peak.png`) and blend `mountain_face.png` over it with a **driven alpha**.

**Anchor:** child the face sprite to the hero-peak quad and centre it on the peak’s face
point — pixel **(62, 52)** from the hero sprite’s top-left (the sprite’s own centre is at
28-px cell 14,13). It should sit just under the summit, over the shadowed upper face.

**The gate — start hidden, reveal gradually:** keep a single float `DreadProgress` (0→1)
that you raise as the game progresses (days survived, story beats, sanity, whatever). Map it
to the apparition so that:

- at `DreadProgress = 0` it **never** appears (fully hidden — how the game opens);
- below a **threshold (~0.18)** still nothing;
- past it, the face **comes and goes** — and as dread climbs it surfaces **more often,
  reaches a higher opacity, and lingers longer**. The stages strip above is that ramp.

```csharp
// strength eases up with progress; below the threshold it stays hidden
float reveal  = SmoothStep(0,1, InverseLerp(0.18f, 1f, DreadProgress));
float peakA   = Lerp(0.35f, 1.0f, reveal);   // strongest opacity it reaches
float surface = Lerp(2.5f, 6.5f, reveal);    // seconds it stays up
float idle    = Lerp(26f,  14f,  reveal);    // seconds between appearances
// during a surfacing: alpha = sin(t/surface · π) · peakA  (fade in → hold → out)
```

Full logic (with the come-and-go cycle and a faint waver) is in **`unity/MountainBackdrop.cs`**
→ `FaceLoop()` and the `DreadProgress` field. Leave `DreadProgress` at 0 and the peak is
just a mountain.

---

## Unity quick-start

1. **Import** every `range_*` / `figure_wanderer` / `mountain_face` PNG at **PPU 16, Point,
   Compression None**. Set the six ridge strips + hero to **Wrap = Repeat**.
2. Build a **Quad** per ridge strip (Unlit/Transparent material, its texture assigned).
   Bottom-align them on a shared baseline at the Y offsets in the stack table; set
   Sorting Order per the table. Add the sky quad behind, the hero quad between L0 and L1.
3. Add a child **SpriteRenderer** to the hero quad for the face at anchor **(62,52)**;
   another SpriteRenderer for the figure, disabled, with the 4 frames.
4. Drop **`MountainBackdrop.cs`** on the backdrop root. Assign the camera, the planes (with
   the parallax factors above), the figure + frames + slope markers, and the face renderer.
5. Raise `DreadProgress` from your game state to let the face creep in over the playthrough.

Palette, timings and ridge shapes all live in `rangegen.js`; re-export the PNGs from there
if you retune anything.
