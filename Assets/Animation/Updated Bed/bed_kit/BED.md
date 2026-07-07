# BED.md — Cabin bed & nightmare portal (Unity 2D)

A rustic **spruce-frame bed** for the wooden cabin interior — the SLEEP prop and the
**Nightmare Portal**. Headboard + footboard, a **plaid quilt**, two pillows, and the dent
where the dog sleeps. The player can **make** the bed (pull the blanket on) or **strip** it
(throw the blanket aside). Its corrupted twin is the way *in*: the sheets tear and **rotting
hands reach up and drag the player under**, into a black void.

Built procedurally in `bedgen.js`. Point-filter / integer-scale pixel art in the cabin's
`dusk` interior palette (spruce `#7c5a3a`, linen `#dccfb4`, tartan red `#b44e34`). Drawn in the
same oblique ¾ as the fireplace & furniture. Live viewer: **`Bed.dc.html`**.

---

## Files

| File | Size | Cell | Grid | View |
|---|---|---|---|---|
| `sprites/bed_front.png` | 512×192 | 64×64 | 8 cols × 3 rows | **Foot** — foot toward camera, headboard at back |
| `sprites/bed_back.png`  | 512×192 | 64×64 | 8 cols × 3 rows | **Head** — headboard toward camera |
| `sprites/bed_left.png`  | 512×192 | 64×64 | 8 cols × 3 rows | **Left side** — head to the left |
| `sprites/bed_right.png` | 512×192 | 64×64 | 8 cols × 3 rows | **Right side** — head to the right |
| `sprites/bed_*_nightmare.png` | 512×192 | 64×64 | 8 cols × 3 rows | corrupted twin of each view |
| `sprites/bed_*-4x.png` | — | — | — | preview blow-ups — **do not ship** |

**Four true views, not mirrors.** Light comes from the **upper-left (window moonlight)** in
every sheet, so `left` and `right` are drawn independently — do **not** flip X to fake one from
the other, or the lighting inverts. Point the SpriteRenderer at the sheet that matches the
camera facing as the player walks around the room.

### Layout — rows are states, columns are frames

**Normal (`bed_<view>.png`):**

```
row 0  IDLE      made bed, resting          — seamless LOOP 0↔7 (subtle breathe + sheen)
row 1  ON        unmade → made (pull up)    — play once, 0→7   ("make the bed")
row 2  OFF       made → thrown aside        — play once, 0→7   ("strip the blanket")
```

**Nightmare (`bed_<view>_nightmare.png`):**

```
row 0  IDLE      the portal at rest, stirring        — LOOP 0↔7 (bulge + faint void)
row 1  REACH     rot-hands erupt & grasp upward      — play once 0→7, then hold or LOOP
row 2  DRAG      void yawns, everything pulled under — play once 0→7, rest on frame 7 (consumed)
```

- **8 frames** per state. Sprite index = `row * 8 + col`.
- **Continuity (normal):** ON frame 7 ≈ IDLE (made); OFF frame 7 ≈ ON frame 0 (both the
  slept-in "unmade" rest). So **made → strip → (unmade) → make → made** flows with no pop.
- **Pillows** carry a deliberately **darker cast shadow** onto the quilt (cool taupe `#7c7154`)
  so they lift off the bedding. The **dog-nest** dent + a few cream fur wisps sit mid-quilt.
- **Nightmare is the same silhouette, rotted:** cold desaturated wood, sickly linen, a plaid
  drained to muddy brown, blood seep, grey-green rot flesh, black void `#06060a`.

---

## Import settings

| Setting | Value |
|---|---|
| Texture Type | Sprite (2D and UI) |
| Sprite Mode | **Multiple** |
| Pixels Per Unit | **16** (the bed reads ~4×4 tiles) |
| Filter Mode | **Point (no filter)** |
| Compression | None · Mip Maps off · Wrap Clamp |

Slice **Grid By Cell Count** → Column **8**, Row **3** → 24 sprites `bed_front_0 … _23`
(left→right, top→bottom); slice every view/twin the same way. Pivot **Bottom-Center** so the
bed plants on the floor line. Give it a footprint collider and a **trigger** around it for the
"sleep / make / strip" interaction prompt.

---

