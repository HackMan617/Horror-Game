# CHOPPING.md — Player axe-swing, log pickup & carry (Unity 2D)

Woodcutting art built directly on the existing player rig. The head, palette and build are the
untouched base-sprite pixels (`character_sprite_sheet*.png`), so these drop in beside the idle
and walk sheets with no seams.

## Files

| File | Size | Cell | Cells |
|---|---|---|---|
| `sprites/chop_male.png`   | 160×32 | 32×32 | 5 — swing 0-3, then **hold-wood** (front) |
| `sprites/chop_female.png` | 160×32 | 32×32 | 5 — swing 0-3, then **hold-wood** (front) |
| `sprites/chop_male_back.png`   | 160×32 | 32×32 | 5 — swing 0-3, then hold-wood (**back view**) |
| `sprites/chop_female_back.png` | 160×32 | 32×32 | 5 — swing 0-3, then hold-wood (**back view**) |
| `sprites/carry_walk_male.png`   | 128×32 | 32×32 | 4 — **walk cycle holding the log** (front) |
| `sprites/carry_walk_female.png` | 128×32 | 32×32 | 4 — walk cycle holding the log (front) |
| `sprites/carry_walk_male_back.png`   | 128×32 | 32×32 | 4 — walk cycle holding the log (**back**) |
| `sprites/carry_walk_female_back.png` | 128×32 | 32×32 | 4 — walk cycle holding the log (**back**) |
| `sprites/log_pickup.png`  | 32×16  | 16×16 | 2 — idle bob + glint |
| `sprites/fp_axe.png`      | 640×128 | 128×128 | 5 — first-person viewmodel swing |
| `sprites/*-6x.png` · `fp_axe-3x.png` | — | — | preview blow-ups — **do not ship** |

**Front + back:** the `_back` sheets share the exact cell map and timing as the front sheets —
swap to them when the player faces **up/away** from the camera (walking north), same as your base
front/back player sheets. The axe is held out to the side so the blade stays visible from behind;
the hold-wood cell shows the carried log peeking past the body. `PlayerChop` / `FirstPersonAxe`
code below is identical — just point `chop[]` at whichever facing sheet the movement state selects.

**Point filter · integer scale.** Cell index → `(col*W, 0, W, H)`.

### Cell map (characters)

```
0 raise   1 swing   2 bite(impact+chips)   3 recoil   4 hold-wood
└────────── 4-frame swing loop ──────────┘   └ carry pose (static) ┘
```

- **Swing** is cells **0→1→2→3** (play once per axe stroke, ~10–12 fps). Land the tree-damage /
  chip VFX on cell **2** (the bite) — that frame already throws a couple of chips.
- **Hold-wood** (cell 4) is a **static carry pose** — use it when the player stands still with a
  log. For **movement**, use the dedicated carry-walk sheets below.

### Cell map (carry-walk — walking with the log)

`carry_walk_*` sheets are a **4-frame walk cycle**, front and back, both genders:

```
0 step-left   1 pass   2 step-right   3 pass
```

- Loop **0→1→2→3** at ~**8–10 fps** while the player moves; the hips bob up on the pass
  frames (1, 3) and the feet alternate lead so it reads as a walk.
- The log is hugged to the chest. From the **front** both forearms cup it across the belly; from
  the **back** it's held in front of the body, so only the cut ends peek past the sides (correct
  occlusion — no floating hands behind the character).
- Pick front vs back from the movement facing, exactly like the base walk sheets. Swap to the
  static **hold-wood** cell when the player stops.

---

## Import settings

| Setting | Value |
|---|---|
| Texture Type | Sprite (2D and UI) |
| Sprite Mode | Multiple |
| Pixels Per Unit | 32 (match your other player sheets) |
| Filter Mode | Point (no filter) |
| Compression | None · Mip Maps off |

Slice **Grid By Cell Count**: chop characters Column **5** Row **1**; carry-walk Column **4**
Row **1**; log Column **2** Row **1**.
Pivot **Bottom-Center** on the characters (feet on the ground line), **Center** on the log.

---

## Character customization = recolor shirt + skin + hair

These sprites reuse the **exact base-player pixels** (`character_sprite_sheet*.png`), so whatever
customization recolor you already run on the idle/walk sheets applies here **unchanged** — the
player's chosen **shirt, skin and hair** must drive every one of these frames too. Nothing here
is hard-coded to "red shirt / this skin / this hair"; the shipped PNGs just happen to show the
default palette. Recolor all customization slots, on **every** sheet in this set
(`chop_*`, `chop_*_back`, `carry_walk_*`, `carry_walk_*_back`, and `fp_axe`), so a facing change
or a first/third-person switch never shows a mismatched character.

**Shirt / sleeve** (the red — arms, shoulders, torso; the only red pixels in the sheets):

| Role | Sprite pixels (source) | Recolor to |
|---|---|---|
| Shirt / sleeve base | `#d83030` | player shirt **base** color |
| Shirt / sleeve shadow | `#983018` | player shirt **shadow** (≈ base × 0.7) |
| Sleeve deep shadow (FP only) | `#6a1e14` | player shirt **deep shadow** (≈ base × 0.5) |

**Skin** (face + the hands gripping the axe/log):

| Role | Sprite pixels (source) | Recolor to |
|---|---|---|
| Skin base | `#f0b890` | player skin **base** |
| Skin shadow | `#c07a4c` | player skin **shadow** |

**Hair** (the fringe/cap on the front sheets and the full head/long-hair on the back sheets):

| Role | Sprite pixels (source) | Recolor to |
|---|---|---|
| Hair base | `#9c5a26` | player hair **base** |
| Hair shadow | `#5e3410` | player hair **shadow** |

- Each slot uses its **own** ramp, so recoloring the shirt never touches skin, hair, wood or
  steel (and vice-versa). Recolor the **base + shadow together** (don't flat-tint) so shading is
  preserved.
- The blue trousers (`#3a5bd0` / `#26398c`) and dark boots (`#4a4a4a`) match the base sheets
  too — fold them into the same recolor if your customization covers legwear.
- Implementation: the same **palette-swap / LUT** you already use for the base character is the
  cleanest path (one lookup table covers shirt + skin + hair + legs at once). A per-slot shader
  tint also works; a flat `Color` multiply does **not** (it would tint every slot the same).
- Apply the identical table to the third-person **and** first-person art so switching views
  reads as the same customized character.

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
