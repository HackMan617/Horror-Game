# CHOPPING.md — Player axe-swing, log pickup & carry (Unity 2D)

Woodcutting art built directly on the existing player rig. The head, palette and build are the
untouched base-sprite pixels (`character_sprite_sheet*.png`), so these drop in beside the idle
and walk sheets with no seams.

## Files

| File | Size | Cell | Cells |
|---|---|---|---|
| `sprites/chop_male.png`   | 160×32 | 32×32 | 5 — swing 0-3, then **hold-wood** |
| `sprites/chop_female.png` | 160×32 | 32×32 | 5 — swing 0-3, then **hold-wood** |
| `sprites/log_pickup.png`  | 32×16  | 16×16 | 2 — idle bob + glint |
| `sprites/fp_axe.png`      | 640×128 | 128×128 | 5 — first-person viewmodel swing |
| `sprites/*-6x.png` · `fp_axe-3x.png` | — | — | preview blow-ups — **do not ship** |

**Point filter · integer scale.** Cell index → `(col*W, 0, W, H)`.

### Cell map (characters)

```
0 raise   1 swing   2 bite(impact+chips)   3 recoil   4 hold-wood
└────────── 4-frame swing loop ──────────┘   └ carry pose (static) ┘
```

- **Swing** is cells **0→1→2→3** (play once per axe stroke, ~10–12 fps). Land the tree-damage /
  chip VFX on cell **2** (the bite) — that frame already throws a couple of chips.
- **Hold-wood** (cell 4) is a **static carry pose** — show it while the player walks back with a
  log, or as the "you got wood" beat.

---

## Import settings

| Setting | Value |
|---|---|
| Texture Type | Sprite (2D and UI) |
| Sprite Mode | Multiple |
| Pixels Per Unit | 32 (match your other player sheets) |
| Filter Mode | Point (no filter) |
| Compression | None · Mip Maps off |

Slice **Grid By Cell Count**: characters Column **5** Row **1**; log Column **2** Row **1**.
Pivot **Bottom-Center** on the characters (feet on the ground line), **Center** on the log.

---

## Shirt color = the player's chosen customization color

The red on these sprites is the player's **shirt**, and it must be tied to whatever color the
player picked in character customization — the sleeve on the arm should always match the shirt
they're wearing, in **both** the third-person swing and the first-person viewmodel. The shirt
pixels are authored in the exact same base palette as `character_sprite_sheet*.png`, so the same
runtime recolor that tints the idle/walk sheets tints these with no extra work:

| Role | Sprite pixels (source) | Recolor to |
|---|---|---|
| Shirt / sleeve base | `#d83030` | player shirt **base** color |
| Shirt / sleeve shadow | `#983018` | player shirt **shadow** (≈ base × 0.7) |
| Sleeve deep shadow (FP only) | `#6a1e14` | player shirt **deep shadow** (≈ base × 0.5) |

- These are the **only** red pixels in the sheets — skin, wood and steel use separate ramps, so
  a shirt recolor never bleeds onto the hand, handle or blade.
- Apply the identical recolor to `chop_male` / `chop_female` **and** `fp_axe` so a perspective
  switch never shows a mismatched shirt.
