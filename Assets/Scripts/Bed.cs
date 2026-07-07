using UnityEngine;

/// <summary>
/// The cabin bed (see BED.md) — the sleep prop and nightmare portal. Uses the four TRUE oblique views
/// (front / back / left / right, plus a rotted nightmare twin of each; 8×3 sheets, 64px cells, PPU 16),
/// picking the view that matches which side of the room the camera is on.
///
/// <para><b>Why it's directional-but-fixed, not a billboard:</b> the bed is long and sits against a wall.
/// A camera-following billboard would rotate the wide quad so its far end sweeps THROUGH the wall
/// ("bleeds through") at side angles — the same defect the fireplace had. Instead each view is drawn on
/// an <b>axis-aligned</b> quad facing the room's nearest cardinal (like the cabin's stitched faces), so it
/// never rotates into the wall; the sprite depth-bias (SpriteBillboardDepthBias) covers the small residual.
/// You still get every side view as you walk around, just without the sweep.</para>
///
/// <para>Rows are states, cols are 8 frames (index = row*8 + col): row 0 IDLE (loop), row 1 ON
/// (make / nightmare REACH), row 2 OFF (strip / nightmare DRAG). Flip <see cref="DreadProgress"/> up and
/// the rotted twin flickers in. Pair with <see cref="BedPortal"/> for the sleep interaction.</para>
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class Bed : MonoBehaviour
{
    public enum View { Front, Back, Left, Right }
    public enum Row { Idle = 0, On = 1, Off = 2 }   // On = make / reach, Off = strip / drag

    [Header("Sheets (bed_<view>[_nightmare].png — 512x192, 64px cells, 8 cols x 3 rows). Self-wired in editor.")]
    public Texture2D frontDay, backDay, leftDay, rightDay;
    public Texture2D frontNight, backNight, leftNight, rightNight;

    [Header("Facing")]
    [Tooltip("World direction the bed's FRONT (foot) faces — into the room. The head sits opposite (at the wall).")]
    public Vector3 homeForward = new Vector3(0f, 0f, -1f);
    [Tooltip("Viewer used to choose the visible side. Defaults to Camera.main.")]
    public Transform viewer;

    [Header("Sheet")]
    public float pixelsPerUnit = 16f;
    // Bottom-CENTRE horizontally, but raised to the art's actual base: every 64px cell has ~8px of
    // transparent padding below the bed, so a true bottom (y=0) pivot floats it ~0.5u off the floor.
    public Vector2 pivot = new Vector2(0.5f, 0.125f);   // 8/64 -> plants the bed legs on the floor line
    public float fps = 8f;

    [Header("Dread flicker (same source as the room's furniture)")]
    [Range(0f, 1f)] public float DreadProgress = 0f;

    const int Cols = 8;

    SpriteRenderer _sr;
    Camera _cam;
    Sprite[][] _day = new Sprite[4][];    // [view][row*8+col]
    Sprite[][] _night = new Sprite[4][];
    View _view = View.Front;
    Row _row = Row.Idle;
    int _frame;
    float _animT, _flickT;
    bool _once, _hold, _nmShown;
    System.Action _onDone;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
#if UNITY_EDITOR
        string dir = "Assets/Animation/Updated Bed/bed_kit/sprites/";
        T(ref frontDay, dir + "bed_front.png");  T(ref backDay, dir + "bed_back.png");
        T(ref leftDay,  dir + "bed_left.png");   T(ref rightDay, dir + "bed_right.png");
        T(ref frontNight, dir + "bed_front_nightmare.png"); T(ref backNight, dir + "bed_back_nightmare.png");
        T(ref leftNight,  dir + "bed_left_nightmare.png");  T(ref rightNight, dir + "bed_right_nightmare.png");
#endif
        _day[0]  = Slice(frontDay);  _day[1]  = Slice(backDay);  _day[2]  = Slice(leftDay);  _day[3]  = Slice(rightDay);
        _night[0]= Slice(frontNight);_night[1]= Slice(backNight);_night[2]= Slice(leftNight);_night[3]= Slice(rightNight);
        _row = Row.Idle; _frame = 0;
        UpdateView(true);
        Paint();
    }

#if UNITY_EDITOR
    static void T(ref Texture2D t, string path)
    { if (t == null) t = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path); }
