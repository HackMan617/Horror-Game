# Robert's Yard — exterior tech-junk props · sprite guide

The machines a mountain lunatic keeps out front. Salvaged, humming, half-tended — he *almost*
acknowledges the mess, but he's up in the mountains, so who's counting. Each machine has a
**wrong** nightmare reskin driven by the **same dread flag** as Robert, the dog, the mountain and
the houses — plus an engine-side *wander* behaviour. Every machine also runs a short **robotic idle
animation** (servo sweeps, blinking LEDs, scrolling traces, spinning reels) in **both** forms.

> 16-px grid, integer scale, **Point / nearest** filter, real alpha. Hero pieces are **32×48**.
> Everything is baked per-region and per-frame by `robertpropsgen.js` — retune palette /
> proportions / the idle frames / the "wrong" passes there and re-export both atlases.

---

## Files

Two atlases, identical layout, **208×80**. Swap on the dread flag.

| Form | File |
|---|---|
| day | `props_robert.png` |
| nightmare | `props_robert_nightmare.png` |

`-8x.png` variants are preview blow-ups — **do not ship**.
`Robert's Yard - Tech Junk.dc.html` is the interactive reference (yard diorama, day↔nightmare
toggle, per-machine callouts, atlas view).

---

## The atlas — regions & frames

Each region is a horizontal **frame strip**: cell `(x, y, cellW, cellH)` × `frames`, laid out left
to right. Draw frame `n` from `atlas, x + n*cellW, y, cellW, cellH → dest`. Anchor by the **feet**
so nightmare growth and glow rise upward, never sink.

| Machine | x,y,cellW,cellH | Frames | Idle motion | Day → Nightmare |
|---|---|---|---|---|
| **dish**        | 0,0,32,48    | 3 | dish sweeps on its servo | scans the sky → turns to **face you**, a listening eye whose pupil darts |
| **crt**         | 96,0,32,48   | 3 | scanline sweeps, cursor + power LED blink | gutted glass → **wakes** into a vertical eye of static, flickering |
| **serverTower** | 0,48,16,32   | 3 | activity LEDs + drive blip cycle | humming box → a vertical **slit-mouth** opens, teeth glint |
| **scope**       | 48,48,32,16  | 4 | waveform scrolls, reels spin a spoke | flat trace → reels become **spiral eyes**, tube shows a **scream** |
| **hacksaw**     | 48,64,32,16  | 2 | grip status LED blinks, gleam runs the blade | **circuit-board teeth** → real **fangs** with a pulsing red gleam |
| **cables**      | 176,48,16,16 | 2 | charge LED + prong spark blink | a tangle → cords **rise like tendrils**, prongs turn to cold eyes |
| **battery**     | 176,64,16,16 | 2 | charge LED blink, terminal spark arc | salvaged cell → **leaks glowing acid**, an eye in the label |

**Playback:** loop each strip at its own rate so the yard doesn't tick in lockstep — the reference
uses roughly dish 520ms · crt 190ms · tower 360ms · scope 150ms · saw 600ms · cables 430ms ·
battery 560ms per frame. The nightmare strips share the same frame counts and can play at the
same rates.

---

## The wrong — how the yard turns

The horror is **wrongness, not gore** (matching Robert): screens open into eyes, the dish turns to
face you, cables lift, the beige goes waxy grey-green. Shared nightmare cues with the rest of the
game — **sick green** glow (`#78dc6e`), cold eye-glints (`#cfe3e8`), void black.

### Wander behaviour (engine-side)
When Robert springs into his stretched form, the machines **go wrong too**: each one eases a few
feet **toward the player's house** (screen-left), then **snaps back home**, over and over —
staggered so they don't move in lockstep. Robert himself does **not** move; he just **stares**.
Gate the whole effect to **proximity / the dread flag** like everything else. The `.dc.html`
reference fakes this with a per-prop eased offset + a faint green trail; own the real timing in
engine. Tweaks `creep` and `liveGlow` toggle the wander and the pulsing screen-glow.

---

## Palette

Day tech is **retro-analog beige** — beige plastic (`#c7bda3`), brushed metal (`#8b929c`), rust,
dead green glass, circuit-board green + copper, black/orange rubber cable, brass knobs. Nightmare
**drains** the beige to waxy grey-green (`#8a9a80`) and lights the wrong bits sick-green. Because
the art is flat (no anti-aliasing), exact-colour region swaps are lossless.

---

## Integration notes

- Layer order in the street: sky → mountains → houses (by base Y) → **props (by base Y)** →
  Robert. Depth-sort the machines with the character layer so he can stand among them.
- Anchor every sprite by the **feet**; place so the base sits on the ground line.
- Loop each frame strip at its per-machine rate (above); own the **form swap**, the **wander**, and
  the **proximity gate** in engine. Idle frames can play always; gate the wander to proximity.
- Retune in `robertpropsgen.js`: the `PA` palette (day + nightmare), each machine's draw fn (which
  now takes a frame index `f`), and the nightmare `if(nm)` passes. Re-export both atlases with the
  bake loop.
