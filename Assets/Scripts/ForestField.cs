using UnityEngine;

/// <summary>
/// The exterior woods, driven as ONE component instead of a <see cref="Billboard"/> +
/// <see cref="LoopSpriteAnimator"/> pair on every trunk — the stand is a few hundred trees now, so
/// per-tree MonoBehaviours would be a few hundred Update calls a frame for no gain.
///
/// Sheets are the reworked <c>tree_spruce(_winter).png</c> (Assets/Animation/TREES_UNITY.md): 6x4 of
/// 96x196 cells, rows 0-1 = a 12-frame IDLE breath, rows 2-3 = a 12-frame dread SWAY (whip + twist +
/// shudder + lurch). Each tree carries its own phase offset so the stand never pulses in lockstep.
///
/// What makes the woods feel alive/wrong:
///   * <b>Gusts</b> — a wave front sweeps across the stand on a random heading; trees flip to SWAY as
///     it passes and settle back to IDLE behind it, so the wind visibly travels through the trees.
///   * <b>Dread</b> — as <see cref="DreadDirector"/> climbs, gusts come more often, the sway plays
///     faster, and a growing share of the stand stops settling down at all.
///   * <b>Depth tint</b> — each tree's baked <see cref="Tree.tint"/> darkens with how deep in the
///     woods it stands, so the forest reads as a wall you can't see into rather than a row of sprites.
///   * <b>Night</b> — the sprites are unlit, so they'd stay noon-bright after dark; the stand is
///     dimmed by the sky's <see cref="SkyController.Darkness"/> to keep it in the scene's light.
///
/// The stand is built by HorrorGame3DSetup (Tools > Horror Game > Reforest Exterior).
/// </summary>
public class ForestField : MonoBehaviour
{
    public const int Frames = 12;      // frames per loop; idle is 0-11, sway 12-23 on the sheet

    [System.Serializable]
    public struct Tree
    {
        public Transform t;
        public SpriteRenderer sr;
        public bool winter;            // snow-loaded sheet (used deeper in the woods)
        public byte phase;             // 0..11 flip-book offset — no two neighbours breathe together
        public float depth01;          // 0 = clearing edge .. 1 = deepest murk
        public float swayBias;         // 0..1 — the lower, the earlier it stops settling as dread climbs
        public Color tint;             // baked daylight/calm depth tint (night + dread multiply into it)
    }

    [Header("Sliced frames — 12 idle + 12 sway per sheet")]
    public Sprite[] summerIdle, summerSway, winterIdle, winterSway;

    [Header("The stand")]
    public Tree[] trees;

    [Header("Playback")]
    public float idleFps = 7f;
    public float swayFps = 11f;

    [Header("Dread")]
    [Tooltip("Share of the stand that stays locked in the sway at dread = 1.")]
    [Range(0f, 1f)] public float dreadSwayShare = 0.85f;
    [Tooltip("Colour the whole stand drifts toward as dread climbs (grey, bloodless).")]
    public Color dreadTint = new Color(0.47f, 0.44f, 0.5f);
    [Range(0f, 1f)] public float dreadTintStrength = 0.5f;

    [Header("Gusts (a wave of sway sweeping through the woods)")]
    public float gustInterval = 11f;   // seconds between fronts at dread 0 (much shorter at dread 1)
    public float gustSpeed = 24f;      // world units/sec the front travels
    public float gustWidth = 16f;      // how deep the swaying band is

    [Header("Night (unlit sprites don't dim on their own)")]
    public SkyController sky;
    public Color nightTint = new Color(0.34f, 0.38f, 0.52f);
    [Range(0f, 1f)] public float nightStrength = 0.55f;

    /// <summary>0..1 — how hard the stand is being shaken right now. <see cref="ForestDebris"/> sheds off this.</summary>
    public float Shake01 { get; private set; }

    Sprite[][] _sheets;      // [summerIdle, summerSway, winterIdle, winterSway]
    byte[] _shown;           // last sheet*12+frame assigned per tree, so we only touch changed renderers
    Transform _cam;
    Vector3 _lastCamPos = new Vector3(float.MaxValue, 0f, 0f);
    float _idleF, _swayF;
    float _gustTimer, _gustPos, _extent = 40f;
    Vector2 _gustDir = Vector2.right;
    bool _gusting;
    float _lastDread = -1f, _lastDark = -1f;

    void Awake()
    {
        _sheets = new[] { summerIdle, summerSway, winterIdle, winterSway };
        _shown = new byte[trees != null ? trees.Length : 0];
        for (int i = 0; i < _shown.Length; i++) _shown[i] = 255;

        // How far the stand reaches, so a gust front can start clear of it and sweep all the way through.
        Vector3 c = transform.position;
        for (int i = 0; i < _shown.Length; i++)
        {
            var t = trees[i].t;
            if (t == null) continue;
            float dx = t.position.x - c.x, dz = t.position.z - c.z;
            _extent = Mathf.Max(_extent, Mathf.Sqrt(dx * dx + dz * dz));
        }
        _gustTimer = Random.Range(2f, gustInterval);
    }

