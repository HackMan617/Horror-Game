using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ambient background clouds for the exterior sky (Assets/Animation/CLOUDS.md). Sibling to
/// <see cref="BirdFlock"/>, but where the birds flock around the player in world space, these ride
/// the <see cref="SkyController"/>'s sky DOME as a backdrop: each cloud sits at a compass azimuth
/// around the FULL sky (not just the scenic sun/moon side), at a low elevation band, and drifts
/// slowly in azimuth — wrapping right around the sky so it never leaves. It shimmers on a slow
/// 2-frame loop and layers by parallax depth band (far / mid / near — farther = slower, fainter,
/// smaller).
///
/// Clouds sit just behind the sun/moon plane and above the gradient/stars — they read against both
/// the dusk and the night keyframes, so (unlike the birds, which roost at night) they carry a single
/// neutral palette and linger the whole cycle. They wrap the dome the same way the stars do
/// (<see cref="SkyController"/>.DomePoint), so they fill every viewing angle instead of clustering on
/// the one-sided sky rect the sun and moon ride.
/// </summary>
public class CloudLayer : MonoBehaviour
{
    [Header("Sprites — 2 frames each (frame 0 = fleck on, frame 1 = shimmer)")]
    public Sprite[] wispFrames;    // far band
    public Sprite[] smallFrames;   // far band
    public Sprite[] medFrames;     // mid band
    public Sprite[] largeFrames;   // mid band
    public Sprite[] heroFrames;    // near band
    public Material material;

    [Header("Refs")]
    public SkyController sky;       // sky geometry (public skyYaw/Distance/Horizon)
    public Transform viewer;        // for the upright billboard; defaults to Camera.main

    [Header("Population")]
    [Tooltip("Clouds are spread around the whole 360° sky, so more are needed than the old one-sided strip.")]
    public int minClouds = 14;
    public int maxClouds = 22;

    [Header("Shimmer & drift")]
    [Tooltip("Frames per second of the 2-frame shimmer — slow: clouds are atmosphere, not characters.")]
    public float shimmerFps = 1f;
    [Tooltip("Elevation band on the sky dome (0 = horizon, 1 = zenith). A low band keeps clouds just above " +
             "the ridge, all the way around the sky.")]
    public Vector2 elevationBand = new Vector2(0.18f, 0.38f);
    [Tooltip("Drift speed in azimuth DEGREES PER SECOND, per band (far slowest → near fastest). Clouds " +
             "circle the whole sky and wrap, so there is no off-screen cull.")]
    public Vector2 farDrift = new Vector2(0.4f, 0.8f);
    public Vector2 midDrift = new Vector2(0.8f, 1.4f);
    public Vector2 nearDrift = new Vector2(1.4f, 2.2f);

    [Header("Depth — distance / scale / alpha per band")]
    [Tooltip("Multiplies SkyController.skyDistance for the cloud's dome radius. All > 1 so clouds sit behind " +
             "the sun/moon; near band closest so it layers in front of the far band.")]
    public float farDepth = 1.08f, midDepth = 1.05f, nearDepth = 1.02f;
    public float farScale = 7f, midScale = 10f, nearScale = 14f;
    [Range(0f, 1f)] public float farAlpha = 0.56f, midAlpha = 0.84f, nearAlpha = 0.96f;

    enum Band { Far, Mid, Near }
    class Cloud
    {
        public Transform tr; public SpriteRenderer sr; public Sprite[] frames;
        public float az, el, depth, speed, alpha, phase; public int dir;   // az in degrees; dir: +1 / -1
    }

    readonly List<Cloud> _clouds = new List<Cloud>();

    Transform Viewer => viewer != null ? viewer : (Camera.main != null ? Camera.main.transform : null);

    void Start()
    {
        if (sky == null) sky = FindObjectOfType<SkyController>();
        if (sky == null) { enabled = false; return; }

        int n = Random.Range(minClouds, maxClouds + 1);
        for (int i = 0; i < n; i++) Spawn();   // Spawn already scatters az across the whole sky
    }

