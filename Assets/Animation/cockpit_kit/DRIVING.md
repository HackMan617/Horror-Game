# DRIVING.md — First-person cockpit & the transport system (Unity 2D)

The interior view you get when you **drive** — and **driving is how you traverse the whole
player area**: the windshield is the outward view from the driver's seat, a real routed road
running toward the landmarks you approach and pull over at. It's the counterpart to the
exterior truck in `CAR.md`. A worn 1940s–50s cabin (matching the truck's faded-teal paint,
cream crash-pad, warm rust and ivory dials) with a steering wheel, a **numbered** speedo +
fuel gauge, a warning-light cluster (with labels), a **P R N D gear indicator**, an odometer
with a **rolling tenths drum**, a rear-view mirror, and a little **carved figure/totem**
swinging off it. Out the glass: **mountains framing the horizon** (we live in the mountains)
and roadside props — **stop signs, dead trees, scattering crows** — scrolling past. Cold
`_nightmare` twins throughout.

> **The one rule that matters: almost nothing is baked.** The cockpit ships as *separate
> layers* and the game drives them live — the wheel rotates, the needles rotate, the gear +
> odometer readouts update, the totem plays an 8-frame idle **and** swings on a physics
> pendulum, the lamps toggle, the mirror composites a real rear view, and the road beyond the
> windshield (mountains + roadside props) is a live parallax rig, not a picture. That is what
> makes the nightmare corruptions (lying gauges, a wheel that turns itself, a backward
> odometer, the passenger in the mirror, the writhing totem) possible without extra art. Bake
> the cockpit into one image and you lose all of it.

Two generators back this: **`cockpitgen.js`** (cabin, dash, gauges, gear, odometer, mirror,
totem) and **`roadpropsgen.js`** (the chunky-16px roadside props + the mountain range band).

Authoring rules as everywhere else: **point/nearest filtering, integer scale, real alpha**,
a `home` file + a cold `_nightmare` twin per sprite. `-Nx` files (e.g. `-6x`, `-4x`, `-2x`) are
preview blow-ups — **do not ship them**.

Live reference (drive it): `Cockpit Drive.dc.html`.

---

## 1 · File inventory & layer stack

The cockpit is a stack of sprites on an overlay, drawn back → front. Everything is authored
in a **260 × 180 "shell frame"** (the windshield opening is transparent); show it at an
integer scale to fill the viewport (the reference runs it ×2 = 520 × 360).

| Sort | Layer | File (`_nightmare` twin each) | Cell | Pivot | Driven live? |
|---|---|---|---|---|---|
| 0 | windshield: mountains | `road_mountains` (strip 320×96) | — | — | **parallax scroll** |
| 1 | windshield: road + roadside props | `road_stopsign` `road_deadtree` `road_crow` `road_debris` | see §5b | bottom | **scroll + flip-book** |
| 10 | cockpit shell | `cockpit_shell` | 260×180 | — (full frame) | static |
| 20 | speedo face (numbered 0–100) | `gauge_speed` | 60×60 | center | static |
| 20 | fuel face (E · ½ · F) | `gauge_fuel` | 60×60 | center | static |
| 21 | needle ×2 | `needle` | 60×60 | **center** | **rotates** |
| 22 | warning lamps (+labels) | `warning_lights` (sheet 144×48) | 24×24 cells | — | **toggles** |
| 24 | gear indicator | `gear_indicator` (sheet 104×11, 4 frames) | 26×11 | — | **frame = gear** |
| 25 | odometer digits | `odometer_digits` (strip 90×13) | 9×13 digit | — | **counts** |
| 25 | odometer tenths drum | `odometer_tenths` (drum 9×143) | 9×13 window | — | **scrolls** |
| 30 | rear-view mirror glass | *(RenderTexture / faked, §6)* | — | — | **yes** |
| 31 | mirror passenger | `mirror_passenger_nightmare` | 80×52 | — | **fades (nightmare)** |
| 32 | mirror frame | `mirror` | 104×40 | center | static |
| 40 | hanging totem | `charm` (sheet 208×52, **8 frames**) | 26×52 | **top-center** | **idle loop + physics swing** |
| 50 | steering wheel (+ hands) | `steering_wheel` | 140×140 | **center** | **rotates** |

