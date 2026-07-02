using UnityEngine;

/// <summary>
/// Plays a footstep sound each time the player takes a step. Steps are paced by distance travelled
/// (so walking and running self-adjust their cadence), and a random clip is drawn from the surface
/// under the player — grass out on the yard by default, with per-surface overrides matched by the
/// name of the collider a short downward ray hits (e.g. the cobble road plays gravel). Pitch and
/// volume are jittered per step so no two footfalls sound identical.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class FootstepAudio : MonoBehaviour
{
    [System.Serializable]
    public class Surface
    {
        [Tooltip("Name of the collider the downward ray must hit for this set to play (e.g. \"Pathway\").")]
        public string colliderName;
        public AudioClip[] clips;
        [System.NonSerialized] public int last = -1;
    }

    public PlayerController3D player;
    public CharacterController controller;

    [Tooltip("Played when the surface under the player matches no entry in 'surfaces' (the grassy yard).")]
    public AudioClip[] defaultSteps;
    [Tooltip("Per-surface overrides, matched by the name of the collider under the player.")]
    public Surface[] surfaces;

    [Header("Cadence")]
    [Tooltip("Metres travelled between footfalls. Faster movement => more frequent steps.")]
    public float strideLength = 1.9f;
    [Tooltip("Hard floor on time between steps (safety against very high frame speeds).")]
    public float minInterval = 0.17f;

    [Header("Variation")]
    public Vector2 pitchRange = new Vector2(0.92f, 1.08f);
    public Vector2 volumeRange = new Vector2(0.8f, 1.0f);

    [Header("Surface")]
    [Tooltip("Downward ray length used to sample the surface under the player.")]
    public float surfaceRayDistance = 1.5f;

    AudioSource _src;
    Vector3 _lastPos;
    float _dist;
    float _sinceStep;
    int _lastDefault = -1;

    void Awake()
    {
        _src = GetComponent<AudioSource>();
        _src.playOnAwake = false;
        _src.spatialBlend = 0f;          // the player's own footsteps: 2D
        if (player == null) player = GetComponentInParent<PlayerController3D>();
        if (controller == null) controller = GetComponentInParent<CharacterController>();
        _lastPos = transform.position;
        _dist = strideLength * 0.5f;      // so the first step lands promptly once you start moving
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

        Vector3 pos = transform.position;
        Vector3 delta = pos - _lastPos; delta.y = 0f;
        _lastPos = pos;
        _sinceStep += Time.deltaTime;

        bool grounded = controller == null || controller.isGrounded;
        bool moving = player != null && player.MoveInput.sqrMagnitude > 0.01f;
        if (!grounded || !moving)
        {
            _dist = strideLength * 0.5f;   // preload half a stride so movement resumes with a prompt step
            return;
        }

        _dist += delta.magnitude;
        if (_dist >= strideLength && _sinceStep >= minInterval)
        {
            _dist = 0f;
            _sinceStep = 0f;
            PlayStep();
        }
    }

    void PlayStep()
    {
        Surface surf = CurrentSurface();
        if (surf != null && surf.clips != null && surf.clips.Length > 0)
            Play(surf.clips, ref surf.last);
        else if (defaultSteps != null && defaultSteps.Length > 0)
            Play(defaultSteps, ref _lastDefault);
    }

    // The surface set matching the collider directly under the player, or null for the default.
    Surface CurrentSurface()
    {
        if (surfaces == null || surfaces.Length == 0) return null;
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        if (Physics.Raycast(origin, Vector3.down, out var hit, surfaceRayDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            string n = hit.collider.gameObject.name;
            foreach (var s in surfaces)
                if (s != null && s.colliderName == n) return s;
        }
        return null;
    }

    void Play(AudioClip[] clips, ref int last)
    {
        int idx = Random.Range(0, clips.Length);
        if (clips.Length > 1 && idx == last) idx = (idx + 1) % clips.Length;   // avoid an immediate repeat
        last = idx;
        if (clips[idx] == null) return;
        _src.pitch = Random.Range(pitchRange.x, pitchRange.y);
        _src.PlayOneShot(clips[idx], Random.Range(volumeRange.x, volumeRange.y));
    }
}
