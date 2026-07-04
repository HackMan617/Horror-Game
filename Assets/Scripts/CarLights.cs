using UnityEngine;

/// <summary>
/// Night-time truck effects, driven live at playback per Assets/Animation/Car/roadside_pack/CAR.md
/// (never baked into the atlas). When the sky's <see cref="SkyController.Darkness"/> crosses
/// <see cref="nightThreshold"/> the parked wagon "wakes up": headlights glow, the hazard lamps blink,
/// and an idle engine rumble jitters the whole sprite. All three read against whichever of the eight
/// views is currently facing the camera, using the per-view anchor table from CAR.md (mirrored when
/// the view is a flipped one).
///
/// The light sprites are runtime children of the billboarded truck, so they track its rotation and
/// scale for free; nothing is serialized into the scene. Headlights are a home-truck feature — the
/// nightmare twin sets <see cref="headlights"/> false (its lamps are dead).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(DirectionalSprite))]
public class CarLights : MonoBehaviour
{
    [Tooltip("Sky whose Darkness gates the effects. Found in the scene if left null.")]
    public SkyController sky;
    [Range(0f, 1f)] public float nightThreshold = 0.5f;
    [Tooltip("Home truck only — the nightmare twin's lamps are dead.")]
    public bool headlights = true;
    [Tooltip("Hazard blink period (CAR.md ≈ 430 ms).")]
    public float blinkInterval = 0.43f;
    [Tooltip("Idle rumble amplitude in cell px (CAR.md ≈ 0.8).")]
    public float rumblePx = 0.8f;
    [Tooltip("How often the rumble offset is refreshed (CAR.md ≈ 110 ms idle).")]
    public float rumbleInterval = 0.11f;

    const float CellH = 32f;   // 64×32 authoring cell (CAR.md)

    SpriteRenderer _truck;
    DirectionalSprite _dir;
    Material _mat;
    Sprite _dot, _cone;
    Vector3 _basePos;
    float _rumbleT;
    Vector3 _rumbleOff;

    // pooled light children (max 2 headlights + 2 blink lamps per view)
    SpriteRenderer _core0, _core1, _halo0, _halo1, _cone0, _cone1, _blink0, _blink1;

    struct ViewFx { public Vector2[] blink; public bool red; public Vector2[] head; public Vector2 hdir; }
    ViewFx _front, _front3q, _side, _back3q, _back;

    static Vector2 V(float x, float y) => new Vector2(x, y);

    // Init in OnEnable (not Awake) so the light children are rebuilt after a domain reload too — the
    // runtime field refs don't survive a reload, but the child GameObjects can, so clear them first.
    void OnEnable()
    {
        _truck = GetComponent<SpriteRenderer>();
        _dir = GetComponent<DirectionalSprite>();
        if (sky == null) sky = FindObjectOfType<SkyController>();
        _basePos = transform.position;

        var stale = new System.Collections.Generic.List<GameObject>();
        foreach (Transform c in transform)
            if (c.name.StartsWith("Head") || c.name.StartsWith("Blink")) stale.Add(c.gameObject);
        foreach (var g in stale) DestroyImmediate(g);

        _mat = new Material(Shader.Find("Sprites/Default"));
        _dot = MakeDot();
        _cone = MakeCone();

        // build back-to-front so the core reads on top of its halo/cone
        _cone0 = Child("HeadCone0", _cone); _cone1 = Child("HeadCone1", _cone);
        _halo0 = Child("HeadHalo0", _dot);  _halo1 = Child("HeadHalo1", _dot);
        _core0 = Child("HeadCore0", _dot);  _core1 = Child("HeadCore1", _dot);
        _blink0 = Child("Blink0", _dot);    _blink1 = Child("Blink1", _dot);

        // Per-view anchors (local px in the 64×32 cell, top-left origin) — CAR.md ANCH table.
        _front   = new ViewFx { blink = new[] { V(21, 20), V(43, 20) }, red = false, head = new[] { V(24, 18), V(40, 18) }, hdir = V(0, 1) };
        _back    = new ViewFx { blink = new[] { V(23, 23), V(41, 23) }, red = true,  head = null,                            hdir = V(0, 0) };
        _side    = new ViewFx { blink = new[] { V(54, 24) },           red = false, head = new[] { V(57, 17) },            hdir = V(1, 0) };
        _front3q = new ViewFx { blink = new[] { V(45, 24) },           red = false, head = new[] { V(52, 18), V(46, 17) }, hdir = V(1, 1) };
        _back3q  = new ViewFx { blink = new[] { V(43, 23), V(50, 23) }, red = true,  head = null,                            hdir = V(0, 0) };

        AllOff();
    }

