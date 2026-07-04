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
    [Tooltip("Played once when the door starts opening or closing.")]
    public AudioClip doorSound;
    [Range(0f, 1f)] public float soundVolume = 1f;

    // CAR.md: door stage 0..3 -> frame in the 7-frame view sheet (0 shut, 4/5/6 swing open).
    static readonly int[] StageToFrame = { 0, 4, 5, 6 };

    DirectionalSprite _ds;
    int _stage;        // current door stage: 0 = shut .. 3 = fully open
    int _target;       // stage we're easing toward
    float _stepT;      // per-step timer
    float _cd;         // interaction cooldown remaining

    /// <summary>True while the door is open or opening (enter/drive hooks can read this later).</summary>
    public bool IsOpen => _target > 0;

    void Awake() { _ds = GetComponent<DirectionalSprite>(); }

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
            _target = IsOpen ? 0 : 3;
            _cd = interactCooldown;
            if (doorSound != null) AudioSource.PlayClipAtPoint(doorSound, transform.position, soundVolume);
        }
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    bool EPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }
}
