using UnityEngine;

/// <summary>
/// The far-range "wanderer" (Assets/Animation/MOUNTAIN_BACKDROP.md): a near-black figure out among the
/// distant trees/slopes that fades IN subtly, creeps sideways a short way, then fades out — then waits a
/// random spell and does it again. It is a normal detail that behaves wrong: the dread is in the timing,
/// so it's deliberately faint (low peak opacity) and easy to second-guess. Cycles its 4 frames and eases
/// its own alpha; runs from the start as background unease, not a scare. (The mountain face apparition is
/// a separate, deferred effect.)
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class WandererFigure : MonoBehaviour
{
    public Sprite[] frames;
    public float fps = 5.9f;                                   // ~170 ms/frame (README)
    public Color tint = new Color(0.047f, 0.039f, 0.070f);     // near-black #0c0a12
    [Range(0f, 1f)] public float maxAlpha = 0.5f;              // faintest it needs to be to unsettle

    [Header("One surfacing (seconds)")]
    public float fadeIn = 2.5f;
    public float hold = 2f;
    public float fadeOut = 2.5f;
    public Vector2 idleRange = new Vector2(12f, 22f);          // gap between appearances
    public float creepDistance = 3f;                           // world units it drifts while visible
    public Vector3 creepDir = Vector3.right;                   // slope tangent (set by the placer)

    SpriteRenderer _sr;
    Vector3 _home;
    float _t, _idle, _phase;
    int _frame = -1;
    bool _surfacing;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _home = transform.position;
        _idle = Mathf.Lerp(idleRange.x, idleRange.y, 0.35f);   // first one comes a touch sooner
        SetAlpha(0f);
    }

    void Update()
    {
        if (frames != null && frames.Length > 0)
        {
            _phase += Time.deltaTime * fps;
            int f = Mathf.Abs((int)_phase) % frames.Length;
            if (f != _frame) { _frame = f; _sr.sprite = frames[f]; }
        }

        _t += Time.deltaTime;
        if (!_surfacing)
        {
            SetAlpha(0f);
            if (_t >= _idle) { _t = 0f; _surfacing = true; transform.position = _home; }
            return;
        }

        float dur = fadeIn + hold + fadeOut;
        float a;
        if (_t < fadeIn) a = Mathf.SmoothStep(0f, maxAlpha, _t / fadeIn);
        else if (_t < fadeIn + hold) a = maxAlpha;
        else if (_t < dur) a = Mathf.SmoothStep(maxAlpha, 0f, (_t - fadeIn - hold) / fadeOut);
        else { _surfacing = false; _t = 0f; _idle = Random.Range(idleRange.x, idleRange.y); SetAlpha(0f); return; }

        SetAlpha(a);
        transform.position = _home + creepDir.normalized * (creepDistance * Mathf.Clamp01(_t / dur));
    }

    void SetAlpha(float a) => _sr.color = new Color(tint.r, tint.g, tint.b, a);
}
