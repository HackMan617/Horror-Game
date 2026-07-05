using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Walk-up interaction for the parked truck (Assets/Animation/Car/roadside_pack/CAR.md). When the
/// player is near, a prompt appears; press E to swing the near door open, E again to close it. The
/// door eases through stages 0→3 (open) / 3→0 (close) over time, and each stage maps to the sheet's
/// door frames [0,4,5,6] (CAR.md). The frame index is fed to the truck's <see cref="DirectionalSprite"/>
/// so whichever view is currently facing the camera animates its own door/tailgate.
///
/// First slice of the vehicle mechanics — enter / drive hooks will hang off this later.
/// </summary>
[RequireComponent(typeof(DirectionalSprite))]
public class CarDoor : MonoBehaviour
{
    [Tooltip("Player transform. Found by name (\"Player\") at Start when left null.")]
    public Transform player;
    public float range = 3.5f;
    public string openPrompt = "Press E to open door";
    public string closePrompt = "Press E to close door";
    [Tooltip("Seconds per door stage step (open 0→3, close 3→0). CAR.md ≈ 70 ms/step.")]
    public float stepSeconds = 0.07f;
    [Tooltip("Minimum seconds between toggles, so E-mashing can't spam the swing.")]
    public float interactCooldown = 0.4f;

    [Header("Audio — one 'Open and Close Door' clip, split into the two foley hits")]
    [Tooltip("The 'Open and Close Door.wav': an open creak, a silence, then a close slam. Self-wired in the editor.")]
    public AudioClip openCloseClip;
    [Range(0f, 1f)] public float soundVolume = 1f;
    [Tooltip("Seconds [start,end] of the OPEN foley inside the clip (leading silence trimmed for a snappy play).")]
    public float openStart = 0.70f, openEnd = 1.45f;
    [Tooltip("Seconds [start,end] of the CLOSE foley inside the clip.")]
    public float closeStart = 3.65f, closeEnd = 4.40f;

    // CAR.md: door stage 0..3 -> frame in the 7-frame view sheet (0 shut, 4/5/6 swing open).
    static readonly int[] StageToFrame = { 0, 4, 5, 6 };

    DirectionalSprite _ds;
    AudioClip _openClip, _closeClip;   // the two foley hits sliced out of openCloseClip
    int _stage;        // current door stage: 0 = shut .. 3 = fully open
    int _target;       // stage we're easing toward
    float _stepT;      // per-step timer
    float _cd;         // interaction cooldown remaining

    /// <summary>True while the door is open or opening (enter/drive hooks can read this later).</summary>
    public bool IsOpen => _target > 0;

    void Awake()
    {
        _ds = GetComponent<DirectionalSprite>();
#if UNITY_EDITOR
        if (openCloseClip == null)
            openCloseClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound Effects/Open and Close Door.wav");
#endif
        // One clip holds both foley hits (open creak, silence, close slam) — slice each into its own
        // clip once so opening plays the open half and closing plays the close half.
        if (openCloseClip != null)
        {
            if (openCloseClip.loadState != AudioDataLoadState.Loaded) openCloseClip.LoadAudioData();
            _openClip  = SubClip(openCloseClip, openStart, openEnd, "DoorOpen");
            _closeClip = SubClip(openCloseClip, closeStart, closeEnd, "DoorClose");
        }
    }

    void Start()
    {
        if (player == null)
        {
            var p = GameObject.Find("Player");
            if (p != null) player = p.transform;
        }
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;
        if (_cd > 0f) _cd -= Time.deltaTime;

        // Ease the door toward its target, one stage per step.
        if (_stage != _target)
        {
            _stepT += Time.deltaTime;
            if (_stepT >= stepSeconds)
            {
                _stepT = 0f;
                _stage += _target > _stage ? 1 : -1;
            }
        }
        if (_ds != null) _ds.frame = StageToFrame[Mathf.Clamp(_stage, 0, 3)];

        bool inRange = player != null &&
            Vector3.Distance(Flat(player.position), Flat(transform.position)) <= range;
        if (!inRange) return;   // out of range: prompt hides, but the door keeps whatever state it's in

        if (DialogUI.Instance != null) DialogUI.Instance.ShowPrompt(IsOpen ? closePrompt : openPrompt);

        if (EPressed() && _cd <= 0f)
        {
            bool willOpen = !IsOpen;                 // currently shut -> this press opens it
            _target = willOpen ? 3 : 0;
            _cd = interactCooldown;

            var clip = willOpen ? _openClip : _closeClip;
            if (clip != null) AudioSource.PlayClipAtPoint(clip, transform.position, soundVolume);
        }
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    // Copies the samples in [startSec, endSec] of src into a fresh clip, so each door action can play
    // just its own foley hit out of the combined "Open and Close Door" recording.
    static AudioClip SubClip(AudioClip src, float startSec, float endSec, string name)
    {
        if (src == null) return null;
        int freq = src.frequency, ch = src.channels;
        int startS = Mathf.Clamp(Mathf.RoundToInt(startSec * freq), 0, src.samples);
        int endS   = Mathf.Clamp(Mathf.RoundToInt(endSec   * freq), startS, src.samples);
        int len = Mathf.Max(1, endS - startS);

        var data = new float[len * ch];
        src.GetData(data, startS);
        var clip = AudioClip.Create(name, len, ch, freq, false);
        clip.SetData(data, 0);
        return clip;
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
