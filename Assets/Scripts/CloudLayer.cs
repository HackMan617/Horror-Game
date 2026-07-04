using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ambient background clouds for the exterior sky (Assets/Animation/CLOUDS.md). Sibling to
/// <see cref="BirdFlock"/>, but where the birds flock around the player in world space, these ride
/// the <see cref="SkyController"/>'s sky rect as a backdrop: each cloud drifts horizontally across
/// the sky in one of three parallax depth bands (far / mid / near — farther = slower, fainter,
/// smaller), shimmers on a slow 2-frame loop, and is culled once it slides off an edge, then
/// respawned on the opposite side after a random delay.
///
/// Clouds sit just behind the sun/moon plane and above the gradient/stars — they read against both
/// the dusk and the night keyframes, so (unlike the birds, which roost at night) they carry a single
/// neutral palette and linger the whole cycle rather than fading out.
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
    public SkyController sky;       // sky-rect geometry (public skyYaw/Distance/Width/Height/Horizon)
    public Transform viewer;        // for the upright billboard; defaults to Camera.main

    [Header("Population")]
    public int minClouds = 4;
    public int maxClouds = 8;
    [Tooltip("Random delay before a culled cloud is respawned on the opposite edge.")]
    public Vector2 respawnDelay = new Vector2(2f, 7f);

    [Header("Shimmer & drift")]
    [Tooltip("Frames per second of the 2-frame shimmer — slow: clouds are atmosphere, not characters.")]
    public float shimmerFps = 1f;
    [Tooltip("Height band on the sky rect (ny, up from the horizon). Tuned to the exterior camera's " +
             "near-level pitch, which compresses the visible sky into a strip just above the ridge.")]
    public Vector2 heightBand = new Vector2(0.33f, 0.55f);
    [Tooltip("Drift as a fraction of sky width per second, per band (far slowest → near fastest).")]
    public Vector2 farDrift = new Vector2(0.006f, 0.012f);
    public Vector2 midDrift = new Vector2(0.012f, 0.020f);
    public Vector2 nearDrift = new Vector2(0.020f, 0.032f);

    [Header("Depth — distance / scale / alpha per band")]
    [Tooltip("Multiplies SkyController.skyDistance. All > 1 so clouds sit behind the sun/moon on the " +
             "rect; near band closest so it layers in front of the far band.")]
    public float farDepth = 1.08f, midDepth = 1.05f, nearDepth = 1.02f;
    public float farScale = 7f, midScale = 10f, nearScale = 14f;
    [Range(0f, 1f)] public float farAlpha = 0.56f, midAlpha = 0.84f, nearAlpha = 0.96f;

    enum Band { Far, Mid, Near }
    class Cloud
    {
        public Transform tr; public SpriteRenderer sr; public Sprite[] frames;
        public float nx, ny, depth, speed, alpha, phase; public int dir;   // dir: +1 / -1 drift
        public float respawn;   // >0 = waiting off-screen to re-enter
    }

    readonly List<Cloud> _clouds = new List<Cloud>();

    Transform Viewer => viewer != null ? viewer : (Camera.main != null ? Camera.main.transform : null);

    void Start()
    {
        if (sky == null) sky = FindObjectOfType<SkyController>();
        if (sky == null) { enabled = false; return; }

        int n = Random.Range(minClouds, maxClouds + 1);
        for (int i = 0; i < n; i++)
        {
            var c = Spawn();
            if (c != null) c.nx = Random.value;   // seed already scattered across the sky, not entering
        }
    }

    void Update()
    {
        if (sky == null) return;

        // Top up toward the minimum population if culls have thinned the sky.
        if (_clouds.Count < minClouds && Random.value < 0.5f) Spawn();

        var v = Viewer;
        float margin = 0.22f;   // let a cloud fully clear the edge before it's culled
        int frames = Mathf.Max(1, 2);

        for (int i = _clouds.Count - 1; i >= 0; i--)
        {
            var c = _clouds[i];
            if (c.tr == null) { _clouds.RemoveAt(i); continue; }

            if (c.respawn > 0f)
            {
                c.respawn -= Time.deltaTime;
                if (c.respawn <= 0f)
                {
                    c.dir = Random.value < 0.5f ? 1 : -1;
                    c.nx = c.dir > 0 ? -margin : 1f + margin;   // re-enter from the leading edge
                    c.ny = Random.Range(heightBand.x, heightBand.y);
                    c.sr.flipX = Random.value < 0.5f;
                }
                else { c.sr.enabled = false; continue; }
                c.sr.enabled = true;
            }

            c.nx += c.dir * c.speed * Time.deltaTime;

            // shimmer: swap frame 0/1 slowly, each cloud on its own phase so the sky never pulses in unison
            int f = (int)((Time.time + c.phase) * shimmerFps) % frames;
            c.sr.sprite = c.frames[f];

            c.tr.position = RectPoint(c.nx, c.ny, c.depth);

            if (v != null)   // upright billboard toward the camera (yaw only), same as the flock
            {
                Vector3 toCam = v.position - c.tr.position; toCam.y = 0f;
                if (toCam.sqrMagnitude > 1e-4f) c.tr.rotation = Quaternion.LookRotation(toCam, Vector3.up);
            }

            var col = c.sr.color; col.a = c.alpha; c.sr.color = col;

            if (c.nx < -margin || c.nx > 1f + margin)   // drifted off an edge → park it, respawn later
            {
                c.respawn = Random.Range(respawnDelay.x, respawnDelay.y);
                c.sr.enabled = false;
            }
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

        int dir = Random.value < 0.5f ? 1 : -1;
        var c = new Cloud
        {
            tr = go.transform, sr = sr, frames = frames,
            depth = depth, alpha = alpha,
            speed = Random.Range(drift.x, drift.y),
            dir = dir,
            nx = dir > 0 ? -0.22f : 1.22f,          // default: enter from the leading edge (Start reseeds)
            ny = Random.Range(heightBand.x, heightBand.y),
            phase = Random.value * 10f,
        };
        sr.flipX = Random.value < 0.5f;
        c.tr.position = RectPoint(c.nx, c.ny, c.depth);
        _clouds.Add(c);
        return c;
    }

    // World point on the sky rect: nx across the width (0..1), ny up from the horizon (0..1), depth a
    // multiple of skyDistance. Mirrors SkyController.RectPoint but with the per-band depth push so
    // clouds render just behind the sun/moon and in front of the gradient dome.
    Vector3 RectPoint(float nx, float ny, float depth)
    {
        float yaw = sky.skyYawDeg * Mathf.Deg2Rad;
        Vector3 fwd = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
        Vector3 tan = new Vector3(-Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
        Vector3 center = sky.transform.position;
        return center
             + fwd * (sky.skyDistance * depth)
             + tan * ((nx - 0.5f) * sky.skyWidth)
             + Vector3.up * (sky.skyHorizonY + ny * sky.skyHeight);
    }
}