The warning sheet is **6 icons × 2 rows** — row 0 = unlit, row 1 = lit — each with a 3-letter
label (OIL TMP BAT CHK HI BLT). Cell `(col*24, row*24, 24, 24)`.

The odometer strip is digits **0–9**, cell `(d*9, 0, 9, 13)`. The **tenths** is a separate
vertical red drum (`0..9,0` stacked, 11×13-tall cells) you scroll continuously for the
rolling last-digit look. The **gear** sheet has 4 frames, one per lit letter (P R N D).
The **charm/totem** is an 8-frame horizontal idle sheet (`f*26, 0, 26, 52`).

### Anchors (shell-frame px; multiply by your display scale)

```
windshield hole : x 16..244,  y 6..100      (transparent; mountains + road show through)
speedo center   : (100, 104)  r 26          (numerals 0..100 baked on the face)
fuel center     : (158, 108)  r 17          (E · ½ · F baked on the face)
warning grid    : center (202,100), 2×2, 17px pitch, 12px lamp (+labels below)
odometer window : x 82, y 120   (5 white digits @9px + 1 red tenths drum)
gear window     : x 116, y 138, 26×11   (P R N D)
steering wheel  : center (130, 192)   — only the top arc shows above the dash
mirror          : center (130, 22),  glass rect x12..92 y8..34 (local to the mirror sprite)
totem pivot     : (176, 34)   — hangs off the mirror's right ear
```

These are exported as `Cockpit.ANCHORS` in `cockpitgen.js` if you re-tune the layout.

---

## 2 · Import settings

| Setting | Value |
|---|---|
| Texture Type | Sprite (2D and UI) |
| Sprite Mode | Single (Multiple for `warning_lights` & `odometer_digits`) |
| Pixels Per Unit | pick one PPU for the whole cockpit (e.g. **1 shell px = 1 unit / N**); keep it consistent so anchors line up |
| Filter Mode | **Point (no filter)** |
| Compression | **None**, Mip Maps **off** |
| Pivot | per the table above — set **Center** on wheel/needle/gauges/mirror, **Top-Center** on the charm |

Slice the two sheets **Grid By Cell Count**: `warning_lights` = Col 6 / Row 2;
`odometer_digits` = Col 10 / Row 1.

**Where to put it.** The cockpit is a HUD-like overlay, not world geometry. Two good options:
- **Overlay camera** (recommended for pixel crispness): a second orthographic `Camera` with
  higher depth, `clear = depth only`, rendering a "Cockpit" sorting layer. Lay the sprites out
  by the anchor table as children of one `CockpitRig` root. The windshield parallax + mirror
  render on the **main** camera behind it.
- **Screen-Space-Camera Canvas** with `Image`s. Simpler to anchor but you fight Unity UI
  scaling; if you go here, force integer scaling and Point filtering.

---

## 3 · The driving rig (state + physics)

One MonoBehaviour owns the numbers every layer reads. This is deliberately a **free-drive on a
looping road**: hold gas, the world scrolls; there is no fixed destination yet (procedural
buildings come later).

