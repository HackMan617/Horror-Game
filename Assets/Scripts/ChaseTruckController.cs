using UnityEngine;

/// <summary>
/// The third-person (high rear 3/4) driving pose for the truck — the twin of the first-person cockpit.
/// Adapted from the handoff's <c>ChaseTruckController</c> (Assets/Animation/Car/roadside_pack/CAR.md,
/// "chase — the third-person DRIVING view") to this project's IN-WORLD driving.
///
/// The handoff version was written for a flat 2D arcade road: it read W/A/S/D itself, integrated its own
/// speed, and slid the truck across a lane by writing <c>transform.localPosition.x</c>. Here the truck is a
/// real vehicle in the 3D map — <see cref="DrivingRig"/> already owns the input and the speed/steer state,
/// and <see cref="TruckDriver"/> already owns the transform (a CharacterController moving along a driven
/// heading). So this component does the one thing that art actually adds: it picks the frame. The lane
/// offset and the opposite road-curve from the handoff are deliberately dropped — the truck genuinely turns
/// through the world, so drifting it sideways as well would double-count the steer.
///
/// Sheet: <c>truck_chase.png</c> — 12 frames of 64x32 (768x32), home only (no <c>_nightmare</c> twin yet):
///   0-3   straight    (4-frame wheel-roll loop; front wheels tucked, not drawn)
///   4-7   steer left  (body banks left,  front wheels pivot into view on the left)
///   8-11  steer right (body banks right, front wheels pivot into view on the right)
/// The banked body and pivoted wheels are BAKED into the frames, so nothing is rotated at runtime. Those
/// left/right labels are drawn for a camera looking AT the truck, so from the chase camera behind it they
/// come out mirrored — see <see cref="mirrorSteerGroups"/>.
///
/// Enabled only while driving in third person: <see cref="TruckDriver.ApplyView"/> turns it on and puts
/// <see cref="DirectionalSprite"/> into <see cref="DirectionalSprite.suppressSprite"/> mode (which keeps its
/// sector live for <see cref="CarLights"/> but stops it fighting over the SpriteRenderer). Parked and in
/// first person it is off, and the truck is the ordinary 8-way billboard again.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class ChaseTruckController : MonoBehaviour
{
    public const int Frames = 12;
    public const int RollFrames = 4;   // per steer group

    [Header("12 sliced frames: 0-3 straight, 4-7 left, 8-11 right")]
    public Sprite[] frames;

    [Header("Feel")]
    [Tooltip("Wheel-roll rate at a crawl (frames/sec). The roll scales between this and " +
             nameof(maxRollFps) + " with speed — a fixed cadence spins the wheels just as fast pulling " +
             "away as at full throttle, which reads as a truck at constant speed no matter what you do.")]
    public float minRollFps = 4f;
    [Tooltip("Wheel-roll rate at full throttle (frames/sec). CAR.md's ~80 ms/frame = 12.5 fps.")]
    public float maxRollFps = 12.5f;
    [Tooltip("Below this |steer| the truck holds the straight group — stops it flicking between poses on " +
             "the small corrections you make holding a lane.")]
    [Range(0f, 1f)] public float steerDeadzone = 0.33f;
    [Tooltip("Below this speed the wheels stop turning and hold the parked frame.")]
    [Range(0f, 1f)] public float rollSpeedThreshold = 0.02f;
    [Tooltip("Swap which group a steer direction picks. The sheet labels frames 4-7 'steer left' and 8-11 " +
             "'steer right', but those are drawn for a camera looking AT the truck — from the chase camera " +
             "sitting BEHIND it they read mirrored, so steering right leant the truck into a left turn and " +
             "the controls felt inverted. Untick only if a re-exported sheet flips the convention back.")]
    public bool mirrorSteerGroups = true;

    /// <summary>Frame index shown this frame (0..11). Read-only; useful for debugging the pose.</summary>
    public int CurrentFrame { get; private set; }

    DrivingRig _rig;
    SpriteRenderer _sr;
    float _rollTimer;
    int _wheel;
    int _shown = -1;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _rig = GetComponent<DrivingRig>();
    }

    void OnEnable()
    {
        // Re-pick from a clean slate so switching into third person never shows a stale pose for a frame.
        _rollTimer = 0f;
        _shown = -1;
        Apply();
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

        float speed = _rig != null ? _rig.speed : 0f;
        if (speed > rollSpeedThreshold)
        {
            // Wheel rate tracks the throttle, so pulling away visibly spins up and easing off winds down —
            // that ramp IS the acceleration you see from behind, since the chase camera holds the truck at
            // a near-constant place in frame.
            float fps = Mathf.Lerp(minRollFps, maxRollFps, Mathf.Clamp01(speed));
            _rollTimer += Time.deltaTime * fps;
            while (_rollTimer >= 1f)
            {
                _rollTimer -= 1f;
                _wheel = (_wheel + 1) % RollFrames;
            }
        }
        else { _wheel = 0; _rollTimer = 0f; }

        Apply();
    }

    void Apply()
    {
        if (_sr == null) _sr = GetComponent<SpriteRenderer>();
        if (frames == null || frames.Length == 0) return;

        float steer = _rig != null ? _rig.steer : 0f;
        int group = steer < -steerDeadzone ? 1 : steer > steerDeadzone ? 2 : 0;   // straight / left / right
        if (mirrorSteerGroups && group != 0) group = 3 - group;                   // seen from behind, 1 <-> 2
        int fi = group * RollFrames + _wheel;

        CurrentFrame = fi;
        if (fi == _shown) return;                      // only touch the renderer when the pose actually changes
        if (fi >= frames.Length || frames[fi] == null) return;
        _shown = fi;
        _sr.sprite = frames[fi];
        _sr.flipX = false;                             // the chase sheet is authored un-mirrored
    }
}
