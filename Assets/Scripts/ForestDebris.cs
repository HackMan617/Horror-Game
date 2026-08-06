using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Leaves and sticks shedding out of the canopy (Assets/Animation/TREES_UNITY.md section 5), adapted
/// from the flat 2D emitter in the handoff to the 2.5D yard: debris drops in a ring around the camera
/// so it always reads, and only over ground that actually has trees over it.
///
/// The rate is tied to <see cref="ForestField.Shake01"/> rather than run free — the woods shed when a
/// gust front is passing or dread is up, so the falling debris is the same wind you can see in the
/// trees, not a separate weather system running underneath them.
///
/// Pooled and code-driven: no ParticleSystem asset and no sprite wiring — the leaf quad is built from
/// <see cref="Texture2D.whiteTexture"/> and tinted per particle from the seasonal palette.
/// </summary>
public class ForestDebris : MonoBehaviour
{
    [Header("Source of the wind (rate scales with its Shake01)")]
    public ForestField field;

    [Header("Rates (per second: at rest -> during a gust)")]
    public float leafRate = 2.5f;
    public float stickRate = 0.35f;
    public float gustMultiplier = 7f;

    [Header("Where it falls (ring around the camera, canopy height)")]
    public float innerRadius = 5f, outerRadius = 18f;
    public float canopyLow = 5f, canopyHigh = 10f;
    public float floorY = 0.05f;
    [Tooltip("Debris only sheds over ground with trees over it — this is the clearing it skips.")]
    public Vector3 forestCentre = new Vector3(0f, 0f, 6f);
    public float clearingRadius = 11f;

    [Header("Look")]
    public Material spriteMaterial;
    public float leafSize = 0.16f;
    public int maxLive = 140;
    public bool winter;

    // Palettes straight from TREES_UNITY.md: autumn canopy, then the cold bare/snow mix.
    static readonly Color[] Summer = {
        new Color(0.36f, 0.54f, 0.23f), new Color(0.47f, 0.62f, 0.29f), new Color(0.79f, 0.47f, 0.18f),
        new Color(0.66f, 0.35f, 0.16f), new Color(0.85f, 0.60f, 0.24f) };
    static readonly Color[] Winter = {
        new Color(0.54f, 0.42f, 0.23f), new Color(0.72f, 0.63f, 0.42f),
        new Color(0.36f, 0.42f, 0.29f), new Color(0.85f, 0.88f, 0.92f) };
    static readonly Color StickColor = new Color(0.42f, 0.29f, 0.17f);

    class P
    {
        public Transform t; public SpriteRenderer sr;
        public Vector3 vel; public float spin, spinV, seed, age; public bool stick;
        public Vector3 drift;
    }

    readonly List<P> _live = new List<P>();
    readonly Queue<P> _leafPool = new Queue<P>();   // kept apart so a leaf burst never eats the sticks
    readonly Queue<P> _stickPool = new Queue<P>();
    Sprite _quad;
    Transform _cam;
    float _leafAcc, _stickAcc;