## Make / strip + nightmare state machine

```csharp
using System.Collections;
using UnityEngine;

public class Bed : MonoBehaviour
{
    public enum Row { Idle = 0, On = 1, Off = 2 }     // On=make/reach, Off=strip/drag

    [SerializeField] Sprite[] normal;   // 24 sliced sprites for the current view (row*8 + col)
    [SerializeField] Sprite[] night;    // 24 from bed_<view>_nightmare.png
    [SerializeField] SpriteRenderer sr;
    [SerializeField] float fps = 8f;    // ~120 ms/frame, matches Bed.dc.html
    [SerializeField] bool dread;        // flip to the nightmare twin on the dread flag

    Sprite[] Sheet => dread ? night : normal;
    int Index(Row r, int f) => (int)r * 8 + f;
    void Show(Row r, int f) => sr.sprite = Sheet[Index(r, f)];

    void OnEnable() => StartCoroutine(Loop(Row.Idle));   // rest on the breathing idle

    /// <summary>Pull the blanket on, then settle to the made idle.</summary>
    public void MakeBed()   => StartCoroutine(Once(Row.On,  () => StartCoroutine(Loop(Row.Idle))));
    /// <summary>Throw the blanket aside; rest on the stripped last frame.</summary>
    public void StripBed()  => StartCoroutine(Once(Row.Off, null));

    // nightmare intent (dread == true): reach loops after erupting; drag rests consumed
    public void Reach() => StartCoroutine(Once(Row.On,  () => StartCoroutine(Loop(Row.On))));
    public void Drag()  => StartCoroutine(Once(Row.Off, null));

    IEnumerator Once(Row r, System.Action done)
    {
        StopAllCoroutines();
        for (int f = 0; f < 8; f++) { Show(r, f); yield return new WaitForSeconds(1f / fps); }
        done?.Invoke();                                   // null ⇒ hold on frame 7
    }
    IEnumerator Loop(Row r)
    {
        int f = 0;
        while (true) { Show(r, f); f = (f + 1) % 8; yield return new WaitForSeconds(1f / fps); }
    }
}
```

### Notes

- **Swap the sheet, not the geometry.** `normal` and `night` are the same 8×3 layout for the
  same view — set `dread` and the same `MakeBed`/`StripBed` calls read the corrupted frames.
  Like the rest of the interior set, **flicker** the nightmare twin in over the day sprite as
  the player realizes they never woke up (drive the flicker on the dread flag).
- **Four views:** keep a sprite array per facing and assign the one matching the camera as the
  player orbits the room. `back` is mostly the headboard with the quilt/pillows peeking over the
  top — the least animated angle; `front` and the two `side`s carry the action.
- **The bed is the portal.** In nightmare, `Reach` is the summon (hands find the air) and `Drag`
  is the take (the void opens and pulls you in) — gate the "you never woke up" beat on `Drag`
  reaching frame 7. Pair with a low sub-bass swell + wet cloth tear.
- **Persist** made/stripped state in your save so a made bed stays made.
- **Ties into the loop:** same spruce palette as `FIREPLACE.md` / `INTERIOR_FURNITURE.md`; the
  dog-nest evidence nods to the companion in `README.md` (`dog_*` sheets).

---

## Re-exporting

```js
eval(await readFile('bedgen.js'));
await window.BedGen.buildAll({ createCanvas, saveFile });                 // all 8 sheets + 6x previews
await window.BedGen.buildAll({ createCanvas, saveFile, only: 'normal' }); // just the cabin bed
await window.BedGen.buildAll({ createCanvas, saveFile, only: 'nightmare' });
```

Tunables in `bedgen.js`: palettes `PN` (cabin) / `PM` (nightmare); `plaidAt` / `shadePlaid`
(the tartan grid + fold shading); `plank` / `finial` (spruce frame + turned posts); `paintPillow`
(pillow + its cast shadow), `dogNest`, `paintSideBlanket*` / `paintFrontBedding` / `paintBackPeek`
(per-view bedding + the ON/OFF coverage sweep); `rotHand` / `voidHole` / `bloodSeep` (the
nightmare overlays); and `schedule` (the per-row coverage / heap / reach / drag / void timeline
that drives every view).
