# Sky — Dawn to Dark · Unity implementation

A **sky-only** day→night backdrop. One vertical **gradient** carries the whole day; a **sun
sprite** arcs across and sets, a **moon sprite** rises, **stars** fade in and twinkle, and it
**ends at the darkest point**. It sits *behind* the mountain range — no terrain, no collision.

Drive one value, **`TimeOfDay` 0 → 1** (0 = morning, 1 = darkest night), from your day-clock
(or let it auto-play). Everything else is derived from it.

> Import `sun.png` / `moon.png` at **PPU 24, Point filter, Compression None**. The gradient is
> generated in-engine from the keyframes below (no texture to import). Star layer is yours to
> place (see the bottom).

---

## Files

| File | Size | Role |
|---|---|---|
| `sun.png` | 24×24 | sun sprite (rayed warm disc) |
| `moon.png` | 24×24 | moon sprite (cratered pale disc) |
| `SkyController.cs` | — | drives gradient + sun/moon from `TimeOfDay` |

---

## The gradient — 7 keyframes × 5 vertical stops

Interpolate the five colours between the two surrounding keyframes at `TimeOfDay`, then paint a
vertical gradient with them at the stop positions. Stops (top → bottom): **0.00, 0.32, 0.58,
0.82, 1.00**. It ends dark: the last keyframe is near-black.

| t | phase | top | | | | horizon | stars |
|---|---|---|---|---|---|---|---|
| 0.00 | dawn | `#1f335f` | `#495085` | `#8a6790` | `#d98a6e` | `#f3c48f` | 0.12 |
| 0.18 | morning | `#2f6ba8` | `#5090c6` | `#86b6d8` | `#bcdcec` | `#dcecf2` | 0 |
| 0.40 | midday | `#2b7fc6` | `#4f97d6` | `#7ab8e6` | `#b2ddf2` | `#d2ecf8` | 0 |
| 0.60 | afternoon | `#345f9e` | `#5a70aa` | `#9a8fb4` | `#e0b088` | `#f0cf9a` | 0 |
| 0.76 | sunset | `#2b3566` | `#4a3a72` | `#8a4a68` | `#cf5f38` | `#ec9a4e` | 0.06 |
| 0.88 | dusk | `#171a44` | `#312a5c` | `#5c3560` | `#8a4550` | `#a85c50` | 0.46 |
| 1.00 | **night** | `#04050d` | `#090c1e` | `#10142c` | `#171d34` | `#20263f` | 1.00 |

The **stars** column is the *darkness* value — 0 in daylight, 1 at full night. `SkyController`
exposes it as `Darkness`; use it to fade your star layer and night ambience.

`SkyController.cs` bakes this into a 1×128 gradient `Texture2D` each frame and assigns it to a
full-screen background quad — cheap, and smooth at any resolution. (Prefer a shader? Feed the
five interpolated colours + positions to a vertical-gradient frag shader instead.)

---

## Sun & moon sprites (arc math)

Positions are in a normalised sky rect — `x` across the width, `y` up from the horizon,
`H` = sky height. `SkyController` maps these into world units (`skyWidth`, `skyBottomY`,
`skyHeight`).

**Sun** — visible `0.02 < t < 0.82`:
```
sp   = (t - 0.02) / 0.80            // 0..1 across the day
x    = (0.14 + 0.72*sp) * W         // left horizon → right horizon
y    = (0.14 + 0.66*sin(sp*π)) * H  // arcs up over noon
noon = sin(sp*π)                    // 0 at the horizons, 1 at noon
tint = lerp(#d64a28 → white, noon)  // red when low, white-hot at noon
alpha= clamp(min(sp, 1-sp) / 0.10)  // fade in at sunrise, out at sunset
```

**Moon** — visible `t > 0.80`, rising into night:
```
mp = (t - 0.80) / 0.20
x  = 0.70 * W
y  = (0.48 + 0.30*mp) * H
alpha = mp
```

Give each a soft additive **glow** behind it (warm for the sun, cool for the moon) if you want
the extra bloom — a second sprite or a light works. Both cross over briefly around `t≈0.80`
(sun setting as the moon lifts), which reads as twilight.

---

## Stars & the little lights (your layer)

Not shipped as art — they’re cheapest as a **ParticleSystem** or a scattered set of 1–2 px star
sprites in the upper ~¾ of the sky, with:

- **visibility** driven by `Darkness` (0 in day → full at night); a handful of brighter
  “lights”/planets wake a little earlier (dusk);
- **twinkle**: `alpha = base * (0.45 + 0.55 * (0.5 + 0.5*sin(time*speed + phase)))`, a
  different `speed`/`phase` per star so they never pulse in sync;
- an occasional **shooting star** at full dark — a short bright streak with a fading tail,
  every 5–12 s.

A ready reference field: ~150 stars, ~12 of them size-2 “lights”, seeded random positions,
`speed` 1.4–4.6, is what the HTML preview uses.

---

## Layer order

Back → front: **gradient quad → stars → sun / moon → (mountain range) → gameplay**. Keep the
sky on its own far sorting layer so parallax and fog sit in front of it. Ends at the darkest
point — leave `loop` off to hold night, or turn it on for a repeating cycle (add a sunrise on
the left if you loop, so the sun doesn’t pop).
