using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// The cabin hearth (see FIREPLACE.md). A stone fireplace on the interior wall the player reignites
/// with spruce felled outside: it catches (<b>Lighting</b>), holds a steady <b>Burn</b> while it has
/// fuel, then untended it <b>Cools</b>, <b>Fades</b> to ash and goes <b>Cold</b>.
///
/// <para>Two 7x5 sheets — <c>fireplace_front.png</c> and <c>fireplace_side.png</c> (64px cells, row =
/// state, col = frame, index <c>row*7 + col</c>) — are sliced at runtime; which one shows follows the
/// camera like the room's other 2.5D props (front head-on, side from the sides). Pair with a
/// <see cref="Billboard"/> so the quad also turns to face the camera.</para>
///
/// <para>On the first visit it's in its most lit form — a full load already blazing — and a full
/// light→burn→die cycle lasts about a minute. Its fuel and state persist across house trips (saved
/// when the cabin unloads, restored on return), so it keeps burning down or stays cold rather than
/// resetting; the save clears on a game restart. Feeding it (E, when near) spends one wood the player
/// carried in from the felled logs (see <see cref="LogPickup.Wood"/>).</para>
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class Fireplace : MonoBehaviour
{
    public enum State { Cold = 0, Lighting = 1, Burn = 2, Cooling = 3, Fading = 4 }

    [Header("Sheets (fireplace_front/side.png — 448x320, 64px cells, 7 cols x 5 rows)")]
    public Texture2D frontSheet;        // self-wired from the known asset path in the editor
    public Texture2D sideSheet;

    [Header("Facing")]
    [Tooltip("World direction the hearth's front faces (into the room). Front sheet shows from here; side sheet from the sides.")]
    public Vector3 homeForward = new Vector3(0f, 0f, -1f);

    [Header("Burn")]
    [Tooltip("Frames per second of the flame animation (~7 frames per state).")]
    public float fps = 9f;
    [Tooltip("Seconds one load of wood burns. A full light->burn->die cycle lasts roughly this + ~1.5s.")]
    public float burnSeconds = 56f;
    [Tooltip("Start already lit with a full load — the blazing hearth the player finds on entering the cabin.")]
    public bool startLit = true;

    [Header("Interaction")]
    public float reach = 3f;
    public string feedPrompt = "Press E to feed the fire";
    public string needWoodPrompt = "Need wood - chop a tree";

    const int Cols = 7, Cell = 64;
    const float Ppu = 16f;
    static readonly Vector2 Pivot = new Vector2(0.5f, 0f);

    // Session save-slot: the fire's fuel + state snapshotted when the cabin unloads, restored on the
    // next visit — so it keeps burning down (or stays dead) across house trips. Static, so it survives
    // the scene load but clears when the game restarts (like LogPickup.Wood / the felled-tree registry).
    static bool _hasSaved;
    static State _savedState;
    static float _savedFuel;

    Sprite[] _front, _side;      // 35 each (row*7 + col)
    SpriteRenderer _sr;
    Transform _player;
    Camera _cam;
    Coroutine _run;
    State _state = State.Cold;
    int _frame;
    float _fuel;
    bool _running, _useSide, _flip;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
#if UNITY_EDITOR
        if (frontSheet == null) frontSheet = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Animation/fireplace_front.png");
        if (sideSheet == null)  sideSheet  = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Animation/fireplace_side.png");
#endif
        _front = Slice(frontSheet);
        _side  = Slice(sideSheet);

        // Wall fixture: face a FIXED direction into the room instead of billboarding. A Billboard would
        // rotate this wide (~3u) sprite so, at side angles, its far edge swings THROUGH the wall behind it
        // ("sprite going into the wall"); a fixed facing keeps the quad parallel to the wall so it never
        // clips. The hearth is only ever seen from the room side, so a fixed front reads fine — it just
        // foreshortens at an angle. Disable any paired Billboard so it doesn't fight this rotation.
        var bb = GetComponent<Billboard>();
        if (bb != null) bb.enabled = false;
        Vector3 face = homeForward; face.y = 0f;
        if (face.sqrMagnitude < 1e-4f) face = Vector3.back;
        transform.rotation = Quaternion.LookRotation(face.normalized, Vector3.up);

        Paint();
    }

    void Start()
    {
        if (_hasSaved)
        {
            // Returning to the cabin: pick up exactly where the fire was left.
            _state = _savedState;
            _fuel = _savedFuel;
            if (_fuel > 0f && (_state == State.Lighting || _state == State.Burn))
            {
                _state = State.Burn;                 // still had fuel -> keep burning it down
                _run = StartCoroutine(Run());
            }
            else { _state = State.Cold; _frame = 0; }   // left it dying/out -> stays cold until fed
            return;
        }

        // First ever visit: enter to a blazing hearth (skip the catch, begin mid-Burn with a full load).
        if (startLit)
        {
            _state = State.Burn;
            _fuel = burnSeconds;
            _run = StartCoroutine(Run());
        }
    }

    // Snapshot the fire as the cabin unloads (or the object is disabled) so the next visit resumes it.
    void OnDisable()
    {
        _hasSaved = true;
        _savedState = _state;
        _savedFuel = _fuel;
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

        if (_player == null)
        {
            var pc = FindAnyObjectByType<PlayerController3D>();
            if (pc != null) _player = pc.transform;
        }
        if (_player == null) return;

        Vector3 a = transform.position; a.y = 0f;
        Vector3 b = _player.position;   b.y = 0f;
        if ((a - b).sqrMagnitude > reach * reach) return;

        // Near the hearth: one press feeds one wood — relighting a dead fire or extending a live one.
        bool hasWood = LogPickup.Wood > 0;
        if (DialogUI.Instance != null) DialogUI.Instance.ShowPrompt(hasWood ? feedPrompt : needWoodPrompt);
        if (hasWood && EPressed())
        {
            LogPickup.Wood--;
            Reignite(1);
        }
    }

    // Pick the front/side sheet from the camera angle each frame, then paint the current state/frame.
    void LateUpdate()
    {
        UpdateFacing();
        Paint();
    }

    /// <summary>Feed the fire spruce → light it (from cold/dying) or extend the current burn.</summary>
    public void Reignite(int logs = 1)
    {
        _fuel += Mathf.Max(1, logs) * burnSeconds;
        // Out or already dying? (re)start the catch->burn cycle. Otherwise the added fuel just extends
        // the burn loop that's already running.
        if (!_running || _state == State.Cooling || _state == State.Fading)
        {
            if (_run != null) StopCoroutine(_run);
            _running = false;
            _run = StartCoroutine(Run());
        }
    }

    /// <summary>Let it burn out now (the coroutine also does this when fuel hits zero).</summary>
    public void LetDie() { _fuel = 0f; }

    /// <summary>Forget the saved fire (call from a New Game flow so the next cabin visit starts lit).</summary>
    public static void ResetSession() { _hasSaved = false; _savedFuel = 0f; _savedState = State.Cold; }

    IEnumerator Run()
    {
        _running = true;
        if (_state != State.Burn) yield return PlayOnce(State.Lighting);   // catch, unless already blazing

        _state = State.Burn; _frame = 0;
        while (_fuel > 0f)
        {
            _frame = (_frame + 1) % Cols;
            _fuel -= 1f / fps;
            yield return new WaitForSeconds(1f / fps);
        }

        yield return PlayOnce(State.Cooling);
        yield return PlayOnce(State.Fading);
        _state = State.Cold; _frame = 0;
        _running = false;
    }

    IEnumerator PlayOnce(State s)
    {
        _state = s;
        for (int f = 0; f < Cols; f++) { _frame = f; yield return new WaitForSeconds(1f / fps); }
    }

    void Paint()
    {
        Sprite[] sheet = (_useSide ? _side : _front) ?? _front ?? _side;
        if (sheet == null || sheet.Length == 0) return;
        int idx = Mathf.Clamp((int)_state * Cols + _frame, 0, sheet.Length - 1);
        _sr.sprite = sheet[idx];
        _sr.flipX = _flip;
    }

    // The hearth is a FIXED-facing wall fixture (see Awake) — the quad no longer turns to the camera, so
    // it always shows the FRONT sheet. The dedicated side sheet was drawn for a rotated/billboarded view;
    // painting it on the fixed front quad would look wrong, so it's no longer used here.
    void UpdateFacing()
    {
        _useSide = false;
        _flip = false;
    }

    // Slice a 7x5 sheet into 35 sprites (row*7 + col). Row 0 (Cold) is the TOP strip of the texture.
    static Sprite[] Slice(Texture2D tex)
    {
        if (tex == null) return null;
        var arr = new Sprite[35];
        for (int r = 0; r < 5; r++)
            for (int c = 0; c < Cols; c++)
            {
                var rect = new Rect(c * Cell, tex.height - (r + 1) * Cell, Cell, Cell);
                arr[r * Cols + c] = Sprite.Create(tex, rect, Pivot, Ppu, 0, SpriteMeshType.FullRect);
            }
        return arr;
    }

    bool EPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }
}
