# SHOWER.md — cabin bathroom: hex tile · plank wainscot · walk-in shower (Unity 2D)

The cabin's old washroom, in the **Cold Dusk** grade (blue moonlight, cool porcelain, warm wood).
Bare-concrete walk-in shower with exposed black iron pipe, a curtain on iron rings, hex penny-tile
floor under plank wainscot, and the fixtures a hundred-year-old cabin bathroom would actually have.
Same **16px oblique** world as `interiorgen.js` / `interiorstructgen.js`.

Baked by `bathroomgen.js` (palette, geometry and the watcher pass live there — re-render, don't
hand-edit). Preview and state toggles in `Bathroom.dc.html`.

---

## Files

| File | Size | Contents |
|---|---|---|
| `bathroom_colddusk.png` | 384×192 | the whole kit — one transparent atlas |
| `bathroom_window_watch.png` | 96×32 | **3 frames** of the window with the watcher outside |
| `*-4x.png` | — | preview blow-ups — **do not ship** |

Import (both): **Sprite Mode Multiple · Point (no filter) · Compression None · PPU 16 · Wrap =
Repeat** for the tileable pieces. Slice by the rects below (Grid By Cell Size won't fit — the atlas
mixes cell sizes, so slice manually or use the rect table in an editor script).

Pivots: **Top-Left** for tiles (`hexFloor`, `wainscotWall`, `plasterWall`, `wainscotCap`,
`wetFloor`), **Bottom-Center** for standing fixtures (sink, toilet, vanity, pan, door),
**Top-Center** for hanging pieces (shower head, curtain, rail, shelf, towel rack, mirror, window).

Nightmare palette ships later — **except the window**, which has its watcher variant baked now.

---

## The atlas — paste-ready rects

```js
// [x, y, w, h, frames] — frame 0 rect; frames laid out horizontally (frame f at x + f*w)
const BATHROOM = {
  // — TILEABLE SURFACES —
  hexFloor:       [  0,  0, 16, 16, 1],   hexFloorWorn:  [ 16,  0, 16, 16, 1],
  wainscotWall:   [ 32,  0, 16, 16, 1],   wainscotCap:   [ 48,  0, 16, 16, 1],
  plasterWall:    [ 64,  0, 16, 16, 1],   wetFloor:      [ 80,  0, 16, 16, 1],
  drainGrate:     [ 96,  0, 16, 16, 1],   soapClutter:   [112,  0, 16, 16, 1],
  // — 2-STATE BITS —
  mirror:         [128,  0, 16, 16, 2],   // [0] clear  [1] fogged
  valveHandle:    [160,  0, 16, 16, 2],   // [0] closed [1] turned open
  showerHead:     [192,  0, 16, 16, 2],   // [0] off (one drip) [1] on
  // — WALL-HUNG / FLOOR PIECES —
  plankShelf:     [  0, 16, 32, 16, 1],   towelRack:     [ 32, 16, 32, 16, 1],
  showerPan:      [ 64, 16, 32, 16, 1],   puddle:        [ 96, 16, 16, 16, 3],
  curtainRail:    [144, 16, 48, 16, 1],
  // — FIXTURES —
  pedestalSink:   [  0, 32, 16, 32, 1],   toilet:        [ 16, 32, 16, 32, 1],
  vanity:         [ 32, 32, 32, 32, 1],   pipeRiser:     [ 64, 32, 16, 32, 1],
  waterStream:    [ 80, 32, 16, 32, 4],   window:        [144, 32, 32, 32, 1],
  // — SHOWER SHELL / FX —
  steam:          [  0, 64, 32, 32, 4],
  curtain:        [  0, 96, 48, 48, 4],   // [0] closed [1] 1/3 [2] 2/3 [3] bunched open
  showerBackWall: [192, 96, 32, 48, 1],   door:          [224, 96, 32, 48, 4],
};
// frame N rect: [x + (N % frames)*w, y, w, h]
const WINDOW_WATCH = { windowWatch: [0, 0, 32, 32, 3] };  // separate sheet
```

---

## The room — what each piece is for

### Shell
| Piece | Cell | Tiling | Notes |
|---|---|---|---|
| `hexFloor` | 16×16 | **seamless X & Y** | small hex penny tiles, half-offset rows, dark grout, faint moon sheen |
| `hexFloorWorn` | 16×16 | seamless | same grid with staining and a few missing tiles — sprinkle at ~15% to break the pattern |
| `wainscotWall` | 16×16 | seamless X & Y | vertical beadboard planks, grain and wear |
| `wainscotCap` | 16×16 | tiles X | the cap rail — **one course**, sits on top of the wainscot band |
| `plasterWall` | 16×16 | seamless X & Y | cool damp plaster above the rail |
| `wetFloor` | 16×16 | seamless | slick sheen **overlay** — lay over `hexFloor` near the stall, fade alpha with runtime |

