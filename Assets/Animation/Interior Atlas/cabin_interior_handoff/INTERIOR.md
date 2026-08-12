# INTERIOR.md — Cabin interior (Cold Dusk) · orientation & positioning (Unity 2.5D)

The enclosed two-floor cabin the player stands inside: **ground-floor living room** and **upstairs
primary bedroom**, lit by the **Cold Dusk** grade (blue moonlight, warm hearth/lamp accents). The
room is fully closed — chinked log walls on three sides, a flat plaster ceiling, a plank floor, and a
window to the night — so nothing shows through to the sky. Preview + tuning live in
`Cabin Interior.dc.html`.

Everything below matches that preview 1:1, so Unity reproduces the same framing.

---

## 1 · Sprite manifest

### Structural tiles (baked here, Cold Dusk + Nightmare)

| Sprite | Size | Tiling | Notes |
|---|---|---|---|
| `interior_colddusk_wall.png` | 32×32 | **tile X & Y** | chinked horizontal logs, grain + knots, cylindrical shading |
| `interior_colddusk_ceiling.png` | 32×32 | **tile X & Y** | cool plaster, faint speckle |
| `interior_colddusk_floor.png` | 32×32 | **tile X & Y** | offset plank courses, grain |
| `interior_colddusk_window.png` | 48×40 | single | night sky + moon + ridge + muntins + curtains |
| `interior_colddusk_nightmare_*` | same | same | warped twin — swap on the dread flag |

Import (all): **Point (no filter) · Compression None · PPU 32 · Wrap = Repeat** for the three tiles,
**Clamp** for the window. Pivot Center.

### Furniture / props

Reuse the existing **`interior_furniture_*`** kit sprites (sofa, bookshelf, coffee table, fireplace,
bed, nightstand+lamp, desk, rug, framed photo, stairs). They are placed as **billboards** on the
floor or mounted on a wall; the Cold Dusk / Nightmare mood comes from the room lighting + a material
tint (§4), not from re-drawn art. Pivot **Bottom-Center** for floor pieces, **Center** for
wall-mounted pieces. PPU 32.

---

## 2 · Room geometry (world units)

Build a real box and let a perspective camera do the foreshortening — do **not** fake the perspective
in 2D. One unit = 1 m.

```
roomW = 8.0   (x: -4 .. +4, left→right)
roomH = 4.5   (y:  0 .. 4.5, floor→ceiling)
roomD = 9.0   (z:  0 .. 9,  camera→back wall)

Camera: position (0, 1.7, -0.4), looks +Z, FOV 55, near 0.05.
```

Five quads (each a textured `MeshRenderer`, tiles from §1):

| Quad | Plane | Tiling (x,y) |
|---|---|---|
| Floor | y=0, faces up | (roomW/1, roomD/1) |
| Ceiling | y=roomH, faces down | (roomW/1, roomD/1) |
| Back wall | z=roomD, faces −Z | (roomW/1, roomH/0.5)  ← logs every 0.5 m |
| Left wall | x=−roomW/2, faces +X | (roomD/1, roomH/0.5) |
| Right wall | x=+roomW/2, faces −X | (roomD/1, roomH/0.5) |

Log courses read best at **0.5 m** tall (so `tileY = roomH / 0.5 = 9`). Floor/ceiling tile at 1 m.

---

## 3 · Placement — `fx` (0..1 left→right) × `depth` (0=back wall, 1=at camera)

Convert to world:

```
worldX = (fx - 0.5) * roomW
worldZ = roomD * (1 - depth)      // depth 1 → z≈0 (near); depth 0 → z=roomD (far)
worldY = 0                        // floor pieces sit on the slab; wall pieces use the wall UV below
```

### Ground floor — LIVING ROOM

| Piece | fx | depth | mount | orient |
|---|---|---|---|---|
| Fireplace | 0.50 | — | **back wall**, UV (0.5, 0.0→up) | flat on wall, faces −Z |
| Sofa | 0.42 | 0.50 | floor | billboard, faces camera |
| Coffee table | 0.55 | 0.66 | floor | billboard |
| Bookshelf | 0.12 | 0.34 | floor (against left wall) | billboard |
| Rug | 0.50 | 0.55 | floor, **flat** | lay on floor (rotate X 90°) |
| Stairs (up) | 0.86 | 0.40 | floor (against right wall) | billboard, rail toward camera |
| Window | — | — | **left wall**, UV (0.30, 0.45) | flat on wall, faces +X |

### Upstairs — PRIMARY BEDROOM

