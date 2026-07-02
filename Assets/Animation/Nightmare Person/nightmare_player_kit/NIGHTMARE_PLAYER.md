# Nightmare Player — sprite sheets & jump-scare guide

The player, wearing the player. A random jump-scare that fires during **queues in marked
areas**: for a few frames the character in front of you is swapped for **you, drained and
wrong**. It reads as *you* first — same red shirt, same jeans, same skin and build — then
the wrongness lands: **two orbits torn too wide** over sunken black rings, an **extra-long,
lipless, toothless grin** stretched ear to ear, a **burst belly with the intestines spilling
and dragging**, and a **twisted spine punched through**. The whole body is **contorted** —
wrung one way, strung the other.

> 32 px cell, integer scale, **Point / nearest** filter, real alpha. The palette is *drained*,
> not replaced — it stays recolour-compatible with the player master so the thing keeps the
> player's identity. Everything is baked per-frame by `nightmareplayergen.js`
> (tunables: `dir`, `tier`, `PAL`). Re-export from there.

---

## Files

Two contortion **directions** × two gore **tiers** × two **archetypes** = 8 sheets.

| File | Dir | Tier | Body |
|---|---|---|---|
| `character_sprite_sheet_nm_wrung_base.png`       | wrung  | base | male   |
| `character_sprite_sheet_nm_wrung_you.png`        | wrung  | you  | male   |
| `character_sprite_sheet_nm_strung_base.png`      | strung | base | male   |
| `character_sprite_sheet_nm_strung_you.png`       | strung | you  | male   |
| `character_sprite_sheet_long_nm_wrung_base.png`  | wrung  | base | female |
| `character_sprite_sheet_long_nm_wrung_you.png`   | wrung  | you  | female |
| `character_sprite_sheet_long_nm_strung_base.png` | strung | base | female |
| `character_sprite_sheet_long_nm_strung_you.png`  | strung | you  | female |

All sheets are **160 × 32**, cell **32 × 32**, **5 frames in one row**. `-8x.png` variants are
preview blow-ups — **do not ship**. `_long` = the female (long-hair) archetype. The
un-suffixed `character_sprite_sheet*.png` are the healthy overworld player (swap to these in
the waking world).

---

## The sheet — frames

Cell `(col*32, 0, 32, 32)`, cols 0–4. Single row. Layout matches the healthy player master
so it drops straight into the existing rig:

| Cols | State | Frames |
|---|---|---|
| 0–1 | **idle** | 2 |
| 2–4 | **walk** | 3 |

The gore is baked into **every** frame; the dangling bits (intestines, dropped organ) **sway
frame-to-frame** so they swing when the frames play.

---

## The two directions

Both keep the player's silhouette and colours — they differ in **how the body is broken**.
Ship both; choose per spawn or per area.

- **WRUNG** *(twisted / broken)* — head cocked hard to the shoulder, the **spine wrenched and
  punched out one side**, torso counter-twisted, and the **gut drags after it** to the lean
  side. Aggressive, snapped, in pain.
- **STRUNG** *(marionette)* — too upright, too still. The **head lolls**, the body is drawn
  **thin**, faint **puppet strings** run up to the crown and hands, and the **guts hang
  straight down** in a slack column. You clock that it doesn't move like a person before you
  clock the face.

---

## What's baked into every frame (the grotesque pass)

- **A pale, drained "you."** The palette is pulled toward waxy death — but the red shirt, the
  jeans, the skin and hair all stay recognisable. It has to be *you* before it's a corpse.
- **Two gaping orbits.** The eyes are gone; the sockets are torn open toward the temples over
  **sunken blackened rings**, punched into a pale mask face so they read as holes.
- **The extra-long toothless grin.** A huge, lipless, **upturned** smile stretched ear to ear,
  hollow, **no teeth** — nothing behind it. The single most uncanny read on the sprite.
- **Burst belly + spilling intestines.** The shirt is torn open low; a tangle of gut loops
  **spills and drags** (WRUNG drags to the lean side; STRUNG hangs straight), with a dropped
  organ, strands and weight-drips. All of it **sways per frame**.
- **A protruding twisted spine.** Knobby vertebrae punched through the side (WRUNG) or showing
  through the stretched throat (STRUNG).

None of this is animated *by you* — it's already in the pixels. Your job is the **swap** and
the **wrong timing**:

---

## The mechanic — a random jump-scare during queues

Fire it at random while the player is **queued / held in a marked zone** (a line, a cutscene
hold, a scripted wait). The swap is the scare:

```js
// on a random timer inside a marked zone (e.g. every 8–24 s, low probability per tick):
function flashNightmare(targetSprite, body /* 'male'|'female' */, dir /* 'wrung'|'strung' */) {
  const long = body === 'female' ? '_long' : '';
  // the reveal ALWAYS references the player at full gore -> the 'you' tier
  targetSprite.texture = `character_sprite_sheet${long}_nm_${dir}_you.png`;
  targetSprite.frame   = 0;                 // a stark idle frame
  // the punch (a transform, not new frames): ~120–300 ms total
  tween(targetSprite, {
    scale:  '+38%',                          // lunges at the camera
    y:      '-10 px',
    tremble: 3,                              // 2–4 px per-frame jitter
    glitch: true,                            // 2–3 offset scanline slices + a brief red wash
  }, /*ms*/ 220);
  // then CUT back to the normal character sprite. no ease-out — it's just gone.
}
```

**Two tiers, deliberately.** A doppelgänger can also just *be around* — walking a queue,
standing wrong — on the **base** sheet, which only reads as "not quite right" (drained, sunken
eyes, that long closed wrong smile). The instant it **references the player** — the flash, a
direct look, the beat where it turns to face you — swap to the **you** sheet. *The gore turns
up when it's wearing you.*

**Resting cadence** (when it's a standing/walking doppelgänger, not mid-scare): play idle
0–1 **slowly**, with occasional **dead holds** (600–1500 ms frozen stares) and rare
micro-judders. It should stand too still. Never a clean, natural loop.

---

## Contexts — when each operates

| World state | Sheet |
|---|---|
| waking overworld, safe | `character_sprite_sheet*.png` (healthy player) |
| a doppelgänger present, not yet triggered | `..._nm_<dir>_base.png` |
| the flash / it references the player | `..._nm_<dir>_you.png` |

Match `body` to the player's archetype (male / female) so the thing that flashes is *their*
build. `dir` can be fixed per area for a consistent haunt, or rolled per spawn.

---

## Integration notes

- Draw `image, col*32, 0, 32, 32 → dest`. Origin **top-left**; place so the feet sit on the
  ground line. Point-filter, no compression, integer scale only.
- The torn orbits, the grin, the dangling gut and the dropped organ extend **below/around the
  original silhouette but stay inside the 32-px cell** — no atlas bleed, but give the sprite a
  little headroom when placing shadows among tiles.
- **Recolour-compatible.** The gore is baked as its own palette; the base garment/skin colours
  are left intact, so the existing player palette-swap still works if you ever want the
  nightmare to wear the player's *exact* chosen colours. (Custom colours aren't required for
  the scare — the default read is enough.)
- Keep the frames untouched; own the **swap** and the **flash transform** in engine.
- Retune in `nightmareplayergen.js`: `dir` ('wrung'|'strung'), `tier` ('base'|'you'), the
  `PAL` drain, and the gore passes (orbits, grin, belly/guts, spine, warp). Re-export all 8.
