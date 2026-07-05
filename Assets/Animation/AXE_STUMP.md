# AXE_STUMP.md — Axe-in-stump world pickup (Unity 2D)

A world prop the player walks up to and takes: an axe buried in a tree stump. While it's
available it sits with a slow blade glint; on pickup the axe vanishes and the stump is left with
a fresh cut slot — a clear read that this one's already been claimed.

## File

| File | Size | Cell | Grid |
|---|---|---|---|
| `sprites/axe_stump.png` | 128×96 | 32×32 | 4 cols × 3 rows |
| `sprites/axe_stump-6x.png` | 768×576 | — | preview blow-up — **do not ship** |

### Layout

```
             col 0        col 1        col 2        col 3
row 0 FRONT  idle-0       idle-1       idle-2       EMPTY (picked up)
row 1 SIDE   idle-0       idle-1       idle-2       EMPTY
row 2 BACK   idle-0       idle-1       idle-2       EMPTY
```

- **cols 0–2** = a 3-frame idle loop (a glint travels across the blade + a 1px handle sway).
- **col 3** = the **empty stump** (axe removed) — swap to it the moment the player picks up.
- **rows** = the facing the player sees the stump from: **front / side / back**. The stump is the
  same block; the axe orientation differs (front = head cheek + handle up-right, side = edge-on
  blade + handle straight up, back = poll + handle up-left).

Sprite index → `(col*32, row*32, 32, 32)`.

---

## Import settings

| Setting | Value |
|---|---|
| Texture Type | Sprite (2D and UI) |
| Sprite Mode | Multiple |
| Pixels Per Unit | 32 (match the player/log) |
| Filter Mode | Point (no filter) |
| Compression | None · Mip Maps off |

Slice **Grid By Cell Count**: Column **4**, Row **3** → 12 sprites `axe_stump_0 … _11`
(left→right, top→bottom, so `row*4 + col`). Pivot **Bottom-Center** so the stump plants on the
ground line.

---

## Behavior

Pick the **row** from which way the prop faces the camera (or just always use FRONT for a
top-down game), loop cols 0–2 while available, and switch to col 3 on pickup.

```csharp
using UnityEngine;

public class AxePickup : MonoBehaviour
{
    public enum View { Front = 0, Side = 1, Back = 2 }

    [SerializeField] Sprite[] sheet;        // 12 sliced sprites (row*4 + col)
    [SerializeField] SpriteRenderer sr;
    [SerializeField] View view = View.Front;
    [SerializeField] float idleFps = 4f;    // slow, subtle glint
    [SerializeField] string playerTag = "Player";

    bool taken;
    int frame;
    float t;

    int Cell(int col) => (int)view * 4 + col;

    void Awake() { if (!sr) sr = GetComponent<SpriteRenderer>(); sr.sprite = sheet[Cell(0)]; }

    void Update()
    {
        if (taken) return;
        t += Time.deltaTime;
        if (t >= 1f / idleFps) { t = 0; frame = (frame + 1) % 3; sr.sprite = sheet[Cell(frame)]; }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (taken || !other.CompareTag(playerTag)) return;
        taken = true;
        sr.sprite = sheet[Cell(3)];                       // empty stump — stays as decor
        other.GetComponent<Inventory>()?.AddItem("axe");  // → inventory
        other.GetComponent<PlayerChop>()?.EquipAxe();     // now Swing() is available
        // optional: play a pickup chime / little sparkle here
    }
}
```

- Give the prop a **trigger** collider a bit larger than the stump so "walk up to it" registers.
- The **empty stump (col 3)** is left in the world as decor — the player can see where the axe
  was. Persist `taken` in your save so it stays claimed across reloads.
- Ties into the wider mechanic: picking this up is what lets `PlayerChop.Swing()` (see
  `CHOPPING.md`) fire — before it, the player simply has no axe.

---

## Re-exporting

```js
eval(await readFile('axestumpgen.js'));
await window.AxeStumpGen.buildAll({ createCanvas, saveFile });
```

Tunables: `drawStump` (bark shading, cut-top rings, root flare), the per-view `drawAxe`
(handle angle, head shape, glint path), and `drawEmptySlot` (the picked-up read).
