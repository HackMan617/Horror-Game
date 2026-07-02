# Neighbor Houses — full 3D structure from the 2D tileset

How to stand each neighbor house up as a **3D object built from its flat tile atlas** — the same
trick as the player's cabin (`CabinShellBuilder`), now completed for **B (hipped roof)** and
**C (saltbox roof)**, with **animated window & smoke frames** cycled on quads. Companion to
`NEIGHBOR_HOUSES.md` (the 2D sprite/tile reference) and `README_NeighborHouses.md` (the 2D
elevation-swap approach). Pick whichever fits your camera:

- **2D** — drop the assembled elevation sprites, swap home↔nightmare (see `NeighborHouse.cs`).
- **3D** — clad a box with the tile atlas + roof geometry (this doc).

Scripts: **`CabinShellBuilder.cs`** (now has `RoofStyle` Gable/Hipped/Saltbox) ·
**`TileStripQuad.cs`** (animated window/smoke frames on a quad).

---

## Tilesets (included)

`neighbor_A_tiles.png` · `neighbor_B_tiles.png` · `neighbor_C_tiles.png` (+ each `_nightmare`),
**192×144, 8×6, 24-px cells**. Import: **Read/Write ON · Filter Point · Compression None**.
Copies live in `unity/` next to `house_tiles.png`.

**Tile → face map** — `(col,row)` in the 8×6 grid:

| Cell | Tile | Use on the 3D house |
|---|---|---|
| (0,0) | wallA | main wall cladding |
| (1,0) | wallB (knot) | scatter into walls for variation |
| (2,0) | postV | corner posts (CabinShellBuilder) |
| (3,0) | beamH | the story belt (a 1-tile band mid-wall) |
| (4,0) | foundation | fieldstone band along the base |
| (5,0)/(6,0) | doorTop / doorBottom | the door quad (front face) |
| (7,0) | window | static window quad |
| (0,1) | roofField | roof slopes (shingles) |
| (1,1)/(2,1) | roofRakeL / roofRakeR | rake trim on gable/saltbox ends |
| (3,1) | eave | eave fascia strip |
| (4,1)/(5,1) | chimney / chimneyTop | the chimney box + cap |
| rows 2–5 | animated strips | window/smoke/candle/door — see TileStripQuad |

---

## 1 · Walls (the box)

1. Make a **Cube**, scale it to the footprint (e.g. B ≈ 7.7×7.2×6 world units at PPU 24; match
   the elevation proportions). This is the wall box.
2. Give it a **material** sampling the house's `_tiles.png` (URP → *Unlit*, Base Map = the atlas,
   **Point** filter, **Render Face = Both**, Alpha Clip on ~0.5). Tile the UVs so `wallA` repeats
   — or clad each face with `CabinShellBuilder`-style `TiledQuad`s if you want per-tile control.
3. Lay the horizontal bands as thin quads across the front (and sides): **foundation** along the
   bottom row, **beamH** as the story belt where the two floors meet.

> The quickest path: add **`CabinShellBuilder`** to the box — it already builds the notched
> **corner posts** (postV) and the **roof** for you, and measures the box's bounds. You supply
> the walls (the cube) + the facade quads below.

---

## 2 · Roof — pick the style on CabinShellBuilder

`CabinShellBuilder` now has a **Roof Style** field:

| House | Roof Style | Key params |
|---|---|---|
| **A** | **Gable** | `ridgeAlongZ`, `ridgeHeight`, `eaveOverhang`, `gableOverhang` |
| **B** | **Hipped** | `ridgeHeight`, `hipInset` (0.4 ≈ classic hip; 0 ≈ pyramid), overhangs |
| **C** | **Saltbox** | `ridgeHeight`, `saltboxRidgeOffset` (0.4 pushes the ridge toward the front → short steep front slope, long shallow back), overhangs |

All three UV-map `roofField` shingles onto the slopes, cap the ridge, and (optionally) run an
`eave` fascia. Roofs render double-sided, so winding is forgiving. Tune `ridgeHeight` /
overhangs in the Inspector and hit **Build / Rebuild Shell**.

---

## 3 · Chimney

Make a small **Cube** clad with the `chimney` tile, stand it on the roof slope at the house's
chimney position, and cap it with a thin quad of `chimneyTop`. (House C's chimney is **dead cold
at night** — see the Smoke quad below.)

---

## 4 · Facade detail — door & windows

Place thin quads **just in front of** the front wall (z − 0.01 to avoid z-fighting), each UV-mapped
to a tile:

- **Door:** two stacked 1×1 quads — `doorTop` over `doorBottom` — at the center-bottom.
- **Windows:** `window` quads on the upper story and flanking the door. These can be **static**,
  or made to come alive (next section).

---

## 5 · Animated window & smoke frames — `TileStripQuad`

This is the "animation frames for windows and smoke" on the 3D object. Put **`TileStripQuad`** on
a quad sitting over a window / the chimney; it scrolls the quad's material UVs through a strip:

| Kind | Reads as | Put it on |
|---|---|---|
| `WinSil` | a shape crosses the lit glass | a downstairs window |
| `WinCandle` | candle flicker (night: a flashlight sweep) | an upstairs window |
| `Smoke` | chimney smoke rising (dead wisp at night) | the chimney top |
| `LoosePlank` | a board rattles / a shadow crosses | a wall panel |
| `DoorTop` + `DoorBottom` | the door swings (ping-pong) | over the door quad |

Assign the house's **Tiles Home** + **Tiles Nightmare** atlases, set the **Kind**, wire
**DreadProgress** + **Player**. It loops with per-quad jitter (off-phase), gates to proximity,
swaps to the nightmare atlas on the dread flag, and — tick **Dead Cold At Night** on house C's
Smoke quad — stops the smoke at night. Call `OpenDoor()` on both door quads to swing the door.

```csharp
foreach (var q in house.GetComponentsInChildren<TileStripQuad>()) q.DreadProgress = DreadManager.Progress;
```

---

## Home ↔ nightmare

Two ways, driven by the **same dread flag** as the dog, mountain, Robert, and interior:

- **Walls/roof:** swap the box + shell **material** from the `_tiles` one to the `_tiles_nightmare`
  one (same UVs, same layout) when dread crosses your threshold.
- **Animated quads:** `TileStripQuad` swaps its own texture automatically.

---

## Per-house recipe

| | Roof | Trim / accent | Night beat (drive the quads to match) |
|---|---|---|---|
| **B** | Hipped, `hipInset ≈ 0.4` | low railing, red-brown | all windows black; a `WinCandle` sweeps one; pale face baked in the nightmare wall texture |
| **C** | Saltbox, `saltboxRidgeOffset ≈ 0.4` | shutters, oxblood | `Smoke` **Dead Cold At Night** = on; one downstairs window burns sick green (baked) |

**Layer order (3D):** ground/street → houses → props → characters, same as the 2D path. Build
each house once as a prefab; instance three along the street; drive one shared dread value.
