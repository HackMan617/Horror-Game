using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ambient distant flock for the exterior sky (Assets/Animation/BIRDS.md). Keeps a handful of
/// silhouette birds drifting across the sky above the player: each picks a size (weighted toward the
/// smaller/farther ones), a height band, a heading and a speed, flaps through its 4-frame loop, and is
/// culled once it drifts past the range — then respawned on a random timer. Birds roost at night: they
/// stop spawning and fade out with the sky's Darkness, so they only fill the daytime sky.
/// </summary>
public class BirdFlock : MonoBehaviour
{
    [Header("Frames (4 each — flap loop: up / level / down / level)")]
    public Sprite[] farFrames;
    public Sprite[] midFrames;
    public Sprite[] nearFrames;
    public Material material;

    [Header("Refs")]
    public Transform viewer;            // defaults to Camera.main
    public SkyController sky;           // optional: fade the flock out at night

    [Header("Population")]
    public int maxBirds = 5;
    public Vector2 spawnDelay = new Vector2(1.5f, 4f);
    [Tooltip("Above this sky Darkness the flock stops spawning and has fully faded (roosting).")]
    [Range(0f, 1f)] public float nightThreshold = 0.6f;

    [Header("Flight")]
    public float flapFps = 7f;
    [Tooltip("Spawn just outside this horizontal distance from the viewer; cull past it.")]
    public float range = 70f;
    public Vector2 heightBand = new Vector2(16f, 38f);   // world Y band
    public Vector2 farSpeed = new Vector2(3f, 5f);
    public Vector2 midSpeed = new Vector2(5f, 8f);
    public Vector2 nearSpeed = new Vector2(8f, 12f);     // nearest = fastest (parallax)
    public float farScale = 1.6f, midScale = 2.3f, nearScale = 3.1f;

    class Bird { public Transform tr; public SpriteRenderer sr; public Sprite[] frames; public Vector3 vel; public float phase; }
    readonly List<Bird> _birds = new List<Bird>();
    float _spawnTimer;

    void Start() { _spawnTimer = Random.Range(0.2f, 1.5f); }

    Transform Viewer => viewer != null ? viewer : (Camera.main != null ? Camera.main.transform : null);
    float Darkness => sky != null ? sky.Darkness : 0f;

    void Update()
    {
        var v = Viewer;
        if (v == null) return;
        bool day = Darkness < nightThreshold;

        _spawnTimer -= Time.deltaTime;
        if (_spawnTimer <= 0f)
        {
            _spawnTimer = Random.Range(spawnDelay.x, spawnDelay.y);
            if (day && _birds.Count < maxBirds)
            {
                int burst = Random.Range(2, 4);                       // 2-3 at a time
                for (int i = 0; i < burst && _birds.Count < maxBirds; i++) Spawn(v.position);
            }
        }

        float alpha = Mathf.Clamp01(1f - Darkness / Mathf.Max(0.01f, nightThreshold));
        for (int i = _birds.Count - 1; i >= 0; i--)
        {
            var b = _birds[i];
            if (b.tr == null) { _birds.RemoveAt(i); continue; }

            b.tr.position += b.vel * Time.deltaTime;

            int f = (int)((Time.time + b.phase) * flapFps) % 4;
            b.sr.sprite = b.frames[f];

            Vector3 toCam = v.position - b.tr.position; toCam.y = 0f;   // billboard (upright silhouette)
            if (toCam.sqrMagnitude > 1e-4f) b.tr.rotation = Quaternion.LookRotation(toCam, Vector3.up);
            b.sr.flipX = b.vel.x < 0f;                                  // mirror for left/right flight

            var c = b.sr.color; c.a = alpha; b.sr.color = c;

            Vector3 d = b.tr.position - v.position; d.y = 0f;
            if (d.magnitude > range * 1.25f) { Destroy(b.tr.gameObject); _birds.RemoveAt(i); }
        }
    }

    void Spawn(Vector3 viewerPos)
    {
        float r = Random.value;                                        // weighted to far/mid
        Sprite[] frames; float scale; Vector2 spd;
        if (r < 0.45f)      { frames = farFrames;  scale = farScale;  spd = farSpeed; }
        else if (r < 0.80f) { frames = midFrames;  scale = midScale;  spd = midSpeed; }
        else                { frames = nearFrames; scale = nearScale; spd = nearSpeed; }
        if (frames == null || frames.Length == 0) return;

        float ang = Random.Range(0f, Mathf.PI * 2f);
        Vector3 heading = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang) * 0.35f).normalized;   // mostly across
        Vector3 vel = heading * Random.Range(spd.x, spd.y);

        Vector3 lateral = Vector3.Cross(Vector3.up, heading);
        Vector3 pos = viewerPos - heading * range + lateral * Random.Range(-range * 0.5f, range * 0.5f);
        pos.y = Random.Range(heightBand.x, heightBand.y);

        var go = new GameObject("Bird");
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = frames[0];
        sr.sharedMaterial = material;

        _birds.Add(new Bird { tr = go.transform, sr = sr, frames = frames, vel = vel, phase = Random.value * 10f });
    }
}