    void Update()
    {
        if (trees == null || trees.Length == 0) return;
        float dt = Time.deltaTime;
        float dread = DreadDirector.Value01;

        TickGust(dt, dread);

        // Flip-book clocks. Kept wrapped to 0..12 so they stay exact over a long session.
        _idleF = Mathf.Repeat(_idleF + idleFps * dt, Frames);
        _swayF = Mathf.Repeat(_swayF + swayFps * Mathf.Lerp(1f, 1.35f, dread) * dt, Frames);
        int fi = (int)_idleF, fs = (int)_swayF;

        // Billboard yaw only depends on where the camera *stands*, not where it looks — so a camera
        // that only turned needs no rotation work at all.
        if (_cam == null || !_cam.gameObject.activeInHierarchy)
        {
            var c = Camera.main;
            _cam = c != null ? c.transform : null;
        }
        Vector3 camPos = _cam != null ? _cam.position : Vector3.zero;
        bool camMoved = _cam != null && (camPos - _lastCamPos).sqrMagnitude > 1e-6f;
        if (camMoved) _lastCamPos = camPos;
        int frame = Time.frameCount;

        Vector3 origin = transform.position;
        float swayCut = dread * dreadSwayShare;

        for (int i = 0; i < trees.Length; i++)
        {
            ref Tree tr = ref trees[i];
            if (tr.sr == null || tr.t == null) continue;
            Vector3 p = tr.t.position;

            // Is the gust front over this tree, or has dread already claimed it for good?
            bool sway = tr.swayBias < swayCut;
            if (!sway && _gusting)
            {
                float proj = (p.x - origin.x) * _gustDir.x + (p.z - origin.z) * _gustDir.y;
                sway = Mathf.Abs(proj - _gustPos) < gustWidth;
            }

            int sheet = (tr.winter ? 2 : 0) + (sway ? 1 : 0);
            var frames = _sheets[sheet];
            if (frames == null || frames.Length == 0) continue;
            int k = ((sway ? fs : fi) + tr.phase) % Frames;
            byte slot = (byte)(sheet * Frames + k);
            if (_shown[i] != slot)
            {
                _shown[i] = slot;
                tr.sr.sprite = frames[k % frames.Length];
            }

            // Near trees re-face every frame; the far murk barely swings, so a quarter of it per frame
            // is well under a pixel of error and keeps the whole pass cheap.
            if (camMoved && (tr.depth01 < 0.4f || ((i + frame) & 3) == 0))
            {
                float dx = camPos.x - p.x, dz = camPos.z - p.z;
                if (dx * dx + dz * dz > 1e-6f)
                    tr.t.rotation = Quaternion.Euler(0f, Mathf.Atan2(dx, dz) * Mathf.Rad2Deg, 0f);
            }
        }

        // Tints only move when the sun or dread does — recolour the stand on the change, not every frame.
        float dark = sky != null ? sky.Darkness : 0f;
        if (Mathf.Abs(dread - _lastDread) > 0.004f || Mathf.Abs(dark - _lastDark) > 0.004f)
            ApplyTints(dread, dark);

        float target = Mathf.Clamp01(dread * 0.55f + (_gusting ? 0.75f : 0f));
        Shake01 = Mathf.MoveTowards(Shake01, target, dt * 1.5f);
    }

    void TickGust(float dt, float dread)
    {
        if (_gusting)
        {
            _gustPos += gustSpeed * dt;
            if (_gustPos > _extent + gustWidth) _gusting = false;
            return;
        }
        _gustTimer -= dt;
        if (_gustTimer > 0f) return;

        float a = Random.value * Mathf.PI * 2f;
        _gustDir = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
        _gustPos = -_extent - gustWidth;
        _gusting = true;
        _gustTimer = gustInterval * Mathf.Lerp(1f, 0.3f, dread) * Random.Range(0.7f, 1.4f);
    }

    /// <summary>Re-multiplies every tree's baked depth tint by the current night + dread mood.</summary>
    public void ApplyTints(float dread, float dark)
    {
        _lastDread = dread; _lastDark = dark;
        Color mood = Color.Lerp(Color.white, nightTint, Mathf.Clamp01(dark) * nightStrength) *
                     Color.Lerp(Color.white, dreadTint, Mathf.Clamp01(dread) * dreadTintStrength);
        if (trees == null) return;
        for (int i = 0; i < trees.Length; i++)
        {
            var sr = trees[i].sr;
            if (sr == null) continue;
            Color c = trees[i].tint * mood;
            c.a = 1f;
            sr.color = c;
        }
    }
}
