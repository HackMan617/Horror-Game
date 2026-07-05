# FIREPLACE.md — Cabin hearth (Unity 2D)

A large stone fireplace for the wooden cabin interior. The player **reignites** it with spruce
they've gathered: it catches, builds to a steady blaze, then — untended — burns down, dies to
ash, and goes cold. Stone surround + spruce mantel + spruce logs, warm fire with a **baked glow**.

Built procedurally in `campfiregen.js`. Point-filter / integer-scale pixel art in the cabin's
`dusk` interior palette (stone `#8a8078`, mortar `#4e4842`, spruce `#7c5a3a`).

---

## Files

| File | Size | Cell | Grid |
|---|---|---|---|
| `sprites/fireplace.png` | 448×320 | 64×64 | **7 cols × 5 rows** — **front view** |
| `sprites/fireplace_side.png` | 448×320 | 64×64 | **7 cols × 5 rows** — **side view** (mouth faces right) |
| `sprites/fireplace-6x.png` · `fireplace_side-6x.png` | — | — | preview blow-ups — **do not ship** |

**Front + side:** two angles so the hearth reads correctly as the player moves around the room.
Both sheets share the **exact same layout, states, frame counts and timing** — everything below
applies to either; just point the SpriteRenderer at the sheet that matches the camera facing
(front = viewing the opening head-on; side = viewing it along the wall, opening toward the room).
The side view is drawn mirror-able: flip X in-engine for a left-facing hearth.

### Layout — rows are states, columns are frames

```
row 0  COLD      bare spruce logs, no fire (needs wood)   — static (use col 0)
row 1  LIGHTING  catches: embers → flames build to full   — play once, 0→6
row 2  BURN      steady blaze                             — seamless LOOP 0↔6
row 3  COOLING   flames shrink to a low ember bed         — play once, 0→6
row 4  FADING    embers die to ash + smoke, goes cold     — play once, 0→6
```

- **7 frames** per state. Sprite index = `row * 7 + col`.
- **Continuity:** lighting frame 6 ≈ burn; cooling frame 0 ≈ burn; cooling 6 ≈ fading 0; fading 6
  ≈ cold. So the chain **cold → lighting → burn → cooling → fading → cold** flows with no pop.
- The warm **glow / light pool** is baked into every lit frame (back wall + a pool on the hearth
  in front), and it pulses with the flames.

---

## Import settings

| Setting | Value |
|---|---|
| Texture Type | Sprite (2D and UI) |
| Sprite Mode | **Multiple** |
| Pixels Per Unit | **16** (the fireplace reads ~4×4 tiles) |
| Filter Mode | **Point (no filter)** |
| Compression | None · Mip Maps off · Wrap Clamp |

Slice **Grid By Cell Count** → Column **7**, Row **5** → 35 sprites `fireplace_0 … _34`
(left→right, top→bottom). Slice `fireplace_side.png` the same way. Pivot **Bottom-Center** so it
plants against the wall on the floor line (for the side sheet the pivot keeps the hearth base on
the floor as you flip X).

Place it flush to a wall; give it a small collider so the player can't walk through the hearth,
and a slightly larger **trigger** in front for the "reignite" interaction prompt.

---

## Reignite / burn-down state machine

Drive the sprite from the player's action + a fuel timer. `Reignite()` (called when the player
feeds wood) plays **lighting** once then loops **burn**; when fuel runs out (or on `LetDie()`),
it plays **cooling → fading** once each and rests on **cold**.

```csharp
using System.Collections;
using UnityEngine;

public class Fireplace : MonoBehaviour
{
    public enum State { Cold = 0, Lighting = 1, Burn = 2, Cooling = 3, Fading = 4 }

    [SerializeField] Sprite[] sheet;          // 35 sliced sprites (row*7 + col)
    [SerializeField] SpriteRenderer sr;
    [SerializeField] Light2D fireLight;       // optional URP 2D light for extra spill
    [SerializeField] float fps = 9f;          // ~7 frames/state
    [SerializeField] float burnSeconds = 30f; // how long a load of wood lasts

    State state = State.Cold;
    int frame;
    float fuel;

    void Awake() { if (!sr) sr = GetComponent<SpriteRenderer>(); Show(State.Cold, 0); }

    int Index(State s, int f) => (int)s * 7 + f;
    void Show(State s, int f) { sr.sprite = sheet[Index(s, f)]; if (fireLight) fireLight.intensity = Intensity(s, f); }

    /// <summary>Player fed the fire spruce → light it (or refuel).</summary>
    public void Reignite(int logs = 1)
    {
        fuel += logs * burnSeconds;
        if (state == State.Cold || state == State.Fading || state == State.Cooling)
            StartCoroutine(Run());
    }

    /// <summary>Force it out (or let the coroutine do it when fuel hits 0).</summary>
    public void LetDie() { fuel = 0f; }

    IEnumerator Run()
    {
        StopAllCoroutines();                     // (guard against overlap in real code)
        yield return PlayOnce(State.Lighting);
        // steady burn: loop until fuel runs out
        frame = 0;
        while (fuel > 0f)
        {
            Show(State.Burn, frame);
            frame = (frame + 1) % 7;
            fuel -= 1f / fps;
            yield return new WaitForSeconds(1f / fps);
        }
        yield return PlayOnce(State.Cooling);
        yield return PlayOnce(State.Fading);
        state = State.Cold; Show(State.Cold, 0);
    }

    IEnumerator PlayOnce(State s)
    {
        state = s;
        for (int f = 0; f < 7; f++) { Show(s, f); yield return new WaitForSeconds(1f / fps); }
    }

    // approx warm-light intensity per frame (mirrors the baked glow) for an optional Light2D
    float Intensity(State s, int f)
    {
        float u = f / 6f;
        switch (s)
        {
            case State.Lighting: return 0.12f + 0.9f * Mathf.Pow(u, 1.3f);
            case State.Burn:     return 0.86f + 0.14f * Mathf.Sin(f * 1.7f);
            case State.Cooling:  return 0.12f + 0.85f * (1f - u);
            case State.Fading:   return Mathf.Max(0f, 0.34f * (1f - u));
            default:             return 0f;   // Cold
        }
    }
}
```

### Notes

- **Baked glow is enough on its own** — the light pool + back-wall wash are in the frames. The
  `Light2D` above is optional polish for spilling warm light onto the player and nearby props;
  drive its intensity from `Intensity()` so it pulses in sync.
- **Cold is the "needs wood" state** — show the interaction prompt only while `state == Cold`.
  Persist the lit/cold state (and remaining `fuel`) in your save.
- **Ties into the loop:** the spruce the player fells (`REDWOOD.md`) → the log pickups
  (`CHOPPING.md`) → `Reignite(logs)` here. Same spruce palette throughout.
- **Ambient audio:** crossfade a soft crackle loop in on Lighting, out on Fading.

---

## Re-exporting

```js
eval(await readFile('campfiregen.js'));
await window.CampfireGen.buildAll({ createCanvas, saveFile });
```

Tunables: `drawShell` / `drawShellSide` (stone surround, spruce mantel, firebox recess, hearth),
`drawLogs` / `drawLogsSide` (spruce logs → char → ash; the side view shows log **ends**),
`drawFire` / `drawFireSide` (the layered flame ramps `R_OUT`/`R_MID`/`R_CORE`, heights, flicker,
and the side view's rightward lean), the glow / smoke / spark passes, and `frameParams` (the
shared per-state intensity / char / smoke schedule that drives both views).