| Piece | fx | depth | mount | orient |
|---|---|---|---|---|
| Bed | 0.50 | 0.46 | floor | billboard, headboard to back wall |
| Nightstand + lamp | 0.72 | 0.47 | floor | billboard |
| Desk | 0.20 | 0.52 | floor | billboard |
| Rug | 0.50 | 0.60 | floor, flat | lay on floor |
| Window | — | — | **back wall**, UV (0.5, 0.55) | flat on wall, faces −Z |
| Framed photos ×3 | — | — | back wall, UV (0.28,0.6)(0.28,0.4)(0.70,0.5) | flat on wall |
| Stairs (down) | 0.86 | 0.40 | floor (against right wall) | billboard |

Sort billboards back→front by `worldZ` (nearer = higher sortingOrder); the perspective camera + a
depth-sorted transparent queue handles overlap.

---

## 4 · Lighting, moonlight & realm swap

- **Cold Dusk grade:** a dim cool ambient (RGB ≈ 90,110,150) + one **moonlight** source.
- **Moonlight shaft:** a `Light` (Spot, cool white ~ (150,178,224)) placed just outside the window,
  angled onto the floor — living room = left window casting right; bedroom = back window casting
  forward. A soft additive "shaft" quad (the window shape, faded toward the floor) sells the volume.
- **Hearth / lamp:** a warm `Light` (point, ~ (255,176,86)) at the fireplace (living) and the
  nightstand lamp (bedroom) for the warm accent against the blue.
- **Dust motes:** a small upward `ParticleSystem` inside the moonlight cone, additive, tiny.
- **Realm swap → Nightmare:** swap the three tile materials + window to the `*_nightmare` textures,
  drop ambient, kill the hearth/lamp lights, and turn on the warp FX (bleeding wall decals, ceiling
  drip particles, rotted/darkened stairs). See `InteriorRoom.SetNightmare`.

---

## 5 · Unity scripts

Two scripts: **`InteriorRoom.cs`** builds the enclosed box + tiles + realm swap; **`RoomFurnisher.cs`**
positions every prop from the §3 tables and swaps the living/bedroom sets.

```csharp
// InteriorRoom.cs — builds the enclosed cabin box, tiles the surfaces, swaps realm.
using UnityEngine;

public class InteriorRoom : MonoBehaviour
{
    [Header("Dimensions (m)")]
    public float roomW = 8f, roomH = 4.5f, roomD = 9f;
    public float logCourse = 0.5f;         // log band height → wall tileY

    [Header("Cold Dusk tiles")]
    public Texture2D wall, ceiling, floor, window_;
    [Header("Nightmare tiles")]
    public Texture2D wallNM, ceilingNM, floorNM, windowNM;
    public Material tileMatPrefab;         // an unlit/lit material we clone per surface

    [Header("Lights")]
    public Light moonLight;                // cool spot outside the window
    public Light hearthLight;              // warm accent (fireplace / lamp)
    public GameObject warpFX;              // bleed decals + ceiling-drip particles (off in home)

    MeshRenderer _floor, _ceil, _back, _left, _right;

    void Start() { Build(); SetNightmare(false); }

    void Build()
    {
        _floor = Quad("Floor",   new Vector3(0, 0, roomD/2),      Quaternion.Euler(90,0,0),   roomW, roomD);
        _ceil  = Quad("Ceiling", new Vector3(0, roomH, roomD/2),  Quaternion.Euler(-90,0,0),  roomW, roomD);
        _back  = Quad("Back",    new Vector3(0, roomH/2, roomD),  Quaternion.Euler(0,180,0),  roomW, roomH);
        _left  = Quad("Left",    new Vector3(-roomW/2, roomH/2, roomD/2), Quaternion.Euler(0,90,0),  roomD, roomH);
        _right = Quad("Right",   new Vector3( roomW/2, roomH/2, roomD/2), Quaternion.Euler(0,-90,0), roomD, roomH);
        Tile(_floor, floor, roomW, roomD);
        Tile(_ceil,  ceiling, roomW, roomD);
        Tile(_back,  wall, roomW, roomH/logCourse);
        Tile(_left,  wall, roomD, roomH/logCourse);
        Tile(_right, wall, roomD, roomH/logCourse);
    }

    MeshRenderer Quad(string name, Vector3 pos, Quaternion rot, float w, float h)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name; go.transform.SetParent(transform, false);
        go.transform.localPosition = pos; go.transform.localRotation = rot;
        go.transform.localScale = new Vector3(w, h, 1);
        var mr = go.GetComponent<MeshRenderer>();
        mr.material = new Material(tileMatPrefab);
        return mr;
    }
    void Tile(MeshRenderer mr, Texture2D tex, float tileX, float tileY)
    {
        var m = mr.material; m.mainTexture = tex;
        m.mainTexture.wrapMode = TextureWrapMode.Repeat;
        m.mainTextureScale = new Vector2(tileX, tileY);
    }

    public void SetNightmare(bool nm)
    {
        Tile(_floor, nm?floorNM:floor, roomW, roomD);
        Tile(_ceil,  nm?ceilingNM:ceiling, roomW, roomD);
        foreach (var w in new[]{_back,_left,_right}) Tile(w, nm?wallNM:wall, (w==_back?roomW:roomD), roomH/logCourse);
        if (hearthLight) hearthLight.enabled = !nm;
        if (moonLight)   moonLight.color = nm ? new Color(0.6f,0.15f,0.13f) : new Color(0.59f,0.70f,0.88f);
        RenderSettings.ambientLight = nm ? new Color(0.06f,0.05f,0.05f) : new Color(0.35f,0.43f,0.59f);
        if (warpFX) warpFX.SetActive(nm);
    }
}
```

