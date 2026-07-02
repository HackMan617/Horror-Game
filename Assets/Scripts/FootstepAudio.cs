using UnityEngine;

/// <summary>
/// Plays a footstep sound each time the player takes a step. Steps are paced by distance travelled
/// (so walking and running self-adjust their cadence), and a random clip is drawn from the surface
/// set — dirt out on the grass, gravel on the cobble road — with a little pitch/volume variation so
/// no two footfalls sound identical. Surface is chosen by a short downward raycast: standing over the
/// Pathway collider plays gravel, anything else plays dirt.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class FootstepAudio : MonoBehaviour
{
    public PlayerController3D player;
    public CharacterController controller;
    public AudioClip[] dirtSteps;
    public AudioClip[] gravelSteps;

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
    [Tooltip("Name of the collider that should play the gravel set (the cobble road).")]
    public string gravelObjectName = "Pathway";

    AudioSource _src;
    Vector3 _lastPos;
    float _dist;
    float _sinceStep;
    int _lastDirt = -1, _lastGravel = -1;

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
        bool gravel = OnGravel();
        AudioClip[] clips = gravel ? gravelSteps : dirtSteps;
        if (clips == null || clips.Length == 0) clips = dirtSteps;      // fall back to dirt if a set is empty
        if (clips == null || clips.Length == 0) return;

        int last = gravel ? _lastGravel : _lastDirt;
        int idx = Random.Range(0, clips.Length);
        if (clips.Length > 1 && idx == last) idx = (idx + 1) % clips.Length;   // avoid an immediate repeat
        if (gravel) _lastGravel = idx; else _lastDirt = idx;

        if (clips[idx] == null) return;
        _src.pitch = Random.Range(pitchRange.x, pitchRange.y);
        _src.PlayOneShot(clips[idx], Random.Range(volumeRange.x, volumeRange.y));
    }

    bool OnGravel()
    {
        if (gravelSteps == null || gravelSteps.Length == 0) return false;
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        if (Physics.Raycast(origin, Vector3.down, out var hit, surfaceRayDistance, ~0, QueryTriggerInteraction.Ignore))
            return hit.collider.gameObject.name == gravelObjectName;
        return false;
    }
}