#endif

    // ---- interaction API (BED.md) ----
    /// <summary>Pull the blanket on, then settle to the made idle.</summary>
    public void MakeBed() => Play(Row.On, hold: false, () => Enter(Row.Idle));
    /// <summary>Throw the blanket aside; rest on the stripped last frame.</summary>
    public void StripBed() => Play(Row.Off, hold: true, null);
    /// <summary>Nightmare summon: rot-hands erupt and keep reaching.</summary>
    public void Reach() => Play(Row.On, hold: false, () => Enter(Row.On));   // then loops row 1
    /// <summary>Nightmare take: the void opens and holds, consumed.</summary>
    public void Drag() => Play(Row.Off, hold: true, null);

    void Enter(Row r) { _row = r; _frame = 0; _once = false; _hold = false; _onDone = null; _animT = 0f; }
    void Play(Row r, bool hold, System.Action done) { _row = r; _frame = 0; _once = true; _hold = hold; _onDone = done; _animT = 0f; }

    void Update()
    {
        // advance the frame clock
        _animT += Time.deltaTime;
        if (_animT >= 1f / Mathf.Max(0.5f, fps))
        {
            _animT = 0f;
            if (!_once)                       // looping state (idle / reach-loop)
                _frame = (_frame + 1) % Cols;
            else if (_frame < Cols - 1)       // one-shot playing
                _frame++;
            else                              // one-shot finished
            {
                if (_hold) { /* rest on frame 7 */ }
                else { var d = _onDone; _once = false; _onDone = null; if (d != null) d(); else _frame = (_frame + 1) % Cols; }
            }
        }

        // nightmare flicker — strobes the rotted twin in as dread climbs (mirrors InteriorObject)
        float dp = DreadProgress;
        _flickT -= Time.deltaTime;
        if (_flickT <= 0f)
        {
            if (dp <= 0f) { _nmShown = false; _flickT = 0.25f; }
            else if (dp >= 1f) { _nmShown = !(Random.value < 0.14f); _flickT = _nmShown ? Random.Range(0.16f, 0.34f) : Random.Range(0.04f, 0.11f); }
            else { _nmShown = Random.value < dp; _flickT = _nmShown ? Random.Range(0.05f, 0.05f + 0.15f * dp) : Random.Range(0.14f, 0.14f + 0.52f * (1f - dp)); }
        }
    }

    // Pick the view + face the quad to the matching cardinal (axis-aligned, never a free billboard), then paint.
    void LateUpdate() { UpdateView(false); Paint(); }

    void UpdateView(bool force)
    {
        Transform cam = viewer != null ? viewer : (_cam != null ? _cam.transform : ((_cam = Camera.main) != null ? _cam.transform : null));
        if (cam == null) return;

        Vector3 to = cam.position - transform.position; to.y = 0f;
        if (to.sqrMagnitude < 1e-4f) return; to.Normalize();

        Vector3 fwd = homeForward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.back; fwd.Normalize();
        Vector3 left = Vector3.Cross(Vector3.up, fwd);            // the room's "west" for the default bed

        // the cardinal (of the four the bed has art for) the camera sits most toward
        Vector3[] card = { fwd, -fwd, left, -left };             // Front, Back, Left, Right
        int best = 0; float bestDot = -2f;
        for (int i = 0; i < 4; i++) { float d = Vector3.Dot(to, card[i]); if (d > bestDot) { bestDot = d; best = i; } }

        if (!force && (View)best == _view) return;
        _view = (View)best;
        // face the quad toward that cardinal so the correct oblique view reads flat-on, axis-aligned to the room
        transform.rotation = Quaternion.LookRotation(card[best], Vector3.up);
    }

    void Paint()
    {
        var sets = (_nmShown ? _night : _day);
        var set = sets[(int)_view] ?? _day[(int)_view] ?? _day[0];
        if (set == null || set.Length == 0) return;
        int idx = Mathf.Clamp((int)_row * Cols + _frame, 0, set.Length - 1);
        _sr.sprite = set[idx];
    }

    Sprite[] Slice(Texture2D tex)
    {
        if (tex == null) return null;
        int cw = tex.width / Cols;              // 64
        int ch = tex.height / 3;                // 64
        var arr = new Sprite[Cols * 3];
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < Cols; c++)
            {
                var rect = new Rect(c * cw, tex.height - (r + 1) * ch, cw, ch);   // row 0 = top strip
                arr[r * Cols + c] = Sprite.Create(tex, rect, pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
            }
        return arr;
    }
}
