using UnityEngine;

/// <summary>
/// Dawn→dark sky system (Assets/Animation/SKY_README.md). One value, <see cref="timeOfDay"/> 0→1
/// (0 = morning, 1 = darkest night), drives everything: a vertical day gradient baked each frame
/// onto a dome behind the mountains, a sun sprite that arcs across and sets, a moon that rises into
/// night, and a field of stars that fade in with <see cref="Darkness"/> and twinkle. Optionally it
/// also drives the scene's directional light + ambient so the world darkens with the sky.
///
/// It OWNS the sky, so MountainBackdrop.buildSky is turned off when this is present; the mountain
/// rings/hero still render in front of the gradient. Meshes/children are procedural and rebuilt in
/// Build() (run at edit time by the exterior generator and again in Start()), matching the other
/// tilers. The sun/moon/stars live on a one-sided "sky rect" aimed at <see cref="skyYawDeg"/> (the
/// scenic side, over the cabin by default) — like the hero peak, it's a directed backdrop.
/// </summary>
public class SkyController : MonoBehaviour
{
    [Header("Time of day")]
    [Range(0f, 1f)] public float timeOfDay = 0f;   // 0 = dawn/morning, 1 = darkest night
    public bool autoPlay = true;
    [Tooltip("Real seconds for a full 0→1 pass.")]
    public float dayLengthSeconds = 120f;
    [Tooltip("Loop back to dawn at night (living sky) vs. hold at darkest.")]
    public bool loop = true;

    [Header("Sky rect — where the sun / moon / stars live")]
    [Tooltip("Compass yaw the sky faces (90° = +Z, north, over the cabin).")]
    public float skyYawDeg = 90f;
    public float skyDistance = 120f;
    public float skyWidth = 280f;
    public float skyHeight = 150f;
    public float skyHorizonY = -6f;

    [Header("Gradient dome (behind everything)")]
    public bool buildDome = true;
    public float domeRadius = 150f;
    public float domeBottomY = -20f;
    public float domeTopY = 260f;

    [Header("Sun & moon")]
    public Sprite sunSprite;
    public Sprite moonSprite;
    public float sunWorldSize = 16f;
    public float moonWorldSize = 12f;
    public bool glow = false;   // off: no second (halo) sun/moon sprite

    [Header("Stars")]
    public int starCount = 150;
    public int starSeed = 9001;
    public float starSize = 0.55f;
    [Tooltip("Fraction of stars that are brighter 'lights' waking a little earlier (dusk).")]
    [Range(0f, 0.3f)] public float lightFraction = 0.08f;

    [Header("Scene lighting (play mode only)")]
    public bool driveSceneLighting = true;
    public Light sunLight;
    public Color dayAmbient = new Color(0.5f, 0.52f, 0.55f);
    public Color nightAmbient = new Color(0.06f, 0.07f, 0.12f);

    /// <summary>0 in daylight → 1 at full night. Fade star layers / night ambience with this.</summary>
    public float Darkness { get; private set; }

    // ---- palette: 7 keyframes × 5 vertical stops (top→horizon) + a darkness value (SKY_README) ----
    static readonly float[] KTime = { 0.00f, 0.18f, 0.40f, 0.60f, 0.76f, 0.88f, 1.00f };
    static readonly float[] KDark = { 0.12f, 0.00f, 0.00f, 0.00f, 0.06f, 0.46f, 1.00f };
    static readonly float[] Stops = { 0.00f, 0.32f, 0.58f, 0.82f, 1.00f };   // top→horizon
    static readonly int[,] KHex =
    {
        { 0x1f335f, 0x495085, 0x8a6790, 0xd98a6e, 0xf3c48f },   // 0.00 dawn
        { 0x2f6ba8, 0x5090c6, 0x86b6d8, 0xbcdcec, 0xdcecf2 },   // 0.18 morning
        { 0x2b7fc6, 0x4f97d6, 0x7ab8e6, 0xb2ddf2, 0xd2ecf8 },   // 0.40 midday
        { 0x345f9e, 0x5a70aa, 0x9a8fb4, 0xe0b088, 0xf0cf9a },   // 0.60 afternoon
        { 0x2b3566, 0x4a3a72, 0x8a4a68, 0xcf5f38, 0xec9a4e },   // 0.76 sunset
        { 0x171a44, 0x312a5c, 0x5c3560, 0x8a4550, 0xa85c50 },   // 0.88 dusk
        { 0x04050d, 0x090c1e, 0x10142c, 0x171d34, 0x20263f },   // 1.00 night
    };
    const int DomeTexH = 96;

    // runtime children
    Transform _sun, _moon, _sunGlow, _moonGlow, _shoot;
    SpriteRenderer _sunSr, _moonSr, _sunGlowSr, _moonGlowSr, _shootSr;
    Transform[] _star;
    SpriteRenderer[] _starSr;
    float[] _starSpeed, _starPhase, _starBaseA;
    bool[] _starEarly;
    Texture2D _domeTex;
    Sprite _dot;

