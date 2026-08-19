// WindowDayNightCycle.cs — drives an interior window through interior_window_cycle.png
// (16 frames, dawn → night) so the view beyond the glass follows the game's hour instead of being
// a single frozen night sprite.
//
// Sheet: 768x40, 16 cells of 48x40 — the same geometry as interior_colddusk_window.png (curtains
// at the edges, mid rail, centre mullion), so it drops onto the renderer that sprite was on. Frame
// 15 lands on the existing night palette, which is where the room used to live permanently.
//
// ONE CHANGE from the version the kit shipped, and it is the whole point of the piece: the clock is
// not its own. The kit's script advanced a private timeOfDay on a private cycleSeconds, which would
// have given every window in the house its own hour, drifting from each other and from the sky
// outside. It reads DayNightClock instead — the DontDestroyOnLoad singleton the sky also runs on —
// so the window shows the same evening the yard did a moment ago, and keeps moving while you are
// indoors. Set `overrideTime` if you ever want one pane deliberately out of step.
//
// Two modes:
//   Stepped   — one renderer, snaps to the nearest frame. Cheapest; reads fine at 16 frames.
//   Crossfade — two stacked renderers, alpha-blends frame N → N+1. Smooth, one extra renderer.
//
// The sheet also drives the ROOM light: SampleLight() returns the colour that matches the current
// frame, so the shaft through the glass follows it automatically — except in the nightmare, which
// owns the light itself (CabinInterior bleeds it red) and must not be fought over every frame.

using UnityEngine;

public class WindowDayNightCycle : MonoBehaviour
{
    public enum Mode { Stepped, Crossfade }

    [Header("Frames — 16, dawn(0) → night(15)")]
    public Sprite[] frames = new Sprite[16];

    [Header("Renderers")]
    public Mode mode = Mode.Crossfade;
    public SpriteRenderer windowRenderer;       // base layer (frame N)
    public SpriteRenderer blendRenderer;        // overlay layer (frame N+1) — Crossfade only

    [Header("Clock")]
    [Tooltip("Ignore DayNightClock and hold the hour below. For a pane that is deliberately out of " +
             "step, or for dressing the room by eye in the editor.")]
    public bool overrideTime = false;
    [Tooltip("0 = dawn · 0.4 = noon · 0.78 = sunset · 1 = deep night.")]
    [Range(0f, 1f)] public float timeOfDay = 1f;

    [Header("Room light (optional) — follows the glass")]
    [Tooltip("The shaft outside this window. Left alone while the room is in its nightmare, which " +
             "drives the same light red.")]
    public Light roomLight;
    [Tooltip("Peak (noon) and floor (night) brightness for the shaft. The installer seeds these from " +
             "the light's own tuned level, so the night the room was built for is preserved exactly.")]
    public float sunIntensity = 6f, nightIntensity = 3.4f;
    [Tooltip("The room this window belongs to, so the light hand-off knows when to keep its hands off.")]
    public CabinInterior room;

    // colours sampled from the sheet's own keyframes, so engine light matches the art
    static readonly float[] KeyT = { 0.00f, 0.18f, 0.40f, 0.62f, 0.78f, 0.90f, 1.00f };
    static readonly Color[] KeyC = {
        new Color(0.94f, 0.78f, 0.60f),   // dawn — warm low sun
        new Color(1.00f, 0.96f, 0.88f),   // morning
        new Color(1.00f, 0.98f, 0.92f),   // noon — near white
        new Color(1.00f, 0.94f, 0.82f),   // afternoon
        new Color(1.00f, 0.72f, 0.42f),   // sunset — deep warm
        new Color(0.62f, 0.60f, 0.72f),   // dusk — cooling
        new Color(0.59f, 0.70f, 0.88f),   // night — moonlight (150,178,224)
    };

    public int FrameCount => frames != null ? frames.Length : 0;

    /// <summary>The hour this window is showing.</summary>
    public float Now => overrideTime ? Mathf.Clamp01(timeOfDay) : DayNightClock.Value01;

    void Start() { Apply(); }
    void Update() { Apply(); }

    public void Apply()
    {
        int n = FrameCount;
        if (n == 0 || windowRenderer == null) return;

        float t = Now;
        float pos = t * (n - 1);
        int i = Mathf.Clamp(Mathf.FloorToInt(pos), 0, n - 1);
        int j = Mathf.Min(i + 1, n - 1);
        float u = pos - i;

        if (frames[i] != null) windowRenderer.sprite = frames[i];
        if (mode == Mode.Crossfade && blendRenderer != null && frames[j] != null)
        {
            blendRenderer.enabled = true;
            blendRenderer.sprite = frames[j];
            var c = blendRenderer.color; c.a = u; blendRenderer.color = c;
        }
        else if (blendRenderer != null) blendRenderer.enabled = false;

        ApplyLight(t);
    }

    void ApplyLight(float t)
    {
        if (roomLight == null) return;
        // The nightmare owns this light — CabinInterior bleeds it red on the flip, and repainting it
        // from the glass every frame would wash that straight back out.
        if (room != null && room.IsNightmare) return;

        roomLight.color = SampleLight(t);
        // An arc rather than a plateau: the sun climbs from nothing at dawn, peaks a little after
        // t=0.4 (noon in this sheet), and is back down by the time the sky goes over at ~0.84. A flat
        // "daytime = bright" ramp made dawn as bright as midday, which is exactly the tell that gives
        // away a faked cycle. Below the arc the room settles on the moonlit level it was built for.
        float dayness = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.84f));
        roomLight.intensity = Mathf.Lerp(nightIntensity, sunIntensity, Mathf.Clamp01(dayness));
    }

    /// <summary>The light colour that matches the glass at time t — the same keyframes the art uses.</summary>
    public static Color SampleLight(float t)
    {
        t = Mathf.Clamp01(t);
        for (int k = 0; k < KeyT.Length - 1; k++)
        {
            if (t <= KeyT[k + 1])
            {
                float u = Mathf.InverseLerp(KeyT[k], KeyT[k + 1], t);
                return Color.Lerp(KeyC[k], KeyC[k + 1], u);
            }
        }
        return KeyC[KeyC.Length - 1];
    }
}
