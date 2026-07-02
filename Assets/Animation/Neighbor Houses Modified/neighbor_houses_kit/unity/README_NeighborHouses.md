# Neighbor Houses — Unity implementation

Wiring the three street houses (A / B / C) into the game. Companion to **`NEIGHBOR_HOUSES.md`**
(the sprite/tile/animation reference). Two scripts, matching the rest of the game (no Animator
Controller): swap the assembled elevation home↔nightmare on the dread flag, and drive the
animated overlay strips at authored anchors, gated to proximity.

Scripts: **`NeighborHouseTiles.cs`** (atlas cells, strips, slicing) · **`NeighborHouse.cs`**
(elevation swap + strip decals + door + dread + proximity).

---

## Assets

Per house (A shown; same for B, C):

| Kind | Files |
|---|---|
| Assembled elevations | `neighbor_A_front.png` / `_side` / `_side_mirror` / `_back` (+ each `_nightmare`) |
| Tile atlas (strip source) | `neighbor_A_tiles.png` + `neighbor_A_tiles_nightmare.png` (192×144, 8×6, 24-px) |

Elevation sizes differ per house (A ≈ 160×182, B ≈ 184×172, C ≈ 184×174) — authored sprites,
placed by their base line. Import everything: **Read/Write ON · Filter Point · Compression None**.

---

## Install

1. Copy **`NeighborHouseTiles.cs`** and **`NeighborHouse.cs`** into `Assets/Scripts/`.
2. Per house: a GameObject + **SpriteRenderer**, add **Neighbor House**, then assign:
   - **Home / Nightmare Elevation** → the view you're showing (e.g. `neighbor_A_front.png` + `…_nightmare.png`)
   - **Tiles Home / Tiles Nightmare** → `neighbor_A_tiles.png` + `…_nightmare.png`
   - **Pixels Per Unit** = 24
3. Add **animated-strip decals** (see below) at the windows / chimney / door.
4. Wire **DreadProgress** and set the **Player** transform for the proximity gate.

```csharp
foreach (var h in street.GetComponentsInChildren<NeighborHouse>())
    h.DreadProgress = DreadManager.Progress;   // the whole block turns together
```

---

## The elevation swap

`NeighborHouse` shows `homeElevation` until `DreadProgress ≥ nightmareThreshold` (0.5), then
swaps to `nightmareElevation` — same position, same base line. Each house's **nightmare beat**
is baked into its `_nightmare` art:

| House | Beat (baked) | Drive strips to match |
|---|---|---|
| **A** | door ajar to black; a figure in the cold-lit upstairs room | `WinCandle` on that upstairs window |
| **B** | all windows black; a flashlight sweeps one; a pale face upstairs | `WinCandle` sweep (face is baked) |
| **C** | chimney dead cold; one downstairs window burns sick green | **no** `Smoke` at night (auto-disabled for C) |

---

## Animated strips — decals & anchors

The motion (a silhouette crossing a window, candle/flashlight flicker, chimney smoke, a rattling
board, the front door swinging) lives in the **tile atlas** (rows 2–5). Add a **Decal** per motion
in the inspector list; each becomes a child SpriteRenderer that plays that strip over the baked
elevation:

- **Strip** — `WinSil`, `WinCandle`, `Smoke`, `LoosePlank`, `DoorTop`, `DoorBottom`.
- **Pixel Offset** — the top-left of the 24px cell, measured in **source pixels from the
  elevation's top-left** (line it up over the baked window / chimney / door). The component
  converts to local units for you.
- **Sorting Order Offset** — draw above the elevation (default +1).
- **Is Door** — tick on the two door halves so they swing together.

Ambient strips loop with a **per-decal jitter** so the block never pulses in sync (same
wrong-timing philosophy as the dog and props). Strips only animate while the **player is within
`stirRange`** — the street is still until you get close.

Example anchors for House A front (eyeball against the art, then nudge):
`WinCandle` @ (108, 40) upstairs window · `Smoke` @ (120, 4) chimney · `WinSil` @ (36, 96)
downstairs window · `DoorTop` @ (72, 108) + `DoorBottom` @ (72, 132).

---

## The front door

Call `house.OpenDoor()` (on interact, or from a trigger volume) to play the door **ping-pong** —
`0 0 0 0 0 1 2 3 3 3 3 3 3 2 1` at ~300 ms: closed → swings open → holds → closes. In the
nightmare palette the same swing opens to black with eyes waiting. Put `DoorTop` + `DoorBottom`
decals over the baked door, both flagged **Is Door**.

```csharp
if (nearDoor && used) houseA.OpenDoor();
```

---

## Layer order

sky → mountain range → **houses** (ground/street layer, sorted by base Y) → props → characters.
Drive the home↔nightmare swap from the same dread value as the dog's true form, the mountain
apparition, Robert, and the interior furniture, so the whole world turns at once.
`Neighbor Houses.dc.html` is the interactive reference for the roof shapes and night beats.