    // sky-rect basis (recomputed from skyYaw each Apply)
    Vector3 _fwd, _tan, _center;

    float _shootTimer, _shootT = -1f;
    Vector3 _shootFrom, _shootTo;

    void Start() { Build(); }

    void Update()
    {
        if (autoPlay && Application.isPlaying)
        {
            timeOfDay += Time.deltaTime / Mathf.Max(1f, dayLengthSeconds);
            if (timeOfDay > 1f) timeOfDay = loop ? timeOfDay - 1f : 1f;
        }
        Apply(timeOfDay);
    }

    // ------------------------------------------------------------------- build
    public void Build()
    {
        // wipe any previously-built children (rebuildable, like the tilers)
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var g = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(g); else DestroyImmediate(g);
        }

        ComputeBasis();
        _dot = MakeDotSprite();

        if (buildDome) BuildDome();

        _sunGlowSr = glow && sunSprite != null ? MakeSprite("SunGlow", sunSprite, sunWorldSize * 2.1f, true) : null;
        _sunGlow = _sunGlowSr != null ? _sunGlowSr.transform : null;
        _sunSr = sunSprite != null ? MakeSprite("Sun", sunSprite, sunWorldSize, true) : null;
        _sun = _sunSr != null ? _sunSr.transform : null;

        _moonGlowSr = glow && moonSprite != null ? MakeSprite("MoonGlow", moonSprite, moonWorldSize * 2.1f, true) : null;
        _moonGlow = _moonGlowSr != null ? _moonGlowSr.transform : null;
        _moonSr = moonSprite != null ? MakeSprite("Moon", moonSprite, moonWorldSize, true) : null;
        _moon = _moonSr != null ? _moonSr.transform : null;

        BuildStars();

        _shootSr = MakeSprite("ShootingStar", _dot, starSize * 3f, true);
        _shoot = _shootSr.transform;
        _shootSr.color = new Color(1f, 1f, 1f, 0f);
        _shootTimer = Random.Range(5f, 12f);
        _shootT = -1f;

