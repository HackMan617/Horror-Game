# DRIVING.md — First-person cockpit & the transport system (Unity 2D)

The interior view you get when you **drive** — the counterpart to the exterior truck in
`CAR.md`. A worn 1940s–50s cabin (matching the truck's faded-teal paint, cream crash-pad,
warm rust and ivory dials) with a steering wheel, a speedo + fuel gauge, a warning-light
cluster, a rear-view mirror and a pine air-freshener swinging off it. It has a cold
`_nightmare` twin where the dash comes apart.

> **The one rule that matters: almost nothing is baked.** The cockpit ships as *separate
> layers* and the game drives them live — the wheel rotates, the needles rotate, the charm
> swings on a pendulum, the lamps toggle, the mirror composites a real rear view, and the
> road beyond the windshield is the parallax rig, not a picture. That is what makes the
> nightmare corruptions (lying gauges, a wheel that turns itself, a backward odometer, the
> passenger in the mirror) possible without any extra art. Bake the cockpit into one image
> and you lose all of it.

Authoring rules as everywhere else: **point/nearest filtering, integer scale, real alpha**,
a `home` file + a cold `_nightmare` twin per sprite. `-8x` files are preview blow-ups —
**do not ship them**. Regenerator: `cockpitgen.js` (palette, geometry, gore all live there).

Live reference (drive it): `Cockpit Drive.dc.html`.

---

## 1 · File inventory & layer stack

The cockpit is a stack of sprites on an overlay, drawn back → front. Everything is authored
in a **260 × 180 "shell frame"** (the windshield opening is transparent); show it at an
integer scale to fill the viewport (the reference runs it ×2 = 520 × 360).

| Sort | Layer | File (`_nightmare` twin each) | Cell | Pivot | Driven live? |
|---|---|---|---|---|---|
| 0 | windshield parallax | *(no sprite — the ridge/road rig, §5)* | — | — | **yes** |
| 10 | cockpit shell | `cockpit_shell` | 260×180 | — (full frame) | static |
| 20 | speedo face | `gauge_speed` | 60×60 | center | static |
| 20 | fuel face | `gauge_fuel` | 60×60 | center | static |
| 21 | needle ×2 | `needle` | 60×60 | **center** | **rotates** |
| 22 | warning lamps | `warning_lights` (sheet 144×48) | 24×24 cells | — | **toggles** |
| 25 | odometer | `odometer_digits` (strip 90×13) | 9×13 digit | — | **counts** |
| 30 | rear-view mirror glass | *(RenderTexture / faked, §6)* | — | — | **yes** |
| 31 | mirror passenger | `mirror_passenger_nightmare` | 80×52 | — | **fades (nightmare)** |
| 32 | mirror frame | `mirror` | 104×40 | center | static |
| 40 | charm | `charm` | 26×52 | **top-center** | **swings** |
| 50 | steering wheel (+ hands) | `steering_wheel` | 140×140 | **center** | **rotates** |

The warning sheet is **6 icons × 2 rows** — row 0 = unlit, row 1 = lit. Icons L→R:
oil, temp, battery, check-engine, high-beam, seat-belt. Cell `(col*24, row*24, 24, 24)`.

The odometer strip is digits **0–9**, cell `(d*9, 0, 9, 13)`.

### Anchors (shell-frame px; multiply by your display scale)

```
windshield hole : x 16..244,  y 6..100      (transparent; the road shows through)
speedo center   : (100, 104)  r 26
fuel center     : (158, 108)  r 17
warning grid    : center (202,104), 2×2, 15px pitch, 12px lamp
odometer window : x 86, y 122   (5 digits, 9px pitch)
steering wheel  : center (130, 192)   — only the top arc shows above the dash
mirror          : center (130, 22),  glass rect x12..92 y8..34 (local to the mirror sprite)
charm pivot     : (176, 34)   — hangs off the mirror's right ear
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

### Odometer — digit strip, up in home / **backward** in nightmare
```csharp
int odo = rig.nightmare
    ? Mathf.Max(0, 99999 - Mathf.FloorToInt(rig.distance*3f) % 100000)   // runs backward
    : Mathf.FloorToInt(rig.distance) % 100000;
// render each digit by blitting cell (d*9,0,9,13) from the strip into 5 slots
```
Give each of the 5 slots a `SpriteRenderer` and assign the sliced digit sprite by value, or use
one mesh with per-quad UVs. The nightmare strip lights the digits green.

### Charm — a one-line pendulum
Pivot is the **top** of the sprite; swing it with lateral accel + a spring restore:
```csharp
public Transform charm; float a, v;
void SwingCharm() {
    float lateral = -rig.steer * rig.speed * 3.4f
                  - (rig.nightmare ? Mathf.Sin(Time.time*3f)*0.4f : 0f);
    v += (lateral - a*7f - v*2.2f) * Time.deltaTime;   // spring + damping
    a += v * Time.deltaTime;
    charm.localEulerAngles = new Vector3(0,0, a * Mathf.Rad2Deg);
}
```
Nightmare charm is the withered sprite (swap, don't recolor).

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

---

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
| withered charm | swap `charm_nightmare` | sprite |

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
| charm spring / damping | 7.0 / 2.2 | lower spring = lazier swing |
| passenger pulse | sin(t·1.3), α .15–.95 | slow breathing fade |

---

## 9 · Recommended split — engine vs. sprite (summary)

**Ship as sprites:** the shell, wheel (+ baked hands), both dial faces, the needle, the warning
sheet, the mirror frame, the passenger, the charm, the digit strip — each with a `_nightmare`
twin. Static art only.

**Do in engine, every frame:** wheel rotation, needle rotation, lamp toggles, odometer value,
charm pendulum, the windshield parallax + reveal, the mirror reflection + drain + passenger
fade, and all fuel/speed/distance state. This is ~11 tiny transforms and one parallax rig — cheap,
and it's the only way the nightmare tells and the "lying gauge" work without extra art.

**Source art:** `cockpitgen.js` in the art workspace (not part of the Unity project) — palette,
gore vocabulary, anchor table and every sprite are generated there; re-export drops PNGs into
the same paths with the same slicing.