    SpriteRenderer Child(string n, Sprite s)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.sharedMaterial = _mat;
        sr.enabled = false;
        return sr;
    }

    void LateUpdate()
    {
        float dark = sky != null ? sky.Darkness : 0f;
        bool on = dark >= nightThreshold;

        // --- idle rumble: jitter the whole sprite while the engine is "running" ---
        if (on)
        {
            _rumbleT -= Time.deltaTime;
            if (_rumbleT <= 0f)
            {
                _rumbleT = rumbleInterval;
                float a = rumblePx / Mathf.Max(1f, _truck.sprite.pixelsPerUnit);
                _rumbleOff = transform.right * Random.Range(-a, a) + Vector3.up * Random.Range(-a, a);
            }
            transform.position = _basePos + _rumbleOff;
        }
        else if (transform.position != _basePos)
        {
            transform.position = _basePos;
        }

        if (!on) { AllOff(); return; }

        // pick the current view + whether it's a flipped one (mirror the anchors)
        bool flip = false; ViewFx fx;
        switch (_dir.CurrentSector)
        {
            case 0: fx = _back; break;                         // N
            case 1: fx = _back3q; break;                       // NE
            case 2: fx = _side; break;                         // E
            case 3: fx = _front3q; break;                      // SE
            case 4: fx = _front; break;                        // S
            case 5: fx = _front3q; flip = true; break;         // SW
            case 6: fx = _side; flip = true; break;            // W
            default: fx = _back3q; flip = true; break;         // NW
        }

        var s = _truck.sprite;

        // --- headlights (steady) ---
        bool lit = headlights && fx.head != null && fx.head.Length > 0;
        SetHead(_core0, _halo0, _cone0, lit && fx.head.Length > 0, fx, 0, flip, s);
        SetHead(_core1, _halo1, _cone1, lit && fx.head.Length > 1, fx, 1, flip, s);

        // --- hazard blinkers (all lamps blink in unison) ---
        bool blinkOn = ((int)(Time.time / blinkInterval)) % 2 == 0;
        Color bc = fx.red ? new Color(1f, 0.20f, 0.13f) : new Color(1f, 0.66f, 0.13f);
        SetBlink(_blink0, fx.blink != null && fx.blink.Length > 0 && blinkOn, fx.blink, 0, bc, flip, s);
        SetBlink(_blink1, fx.blink != null && fx.blink.Length > 1 && blinkOn, fx.blink, 1, bc, flip, s);
    }

    void SetHead(SpriteRenderer core, SpriteRenderer halo, SpriteRenderer cone, bool en, ViewFx fx, int i, bool flip, Sprite s)
    {
        if (!en) { core.enabled = halo.enabled = cone.enabled = false; return; }

        Vector3 p = Local(fx.head[i], flip, s, 0.03f);
        core.transform.localPosition = p;
        core.transform.localScale = Vector3.one * 0.26f;
        core.color = new Color(1f, 1f, 0.92f, 1f);
        core.enabled = true;

        halo.transform.localPosition = new Vector3(p.x, p.y, 0.02f);
        halo.transform.localScale = Vector3.one * 0.80f;
        halo.color = new Color(1f, 0.90f, 0.60f, 0.70f);
        halo.enabled = true;

        // soft beam cone along the view's headlight direction (screen space: +x right, up = -cellY)
        Vector2 dir = new Vector2(flip ? -fx.hdir.x : fx.hdir.x, -fx.hdir.y);
        if (dir.sqrMagnitude < 1e-4f) dir = new Vector2(0f, -1f);
        dir.Normalize();
        float ang = Mathf.Atan2(-dir.x, dir.y) * Mathf.Rad2Deg;   // rotate sprite's +Y onto dir
        cone.transform.localPosition = new Vector3(p.x, p.y, 0.015f);
        cone.transform.localRotation = Quaternion.Euler(0f, 0f, ang);
        cone.transform.localScale = new Vector3(0.85f, 1.25f, 1f);
        cone.color = new Color(1f, 0.92f, 0.66f, 0.42f);
        cone.enabled = true;
    }

    void SetBlink(SpriteRenderer sr, bool en, Vector2[] pts, int i, Color c, bool flip, Sprite s)
    {
        if (!en) { sr.enabled = false; return; }
        sr.transform.localPosition = Local(pts[i], flip, s, 0.04f);
        sr.transform.localScale = Vector3.one * 0.34f;
        sr.color = c;
        sr.enabled = true;
    }

    // Cell anchor (top-left origin) -> local offset from the truck's centre pivot. Per-view trim is
    // uniform across frames, so the current sprite's rect gives the pivot's cell position directly.
    Vector3 Local(Vector2 cell, bool flip, Sprite s, float z)
    {
        float pivX = (s.rect.x % 64f) + s.pivot.x;
        float pivY = s.rect.y + s.pivot.y;
        float ox = (cell.x - pivX) / s.pixelsPerUnit;
        float oy = ((CellH - cell.y) - pivY) / s.pixelsPerUnit;
        if (flip) ox = -ox;
        return new Vector3(ox, oy, z);
    }

    void AllOff()
    {
        _core0.enabled = _core1.enabled = _halo0.enabled = _halo1.enabled = false;
        _cone0.enabled = _cone1.enabled = _blink0.enabled = _blink1.enabled = false;
    }

    // soft round glow — cores, halos, blink lamps
    static Sprite MakeDot()
    {
        const int S = 16;
        var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "LightDot" };
        float c = (S - 1) * 0.5f;
        var px = new Color32[S * S];
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / (S * 0.5f);
            float a = Mathf.Pow(Mathf.Clamp01(1f - d), 1.7f);
            px[y * S + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
        }
        t.SetPixels32(px); t.Apply();
        return Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
    }

    // soft cone — apex near the bottom, widening/fading upward (pivot near the apex)
    static Sprite MakeCone()
    {
        const int W = 24, H = 40;
        var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "LightCone" };
        var px = new Color32[W * H];
        for (int y = 0; y < H; y++)
        {
            float v = y / (float)(H - 1);                 // 0 = apex (bottom), 1 = far end
            float halfW = Mathf.Lerp(1.2f, W * 0.5f, v);  // widen with distance
            float along = 1f - v;                         // brightest at the apex
            for (int x = 0; x < W; x++)
            {
                float dx = Mathf.Abs(x - (W - 1) * 0.5f);
                float across = Mathf.Clamp01(1f - dx / halfW);
                float a = along * across * across;
                px[y * W + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
            }
        }
        t.SetPixels32(px); t.Apply();
        return Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0.06f), H);  // 1 world unit long at scale 1
    }
}
