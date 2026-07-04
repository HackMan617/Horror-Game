# DREAD.md — Dread Detector in Unity 2D

Concrete Unity 2D wiring for the Dread Detector face readout. Art + layout reference lives in
`DREAD_DETECTOR.md`; this file is the engine-side integration only.

---

## 1. The assets

Ship the six per-body sheets **or** the single master atlas — same pixels either way.

```
sprites/dread_parasite.png        sprites/dread_parasite_long.png     (male / female)
sprites/dread_melt.png            sprites/dread_melt_long.png
sprites/dread_fracture.png        sprites/dread_fracture_long.png
sprites/dread_master_atlas.png    (576×576 — all of the above in one)
```

- **Cell:** 48×48 px
- **Per sheet:** 4 columns (anim frame 0–3) × 6 rows (dread level 0–5)
- **Master atlas:** 2 body-rows × 3 variant-cols of those 192×288 blocks

---

## 2. Import settings (per texture)

| Setting | Value |
|---|---|
| Texture Type | **Sprite (2D and UI)** |
| Sprite Mode | **Multiple** |
| Pixels Per Unit | **48** (1 cell = 1 unit; adjust to taste) |
| Filter Mode | **Point (no filter)** |
| Compression | **None** |
| Generate Mip Maps | off |
| Wrap Mode | Clamp |

### Slicing (Sprite Editor → Slice → **Grid By Cell Count**)

- **Per-body sheet:** Column **4**, Row **6** → 24 sprites, named `<sheet>_0 … _23`.
- **Master atlas:** Column **12**, Row **12** → 144 sprites, named `dread_master_atlas_0 … _143`.

Sprites are numbered **left→right, then top→bottom**.

---

## 3. Indexing math

### Per-body sheet (24 sprites, 4 cols)

```csharp
int Index(int level, int frame) => level * 4 + frame;   // level 0..5, frame 0..3
```

### Master atlas (144 sprites, 12 cols)

```csharp
// variantCol: parasite=0, melt=1, fracture=2   |   bodyRow: male=0, female=1
int AtlasIndex(int variantCol, int bodyRow, int level, int frame)
{
    int col = variantCol * 4 + frame;   // 0..11
    int row = bodyRow    * 6 + level;   // 0..11
    return row * 12 + col;
}
```

---

## 4. Frame timing (escalates with dread)

Animation is **near-static and slow when calm, faster with more frames as dread rises**.
Columns past a level's real frame count repeat the last frame, so playing 4 columns is safe.

```csharp
static readonly int[]   Frames   = { 2, 2, 2, 3, 3, 4 };            // usable frames per level
static readonly float[] FrameSec = { 0.52f, 0.44f, 0.36f, 0.22f, 0.16f, 0.11f };
static readonly int[]   Bpm      = { 48, 58, 74, 96, 124, 168 };    // for the heartbeat hook
```

Level names: `0 Dormant · 1 Uneasy · 2 Worried · 3 Anxious · 4 Parasitic · 5 Nightmare`.

---

## 5. Drop-in component

Drives a UI `Image` (or swap for `SpriteRenderer.sprite`) from a dread value. Assign the 24
sliced sprites of the chosen variant+body to `frames` in the inspector.

```csharp
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class DreadDetector : MonoBehaviour
{
    [Tooltip("24 sliced sprites: level*4 + frame (4 cols x 6 rows)")]
    [SerializeField] private Sprite[] frames;      // length 24
    [SerializeField] private Image image;

    static readonly int[]   FrameCount = { 2, 2, 2, 3, 3, 4 };
    static readonly float[] FrameSec   = { 0.52f, 0.44f, 0.36f, 0.22f, 0.16f, 0.11f };

    int level;          // 0..5
    int frame;
    float timer;

    void Reset()  => image = GetComponent<Image>();
    void Awake()  { if (image == null) image = GetComponent<Image>(); }

    /// <summary>Feed dread as 0..1; maps to the six discrete levels.</summary>
    public void SetDread(float dread01)
        => level = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(dread01) * 5f), 0, 5);

    /// <summary>Or set the level directly (0..5).</summary>
    public void SetLevel(int lvl) => level = Mathf.Clamp(lvl, 0, 5);

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= FrameSec[level])
        {
            timer -= FrameSec[level];
            frame = (frame + 1) % FrameCount[level];
        }
        image.sprite = frames[level * 4 + frame];
    }
}
```

### Switching variant / body at runtime

Keep one `Sprite[24]` per variant+body you use and swap the reference:

```csharp
[SerializeField] Sprite[] parasiteMale, parasiteFemale, meltMale, /* … */;
Sprite[] active;

void SelectSkin(bool female, int variant /*0..2*/)
{
    active = (variant, female) switch {
        (0, false) => parasiteMale, (0, true) => parasiteFemale,
        (1, false) => meltMale,     (1, true) => meltFemale,
        _          => female ? fractureFemale : fractureMale,
    };
    // then read `active[level*4 + frame]` in Update instead of `frames[...]`
}
```

Pick the **body** to match the player's archetype; fix the **variant** per area/chapter (or
escalate it across the game) so the haunt reads consistently.

---

## 6. Selling it in-engine

- **Heartbeat:** drive a pulse SFX / screen-edge vignette off `Bpm[level]` (≈48→168 BPM) so the
  HUD feels like a live vitals monitor.
- **Snap, don't lerp** when the level jumps — the face should lurch into the worse state.
- **Screen shake / chromatic split** at levels 4–5 amplifies the baked tremble.
- **Hold on Nightmare:** when dread maxes, let it sit and stare before any payoff.
- **PPU / canvas:** for a crisp HUD element on a Screen-Space Canvas, keep the RectTransform an
  integer multiple of 48 px (48/96/144…) and leave the Image `Preserve Aspect` on.

---

## 7. Re-exporting the art

All procedural in `dreaddetectorgen.js`:

```js
eval(await readFile('dreaddetectorgen.js'));
await window.DreadDetector.buildAll({ createCanvas, saveFile });   // all 6 sheets + master atlas
```

Tunables: per-level `paramsFor` (brow/eye/pupil/mouth/sweat, frame counts, tremble), `skinAt`
(drain + sicken curve), the accent palette, and the `midDistort` / `goreDistort` passes per
variant. `body` ('male' | 'female') controls the hair.
