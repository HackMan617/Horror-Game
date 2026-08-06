# Nightmare Forest — Unity 2D implementation & animation

Sprites exported from `treegen.js`. The importable sheets are **`tree_spruce.png`** (summer) and
**`tree_spruce_winter.png`** (snow-loaded). Same geometry, so one set of slice settings covers both.

```
Cell        96 × 196 px
Grid        6 cols × 4 rows  → 24 frames
Sheet       576 × 784 px
Frames      0–11  = IDLE  (rows 0–1) — uneasy breathing shimmer
            12–23 = SWAY  (rows 2–3) — DREAD gust: whip + twist + shudder + lurch
Loop        seamless (frame 11→0 and 23→12)
Play rate   8–12 fps  (10 is the sweet spot)
```

Pine / snag / ridge share the same 6×4 / idle-sway layout if you export them the same way; the
depth code below treats them all identically — only the `Sprite[]` you feed in differs.

---

## 1 · Import settings (per sheet)

Select `tree_spruce.png` (and the winter sheet) in the Project window → Inspector:

| Setting | Value |
|---|---|
| Texture Type | **Sprite (2D and UI)** |
| Sprite Mode | **Multiple** |
| Pixels Per Unit | **196** (1 tree cell = 1 world unit tall — tune to taste) |
| Filter Mode | **Point (no filter)** |
| Compression | **None** |
| Generate Mip Maps | **off** |
| Wrap Mode | Clamp |

Open **Sprite Editor → Slice → Type: Grid By Cell Count → Column & Row = 6 × 4 → Pivot: Bottom
Center** (bottom pivot keeps trees planted when you scale them for depth). Apply. You now have
`tree_spruce_0 … tree_spruce_23` in row-major order — exactly the frame indices above.

Drag the 24 sliced sprites into `idleFrames`/`swayFrames` on the component below (0–11 and 12–23),
or assign the whole 24-length array and let the animator split them.

---

## 2 · TreeAnimator.cs — plays idle / dread-sway on one tree

Flip-book on a `SpriteRenderer` via a coroutine. Each tree gets its own random phase offset so a
stand never pulses in lockstep. Swap `summer`/`winter` frame sets at runtime for the season.

```csharp
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class TreeAnimator : MonoBehaviour
{
    public enum Mode { Idle, Sway }

    [Header("24 sliced frames: 0–11 idle, 12–23 sway")]
    public Sprite[] summerFrames;   // tree_spruce_0..23
    public Sprite[] winterFrames;   // tree_spruce_winter_0..23  (optional)

    [Header("Playback")]
    public Mode mode = Mode.Sway;
    public float fps = 10f;
    public bool  winter = false;
    [Range(0, 11)] public int phaseOffset = 0;   // set randomly per instance

    SpriteRenderer _sr;
    Coroutine _loop;

    void Awake() { _sr = GetComponent<SpriteRenderer>(); }
    void OnEnable()  { _loop = StartCoroutine(Play()); }
    void OnDisable() { if (_loop != null) StopCoroutine(_loop); }

    Sprite[] Sheet => (winter && winterFrames != null && winterFrames.Length == 24)
                      ? winterFrames : summerFrames;

    IEnumerator Play()
    {
        int f = 0;
        var wait = new WaitForSeconds(1f / Mathf.Max(1f, fps));
        while (true)
        {
            int baseIdx = (mode == Mode.Sway) ? 12 : 0;         // idle 0.., sway 12..
            int idx = baseIdx + ((f + phaseOffset) % 12);
            var sheet = Sheet;
            if (sheet != null && idx < sheet.Length && sheet[idx]) _sr.sprite = sheet[idx];
            f++;
            yield return wait;
        }
    }

    // call when the wind picks up / the dread beat hits
    public void SetMode(Mode m) => mode = m;
    public void SetWinter(bool w) => winter = w;
}
```

---

## 3 · ForestDepth.cs — atmospheric perspective (haze out far, grow on approach)

In the 2.5D env, distance is a single float per tree (`depth01`: 0 = far horizon, 1 = right at the
camera). It drives three things at once — **scale**, **haze tint**, and **sorting order** — so trees
dissipate into the distance and read larger/sharper as the player approaches.