Stack per column, top → bottom: `plasterWall` × N → `wainscotCap` × 1 → `wainscotWall` × N → floor
is `hexFloor`/`hexFloorWorn`. `BathroomRoomBuilder.cs` does exactly this.

### Walk-in shower
| Piece | Cell | Notes |
|---|---|---|
| `showerBackWall` | 32×48 | **full stall height** of poured concrete — form-board seams, damp streaks, rust at the tie holes. Two side by side = a 4-cell-wide stall. |
| `showerPan` | 32×16 | concrete pan with a **raised lip you step over**, sunken basin, drain, mineral staining. Sits on the floor line. |
| `pipeRiser` | 16×32 | exposed black iron pipe, threaded couplings, wall strap, rust bloom |
| `showerHead` | 16×16 ×2 | gooseneck + brass face. Frame 0 is **off but dripping** (old head never quite shuts) |
| `valveHandle` | 16×16 ×2 | brass cross handle: upright closed → turned 45° open |
| `curtainRail` | 48×16 | iron rod on end brackets, 8 rings |
| `curtain` | 48×48 ×4 | closed → 1/3 → 2/3 → bunched open. Folds, ring grommets, weighted hem, mildew spotting |
| `waterStream` | 16×32 ×4 | falling water + splash ring in the pan + flung droplets |
| `steam` | 32×32 ×4 | wisps rising and dissipating — place 1–3 emitters around the stall, offset their frames |
| `puddle` | 16×16 ×3 | still → drip lands → ripple spreads |
| `drainGrate` | 16×16 | iron grate, for the room floor drain outside the stall |

**Stall geometry that reads correctly** (in 16px cells): rail on top, back wall **3 cells tall**
(`showerBackWall` is 48px = 3 cells), pan directly beneath it on the floor line, curtain hanging the
full 3-cell drop from the rail to the pan's top edge. The curtain and the back wall are the same
48px height, so **place both at the same top Y** and put the pan's top edge exactly 48px (3 cells)
below that — no gap, or the room wall shows through the stall. The curtain is 48px wide (3 cells),
so use a 3-cell-wide opening and span the back wall across those same 3 cells (two 32px walls
overlapped, or one wall plus a half) so nothing peeks past the fabric.

### Fixtures
`pedestalSink` (porcelain basin, fluted column, brass tap) · `vanity` (plank cabinet, dropped-in
basin, brass pulls) · `toilet` (wooden tank lid + seat ring, brass lever) · `plankShelf` (two boards
on iron brackets, bottles and folded cloth) · `towelRack` (iron rod on wood blocks, two hanging
towels) · `soapClutter` (dish, worn bar, bottles) · `mirror` (wood frame, clear/fogged) ·
`window` (plank casing, clear upper band with moon and treeline, frosted lower panes, sill).

### Door
`door` 32×48, 4 frames: **[0] shut and latched · [1] hook-and-eye lifted clear of the eye ·
[2] half open · [3] wide open** with light spilling onto the floor. The latch animating before the
swing is the whole point — hold frame 1 ~0.2s so the unlatch reads.

---

## The watcher (window scare)

`bathroom_window_watch.png` is the same window rect with **two glowing red eyes outside the frosted
glass** — a body-shaped darkening pressed against the pane, breath fog blooming between the eyes,
3 frames (dim → bright pulse → drifted). It's a straight sprite swap on the window's renderer.

Rules that keep it frightening:
- **Only while the player is actually showering** (inside the stall **and** water running).
- **Rare and random** — `watcherChance ≈ 0.18` on a check every 8–20s, so most showers are fine.
- **Short** — 1.2–3.5s, then it's simply not there. No fade-out; it's gone between blinks.
- Fire `onSeen` only when the player is *facing* the window, so the dread hit lands when it's earned.

---

## Unity scripts

| Script | Job |
|---|---|
| `BathroomRoomBuilder.cs` | builds the shell (plaster / cap / wainscot / hex floor) from the tiles |
| `ShowerController.cs` | enter/exit the stall, curtain slide, water on/off, steam, wet floor, fogged mirror, watcher rolls |
| `WindowWatcher.cs` | the eyes-at-the-window scare — frame loop, timing, `onAppear` / `onSeen` events |
| `BathroomDoor.cs` | plank door + hook-and-eye latch, 4-frame open/close, collider blocking |

### Wiring
1. Slice `bathroom_colddusk.png` with the rect table. Slice `bathroom_window_watch.png` as 3 × 32×32.
2. Empty `Bathroom` GameObject → `BathroomRoomBuilder` (set `widthCells`, the three band heights) → Build.
3. Place the stall: `curtainRail` on top, two `showerBackWall`, two `showerPan` on the floor line,
   `pipeRiser` + `showerHead` on the back wall, `valveHandle` at reachable height, `curtain` in front
   (sorting order **above** the pan and the player when closed).
