# Cabin Shell Builder — connected corners + closed gable roof

Your house is a textured box, so the four walls meet at bare 90° seams (nothing
visually ties them together) and the roof planes don't close at the ridge/corners.
Flat facade tiles can't fix that — a corner needs **geometry**. This package adds:

- **Notched log corner posts** at all four vertical edges. They poke out past the
  walls so, from any angle the player walks, the logs read as interlocking — the
  classic cabin corner.
- **A closed gable roof**: two sloped shingle planes + a ridge cap + the two
  triangular gable ends + eave fascia, with proper overhang.

Everything is UV-mapped into your existing `house_tiles.png` atlas. Two new tiles
were added to the sheet for this (and to `house_tiles_nightmare.png`):

| Tile        | Atlas cell (col,row) | Used for                |
|-------------|----------------------|-------------------------|
| `cornerLog` | 6, 1                 | the corner post columns |
| `roofRidge` | 7, 1                 | the ridge cap           |

Atlas is unchanged otherwise: **192×144, 24px tiles, 8 columns × 6 rows.**

---

## Install

1. Copy **`CabinShellBuilder.cs`** into `Assets/Scripts/` (or anywhere under Assets).
2. Re-import the two updated sheets, replacing your current ones:
   - `house_tiles.png`
   - `house_tiles_nightmare.png`
3. Select each sheet in the Project window and set, in the Inspector:
   - **Filter Mode → Point (no filter)**  ← keeps pixels crisp
   - **Compression → None**
   - (If you slice it as a Sprite sheet, that's fine; the builder samples the
     raw texture, so a Texture import is enough.)

### Material (URP)
The builder needs a material that samples the atlas. Either:

- **Let it auto-create one** — put `house_tiles.png` in a `Resources/` folder
  (so `Resources.Load("house_tiles")` finds it) and leave the *Atlas Material*
  field empty. It builds a `Universal Render Pipeline/Unlit` material, sets the
  texture to Point filter, and turns culling off. **or**
- **Make your own** — create Material → shader `Universal Render Pipeline/Unlit`,
  set **Base Map** to `house_tiles`, **Surface Type = Opaque**, **Alpha Clipping
  on** (threshold ~0.5) if you want the transparent tile edges to cut out, and
  **Render Face = Both**. Assign it to the *Atlas Material* field. Use a second
  material with `house_tiles_nightmare` for the nightmare state.

---

## Build it

**Option A — menu command**
1. Select your **House** GameObject in the Hierarchy.
2. **Tools ▸ Cabin ▸ Add Corners + Roof to Selection.**

**Option B — component**
1. Add the **Cabin Shell Builder** component to the House.
2. Set **House Target** (defaults to itself) and assign **Atlas Material**.
3. Right-click the component header ▸ **Build / Rebuild Shell** (or just press
   the context-menu item). Re-run any time — it replaces the previous shell.

The result is a child object named `__CabinShell`. Delete it to remove the
shell; rebuild to regenerate it. Nothing else in the scene is modified.

---

## Tune it (Inspector fields)

| Field                | What it does |
|----------------------|--------------|
| **House Target**     | Object whose renderer bounds set the footprint & wall height. |
| **Size Override**    | Force `(width, wallHeight, depth)`; leave `0,0,0` to auto-measure. |
| **Atlas Material**   | Material sampling `house_tiles.png`. Empty = auto-create. |
| **World Units Per Tile** | World size of one 24px tile. Lower = denser logs/shingles. |
| **Corner Post Size** | Square cross-section of each corner post. |
| **Corner Overhang**  | How far the post pokes past the walls. |
| **Ridge Along Z**    | `true`: front/back are the gable (triangular) ends. `false`: rotate 90°. |
| **Ridge Height**     | Peak height above the wall top (controls roof pitch). |
| **Eave / Gable Overhang** | Roof overhang on the long / end sides. |
| **Roof Lift**        | Nudges the roof up to avoid z-fighting an old painted-on roof. |
| **Build Eave Fascia**| Adds a trim board along the eaves. |

> **If your old box already had a roof painted on its top faces**, hide or delete
> those faces (or just leave `Roof Lift` at a small positive value so the new roof
> sits cleanly above). The new roof is the one that actually closes the corners.

---

## Match the gable direction
If the triangular end ends up on the wrong side, flip **Ridge Along Z** and
rebuild. With `Ridge Along Z = true` the door/front and back walls are the
triangular gable ends (matching the current facade art).

## Nightmare swap
The shell reads from whatever material you assign. To switch the whole house to
the night look, swap the shell's material (and your wall materials) from the
`house_tiles` material to the `house_tiles_nightmare` one — same UVs, same layout.