```csharp
using UnityEngine;

public class DrivingRig : MonoBehaviour
{
    [Range(0,1)] public float speed;      // 0..1  (×80 = mph)
    [Range(-1,1)] public float steer;     // -1 left .. +1 right
    [Range(0,1)] public float fuel = 1f;  // 1 full .. 0 empty
    public float distance;                // "miles" — odometer + parallax driver
    public float reveal;                  // 0..1 how much has crested ahead
    public float rearFill = 1f;           // 1 full .. 0 drained (rear-view)
    public bool  nightmare;

    [Header("tuning")]
    public float accel = 1.6f, dragIdle = 0.28f, dragBrake = 1.6f;
    public float fuelBurn = 0.018f, milesPerSec = 22f, revealMiles = 900f;

    float throttle, brake, steerIn;   // set these from Input (held)

    public void SetThrottle(float v) => throttle = v;
    public void SetBrake(float v)    => brake = v;
    public void SetSteer(float v)    => steerIn = Mathf.Clamp(v, -1, 1);
    public void Refuel()             => fuel = 1f;

    void Update()
    {
        float dt = Time.deltaTime;
        bool hasFuel = fuel > 0.001f;
        float target = (throttle > 0 && hasFuel) ? 1f : 0f;

        speed += (target - speed) * (throttle > 0 && hasFuel ? accel : 0f) * dt;
        speed -= (brake > 0 ? dragBrake : dragIdle) * speed * dt;
        speed  = Mathf.Clamp01(speed);

        steer += (steerIn - steer) * Mathf.Min(1, dt * 8f);

        fuel      = Mathf.Max(0, fuel - speed * dt * fuelBurn);
        distance += speed * dt * milesPerSec;

        // THE HORIZON: reveal grows with how far you've driven; rear drains with speed
        reveal   = Mathf.MoveTowards(reveal,   Mathf.Clamp01(distance / revealMiles), dt * 1.2f);
        rearFill = Mathf.MoveTowards(rearFill, 1f - speed * 0.92f,                    dt * 2.5f);
    }
}
```

Wire `SetThrottle/SetBrake/SetSteer` to held input (new Input System or `GetKey`). Empty fuel
zeroes the throttle target — you coast to a stop and can't pull away until you `Refuel()`.

---

## 4 · Live layers (the point of the split)

### Steering wheel — rotate the transform
```csharp
public Transform wheel; public float maxWheelDeg = 135f;
void LateUpdate() {
    float z = -rig.steer * maxWheelDeg;
    if (rig.nightmare) z += Mathf.Sin(Time.time*0.9f)*28f + Mathf.Sin(Time.time*2.3f)*11f; // turns itself
    wheel.localEulerAngles = new Vector3(0,0,z);
}
```
The **hands are part of the wheel sprite** (they grip at 10 & 2), so they rotate with it — no
separate rig. The nightmare wheel already carries the gore hand + muscle/bone rim; you don't
recolor it, you just swap the sprite (§7) and add the self-turn above.

### Gauges — one needle prefab, reused, value → angle
The dial faces are static; only the needle moves. Sweep runs **−120° (min) → +120° (max)**:
```csharp
public Transform speedNeedle, fuelNeedle;
float Ang(float t) => -120f + Mathf.Clamp01(t) * 240f;   // t in 0..1
void UpdateGauges() {
    float sv = rig.speed, fv = rig.fuel;
    if (rig.nightmare) {                       // the gauges LIE — same asset, wrong value
        sv = Mathf.Clamp01(rig.speed + Mathf.Sin(Time.time*7f)*0.28f);   // speedo spins
        float p = Mathf.Sin(Time.time*0.7f);
        fv = p > 0.4f ? 1f : (p < -0.4f ? rig.fuel*0.35f : rig.fuel);    // full…then empty
    }
    speedNeedle.localEulerAngles = new Vector3(0,0,-Ang(sv)); // -z so +angle reads clockwise
    fuelNeedle .localEulerAngles = new Vector3(0,0,-Ang(fv));
}
```
The **same `needle` sprite** serves both gauges (scale it down for the fuel dial) — the fuel
lie reuses the fuel asset exactly, per the brief.

### Warning lamps — toggle the lit frame
Each lamp is a `SpriteRenderer` with the unlit sprite; enable the lit sprite (row 1) on its
condition. Daytime is mostly dark (oil at idle, temp when you thrash it, battery on low fuel).
Nightmare **trips them all** and flickers:
```csharp
bool Lit(int i) => rig.nightmare ? ((Mathf.FloorToInt(Time.time*6)+i) % 2 == 0) || true
                                 : DaytimeCondition(i);
```
The nightmare check-engine lamp glows **green phosphor** — it's baked into `warning_lights_nightmare`'s
lit row, so just swap the sheet.

### Gear indicator — pick the lit frame
`gear_indicator` is a 4-frame sheet (P, R, N, D each lit in turn). Show the frame for the
current gear:
```csharp
int gear = rig.speed > 0.03f ? 3 : 0;                 // D moving, P parked
if (rig.nightmare) { int k = Mathf.FloorToInt(Time.time*1.5f) % 9;
    if (k == 0) gear = 1; else if (k == 3) gear = 2; } // glitches to R / N on its own
gearRenderer.sprite = gearFrames[gear];
```