4. `ShowerController` on the stall's trigger collider — assign renderers and frame arrays, plus
   `insideStandPoint` / `outsideStandPoint`. Call `EnterShower()` / `ExitShower()` from your
   interaction prompt, `ToggleWater()` from the valve.
5. `WindowWatcher` on the window GameObject: `normalWindow` = the atlas window sprite, `watchFrames`
   = the 3 watch frames, `playerCamera` = the POV camera. Hook it to `ShowerController.watcher`.
6. `BathroomDoor` on the door: 4 frames + the blocking collider; `Toggle()` from the prompt.

### Lighting
One cool moonlight source through the window (RGB ≈ 150,178,224) is the whole room — the art already
bakes a moon sheen on the floor, the rail, the plaster top edge and the curtain crests, so keep the
in-engine light dim and let the sprites carry it. Add a faint additive glow behind the steam
emitters while the water runs; kill it the moment the valve closes.

---

## As installed in this project

The room is built by `HorrorGame3DSetup.InstallCabinBathroom()`
(**Tools > Horror Game > Install Cabin Bathroom**), which drops it into the upper storey's empty
south-west quarter of `Sandbox3D` — footprint `x[-9.75,-4.15] z[-9.75,-4.75]`, 3.2m to the ceiling,
a 2m door onto the landing. It is an in-place installer on its own EditorPrefs version, so it never
wipes the scene, and `BuildCabinInterior` calls it too so a full interior rebuild keeps the bathroom.

The four scripts above shipped as flat-2D and were ported; the versions the game runs live in
`Assets/Scripts/`, alongside the rest of the interior:

| Kit script | In this project | What changed |
|---|---|---|
| — | `BathroomAtlas.cs` | the rect table above, as data, with the top-left → Unity Y flip |
| — | `BathroomFixture.cs` | one component per piece; slices itself from the atlas at runtime |
| `BathroomRoomBuilder.cs` | `BathroomShell.cs` + the installer | the shell is tiled 3D surfaces, not a carpet of 16px sprites — the five tileable cells are cut out to standalone repeat-wrapped textures under `tiles/` |
| `ShowerController.cs` | `ShowerStall.cs` | no stand points and no teleport: the stall is real space you walk into, and the trigger box containing the player is what gates the watcher. One key: E turns the water on, E turns it off, walking out shuts it |
| `WindowWatcher.cs` | `WindowWatcher.cs` | slices its own 3-frame sheet and suspends the window fixture while the eyes are up |
| `BathroomDoor.cs` | `BathroomDoor.cs` | `Collider` not `Collider2D`, driven by the game's walk-up-and-press-E prompt, and the leaf is a depth-writing quad rather than a sprite. Only the two SHUT frames are drawn — the kit's open frames paint the opening as a black void, which is a picture of a doorway rather than a hole in a wall |

Three things that bite when placing pieces in the 3D room:

- **Anything that has to HIDE something is geometry, not a sprite.** The sprite shader is `ZWrite
  Off`, so a sprite writes no depth and occludes nothing — it can only be ordered against other
  sprites. That is why the shower's concrete and the door leaf are alpha-clipped quads (`MakeBathQuad`
  / `BathroomOpaque.mat`, the same setup as the cabin's own front door). As sprites, a shut bathroom
  door hid nothing and the shower curtain hung in the middle of it from out on the landing.
- **Then leave every sprite on sorting order 0.** Once the concrete writes depth, plain distance
  sorting is correct everywhere, including the case SHOWER.md cares about: from the room a drawn
  curtain is nearer than the player behind it and covers them; from inside the stall the player is
  nearer and covers it. Reaching for explicit orders instead is a trap in both directions — pushing
  the shell DOWN makes it vanish (a negative order falls behind the world; it is the band the sky
  backdrop lives in), and pushing the plumbing UP makes the pipes paint over the player standing in
  front of them.
- **Mount to the skin, not the structure.** The wainscot and plaster stand 4cm proud of the wall
  they are laid on, so anything positioned off the structural face ends up half-buried in its own
  wainscot. The installer keeps `BathFaceS/W/E/N` for this; note the standoff sign differs per wall.

The watcher is fully built — sheet sliced, window and camera wired — but `ShowerStall.watcher` is
deliberately left EMPTY, so the eyes never come. Assigning it is the whole of that hookup.

The nightmare pass is still the one thing the art does not have. `BathroomFixture.nightmareAtlas`
and its `DreadProgress` are wired and pumped by `CabinInterior`, so dropping the rotted sheet in is
all that stands between here and a washroom that turns over with the rest of the house.
