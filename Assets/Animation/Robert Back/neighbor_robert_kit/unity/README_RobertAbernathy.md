# Robert Abernathy — animation frames & Unity implementation

The neighbor NPC. Six 7-frame sheets — **front / back / side × daytime + stretched nightmare** —
driven the same way as the rest of the game — **no Animator Controller**: recolor once at spawn,
slice, and cycle a small idle / walk / speak state machine. The sheet is chosen by **facing**
(side mirrors for left). Form swaps on the same dread flag as the dog and the mountain; motion is
gated to the player's proximity so the wrongness only stirs when you're near.

Scripts: **`NeighborRobert.cs`** (animator + state machine) · **`NeighborPalette.cs`** (workwear
roll). Sheets: `neighbor_robert_{front,back,side}[_nightmare].png` (all six sit in `unity/`).

---

## The animation frames — and what each is for

One row, cell `(col*32, 0, 32, 32)`, cols 0–6. **All six sheets share this exact layout**, so the
form swap and the view swap are texture changes, never a re-timing.

| # | State | Frame | Purpose |
|---|---|---|---|
| **0** | idle | neutral rest | Default standing pose. The frame he falls back to, and the one he freezes on when dormant (player far away). |
| **1** | idle | the held-too-long smile | The mouth stretches 1 px wider. Alternate `0 → 1` on a **slow, uneven** cadence with dead holds — he should stand unnervingly still, then "smile" a beat longer than a person would. |
| **2** | walk | contact (left lead) | Left foot plants, right heel lifts, arms swing. |
| **3** | walk | passing | Legs together, body rises 1 px — the up-beat of the step. Used twice per cycle. |
| **4** | walk | contact (right lead) | Right foot plants, left heel lifts, arms swing opposite. |
| **5** | speak | mouth closed | The between-words beat of the talk flap (over the idle body). |
| **6** | speak | mouth open | **Daytime:** a small, polite "O". **Nightmare:** the jaw unhinges into a black void down the throat — the scare beat. |

**Loops the scripts play**

- **Idle** — `0, 1` with random dead-holds (0.6–1.6 s on 0, ~0.5 s on 1). Never a metronome.
- **Walk** — `2, 3, 4, 3` at ~8 fps.
- **Speak** — `5 ↔ 6` at ~6.5 fps over the idle body, for a set duration, then back to idle.

> The **nightmare** silhouette is taller and reaches the top of the cell. Pivot is
> **bottom-center** so he grows *upward* when the form swaps in place — he never sinks.

---

## Install

1. Copy **`NeighborRobert.cs`** and **`NeighborPalette.cs`** into `Assets/Scripts/`.
2. Import both sheets and set, in the Inspector:
   - **Read/Write Enabled → ON**  (the palette roll reads pixels)
   - **Filter Mode → Point (no filter)**
   - **Compression → None**
   - Sprite Mode can stay *Single* — the scripts slice the raw texture themselves.
3. Make a GameObject with a **SpriteRenderer**, add **Neighbor Robert**, and assign the sheets:
   - **Daytime** Front / Back / Side → `neighbor_robert_front.png` · `_back.png` · `_side.png`
   - **Nightmare** Front / Back / Side → `neighbor_robert_front_nightmare.png` · `_back_nightmare.png` · `_side_nightmare.png`
   - **Player** → the player's transform (for the proximity gate)

That's it — he rolls his workwear, slices every view, and starts idling on play.

---

## Facing — front / back / side (side mirrors for left)

Robert is a 4-direction character built from **three** sheets, same as the player: the **side**
sheet faces right and is mirrored (flipX) for left. `SetMovement(velocity)` picks the facing
automatically — vertical movement uses front/back, horizontal uses side (flipped for left) —
and drives the walk cycle; zero velocity returns him to idle. To point him while he stands
(e.g. to turn and face the player before a line), call `FaceDirection(0..3)` — `0` front, `1`
back, `2` left, `3` right.

```csharp
robert.SetMovement(agent.velocity);   // walks + faces travel direction
robert.FaceDirection(0);              // or just turn to face the camera
```

> **Back-view speaking** has no visible mouth, so frames 5/6 read as a small nod — the flap
> still plays, you just don't see teeth. Front and side show the mouth (nightmare: the gape).

---

## The dread flag — daytime ↔ nightmare

`NeighborRobert` exposes `[Range(0,1)] DreadProgress` and `nightmareThreshold` (default 0.5),
matching `MountainBackdrop.DreadProgress`. Feed it from the **same** game-wide dread value you
drive the dog and the mountain with:

```csharp
robert.DreadProgress = DreadManager.Progress;   // he stretches once it crosses the threshold
```

Below the threshold he's the daytime man (only *off*); at/above it he snaps to the stretched
nightmare form — same frame, same position, taller.

---

## The workwear roll — a different man each load

`NeighborPalette` recolors four regions of the **daytime** sheet — **coveralls, turtleneck,
gloves, boots** — from curated *worn* pools, deriving each shadow from its base. Skin, grey
hair, the round glasses and the shears are identity and never change; the nightmare sheet is
drained and never recolored.

- `rollOnAwake = true` (default) picks a fresh assortment every load — he's never quite the
  same twice.
- To pin a specific look, turn it off and set the four indices on `look`, then call `Rebuild()`.

The shade factors match the `.dc.html` reference exactly, so a rolled look reads identically
in-engine and in the browser preview.

---

## Proximity + speaking

- **Proximity gate:** beyond `stirRange` (12 u) Robert is **dormant** — frozen on frame 0. Step
  inside and he begins to idle. Wrongness only stirs when you're close.
- **Speaking:** call `robert.Speak(seconds)` from your dialogue trigger. It only takes if the
  player is within `speakRange` (3.5 u). Daytime = a polite flap; nightmare = the jaw drops open
  far too wide on every open frame.
- **Walking / facing:** if he patrols, feed `robert.SetMovement(velocity)` each frame (see the
  Facing section above); zero velocity returns him to idle.

```csharp
// dialogue beat
if (Input.GetKeyDown(KeyCode.E)) robert.Speak(3f);

// optional patrol
robert.SetMovement(agent.velocity);
```

---

## Where he lives

Robert stands out front of **house C** (saltbox, oxblood trim) in `neighbor_houses_kit` — always
**visible**. House **A** (gable) is the recluse's, **never home** (dark, no smoke, in day and
night); **B** is vacant. Layer order: sky → mountains → houses (sorted by base Y) → **Robert**
(character layer, in front of C). See `NEIGHBOR_ROBERT.md` for the sprite/scene guide and
`neighborgen.js` to retune the art.