    void Update()
    {
        if (sky == null) return;

        // Top up toward the minimum population if anything was lost.
        if (_clouds.Count < minClouds && Random.value < 0.5f) Spawn();

        var v = Viewer;
        const int frames = 2;

        for (int i = _clouds.Count - 1; i >= 0; i--)
        {
            var c = _clouds[i];
            if (c.tr == null) { _clouds.RemoveAt(i); continue; }

            c.az = Mathf.Repeat(c.az + c.dir * c.speed * Time.deltaTime, 360f);   // circle the sky, wrapping

            // shimmer: swap frame 0/1 slowly, each cloud on its own phase so the sky never pulses in unison
            int f = (int)((Time.time + c.phase) * shimmerFps) % frames;
            c.sr.sprite = c.frames[f];

            c.tr.position = DomePoint(c.az, c.el, sky.skyDistance * c.depth);

            if (v != null)   // upright billboard toward the camera (yaw only), same as the flock
            {
                Vector3 toCam = v.position - c.tr.position; toCam.y = 0f;
                if (toCam.sqrMagnitude > 1e-4f) c.tr.rotation = Quaternion.LookRotation(toCam, Vector3.up);
            }

            var col = c.sr.color; col.a = c.alpha; c.sr.color = col;
        }
    }

    Cloud Spawn()
    {
        if (_clouds.Count >= maxClouds) return null;

        float r = Random.value;                    // weighted toward far / mid, sparse hero clouds
        Band band = r < 0.45f ? Band.Far : (r < 0.80f ? Band.Mid : Band.Near);

        Sprite[] frames; float depth, scale, alpha; Vector2 drift;
        switch (band)
        {
            case Band.Far:
                frames = Random.value < 0.5f ? wispFrames : smallFrames;
                depth = farDepth; scale = farScale; alpha = farAlpha; drift = farDrift; break;
            case Band.Mid:
                frames = Random.value < 0.5f ? medFrames : largeFrames;
                depth = midDepth; scale = midScale; alpha = midAlpha; drift = midDrift; break;
            default:
                frames = heroFrames;
                depth = nearDepth; scale = nearScale; alpha = nearAlpha; drift = nearDrift; break;
        }
        if (frames == null || frames.Length == 0) return null;

        var go = new GameObject("Cloud");
        go.transform.SetParent(transform, false);
        go.transform.localScale = Vector3.one * scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = frames[0];
        sr.sharedMaterial = material;

        var c = new Cloud
        {
            tr = go.transform, sr = sr, frames = frames,
            depth = depth, alpha = alpha,
            speed = Random.Range(drift.x, drift.y),
            dir = Random.value < 0.5f ? 1 : -1,
            az = Random.Range(0f, 360f),                        // scattered around the whole sky
            el = Random.Range(elevationBand.x, elevationBand.y),
            phase = Random.value * 10f,
        };
        sr.flipX = Random.value < 0.5f;
        c.tr.position = DomePoint(c.az, c.el, sky.skyDistance * c.depth);
        _clouds.Add(c);
        return c;
    }

    // World point on the sky dome from a compass azimuth and elevation (0 = horizon, 1 = zenith) at the
    // given radius. Mirrors SkyController.DomePoint so clouds wrap every viewing angle like the stars,
    // rather than riding the one-sided sun/moon rect.
    Vector3 DomePoint(float azDeg, float el01, float radius)
    {
        float az = azDeg * Mathf.Deg2Rad;
        float el = Mathf.Clamp01(el01) * (Mathf.PI * 0.5f);
        float cosEl = Mathf.Cos(el);
        Vector3 dir = new Vector3(cosEl * Mathf.Cos(az), Mathf.Sin(el), cosEl * Mathf.Sin(az));
        return sky.transform.position + dir * radius + Vector3.up * sky.skyHorizonY;
    }
}
