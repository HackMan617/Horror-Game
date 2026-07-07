# REDWOOD.md — Giant trees & choppable spruce (Unity 2D)

Two tree systems for the Nightmare Realm's wooded mountain terrain:

- **Huge idle giants** — impossibly tall, oppressive redwoods with **massive trunks** the player
  navigates *around*. Dark spruce canopy far above, furrowed bark studded with **barbed ridges**,
  **clawed roots gripping into the earth**, faint **bio-glow** sap-hollows and fungal shelves, and
  a **foliage brush mound** hugging each base. **No cast ground-shadow** — the roots + brush carry
  the ground contact instead (a shadow puddle made Unity's renderer read them as floating). Slow
  heavy sway + a base groan-flex + shedding leaves. 3 variants.
- **Choppable spruce** — a mid-large dark-spruce you can fell for wood: an 8-stage sequence
  from intact → deepening notch (chips) → creak-lean → topple → stump + dropped logs, **plus an
  8-frame idle sway** so the standing tree breathes like the giants.

All art is procedural in `redwoodgen.js`. Point-filter / integer-scale pixel art.

---

## 1. Files

| File | Cell | Layout | Notes |
|---|---|---|---|
| `sprites/giant_elder.png`  | 224×480 | 8 cols × 1 row | huge idle — massive monolith, clawed roots |
| `sprites/giant_gnarl.png`  | 224×480 | 8 cols × 1 row | huge idle — twisted lean, extra barbs/roots |
| `sprites/giant_winter.png` | 224×480 | 8 cols × 1 row | huge idle — snow-dusted, dim glow |
| `sprites/tree_chop.png`    | 112×272 | **8 cols × 2 rows** | choppable — row 0 damage/fall stages, row 1 idle sway |
| `sprites/*-2x.png`         | — | — | preview blowups — **do not ship** |

- Giants are now **224×480** (logical 112×240): a much girthier trunk that fills the frame, a
  large root flare + clawed roots below the base, and a foliage brush mound at the very bottom
  of the cell. **There is intentionally no cast-shadow ellipse.**

- **Giants:** columns 0–7 are a **seamless idle loop** (sway + glow pulse + falling leaves/snow).
- **Choppable, row 0:** columns 0–7 are **discrete stages**, NOT a loop — you drive the index
  from gameplay (axe hits), not a timer.
- **Choppable, row 1:** columns 0–7 are a **seamless idle sway loop** of the *intact* tree (gentle
  branch drift, same 8-frame period as the giants). Play this while the tree is untouched, then
  switch to row 0 the moment the first axe hit lands. Row-0 stage 0 and row-1 frame 0 are the
  same pose, so the hand-off doesn't pop.

---

## 2. Import settings (per texture)

| Setting | Value |
|---|---|
| Texture Type | **Sprite (2D and UI)** |
| Sprite Mode | **Multiple** |
| Pixels Per Unit | **32** (giants now read ~7.5 units tall at the 480-px cell; tune to your world) |
| Filter Mode | **Point (no filter)** |
| Compression | **None** |
| Mip Maps | off · Wrap: Clamp |

> **Pivot / grounding:** set the pivot at **Bottom (Bottom-Center)** so the tree plants on the
> terrain line. The sprites carry **no cast shadow** by design — the clawed roots and the brush
> mound sit right at the bottom edge of the cell and read as the ground contact, so the trunk
> never looks like it floats above a detached shadow. If you want a contact shadow, drop a
> separate soft blob **on the ground sorting layer beneath the trunk**, not baked into the sprite.

### Slicing (Sprite Editor → **Grid By Cell Count**)

- **Giant sheets:** Column **8**, Row **1** → 8 frames (`giant_elder_0 … _7`).
- **Chop sheet:** Column **8**, Row **2** → 16 sprites (`tree_chop_0 … _15`). Row 0 = stages
  `0–7`, row 1 = idle-sway `8–15` (i.e. idle frame *f* is sprite `8 + f`).

Pivot: set **Bottom** (or Bottom-Center) on every slice so trees plant on the ground line and
the choppable pivots at its base as it falls.

---

## 3. Huge idle giant — flipbook

A slow, heavy loop. Giants are enormous, so keep it **slow** — ~7–9 fps — and stagger the start
frame per instance so a stand doesn't sway in lockstep.

```csharp
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class GiantTree : MonoBehaviour
{
    [SerializeField] Sprite[] frames;      // 8 sliced frames of one giant variant
    [SerializeField] float fps = 8f;
    SpriteRenderer sr;
    float t;
    int frame;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        frame = Random.Range(0, frames.Length);       // stagger the stand
        t = Random.value / fps;
    }

    void Update()
    {
        t += Time.deltaTime;
        if (t >= 1f / fps) { t -= 1f / fps; frame = (frame + 1) % frames.Length; sr.sprite = frames[frame]; }
    }
}
```

**Navigating *around* them:** the sprite is a tall billboard; give the trunk a `CapsuleCollider2D`
(or a narrow `BoxCollider2D`) covering only the **lower trunk**, not the canopy, so the player
collides with the base and walks freely "behind" the crown. In a 2.5D setup, sort by the trunk
base Y so the player passes in front of/behind correctly.

**Selling the dread:** the sway/glow is baked, but add a **parallax** offset (giants drift
slightly opposite camera motion) and a low, occasional **wood-groan SFX** synced to nothing in
particular — the irregularity is what unsettles. The bio-glow hollows pulse on their own; you
can tint a faint `Light2D` at each hollow's local position for extra bleed.

---

## 4. Choppable spruce — stage machine

8 stages, driven by axe hits (not a timer). While the tree is **intact and untouched**, loop the
**idle-sway row** (sprites `8–15`) at ~7–9 fps; on the first hit, stop the loop and step through
the stage row:

```
row 1  idle sway 0..7  (loop while full health)
row 0  0 Intact → 1 Notched → 2 Cut → 3 Deep cut → 4 Creaking(lean) → 5 Toppling → 6 Falling → 7 Felled(stump+logs)
```

- Hits **0→4** advance one stage per swing (the notch deepens, chips fly).
- Past the deep cut, stages **4→7 auto-play** as the fall animation (timer, ~0.2s/frame).
- Stage **7** is the harvestable result: a stump + dropped logs. Spawn your wood pickups here.

```csharp
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class ChoppableTree : MonoBehaviour
{
    [SerializeField] Sprite[] stages;          // 8 sliced stages (row 0: tree_chop_0..7)
    [SerializeField] Sprite[] idle;            // 8 idle-sway frames (row 1: tree_chop_8..15)
    [SerializeField] int hitsPerStage = 3;     // axe swings to deepen one notch
    [SerializeField] GameObject chipVfx;       // optional per-hit splinter burst
    [SerializeField] GameObject logPickup;     // spawned when felled
    [SerializeField] float fallFrameSec = 0.2f;
    [SerializeField] float idleFps = 8f;       // gentle standing sway

    SpriteRenderer sr;
    int stage, hits, idleFrame;
    float idleT;
    bool falling, felled, touched;

    void Awake() { sr = GetComponent<SpriteRenderer>(); sr.sprite = idle[0]; }

    void Update()                               // idle sway until the first hit
    {
        if (touched) return;
        idleT += Time.deltaTime;
        if (idleT >= 1f / idleFps) { idleT = 0; idleFrame = (idleFrame + 1) % idle.Length; sr.sprite = idle[idleFrame]; }
    }

    /// <summary>Call on each axe hit that connects with this tree.</summary>
    public void Chop()
    {
        if (falling || felled) return;
        if (!touched) { touched = true; sr.sprite = stages[0]; }   // leave the idle loop
        if (chipVfx) Instantiate(chipVfx, transform.position, Quaternion.identity);

        if (stage < 3)                              // deepen the notch
        {
            if (++hits >= hitsPerStage) { hits = 0; sr.sprite = stages[++stage]; }
        }
        else                                        // past the heartwood → it goes
        {
            StartCoroutine(Fall());
        }
    }

    IEnumerator Fall()
    {
        falling = true;
        for (int s = 4; s <= 7; s++) { sr.sprite = stages[s]; yield return new WaitForSeconds(fallFrameSec); }
        falling = false; felled = true;
        if (logPickup) Instantiate(logPickup, transform.position, Quaternion.identity);
        // optional: disable the trunk collider now that only a stump remains
    }
}
```

Wire your axe/attack to raycast or overlap the tree and call `Chop()`. The **wood yield** can
scale with `hitsPerStage`, tree size, or a player tool tier — the sprite sequence is agnostic.
Stage 7 (stump) can be left as harvestable decor or made a re-standing spawn point.

**No axe?** The choppable simply reads as a normal spruce — the idle-sway row loops so it blends
in with the giants — and the felling mechanic is opt-in, so trees are safe to scatter whether or
not the player ever picks up a tool.

---

## 5. Re-exporting the art

All procedural in `redwoodgen.js`:

```js
eval(await readFile('redwoodgen.js'));
await window.RedwoodGen.buildAll({ createCanvas, saveFile });   // 3 giants + chop + blowups
```

Tunables: `trunkHalf` / `trunkAxis` (silhouette girth + twist/lean per variant), `swayAt` (idle
motion + base groan-flex), `hollowsFor` / `GLOW` (bio-glow placement + colour), `drawRoots`
(clawed root count/spread/curl), `drawThorns` (barb spacing + length), `drawBrush` (mound size +
fern fronds), `drawFungus`, `drawLeaves`, and the `CTIERS` / chop stage functions (`drawNotch`,
fall angles, `drawStump`). Add a variant by extending the `variants` list in `buildAll`.

> Cell size lives in the `HW`/`HH` constants (currently 224×480). If you change them, update the
> Unity slice (still **Grid By Cell Count**, Column 8 / Row 1) and the preview canvas sizes in
> `Giant Trees.dc.html`.