```csharp
// RoomFurnisher.cs — positions props from the fx/depth tables and swaps living/bedroom.
using System.Collections.Generic;
using UnityEngine;

public class RoomFurnisher : MonoBehaviour
{
    public InteriorRoom room;
    public Transform camTransform;                 // billboards face this
    public enum Mount { Floor, FloorFlat, Wall }
    public enum Wall  { Back, Left, Right }

    [System.Serializable] public class Prop {
        public string name; public Sprite sprite;
        public float fx, depth;                     // floor pieces
        public Mount mount = Mount.Floor;
        public Wall wall = Wall.Back; public Vector2 wallUV;   // wall pieces (u across, v up)
        public float scale = 1f;
    }

    [Header("Living room set")] public List<Prop> living = new();
    [Header("Bedroom set")]     public List<Prop> bedroom = new();

    readonly List<GameObject> _spawned = new();

    public void ShowLiving() { Spawn(living); }
    public void ShowBedroom(){ Spawn(bedroom); }

    void Spawn(List<Prop> set)
    {
        foreach (var g in _spawned) Destroy(g); _spawned.Clear();
        float W = room.roomW, H = room.roomH, D = room.roomD;
        foreach (var p in set)
        {
            var go = new GameObject(p.name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = p.sprite; sr.sharedMaterial = null;

            if (p.mount == Mount.Wall)
            {
                Vector3 pos; Quaternion rot;
                float u = (p.wallUV.x - 0.5f);
                float v = p.wallUV.y * H;
                switch (p.wall) {
                    case Wall.Back:  pos = new Vector3(u*W, v, D - 0.02f);         rot = Quaternion.identity; break;
                    case Wall.Left:  pos = new Vector3(-W/2 + 0.02f, v, u*D + D/2); rot = Quaternion.Euler(0, 90,0); break;
                    default:         pos = new Vector3( W/2 - 0.02f, v, u*D + D/2); rot = Quaternion.Euler(0,-90,0); break;
                }
                go.transform.localPosition = pos; go.transform.localRotation = rot;
            }
            else
            {
                float x = (p.fx - 0.5f) * W;
                float z = D * (1f - p.depth);
                go.transform.localPosition = new Vector3(x, 0.01f, z);
                if (p.mount == Mount.FloorFlat) go.transform.localRotation = Quaternion.Euler(90,0,0);  // rug
                else BillboardTo(go.transform);                                                          // faces camera
                sr.sortingOrder = Mathf.RoundToInt((D - z) * 100);                                       // nearer draws on top
            }
            go.transform.localScale = Vector3.one * p.scale;
            _spawned.Add(go);
        }
    }

    void BillboardTo(Transform t)
    {
        if (!camTransform) return;
        var dir = t.position - camTransform.position; dir.y = 0;
        t.rotation = Quaternion.LookRotation(dir);
    }

    // keep billboards facing the camera if it moves
    void LateUpdate() { foreach (var g in _spawned) if (g && g.transform.localRotation.eulerAngles.x < 45f) BillboardTo(g.transform); }
}
```

### Wiring
1. Empty `Room` GameObject → `InteriorRoom`; assign the 8 tile textures + a base material (`tileMatPrefab`,
   an unlit or lit transparent-cutout material) and the two `Light`s + `warpFX`.
2. Empty `Furniture` GameObject → `RoomFurnisher`; assign `room`, `camTransform`, and fill the
   **Living** / **Bedroom** lists straight from the §3 tables (fx, depth, mount, wall+UV, scale).
3. Call `ShowLiving()` on load; on the stair trigger swap to `ShowBedroom()` (and back). On the dread
   flag call `InteriorRoom.SetNightmare(true)` + re-spawn the current set (rotted stairs sprite).
4. Moonlight `Light` just outside the active window, angled to the floor; a small additive
   `ParticleSystem` inside the cone for dust motes.

Positions, tiling counts, and the fx/depth values are identical to `Cabin Interior.dc.html`, so the
Unity room frames up like the preview.