### Odometer — digits up in home / **backward** in nightmare, with a rolling tenths drum
```csharp
int odo = rig.nightmare
    ? Mathf.Max(0, 99999 - Mathf.FloorToInt(rig.distance*3f) % 100000)   // runs backward
    : Mathf.FloorToInt(rig.distance) % 100000;
// render each of the 5 whole digits by blitting cell (d*9,0,9,13) from the strip
```
Give each of the 5 slots a `SpriteRenderer` (or one mesh with per-quad UVs). The nightmare
strip lights the digits green. The **tenths** is a separate vertical drum you scroll: clip a
one-digit window and offset the drum by the fractional tenth so the last digit is always
mid-roll — the classic mechanical-odometer feel:
```csharp
float val   = rig.nightmare ? Mathf.Max(0, 100000 - rig.distance*3f) : rig.distance;
float tenth = val * 10f;
int   cur   = ((Mathf.FloorToInt(tenth) % 10) + 10) % 10;
float roll  = Mathf.Repeat(tenth, 1f);           // 0..1 within the current tenth
// draw the drum sprite offset up by roll*cellHeight inside a 1-digit scissor/mask;
// the drum is digits 0..9,0 stacked, so cur..cur+1 covers the wrap seamlessly.
tenthsTransform.localPosition = baseTenthsPos + Vector3.up * (roll * digitHeight);
```

### Hanging totem — 8-frame idle **plus** a physics swing (layered)
The charm is now a **carved little figure/totem** with an 8-frame idle sheet (a slow twist +
bob). Play the flip-book **and** rotate the whole sprite on a pendulum so turns/bumps make it
lean — the two layer cleanly because the frames don't move the pivot:
```csharp
public SpriteRenderer totem; public Sprite[] totemFrames;   // 8 frames
public Transform totemPivot;                                 // pivot at the TOP (the string)
float a, v; int frame; float ft;
void Update() {
    // idle flip-book ~7 fps
    ft += Time.deltaTime; if (ft >= 1f/7f) { ft = 0; frame = (frame+1) % 8; totem.sprite = totemFrames[frame]; }
    // reactive swing: lateral accel from steer·speed, spring restore, + bump impulses
    float lateral = -rig.steer * rig.speed * 3.4f - (rig.nightmare ? Mathf.Sin(Time.time*3f)*0.4f : 0f);
    v += (lateral - a*7f - v*2.2f) * Time.deltaTime;
    if (rig.speed > 0.12f && Random.value < rig.speed*0.04f) v += Random.Range(-0.5f, 0.5f); // road bumps
    a += v * Time.deltaTime;
    totemPivot.localEulerAngles = new Vector3(0, 0, a * Mathf.Rad2Deg);
}
```
Nightmare uses the `charm_nightmare` sheet (it writhes, its bead eyes open + glow, and it
turns to look at you) — swap the sheet, keep the same rig.

---

## 5 · The horizon — the parallax windshield

This is the "transportation" feel and the mechanic you asked for. **Reuse the mountain
backdrop rig** (`MOUNTAIN_BACKDROP.md` / `MountainBackdrop.cs`) as the world beyond the glass,
plus the **road tiles** from `CAR.md` laid in perspective toward a vanishing point. Two driven
quantities shape it:

- **Forward reveal (`rig.reveal`, grows with `distance`)** — *the farther you drive, the more
  of what's ahead you see.* Model it as the road **cresting a hill**: at `reveal = 0` the far
  ridge planes sit just under the horizon (little beyond the crest); as `reveal → 1` you lift
  the distant planes **into view** (raise their sort-line / fade them in), so valley and far
  peaks rise over the hill the longer you travel. In practice: `layer.localY = Lerp(hidden,
  shown, reveal)` and/or `layer alpha = reveal` on the farthest 2–3 planes.