        Apply(timeOfDay);
    }

    void ComputeBasis()
    {
        float yaw = skyYawDeg * Mathf.Deg2Rad;
        _fwd = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
        _tan = new Vector3(-Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
        _center = transform.position;
    }

    // World point on the sky rect from normalised (nx across width, ny up from horizon).
    Vector3 RectPoint(float nx, float ny) =>
        _center + _fwd * skyDistance + _tan * ((nx - 0.5f) * skyWidth) + Vector3.up * (skyHorizonY + ny * skyHeight);

    SpriteRenderer MakeSprite(string name, Sprite sp, float worldSize, bool billboard)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localScale = Vector3.one * worldSize;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sp;
        if (billboard) go.AddComponent<Billboard>();
        return sr;
    }

    // A tall cylinder carrying the vertical day gradient, drawn behind the mountains. The gradient
    // texture is re-baked from the palette each frame in Apply().
    void BuildDome()
    {
        _domeTex = new Texture2D(1, DomeTexH, TextureFormat.RGBA32, false)
        { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "SkyGradient" };

        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.mainTexture = _domeTex;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", _domeTex);
        mat.SetFloat("_Cull", 0f);        // viewed from inside
        mat.renderQueue = 1900;           // behind the alpha-clipped mountains (2450)

        const int segs = 48;
        int ring = (segs + 1) * 2;
        var verts = new Vector3[ring + 1];        // + a centre vertex for the overhead cap
        var uvs = new Vector2[ring + 1];
        var tris = new int[segs * 6 + segs * 3];  // cylinder walls + cap fan
        int ti = 0;
        for (int i = 0; i <= segs; i++)
        {
            float ang = i / (float)segs * Mathf.PI * 2f;
            float x = Mathf.Cos(ang) * domeRadius, z = Mathf.Sin(ang) * domeRadius;
            verts[i * 2 + 0] = new Vector3(x, domeBottomY, z);
            verts[i * 2 + 1] = new Vector3(x, domeTopY, z);
            uvs[i * 2 + 0] = new Vector2(0f, 0f);   // v=0 horizon
            uvs[i * 2 + 1] = new Vector2(0f, 1f);   // v=1 top
            if (i < segs)
            {
                int a = i * 2, b = (i + 1) * 2;
                tris[ti++] = a; tris[ti++] = a + 1; tris[ti++] = b + 1;
                tris[ti++] = a; tris[ti++] = b + 1; tris[ti++] = b;
            }
        }
        // Overhead cap: a fan from a centre vertex to the top ring, painted the zenith colour (v=1), so
        // looking straight up shows sky instead of past the open top of the cylinder. Material is
        // double-sided (_Cull 0), so we see it from below.
        int capCentre = ring;
        verts[capCentre] = new Vector3(0f, domeTopY, 0f);
        uvs[capCentre] = new Vector2(0f, 1f);
        for (int i = 0; i < segs; i++)
        {
            tris[ti++] = capCentre; tris[ti++] = i * 2 + 1; tris[ti++] = (i + 1) * 2 + 1;
        }
        var go = new GameObject("SkyDome", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(transform, false);
        var m = new Mesh { name = "SkyDome" };
        m.vertices = verts; m.uv = uvs; m.triangles = tris;
        m.RecalculateBounds();
        go.GetComponent<MeshFilter>().sharedMesh = m;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    void BuildStars()
    {
        int n = Mathf.Max(0, starCount);
        _star = new Transform[n];
        _starSr = new SpriteRenderer[n];
        _starSpeed = new float[n];
        _starPhase = new float[n];
        _starBaseA = new float[n];
        _starEarly = new bool[n];
        if (n == 0) return;

        var root = new GameObject("Stars").transform;
        root.SetParent(transform, false);

        var rng = new System.Random(starSeed);
        float R() => (float)rng.NextDouble();
        for (int k = 0; k < n; k++)
        {
            bool early = R() < lightFraction;
            float nx = 0.02f + R() * 0.96f;
            float ny = 0.30f + R() * 0.68f;              // upper ~¾ of the sky
            var go = new GameObject("Star");
            go.transform.SetParent(root, false);
            go.transform.position = RectPoint(nx, ny);
            go.transform.localScale = Vector3.one * (early ? starSize * 1.8f : starSize);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _dot;
            sr.color = new Color(1f, 1f, 1f, 0f);
            go.AddComponent<Billboard>();

            _star[k] = go.transform;
            _starSr[k] = sr;
            _starSpeed[k] = 1.4f + R() * 3.2f;           // README: speed 1.4–4.6
            _starPhase[k] = R() * Mathf.PI * 2f;
            _starBaseA[k] = early ? 1f : 0.45f + R() * 0.4f;
            _starEarly[k] = early;
        }
    }

    // ------------------------------------------------------------------- apply
    void Apply(float t)
    {
        ComputeBasis();

        // interpolate the 5 stop colours + darkness between the two surrounding keyframes
        int i = 0;
        while (i < KTime.Length - 1 && t > KTime[i + 1]) i++;
        int j = Mathf.Min(i + 1, KTime.Length - 1);
        float f = (KTime[j] > KTime[i]) ? Mathf.Clamp01((t - KTime[i]) / (KTime[j] - KTime[i])) : 0f;

        var col = new Color[5];
        for (int k = 0; k < 5; k++) col[k] = Color.Lerp(Hex(KHex[i, k]), Hex(KHex[j, k]), f);
        Darkness = Mathf.Lerp(KDark[i], KDark[j], f);

        if (_domeTex != null) BakeDome(col);
        ApplySun(t);
        ApplyMoon(t);
        ApplyStars();
        ApplyShootingStar();
        if (driveSceneLighting && Application.isPlaying) ApplyLighting(t);
    }

    void BakeDome(Color[] col)
    {
        for (int y = 0; y < DomeTexH; y++)
        {
            float v = y / (float)(DomeTexH - 1);   // 0 = horizon (bottom), 1 = top
            float q = 1f - v;                       // palette position: 0 = top, 1 = horizon
            _domeTex.SetPixel(0, y, SampleStops(col, q));
        }
        _domeTex.Apply(false);
    }

    static Color SampleStops(Color[] col, float q)
    {
        for (int s = 0; s < Stops.Length - 1; s++)
            if (q <= Stops[s + 1])
            {
                float u = (Stops[s + 1] > Stops[s]) ? (q - Stops[s]) / (Stops[s + 1] - Stops[s]) : 0f;
                return Color.Lerp(col[s], col[s + 1], Mathf.Clamp01(u));
            }
        return col[col.Length - 1];
    }

    void ApplySun(float t)
    {
        if (_sunSr == null) return;
        bool vis = t > 0.02f && t < 0.82f;
        _sunSr.gameObject.SetActive(vis);
        if (_sunGlow != null) _sunGlow.gameObject.SetActive(vis);
        if (!vis) return;

        float sp = (t - 0.02f) / 0.80f;
        float nx = 0.14f + 0.72f * sp;
        float ny = 0.14f + 0.66f * Mathf.Sin(sp * Mathf.PI);
        float noon = Mathf.Sin(sp * Mathf.PI);
        Vector3 p = RectPoint(nx, ny);
        _sun.position = p;

        Color tint = Color.Lerp(Hex(0xd64a28), Color.white, noon);   // red low → white-hot at noon
        float a = Mathf.Clamp01(Mathf.Min(sp, 1f - sp) / 0.10f);     // fade in at sunrise, out at sunset
        _sunSr.color = new Color(tint.r, tint.g, tint.b, a);
        if (_sunGlowSr != null)
        {
            _sunGlow.position = p;
            _sunGlowSr.color = new Color(tint.r, tint.g, tint.b, a * 0.30f);
        }
    }

    void ApplyMoon(float t)
    {
        if (_moonSr == null) return;
        bool vis = t > 0.80f;
        _moonSr.gameObject.SetActive(vis);
        if (_moonGlow != null) _moonGlow.gameObject.SetActive(vis);
        if (!vis) return;

        float mp = Mathf.Clamp01((t - 0.80f) / 0.20f);
        Vector3 p = RectPoint(0.70f, 0.48f + 0.30f * mp);
        _moon.position = p;

        Color moonCol = Hex(0xdfe6f2);
        _moonSr.color = new Color(moonCol.r, moonCol.g, moonCol.b, mp);
        if (_moonGlowSr != null)
        {
            _moonGlow.position = p;
            _moonGlowSr.color = new Color(0.70f, 0.80f, 1f, mp * 0.28f);
        }
    }

    void ApplyStars()
    {
        if (_starSr == null) return;
        float time = Application.isPlaying ? Time.time : 0f;
        for (int k = 0; k < _starSr.Length; k++)
        {
            if (_starSr[k] == null) continue;
            float vis = _starEarly[k] ? Mathf.Clamp01(Darkness * 1.8f) : Darkness;
            float twinkle = 0.45f + 0.55f * (0.5f + 0.5f * Mathf.Sin(time * _starSpeed[k] + _starPhase[k]));
            float a = _starBaseA[k] * vis * twinkle;
            _starSr[k].color = new Color(1f, 1f, 1f, a);
        }
    }

    // A short bright streak at full dark, every 5–12 s (SKY_README).
    void ApplyShootingStar()
    {
        if (_shootSr == null) return;
        if (!Application.isPlaying) { _shootSr.color = new Color(1f, 1f, 1f, 0f); return; }

        if (_shootT < 0f)
        {
            _shootTimer -= Time.deltaTime;
            if (Darkness > 0.85f && _shootTimer <= 0f)
            {
                float x0 = Random.Range(0.10f, 0.60f), y0 = Random.Range(0.60f, 0.95f);
                _shootFrom = RectPoint(x0, y0);
                _shootTo = RectPoint(x0 + Random.Range(0.20f, 0.40f), y0 - Random.Range(0.10f, 0.25f));
                _shootT = 0f;
            }
            else { _shootSr.color = new Color(1f, 1f, 1f, 0f); return; }
        }

        _shootT += Time.deltaTime / 0.5f;   // ~half-second streak
        if (_shootT >= 1f)
        {
            _shootT = -1f;
            _shootTimer = Random.Range(5f, 12f);
            _shootSr.color = new Color(1f, 1f, 1f, 0f);
            return;
        }
        _shoot.position = Vector3.Lerp(_shootFrom, _shootTo, _shootT);
        _shootSr.color = new Color(1f, 1f, 1f, Mathf.Sin(_shootT * Mathf.PI));   // fade in then out
    }

    void ApplyLighting(float t)
    {
        RenderSettings.ambientLight = Color.Lerp(dayAmbient, nightAmbient, Darkness);
        if (sunLight == null) return;

        float day = 1f - Darkness;
        if (t < 0.82f)   // sun up: warm, aim the light from the sun's position
        {
            sunLight.intensity = Mathf.Lerp(0.12f, 1.2f, day);
            sunLight.color = Color.Lerp(Hex(0xffb27a), Color.white, day);   // warm at the horizons
            if (_sun != null && _sun.gameObject.activeSelf)
            {
                Vector3 groundTarget = _center + Vector3.up * 0.5f;
                Vector3 dir = groundTarget - _sun.position;
                if (dir.sqrMagnitude > 0.001f) sunLight.transform.rotation = Quaternion.LookRotation(dir);
            }
        }
        else             // night: faint cool moonlight from overhead
        {
            sunLight.intensity = 0.10f;
            sunLight.color = Hex(0x9fb0d6);
        }
    }

    // A soft round dot generated in-engine (stars + shooting star) — no art to import.
    static Sprite MakeDotSprite()
    {
        const int S = 8;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
        { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "StarDot" };
        float c = (S - 1) * 0.5f, rad = S * 0.5f;
        var px = new Color32[S * S];
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / rad;
            float a = Mathf.Pow(Mathf.Clamp01(1f - d), 1.4f);
            px[y * S + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);   // PPU=S → 1 world unit
    }

    static Color Hex(int rgb) =>
        new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f, 1f);
}
