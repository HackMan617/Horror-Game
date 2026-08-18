using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// The boy/girl partner companion, and everything they look like doing it.
///
/// <para>They keep house on their own (<see cref="PartnerHouseRoam"/> walks them round the same
/// waypoint graph the dog uses, stairs included) and this picks the loop that matches: the 8-way walk
/// cycle while moving, the cooking loop when they settle in at the hearth, the resting stance when
/// they stop — with the dog's own frames in that stance whenever the real dog is at their feet. Press
/// E nearby and they break off what they were doing to speak a line; petting the dog still makes them
/// smile.</para>
///
/// <para><b>Which walk frame.</b> The partner is a billboard, so the compass row is chosen from how
/// their heading looks <i>from where the camera is</i> — the same apparent-facing maths as
/// <see cref="DirectionalSprite"/> — not from their world heading. Walk frames advance by distance
/// covered rather than by time, so the feet never skate. See
/// Assets/Animation/UpdatedPartner/partner_walk_handoff/PARTNER_WALK.md for the sheet layout.</para>
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class PartnerController : MonoBehaviour
{
    /// <summary>One partner's frames. Sliced and filled by HorrorGame3DSetup.</summary>
    [System.Serializable]
    public class Sheets
    {
        public string name;                    // Boy / Girl (label only)
        [Header("Face sheet (partner_<who>.png)")]
        public Sprite[] idle;
        public Sprite[] speak;
        public Sprite[] smile;
        [Header("partner_<who>_walk.png — 8 rows x 6, in order S,SE,E,NE,N,NW,W,SW")]
        public Sprite[] walk;
        [Header("partner_<who>_action.png")]
        public Sprite[] cook;                  // row 0: over the pot
        public Sprite[] rest;                  // row 3: resting stance (the drawn-in dog is stripped)
    }

    public Transform player;
    public Sheets boy = new Sheets();
    public Sheets girl = new Sheets();

    [Header("Talking")]
    public float talkRange = 2.6f;
    [Tooltip("How far above/below the partner still counts. The cabin has two storeys, so a flat XZ " +
             "test would offer 'press E to talk' to a player up in the bedroom directly above them.")]
    public float verticalRange = 2.2f;
    public float lineDuration = 4f;
    public float smileDuration = 3f;
    [TextArea]
    public string[] lines =
    {
        "Hey! It's good to see you.",
        "Stay close to me, okay?",
        "Did you give the dog a pat today?",
        "I had the strangest dream last night...",
        "I'm glad we're together.",
    };

    [Header("Animation")]
    [Tooltip("Frames per second for the standing loops (idle, resting, cooking, speaking).")]
    public float idleFps = 6f;
    [Tooltip("Walk frames per metre walked. The cycle is six frames of contact->passing->contact, so " +
             "this is really 'how long a stride is' — raise it for a scurry, lower it for a stroll.")]
    public float walkFramesPerMetre = 4.2f;
    [Tooltip("How close the real dog has to be for the partner to stand in their with-the-dog pose. " +
             "The frames were drawn with a dog at the feet, so it only reads right when one is there.")]
    public float dogNearRange = 2.2f;

    const int WalkCols = 6;                    // frames per direction row
    const int WalkRows = 8;

    // Sheet row for each apparent-facing sector (DirectionalSprite's 0=N,1=NE,..,7=NW) — the sheet is
    // laid out S,SE,E,NE,N,NW,W,SW, so seeing their north side means drawing the N row, and so on.
    static readonly int[] RowForSector = { 4, 3, 2, 1, 0, 7, 6, 5 };

    SpriteRenderer _sr;
    PartnerHouseRoam _roam;
    Sheets _me;
    Transform _cam;
    DogCompanion _dog;
    bool _dogSearched;

    Sprite[] _temp;                            // speak / smile, played over whatever else is happening
    float _tempTimer;
    float _loopT;                              // seconds, for the fixed-fps standing loops
    float _walkCycle;                          // frame within the walk cycle, advanced by distance
    int _line;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _roam = GetComponent<PartnerHouseRoam>();
        _me = CharacterStore.LoadPartner() == 1 ? girl : boy;      // 0 = boy, 1 = girl
        if (_me.idle != null && _me.idle.Length > 0) _sr.sprite = _me.idle[0];
    }

    /// <summary>React happily for a few seconds (used when the dog is petted).</summary>
    public void Smile() => PlayTemp(_me != null ? _me.smile : null, smileDuration);

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

        float dt = Time.deltaTime;
        if (_tempTimer > 0f)
        {
            _tempTimer -= dt;
            if (_tempTimer <= 0f) _temp = null;
        }

        // Speaking or smiling stops them where they are — the housework can wait until you are done.
        bool busy = _temp != null;
        Vector3 before = transform.position;
        bool moved = _roam != null && _roam.Step(dt, busy);
        float walked = Flat(transform.position - before).magnitude;

        Prompt();
        Animate(moved, walked, dt);
    }

    // The "press E to talk" offer, and the line itself.
    void Prompt()
    {
        if (player == null) return;

        Vector3 a = Flat(player.position);
        Vector3 b = Flat(transform.position);
        if (Vector3.Distance(a, b) > talkRange) return;
        if (Mathf.Abs(player.position.y - transform.position.y) > verticalRange) return;   // a storey apart

        if (DialogUI.Instance != null) DialogUI.Instance.ShowPrompt("Press E to talk");
        if (TalkPressed()) Talk();
    }

    void Talk()
    {
        PlayTemp(_me != null ? _me.speak : null, lineDuration);
        if (lines != null && lines.Length > 0 && DialogUI.Instance != null)
        {
            DialogUI.Instance.ShowDialog(lines[_line % lines.Length], lineDuration);
            _line++;
        }
    }

    // ---------------------------------------------------------------- animation
    void Animate(bool moved, float walked, float dt)
    {
        _loopT += dt * idleFps;

        // Speaking / smiling wins outright — those are the frames that answer the player.
        if (_temp != null && _temp.Length > 0) { _sr.sprite = Loop(_temp); return; }

        if (moved && _me.walk != null && _me.walk.Length >= WalkRows * WalkCols)
        {
            _walkCycle = (_walkCycle + walked * walkFramesPerMetre) % WalkCols;
            int row = RowForSector[Sector(_roam.Heading)];
            _sr.sprite = _me.walk[row * WalkCols + (int)_walkCycle];
            return;
        }

        // Stood still. Cooking at the hearth, the with-the-dog stance when the dog is actually here,
        // else the plain idle. Each falls back to the one behind it when a sheet is missing.
        var roaming = _roam != null ? _roam.Current : PartnerHouseRoam.Activity.Resting;
        Sprite[] frames = null;
        if (roaming == PartnerHouseRoam.Activity.Cooking) frames = _me.cook;
        else if (DogIsNear()) frames = _me.rest;
        if (frames == null || frames.Length == 0) frames = _me.idle;
        if (frames != null && frames.Length > 0) _sr.sprite = Loop(frames);
    }

    Sprite Loop(Sprite[] frames) => frames[Mathf.Abs((int)_loopT) % frames.Length];

    /// <summary>
    /// Which of the eight views of them the camera is currently looking at, given the way they are
    /// heading. 0 = we see their north side, 1 = north-east, ... 7 = north-west (see DirectionalSprite).
    /// </summary>
    int Sector(Vector3 heading)
    {
        if (_cam == null)
        {
            var c = Camera.main;
            if (c == null) return 4;                   // no camera yet: show the front
            _cam = c.transform;
        }
        Vector3 toCam = Flat(_cam.position - transform.position);
        if (toCam.sqrMagnitude < 1e-4f || heading.sqrMagnitude < 1e-4f) return 4;

        float bearing = Mathf.Atan2(toCam.x, toCam.z) * Mathf.Rad2Deg;      // compass: which side we view from
        float nose = Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg;     // compass: the way they walk
        float apparent = nose - bearing + 180f;                             // camera dead ahead => 180 => S => front
        return Mathf.RoundToInt(Mod(apparent, 360f) / 45f) & 7;
    }

    // Is the real dog at their feet? The resting frames were drawn round a dog, so they only read
    // right when the dog is genuinely there — the rest of the time they simply stand.
    bool DogIsNear()
    {
        if (!_dogSearched) { _dog = FindAnyObjectByType<DogCompanion>(); _dogSearched = true; }
        if (_dog == null) return false;
        Vector3 d = _dog.transform.position - transform.position;
        return Mathf.Abs(d.y) <= 1.2f && Flat(d).magnitude <= dogNearRange;
    }

    void PlayTemp(Sprite[] frames, float dur)
    {
        if (frames == null || frames.Length == 0) return;
        _temp = frames;
        _tempTimer = dur;
    }

    bool TalkPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    static float Mod(float a, float m) { float r = a % m; return r < 0f ? r + m : r; }
}