- **Rear drain (`rig.rearFill`, shrinks with `speed`)** — *look behind and there's less and
  less.* Applied to the mirror (§6): a dark overlay whose alpha = `1 - rearFill`, so at speed
  the rear view empties to black. It reads as **no turning back**.

Scroll everything off `distance` and `steer`, exactly like the backdrop's parallax:
```csharp
// far → near planes barely move → move most; steer shifts the vanishing point
foreach (var p in planes)
    p.mat.mainTextureOffset += new Vector2((rig.distance*plane.factor + rig.steer*plane.sway)
                                            * Time.deltaTime, 0);
```
The **road**: build it as a perspective quad (or a shader) with the vanishing point at
`x = center - steer*0.22`, the centre dashes and shoulder posts scrolling toward the camera by
`distance` (scale up as they approach = speed sensation). Curve the road by steering the
vanishing point + edge offset. The reference DC draws exactly this model on a canvas if you
want a concrete read on proportions and timing.

> Reveal is intentionally **decoupled from a destination** for now — it's a continuous
> "how far in" readout on an endless road. When procedural buildings arrive, gate their
> spawn on `distance` and let `reveal` control how early they crest into view.

### 5a · Mountains framing the horizon

`road_mountains` (320×96, home + nightmare) is a wide snow-capped range band — we live in
the mountains, so they ring the whole drive. Tile it across the windshield behind the road,
scroll it slowly for parallax, and lift it a touch with `reveal`:
```csharp
float scroll = Mathf.Repeat(rig.distance*0.06f - rig.steer*width*0.04f, bandWidth);
float baseY  = horizonY + 6 - rig.reveal*10;      // the range rises as you press on
// draw the band tiled from -bandWidth..width+bandWidth at (x - scroll, baseY - bandHeight)
```
It's the farthest parallax layer; the abstract near ridges (or `MOUNTAIN_BACKDROP` planes) sit
in front for depth.

### 5b · Roadside props (pure scenery, scrolling past)

Chunky 16px world-prop sprites from **`roadpropsgen.js`**, placed on the left/right shoulders
and scaled by the same perspective as the road so they rush past as you drive. **Scenery
only** — no collision, no interaction (procedural buildings/stops come later):

| Sprite | Cell × frames | Animation |
|---|---|---|
| `road_stopsign`  | 24×44 × 4 | creak sway; nightmare leans, rusts + **flickers** |
| `road_deadtree`  | 40×56 × 4 | bare branches sway in the wind |
| `road_crow`      | 16×16 × 4 | flap loop — scatter them across the sky on approach |
| `road_debris`    | 16×12 × 4 | blowing paper/leaves gusting low over the road |

Place them like the shoulder posts — step a spawn `t` from the horizon (0) to the camera (1),
`y = horizon + (bottom-horizon)*t*t`, scale `≈ 1/(0.12+(1-t)*1.7)`, shoulder x offset from the
road edge — and advance the flip-book on a timer. Crows fly faster as `speed` rises (they
**scatter** as you bear down on them); debris only blows when `speed > 0.15`. In Unity, pool a
handful of shoulder `SpriteRenderer`s and recycle them from far to near rather than spawning.
Each has a `_nightmare` twin (rusted sign, blacker trees, green-eyed crows).

## 6 · The rear-view mirror

The mirror frame is a sprite with a **glass hole**; fill the hole one of two ways:

- **Real reflection (recommended):** a second `Camera` pointed backward along the route,
  rendering to a small `RenderTexture` shown on a quad clipped to the glass rect. Multiply its
  brightness / overlay a dark quad by `1 - rig.rearFill` so it **drains as you speed up**.
- **Cheap fake:** render the same parallax rig a second time into the glass rect with the
  scroll reversed and the same `1 - rearFill` dark overlay. Costs nothing extra and reads the
  same at this scale (this is what the DC does).

**The passenger.** Nightmare only: `mirror_passenger_nightmare` (a pale, hollow-eyed figure
you never picked up) fades in over the glass **behind the frame**, on a slow pulse:
```csharp
float a = 0.55f + 0.4f * Mathf.Sin(Time.time*1.3f);
paxRenderer.enabled = rig.nightmare;
paxRenderer.color = new Color(1,1,1, Mathf.Clamp01(a));
```
It surfaces most when you're slow/stopped (the rear view hasn't drained to black) — which is
the moment the player actually looks. The nightmare mirror sprite also carries a cracked-glass
overlay.

