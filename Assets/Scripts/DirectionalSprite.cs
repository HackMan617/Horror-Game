using UnityEngine;

/// <summary>
/// Doom-style 8-way facing for a billboarded sprite. The object has a fixed world "nose" heading
/// (<see cref="noseYaw"/>); as the camera walks around it, this swaps the <see cref="SpriteRenderer"/>
/// to the view that shows the side currently facing the camera — front, front-3/4, side, back-3/4 or
/// back — mirroring the three west-facing views, exactly the table in
/// Assets/Animation/Car/roadside_pack/CAR.md ("8-way facing — five sheets, mirror three").
///
/// Pairs with <see cref="Billboard"/> (which keeps the quad turned to the camera); this only chooses
/// which of the five sprites is drawn and whether it is flipped. Runs in edit mode too so the parked
/// truck reads correctly in the Scene view.
///
/// Grid/compass convention matches the rest of the exterior: +z = North, +x = East, so a heading of
/// 0 = N, 90 = E, 180 = S, 270 = W.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class DirectionalSprite : MonoBehaviour
{
    [Header("Frame-0 sprite for each authored view (fallback / Scene-view parked look)")]
    public Sprite front;
    public Sprite front3q;   // authored facing down-right (SE)
    public Sprite side;      // authored facing right (E)
    public Sprite back3q;    // authored facing up-right (NE)
    public Sprite back;

    [Header("Full per-view frame sheets (optional — enables roll/door animation)")]
    [Tooltip("The whole authored sheet per view (frame 0 = parked, 1–3 roll, 4–6 door/tailgate). " +
             "When populated, the selected " + nameof(frame) + " is shown instead of the frame-0 sprite above.")]
    public Sprite[] frontFrames;
    public Sprite[] front3qFrames;
    public Sprite[] sideFrames;
    public Sprite[] back3qFrames;
    public Sprite[] backFrames;

    [Tooltip("Frame index shown within the current view's sheet (0 = parked). Driven at runtime by " +
             "interaction scripts such as CarDoor; ignored when the frame sheets above are empty.")]
    public int frame = 0;

    [Tooltip("Compass heading the object's nose points: 0=N, 90=E, 180=S, 270=W.")]
    public float noseYaw = 180f;           // parked pointing south by default
    [Tooltip("Fine-tune rotation, degrees. Adjust if the chosen view is one step off.")]
    public float angleOffset = 0f;
    [Tooltip("Flip the direction the sheets rotate through, if the mirror handedness is reversed.")]
    public bool invertHanded = false;

    SpriteRenderer _sr;
    Transform _cam;
    int _lastSector = -1;

    void OnEnable() { _sr = GetComponent<SpriteRenderer>(); }

    void LateUpdate()
    {
        if (_sr == null) _sr = GetComponent<SpriteRenderer>();
        if (_cam == null)
        {
            var c = Camera.main;
            if (c == null) return;
            _cam = c.transform;
        }
        Apply(_cam.position);
    }

    /// <summary>Pick and assign the view for a camera at <paramref name="camPos"/>.</summary>
    public void Apply(Vector3 camPos)
    {
        Vector3 d = camPos - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude < 1e-4f) return;

        // Bearing from the object to the camera (compass: 0=N,90=E), i.e. which side we view from.
        float bearing = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        // Apparent facing = how the nose looks in the viewer's frame. Camera in front (bearing==noseYaw)
        // => we see the front (S in the table); directly behind => the back (N).
        float apparent = (invertHanded ? (bearing - noseYaw) : (noseYaw - bearing)) + 180f + angleOffset;
        int sector = Mathf.RoundToInt(Mod(apparent, 360f) / 45f) & 7;   // 0=N,1=NE,...,7=NW

        Sprite fallback; Sprite[] frames; bool flip;
        switch (sector)
        {
            case 0: fallback = back;    frames = backFrames;    flip = false; break;   // N
            case 1: fallback = back3q;  frames = back3qFrames;  flip = false; break;   // NE
            case 2: fallback = side;    frames = sideFrames;    flip = false; break;   // E
            case 3: fallback = front3q; frames = front3qFrames; flip = false; break;   // SE
            case 4: fallback = front;   frames = frontFrames;   flip = false; break;   // S
            case 5: fallback = front3q; frames = front3qFrames; flip = true;  break;   // SW
            case 6: fallback = side;    frames = sideFrames;    flip = true;  break;   // W
            default: fallback = back3q; frames = back3qFrames;  flip = true;  break;   // NW
        }
        // Prefer the selected frame from the full sheet; fall back to the single parked sprite.
        Sprite sp = (frames != null && frames.Length > 0)
            ? frames[Mathf.Clamp(frame, 0, frames.Length - 1)]
            : fallback;
        if (sp != null) _sr.sprite = sp;
        _sr.flipX = flip;
        _lastSector = sector;
    }

    public int CurrentSector => _lastSector;

    static float Mod(float a, float m) { float r = a % m; return r < 0f ? r + m : r; }
}