- Implementation options: the same **palette-swap/LUT** you already use for the base character;
  or, if the shirt is the only tinted element, a shader/material that maps the shirt ramp to the
  chosen color; or a `Color` multiply **only** if the shirt is isolated on its own material.
  Recolor the **base and shadow together** (don't flat-tint) so the fabric keeps its shading.

If your customization also swaps skin or hair tone, those pixels (`#f0b890`/`#c07a4c` skin;
hair ramp on the characters) map the same way as the base sheets — the chop frames reuse the
exact base pixels, so every existing customization option Just Works here too.

---

## Swing → drop → carry flow

```csharp
using System.Collections;
using UnityEngine;

public class PlayerChop : MonoBehaviour
{
    [SerializeField] Sprite[] chop;      // 5 sliced cells (0-3 swing, 4 hold)
    [SerializeField] SpriteRenderer sr;
    [SerializeField] float swingFps = 11f;
    [SerializeField] GameObject logPickupPrefab;

    bool busy;

    public void Swing(ChoppableTree target)   // call on an axe input aimed at a tree
    {
        if (!busy) StartCoroutine(SwingRoutine(target));
    }

    IEnumerator SwingRoutine(ChoppableTree target)
    {
        busy = true;
        for (int i = 0; i < 4; i++)
        {
            sr.sprite = chop[i];
            if (i == 2 && target) target.Chop();     // land the bite on the impact frame
            yield return new WaitForSeconds(1f / swingFps);
        }
        sr.sprite = chop[0];                          // back to a neutral hold of the axe
        busy = false;
    }

    public void ShowCarry(bool carrying) => sr.sprite = chop[carrying ? 4 : 0];
}
```

Pair this with the **choppable spruce** (`tree_chop.png`, see `REDWOOD.md`): each `Swing()`
calls `ChoppableTree.Chop()`, and when that tree fells it spawns `log_pickup`.

---

## Log pickup (16×16, 2 frames)

A world item, not a player frame. Loop the 2 cells slowly (~4–5 fps) for the bob + cut-end
glint; on overlap, grant wood and swap the player to the **hold-wood** pose.

```csharp
public class LogPickup : MonoBehaviour
{
    [SerializeField] Sprite[] frames;   // 2 sliced cells
    [SerializeField] SpriteRenderer sr;
    [SerializeField] int wood = 1;
    float t; int f;

    void Update()
    {
        t += Time.deltaTime;
        if (t >= 0.22f) { t = 0; f ^= 1; sr.sprite = frames[f]; }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        other.GetComponent<Inventory>()?.AddWood(wood);
        other.GetComponent<PlayerChop>()?.ShowCarry(true);   // optional carry beat
        Destroy(gameObject);
    }
}
```

**Opt-in mechanic:** with no axe equipped, just never call `Swing()` — the player keeps their
normal idle/walk sheets and trees stay standing. The chop + carry sprites only ever show when
the player actually has the tool.

---

## First-person viewmodel (`fp_axe.png`, 128×128, 5 frames)

When the player switches to first person, the axe becomes a **screen-space overlay** — the
player's own arm rising from the bottom-right corner. Same red-sleeve / skin / steel palette as
the third-person sprites, so switching views reads as the same character. The sleeve red is the
player's **chosen shirt color** — recolor it exactly as in *"Shirt color = the player's chosen
customization color"* above, and apply the same tint here as on the third-person sheets.

```
0 Ready   1 Raise   2 Sweep   3 Bite(impact+chips)   4 Recoil
```

One swing plays **0→1→2→3→4** then rests on **Ready**; land the tree damage on **Bite** (cell 3),
which already throws chips. Drive it from a UI `Image` on a screen-space canvas (or a camera-
space quad), not a world `SpriteRenderer`.

### Import + placement

- Import as **Sprite (2D and UI)**, **Point** filter, Multiple; slice **Grid By Cell Count**
  Column **5** Row **1**.
- Anchor the `Image` to the **bottom-right**; size it so the fist sits just off the corner and
  the blade swings into the screen centre where the target trunk is. Keep the RectTransform an
  integer scale of 128 so pixels stay crisp.
- Optional juice: a small downward **view-bob** on the whole image during the swing, and a
  1–2px camera kick on Bite. The art itself already leans the arm and flares chips.

```csharp
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class FirstPersonAxe : MonoBehaviour
{
    [SerializeField] Sprite[] frames;   // 5 sliced cells (fp_axe)
    [SerializeField] Image view;        // bottom-right anchored UI Image
    [SerializeField] float fps = 12f;
    bool busy;

    void OnEnable() { view.sprite = frames[0]; }   // Ready when first-person is active

    public void Swing(ChoppableTree target)
    {
        if (!busy) StartCoroutine(SwingRoutine(target));
    }

    IEnumerator SwingRoutine(ChoppableTree target)
    {
        busy = true;
        for (int i = 0; i <= 4; i++)
        {
            view.sprite = frames[i];
            if (i == 3 && target) target.Chop();   // damage on the Bite frame
            yield return new WaitForSeconds(1f / fps);
        }
        view.sprite = frames[0];                    // rest on Ready
        busy = false;
    }
}
```

Toggle `FirstPersonAxe.view` on / `PlayerChop` third-person sprite off (and vice-versa) when the
player switches perspective — both read `ChoppableTree.Chop()` the same way, so the woodcutting
logic is identical in either view.

---

## Re-exporting

```js
eval(await readFile('choppergen.js'));
await window.ChopperGen.buildAll({ createCanvas, saveFile });   // 3rd-person swing + log

eval(await readFile('fpaxegen.js'));
await window.FpAxeGen.buildAll({ createCanvas, saveFile });     // first-person viewmodel
```

`choppergen.js` re-poses only the arms and stamps the axe over the exact base-player pixels
(`ChopperGen.MALE` / `.FEMALE` grids). `fpaxegen.js` renders one rigid axe (`buildAxe`) and
rotates it around the grip per frame (`POSES`), with the forearm as an overlapping-disc capsule
(`arm`). Tunables there: the `POSES` angles, `buildAxe` head profile, and `chips`.