---

## 7 · Nightmare mode — swap, don't recolor

Drive the whole cockpit off the **same dread flag** as the truck, dog, houses and mountain
face. Every corruption is **already drawn into the `_nightmare` sprites or driven in code** —
you never tint at runtime:

| Tell | How | Where |
|---|---|---|
| dash comes apart | swap `cockpit_shell_nightmare` | sprite |
| wheel: gore hand + bone rim | swap `steering_wheel_nightmare` | sprite |
| wheel turns on its own | self-turn term (§4) | code |
| gauges lie (speedo spins, fuel full↔empty) | feed the needle a lying value (§4) | code |
| dingy cracked dials | swap `gauge_*_nightmare` | sprite |
| bent needle | swap `needle_nightmare` | sprite |
| all warning lamps trip, green check-engine | swap `warning_lights_nightmare` + flicker | sprite + code |
| odometer runs backward, green | backward count (§4) + swap strip | sprite + code |
| passenger in the mirror | `mirror_passenger_nightmare` fade (§6) | sprite + code |
| cracked mirror | swap `mirror_nightmare` | sprite |
| writhing totem, glowing eyes | swap `charm_nightmare` (8-frame) | sprite |
| gear glitches to R / N on its own | frame flips in code (§4) | code |
| rusted leaning stop signs, black trees, green-eyed crows | swap `road_*_nightmare` | sprite |

Because the twins are pre-drained (same as the truck), a plain sprite-set swap on the flag is
all the visual change needed; no palette LUT. If you *want* a partial-corruption ramp (dash
flips before the mirror does), swap layers independently on their own thresholds.

**Fuel is a real resource that also lies.** Keep `fuel` a genuine draining value that strands
the player at 0 (§3); in nightmare you don't change the resource — you only change what the
**needle is told to show** (§4). Same asset, real stakes, unreal reading.

---

## 8 · Timing & tuning

| Thing | Value | Notes |
|---|---|---|
| speed → mph | ×80 | needle sweep −120°..+120° |
| accel / idle drag / brake drag | 1.6 / 0.28 / 1.6 | feel; raise accel for punchier |
| fuel burn | 0.018 /s at full speed | a tank ≈ 90 s flat-out |
| miles/sec | 22 | odometer + parallax rate |
| reveal distance | 900 mi | miles to fully crest the horizon |
| wheel max angle | 135° | plus nightmare self-turn |
| totem swing spring / damping | 7.0 / 2.2 | lower spring = lazier swing; +bump impulses |
| totem idle | 7 fps, 8 frames | flip-book under the physics swing |
| passenger pulse | sin(t·1.3), α .15–.95 | slow breathing fade |
| mountain scroll | dist×0.06 | farthest parallax band |
| roadside spawn rate | dist×0.4 | props per screen; crows scale with speed |

---

## 9 · Recommended split — engine vs. sprite (summary)

**Ship as sprites:** the shell, wheel (+ baked hands), both numbered dial faces, the needle,
the warning sheet (+labels), the **gear** sheet, the **odometer** digit strip + **tenths**
drum, the mirror frame, the passenger, the **8-frame totem**, and the roadside props +
mountain band — each with a `_nightmare` twin. Static/flip-book art only.

**Do in engine, every frame:** wheel rotation, needle rotation, lamp toggles, gear-frame
select, odometer value + tenths roll, totem flip-book **and** physics swing, the windshield
parallax + reveal + mountain scroll + roadside scroll, the mirror reflection + drain +
passenger fade, and all fuel/speed/distance state. Cheap — and it's the only way the nightmare
tells and the "lying gauge" work without extra art.

**Source art:** `cockpitgen.js` (cockpit + dash + totem) and `roadpropsgen.js` (roadside props
+ mountains) in the art workspace — not part of the Unity project. Palette, geometry, the
anchor table and every sprite are generated there; re-export drops PNGs into the same paths
with the same slicing.