```csharp
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class ForestDepth : MonoBehaviour
{
    [Range(0f, 1f)] public float depth01 = 0.5f;    // 0 far → 1 near

    [Header("Scale by depth")]
    public float farScale  = 0.35f;
    public float nearScale = 1.15f;

    [Header("Atmospheric haze (far trees fade toward the sky)")]
    public Color hazeColor = new Color(0.79f, 0.54f, 0.41f, 1f); // warm sunset haze; winter ≈ (.62,.69,.77)
    [Range(0f, 1f)] public float maxHaze = 0.72f;   // how far a horizon tree tints toward the sky

    [Header("Foreground giants (near = darkened backlit silhouette)")]
    public bool giant = false;
    public Color giantTint = new Color(0.10f, 0.06f, 0.13f, 1f);
    [Range(0f, 1f)] public float giantStrength = 0.48f;

    [Header("Sorting")]
    public int baseSortingOrder = 0;

    SpriteRenderer _sr;
    void Awake() { _sr = GetComponent<SpriteRenderer>(); }
    void LateUpdate() => Apply();

    public void Apply()
    {
        if (!_sr) _sr = GetComponent<SpriteRenderer>();
        float d = Mathf.Clamp01(depth01);

        // scale — bottom-pivoted sprites stay planted
        float s = Mathf.Lerp(farScale, nearScale, d);
        transform.localScale = new Vector3(s, s, 1f);

        // tint: far → haze toward sky; giants → darkened silhouette
        Color c;
        if (giant) c = Color.Lerp(Color.white, giantTint, giantStrength);
        else       c = Color.Lerp(hazeColor, Color.white, Mathf.Lerp(1f - maxHaze, 1f, d));
        _sr.color = c;

        // nearer trees draw in front
        _sr.sortingOrder = baseSortingOrder + Mathf.RoundToInt(d * 1000f);
    }
}
```

Wire it up: a driving-POV tree also needs to **move** through depth. Advance `depth01` each frame
from the car's speed, and recycle a tree to the horizon once it passes the camera — pool them, never
Instantiate/Destroy per frame:

```csharp
// on the rig or a ForestField manager, per pooled tree:
tree.depth01 += speed01 * Time.deltaTime;          // speed01 from the car
if (tree.depth01 >= 1.05f) RecycleToHorizon(tree);  // reset depth01≈0, new x, new phase
tree.Apply();
```

---

## 4 · ForestField.cs — the packed wall + framing giants

Spawns a pooled, depth-banded stand: a hazy horizon line of ridge trees, a mid band, a near band of
full trees, and two darkened giants crowding the edges. Place it once; it fills the view.

```csharp
using System.Collections.Generic;
using UnityEngine;

public class ForestField : MonoBehaviour
{
    public TreeAnimator treePrefab;          // prefab with TreeAnimator + ForestDepth + SpriteRenderer
    public Sprite[] spruceSummer, spruceWinter, pineSummer, pineWinter;
    public int midCount = 10, nearCount = 6, ridgeCount = 14;
    public float fieldWidth = 24f;           // world units across
    public bool winter = false;

    readonly List<ForestDepth> _depths = new();

    void Start()
    {
        // horizon ridge silhouettes (heavy haze, tiny)
        for (int i = 0; i < ridgeCount; i++) Spawn(depth: Random.Range(0.02f, 0.12f), giant: false, sort: -200);
        // mid stand
        for (int i = 0; i < midCount; i++)  Spawn(depth: Random.Range(0.30f, 0.55f), giant: false, sort: 0);
        // near, full-detail trees
        for (int i = 0; i < nearCount; i++) Spawn(depth: Random.Range(0.70f, 0.92f), giant: false, sort: 200);
        // two framing giants at the edges
        Spawn(depth: 1.0f, giant: true, sort: 900, xNorm: 0.04f);
        Spawn(depth: 1.0f, giant: true, sort: 900, xNorm: 0.96f);
    }

    void Spawn(float depth, bool giant, int sort, float xNorm = -1f)
    {
        var t = Instantiate(treePrefab, transform);
        t.summerFrames = spruceSummer; t.winterFrames = spruceWinter;   // swap in pine for variety
        t.winter = winter;
        t.mode = TreeAnimator.Mode.Sway;
        t.phaseOffset = Random.Range(0, 12);
        t.fps = 10f;

        float nx = (xNorm >= 0f) ? xNorm : Random.value;
        float x = (nx - 0.5f) * fieldWidth;
        t.transform.localPosition = new Vector3(x, 0f, 0f);

        var d = t.GetComponent<ForestDepth>();
        d.depth01 = depth;
        d.giant = giant;
        d.baseSortingOrder = sort;
        d.hazeColor = winter ? new Color(0.62f, 0.69f, 0.77f) : new Color(0.79f, 0.54f, 0.41f);
        d.Apply();
        _depths.Add(d);
    }

    public void SetWinter(bool w)
    {
        winter = w;
        foreach (var d in _depths)
        {
            d.hazeColor = w ? new Color(0.62f, 0.69f, 0.77f) : new Color(0.79f, 0.54f, 0.41f);
            d.GetComponent<TreeAnimator>().SetWinter(w);
            d.Apply();
        }
    }
}
```

For a **static** wall (house scene) leave depths fixed. For the **driving POV**, add the advance +
recycle snippet from §3 so the stand scrolls and dissipates toward the horizon.

---

## 5 · FallingDebris.cs — leaves & sticks shedding from the canopy

Lightweight code-driven emitter (no ParticleSystem asset needed). Leaves flutter side-to-side and
"flip" as they fall; sticks tumble and drop faster. Point `emitBounds` at the canopy area.

