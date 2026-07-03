using UnityEngine;
using Game.Neighbors;
using Game.Houses;

/// <summary>
/// Every so often Robert wanders from his yard into his house and later back out — an ambient bit of
/// life. Drives his walk animation via <see cref="NeighborRobert.SetMovement"/> (so he faces away as he
/// heads in, toward you as he comes out) and swings the front door open/closed via the house's
/// <see cref="TileStripQuad"/> door halves. Only triggers in daylight (not nightmare — then he just
/// stares) and while the player is near enough to witness it. The camera-facing helper is paused during
/// a trip so the movement facing wins.
/// </summary>
[RequireComponent(typeof(NeighborRobert))]
public class RobertGoesHome : MonoBehaviour
{
    [Tooltip("Ground point at the house door he walks to and vanishes through.")]
    public Vector3 doorPoint;
    [Tooltip("House door halves to swing (NeighborHouse_C DoorTop + DoorBottom).")]
    public TileStripQuad[] doorQuads;
    [Tooltip("Only wander when the player is within this range to see it.")]
    public Transform player;
    public float witnessRange = 16f;

    public float walkSpeed = 1.6f;
    public Vector2 idleWait = new Vector2(2f, 5f);     // seconds outside (at his spot) before heading back in
    public Vector2 insideWait = new Vector2(3f, 6f);   // seconds spent indoors before coming back out
    public float arriveDist = 0.25f;
    [Tooltip("Lead time to start the exit door swing before Robert steps out, to cover the door " +
             "animation's ~1.5s closed pre-roll so it's open as he emerges.")]
    public float doorOpenLead = 1.5f;

    NeighborRobert _robert;
    NeighborRobertFacing _facing;
    SpriteRenderer _sr;
    Vector3 _home;
    enum S { Idle, ToDoor, Inside, ToHome }
    S _state = S.Idle;
    float _timer;
    bool _exitDoorSwung;

    void Awake()
    {
        _robert = GetComponent<NeighborRobert>();
        _facing = GetComponent<NeighborRobertFacing>();
        _sr = GetComponent<SpriteRenderer>();
        _home = transform.position;
        _timer = 0f;   // start by heading straight in (once the player's near to see it)
    }

    bool PlayerNear => player == null ||
        (player.position - transform.position).sqrMagnitude <= witnessRange * witnessRange;
    bool Nightmare => _robert != null && _robert.DreadProgress >= _robert.nightmareThreshold;

    void SwingDoor() { if (doorQuads != null) foreach (var q in doorQuads) if (q != null) q.OpenDoor(); }
    void FacingEnabled(bool on) { if (_facing != null) _facing.enabled = on; }

    // Walk toward target on the XZ plane, driving the walk cycle. Returns remaining distance.
    float StepToward(Vector3 target)
    {
        Vector3 to = target - transform.position; to.y = 0f;
        float dist = to.magnitude;
        if (dist > 1e-4f)
        {
            Vector3 dir = to / dist;
            transform.position += dir * Mathf.Min(walkSpeed * Time.deltaTime, dist);
            _robert.SetMovement(new Vector2(dir.x, dir.z) * walkSpeed);   // y = world Z (front/back)
        }
        return dist;
    }

    void Update()
    {
        switch (_state)
        {
            case S.Idle:
                if (!PlayerNear || Nightmare) return;
                _timer -= Time.deltaTime;
                if (_timer <= 0f) { FacingEnabled(false); SwingDoor(); _state = S.ToDoor; }
                break;

            case S.ToDoor:
                if (StepToward(doorPoint) <= arriveDist)
                {
                    _robert.SetMovement(Vector2.zero);
                    _sr.enabled = false;                       // stepped inside
                    _timer = Random.Range(insideWait.x, insideWait.y);
                    _exitDoorSwung = false;
                    _state = S.Inside;
                }
                break;

            case S.Inside:
                _timer -= Time.deltaTime;
                // open the door a beat early so it's swinging by the time he steps out
                if (!_exitDoorSwung && _timer <= doorOpenLead) { SwingDoor(); _exitDoorSwung = true; }
                if (_timer <= 0f)
                {
                    transform.position = doorPoint;            // reappear at the (already-opening) door
                    _sr.enabled = true;
                    _state = S.ToHome;
                }
                break;

            case S.ToHome:
                if (StepToward(_home) <= arriveDist)
                {
                    _robert.SetMovement(Vector2.zero);
                    transform.position = _home;
                    FacingEnabled(true);                       // camera-facing resumes at rest
                    _timer = Random.Range(idleWait.x, idleWait.y);
                    _state = S.Idle;
                }
                break;
        }
    }
}
