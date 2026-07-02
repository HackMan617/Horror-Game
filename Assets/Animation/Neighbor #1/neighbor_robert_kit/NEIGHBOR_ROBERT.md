# Robert Abernathy — the neighbor · sprite sheet & scene guide

The man three doors down. **Robert Abernathy**: a worn-out-Steve-Jobs technology lunatic who
moved to the mountains — round wire glasses, balding grey, a black turtleneck under grubby work
**coveralls**, **yellow rubber gloves**, **hedge shears** in one hand. Friendly. Brilliant. Knows
every wire in the valley. He waves, he over-explains, and he **smiles a beat too long**.

His daytime self is only *off*. His nightmare self is a **different kind of wrong than the
player** — no gore, no spilled organs. Instead he **stretches**: too tall, too thin, the neck
drawn out, the glasses gone to blank glare, and when he speaks the **jaw drops open far too
wide** — a black void down the throat.

> 32-px cell, integer scale, **Point / nearest** filter, real alpha. Front-facing sheet matches
> the player's character grammar, extended with a speak pair. Everything is baked per-frame by
> `neighborgen.js` — retune palette / proportions / gape there and re-export.

---

## Files

| File | Size | Frames |
|---|---|---|
| `neighbor_robert_front.png`            | 224×32 | daytime — idle 0–1 · walk 2–4 · speak 5–6 |
| `neighbor_robert_front_nightmare.png`  | 224×32 | stretched — same 7-frame layout |

`-8x.png` variants are preview blow-ups — **do not ship**. Back & side sheets can follow later
on the identical 7-frame layout (front-facing was the agreed first pass).

---

## The sheet — frames

Cell `(col*32, 0, 32, 32)`, cols 0–6. Single row. Matches the player rig, plus speak:

| Cols | State | Frames | Notes |
|---|---|---|---|
| 0–1 | **idle** | 2 | stands eerily still; frame 1's smile stretches 1 px wider — *held too long* |
| 2–4 | **walk** | 3 | contact → passing (body rises) → contact; arms swing |
| 5–6 | **speak** | 2 | mouth-flap. Loop `5 ↔ 6` while he's talking |

- **Idle:** play 0–1 slowly with **dead holds** (600–1500 ms frozen) — he should stand too still.
- **Walk:** `2 3 4 3` at ~150 ms.
- **Speak:** `5 6 5 6` at ~150 ms over the idle body. **Daytime** = a small polite flap.
  **Nightmare** = the jaw unhinges into a void every open frame — the scare beat.

---

## The two forms

Swap on the **same dread flag** as the dog's true form, the mountain apparition, the player and
the houses.

- **Daytime (`neighbor_robert_front.png`)** — human, but visibly *off*: stiff posture, the
  fixed too-wide smile, glasses catching the light so you can't quite see his eyes. Uncanny, not
  monstrous. This is who he is on the street when you visit.
- **Nightmare (`..._nightmare.png`)** — he **stretches**. Taller, thinner, a long drawn-out neck,
  small high head, blank-glare lenses (nothing behind them), long thin gloved fingers, drained
  waxy grey-green skin. The shears rust in his grip. In the speak frame the mouth **opens far too
  wide**. The horror is *proportion and stillness*, deliberately not the player's body-gore — two
  different nightmares sharing a street.

---

## Palette — recolour-compatible, and a fresh roll each load

The sheet uses flat, region-keyed colours so the engine can palette-swap his workwear — and,
per design, **roll a fresh assortment every time the game loads** so he's never quite the same
man twice.

**Rollable regions** (base colour → derive the shade by darkening ~0.7):

| Region | Source base | Source shade | Also |
|---|---|---|---|
| coveralls | `#5f7488` | `#42525f` | straps `#3c4855` (darker still) |
| turtleneck | `#2b2f36` | `#181b20` | |
| gloves | `#d8c24e` | `#a6923a` | |
| boots | `#3a332c` | `#241f1a` | |

**Fixed** (identity — leave alone): skin `#e3b78e`/`#bf885d`, grey hair `#b8b0a6`/`#877e73`,
glasses frame `#20242b` + lens `#cfe3e8`, brass buckle `#c9a24a`, shears
`#c9ced6`/`#8b929c`/handle `#8a2f2f`, outline `#150f12`.

**Roll recipe (runtime):** load the daytime PNG to an offscreen canvas, pick one base per
rollable region from a curated *worn* pool (denim slate, olive drab, rust, grey-green, khaki,
steel blue, oxblood… for coveralls; yellow / blue / red / green / orange rubber for gloves; etc.),
then exact-colour-replace base → new base and shade → `darken(new base)`. Because the art is flat
(no anti-aliasing), exact-match replacement is lossless. The **nightmare drain overrides identity**,
so it reads the same over whatever he happened to be wearing — roll only the daytime sheet.

---

## The street — three houses

Robert lives on the block from `neighbor_houses_kit` (A / B / C). The scene contrast the design
calls for:

- **C — Robert's** (saltbox, oxblood trim). He's out front, **visible**, always. By day a warm
  porch; on the dread flag his downstairs window burns the wrong **sick green** (the house's own
  nightmare beat) and he stretches where he stands.
- **A — the recluse** (gable). A neighbor nobody has met — **never home**: dark windows, no
  smoke, no light, in both day and night. It reads as though it isn't lived in at all… because it
  isn't.
- **B — vacant** — the third house, empty, filling out the neighborhood.

Layer order (per the houses guide): sky → mountains → **houses** (sorted by base Y) → props →
**Robert** (character layer, in front of C). Gate his idle/speak flap to **proximity** so the
wrongness only stirs when the player is near — same philosophy as the rest of the game.

`Neighbor - Robert Abernathy.dc.html` is the interactive reference: form toggle (daytime ↔
nightmare), idle / walk / speak, a palette **roll**, the sheet strip, and the day↔nightmare
street diorama.

---

## Integration notes

- Draw `image, col*32, 0, 32, 32 → dest`. Origin **top-left**; place so the boots sit on the
  ground line. Point-filter, Compression None, integer scale only.
- The **nightmare** silhouette is taller and reaches the top of the cell — anchor by the **feet**
  so he grows *upward* when the form swaps in place (don't re-center, or he'll appear to sink).
  If you want him to physically tower over the player, render the nightmare sheet at a taller cell
  (e.g. 32×48) anchored at the feet; the generator can output that by raising `CFG.nightmare`.
- Keep the frames untouched; own the **form swap**, the **speak flap**, and the **proximity gate**
  in engine.
- Retune in `neighborgen.js`: `PAL` (both forms), `CFG` (armature / proportions / the stretch),
  and the face/gape passes. Re-export both sheets.