```csharp
using System.Collections.Generic;
using UnityEngine;

public class FallingDebris : MonoBehaviour
{
    public SpriteRenderer leafPrefab;        // a 2–3px square sprite
    public SpriteRenderer stickPrefab;       // a short 1px-wide bar sprite
    public Rect emitBounds = new Rect(-10f, 2f, 20f, 4f);   // x,y,w,h in world units (canopy band)
    public float floorY = -4f;
    public float leafRate = 6f, stickRate = 1.5f;   // spawns per second
    public bool winter = false;

    static readonly Color[] Summer = {
        new(0.36f,0.54f,0.23f), new(0.47f,0.62f,0.29f), new(0.79f,0.47f,0.18f),
        new(0.66f,0.35f,0.16f), new(0.85f,0.60f,0.24f) };
    static readonly Color[] Winter = {
        new(0.54f,0.42f,0.23f), new(0.72f,0.63f,0.42f), new(0.36f,0.42f,0.29f), new(0.85f,0.88f,0.92f) };

    class P { public Transform t; public SpriteRenderer sr; public Vector2 vel; public float spin, spinV, seed, age; public bool stick; }
    readonly List<P> _live = new();
    readonly Queue<P> _pool = new();
    float _lAcc, _sAcc;

    void Update()
    {
        float dt = Time.deltaTime;
        _lAcc += leafRate * dt; _sAcc += stickRate * dt;
        while (_lAcc >= 1f) { _lAcc -= 1f; Emit(false); }
        while (_sAcc >= 1f) { _sAcc -= 1f; Emit(true);  }

        for (int i = _live.Count - 1; i >= 0; i--)
        {
            var p = _live[i];
            p.age += dt;
            p.vel.y -= (p.stick ? 3.2f : 1.9f) * dt;                    // gravity
            float flutter = p.stick ? 0f : Mathf.Sin(p.age * 5.5f + p.seed) * 0.5f;
            p.t.localPosition += new Vector3(p.vel.x + flutter, p.vel.y, 0f) * dt;
            p.spin += p.spinV * dt;
            p.t.localRotation = Quaternion.Euler(0, 0, p.spin * Mathf.Rad2Deg);
            if (!p.stick) p.t.localScale = new Vector3(Mathf.Abs(Mathf.Cos(p.spin)) * 0.9f + 0.1f, 1f, 1f); // "flip"

            float a = Mathf.Clamp01((p.t.localPosition.y - floorY) / 1.5f + 0.25f);
            var c = p.sr.color; c.a = a; p.sr.color = c;

            if (p.t.localPosition.y <= floorY || p.age > 12f) Recycle(p, i);
        }
    }

    void Emit(bool stick)
    {
        P p = _pool.Count > 0 ? _pool.Dequeue() : NewP(stick);
        if (p.stick != stick) { Destroy(p.t.gameObject); p = NewP(stick); }
        p.t.gameObject.SetActive(true);
        p.age = 0f;
        p.t.localPosition = new Vector3(
            emitBounds.x + Random.value * emitBounds.width,
            emitBounds.y + Random.value * emitBounds.height, 0f);
        p.vel = new Vector2((Random.value - 0.62f) * (stick ? 0.6f : 0.9f), -(0.3f + Random.value * 0.5f));
        p.spin = Random.value * 6.28f;
        p.spinV = (Random.value - 0.5f) * (stick ? 3f : 5f);
        p.seed = Random.value * 6.28f;
        var pal = winter ? Winter : Summer;
        p.sr.color = stick ? new Color(0.42f, 0.29f, 0.17f) : pal[Random.Range(0, pal.Length)];
        _live.Add(p);
    }

    P NewP(bool stick)
    {
        var sr = Instantiate(stick ? stickPrefab : leafPrefab, transform);
        return new P { t = sr.transform, sr = sr, stick = stick };
    }
    void Recycle(P p, int i) { p.t.gameObject.SetActive(false); _live.RemoveAt(i); _pool.Enqueue(p); }

    public void SetWinter(bool w) => winter = w;
}
```

---

## 6 · Scene wiring (quick recipe)

1. Make a **Tree** prefab: empty GameObject → `SpriteRenderer` (Sprite = `tree_spruce_0`, Draw Mode
   Simple) + `TreeAnimator` + `ForestDepth`. Pivot bottom-center.
2. Drop a **ForestField** in the scene, assign the prefab + the sliced `tree_spruce` / `tree_spruce_winter`
   sprite arrays (and pine variants). Set `fieldWidth` to your camera's world width.
3. Add a **FallingDebris** object over the canopy; assign tiny leaf/stick sprites, set `emitBounds`.
4. Driving POV only: feed the car's normalized speed into the §3 advance/recycle loop so the stand
   scrolls toward the horizon and hazes out; the house scene leaves depths static.
5. Season toggle: call `ForestField.SetWinter(true)` + `FallingDebris.SetWinter(true)` together — the
   winter frame set brings the snow load, the haze/particle palettes shift cold.

Dread is already baked into the sway frames (whip + twist + shudder + lurch), so you get it for free
just by running `TreeAnimator.Mode.Sway` at ~10 fps.