    void Awake()
    {
        // A plain white quad; every particle is a tint of it. texture px / ppu = leafSize world units.
        var tex = Texture2D.whiteTexture;
        _quad = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f),
                              tex.width / Mathf.Max(0.001f, leafSize));
        _quad.name = "ForestDebrisQuad";
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (_cam == null || !_cam.gameObject.activeInHierarchy)
        {
            var c = Camera.main;
            _cam = c != null ? c.transform : null;
        }

        float shake = field != null ? field.Shake01 : 0f;
        float mul = Mathf.Lerp(1f, gustMultiplier, shake);
        _leafAcc += leafRate * mul * dt;
        _stickAcc += stickRate * mul * dt;
        while (_leafAcc >= 1f) { _leafAcc -= 1f; Emit(false); }
        while (_stickAcc >= 1f) { _stickAcc -= 1f; Emit(true); }

        Vector3 camPos = _cam != null ? _cam.position : Vector3.zero;

        for (int i = _live.Count - 1; i >= 0; i--)
        {
            var p = _live[i];
            p.age += dt;
            p.vel.y -= (p.stick ? 3.2f : 1.4f) * dt;                       // sticks drop, leaves dawdle
            p.vel.y = Mathf.Max(p.vel.y, p.stick ? -6f : -1.8f);           // terminal velocity
            float flutter = p.stick ? 0f : Mathf.Sin(p.age * 5.5f + p.seed) * 0.55f;
            p.t.position += (p.vel + p.drift * flutter) * dt;

            p.spin += p.spinV * dt;

            // Face the camera, then roll about the view axis; leaves also squash on the roll so they
            // "flip" edge-on the way a falling leaf does.
            Vector3 toCam = camPos - p.t.position; toCam.y = 0f;
            float yaw = toCam.sqrMagnitude > 1e-6f ? Mathf.Atan2(toCam.x, toCam.z) * Mathf.Rad2Deg : 0f;
            p.t.rotation = Quaternion.Euler(0f, yaw, p.spin * Mathf.Rad2Deg);
            p.t.localScale = p.stick
                ? new Vector3(0.34f, 2.1f, 1f)
                : new Vector3(Mathf.Abs(Mathf.Cos(p.spin)) * 0.9f + 0.1f, 1f, 1f);

            var c = p.sr.color;
            c.a = Mathf.Clamp01((p.t.position.y - floorY) / 1.5f + 0.2f);   // settle out as it lands
            p.sr.color = c;

            if (p.t.position.y <= floorY || p.age > 14f) Recycle(p, i);
        }
    }

    void Emit(bool stick)
    {
        if (_cam == null || _live.Count >= maxLive) return;

        // A point in the ring around the camera that still has canopy over it (skip the open clearing).
        Vector3 pos = Vector3.zero;
        bool found = false;
        for (int tries = 0; tries < 4 && !found; tries++)
        {
            float a = Random.value * Mathf.PI * 2f;
            float r = Mathf.Lerp(innerRadius, outerRadius, Random.value);
            pos = _cam.position + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            pos.y = Random.Range(canopyLow, canopyHigh);
            float dx = pos.x - forestCentre.x, dz = pos.z - forestCentre.z;
            found = dx * dx + dz * dz >= clearingRadius * clearingRadius;
        }
        if (!found) return;

        var pool = stick ? _stickPool : _leafPool;
        P p = pool.Count > 0 ? pool.Dequeue() : NewP(stick);

        p.t.gameObject.SetActive(true);
        p.t.position = pos;
        p.age = 0f;
        p.vel = new Vector3(0f, -(0.3f + Random.value * 0.5f), 0f);
        float wind = Random.value * Mathf.PI * 2f;
        p.drift = new Vector3(Mathf.Cos(wind), 0f, Mathf.Sin(wind));
        p.vel += p.drift * (stick ? 0.2f : 0.5f) * Random.value;
        p.spin = Random.value * 6.28f;
        p.spinV = (Random.value - 0.5f) * (stick ? 3f : 5f);
        p.seed = Random.value * 6.28f;
        var pal = winter ? Winter : Summer;
        p.sr.color = stick ? StickColor : pal[Random.Range(0, pal.Length)];
        _live.Add(p);
    }

    P NewP(bool stick)
    {
        var go = new GameObject(stick ? "Stick" : "Leaf");
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _quad;
        if (spriteMaterial != null) sr.sharedMaterial = spriteMaterial;
        return new P { t = go.transform, sr = sr, stick = stick };
    }

    void Recycle(P p, int i)
    {
        p.t.gameObject.SetActive(false);
        _live.RemoveAt(i);
        (p.stick ? _stickPool : _leafPool).Enqueue(p);
    }

    public void SetWinter(bool w) => winter = w;
}
