using UnityEngine;

/// <summary>
/// Gives <see cref="DogCompanion"/> the run of the two-storey cabin: it wanders the house on its own,
/// climbs and descends the stairwell flight, lets itself into the bedroom, and only comes looking for
/// the player every so often.
///
/// <para>The graph it walks, and the walking itself, live in <see cref="HouseNavWalker"/> — the same
/// waypoints the partner roams (see <see cref="PartnerHouseRoam"/>). This is only the dog's half: the
/// roam/follow alternation and coming to find you.</para>
///
/// <para>Absent or unpopulated, <see cref="DogCompanion"/> falls back to its old flat beeline follow,
/// so this is safe to leave off any scene that is one storey of open floor.</para>
/// </summary>
[DisallowMultipleComponent]
public class DogHouseNav : HouseNavWalker
{
    [Header("Wandering")]
    [Tooltip("An amble, not the trot it uses to catch up with you.")]
    public float roamSpeed = 2.4f;
    [Tooltip("Seconds of wandering before it thinks to come and find you (min, max). Long enough to fit " +
             "several trips — the climb up to the bedroom alone is most of twenty seconds.")]
    public Vector2 roamDuration = new Vector2(25f, 50f);
    [Tooltip("Seconds it stops and noses about on reaching a waypoint (min, max).")]
    public Vector2 pause = new Vector2(1.5f, 5f);
    [Range(0f, 1f)]
    [Tooltip("Chance a new wander target is on the storey it is already on. The rest of the time it " +
             "deliberately picks the other storey, which is what sends it up and down the stairs.")]
    public float sameFloorBias = 0.6f;

    [Header("Following")]
    [Tooltip("Seconds it sticks with you before wandering off again (min, max).")]
    public Vector2 followDuration = new Vector2(9f, 17f);
    [Tooltip("Past this (straight-line, height included) it counts as having lost you and breaks off " +
             "its wander. The cabin is 20m square, so this is most of the way across the house.")]
    public float leashDistance = 18f;
    [Tooltip("Seconds it will tolerate being lost / on another storey before breaking off to find you.")]
    public float strayPatience = 8f;
    [Tooltip("Once on your waypoint and this close, it leaves the graph and walks straight at you — " +
             "but only with a clear line, so it never cuts a corner through a wall.")]
    public float freeApproachRange = 9f;

    /// <summary>True while it is coming to find the player rather than wandering on its own.</summary>
    public bool Following => _following;

    bool _following;
    float _modeTimer, _strayTimer, _pauseTimer;

    protected override void Awake()
    {
        base.Awake();
        _modeTimer = Random.Range(roamDuration.x, roamDuration.y);
    }

    /// <summary>
    /// Drive the dog for one frame. Returns true if it actually moved, which is what
    /// <see cref="DogCompanion"/> animates the walk cycle off.
    /// </summary>
    public bool Step(Transform player, float dt, float stopDistance, float trotSpeed, bool holdStill)
    {
        if (!Ready || player == null) return false;

        EnsureOnGraph();

        // Being petted outranks everything: it sits still, and forgets where it was headed.
        if (holdStill)
        {
            _path.Clear();
            _goal = -1;
            _pauseTimer = 0f;
            EnterFollow();          // it hangs about with you for a while after a fuss
            return false;
        }

        Vector3 target = player.position;
        bool sameFloor = Mathf.Abs(target.y - transform.position.y) <= floorEpsilon;
        float flat = Flat(target - transform.position).magnitude;
        // The leash is a real distance with the height counted in. Being a storey up is NOT being lost —
        // it is the whole point — so only genuinely losing the player cuts a wander short. (Treating a
        // different storey as astray made it bolt back down the stairs within seconds of getting up them.)
        bool astray = (target - transform.position).magnitude > leashDistance;
        _strayTimer = astray ? _strayTimer + dt : 0f;

        // Roam and follow alternate on a timer. A wander is only broken off at a natural break — on
        // arriving somewhere, not mid-stride — because the walk up to the bedroom takes most of a roam
        // to make, and a timer that fires halfway turns the dog round on the landing every single time,
        // so it never actually gets anywhere. Losing the player still cuts a wander short immediately.
        _modeTimer -= dt;
        if (!_following)
        {
            bool atRest = _path.Count == 0 || _pauseTimer > 0f;
            if (_strayTimer >= strayPatience || (_modeTimer <= 0f && atRest)) EnterFollow();
        }
        else if (_modeTimer <= 0f && !astray) EnterRoam();

        return _following ? StepFollow(target, dt, stopDistance, trotSpeed, sameFloor, flat)
                          : StepRoam(dt);
    }

    bool StepFollow(Vector3 target, float dt, float stopDistance, float trotSpeed, bool sameFloor, float flat)
    {
        int want = Nearest(target);
        if (want != _goal) Repath(want);

        if (_path.Count == 0)
        {
            // Standing on the player's own waypoint. Leave the graph and walk straight at them —
            // but only across a clear line and a level floor, so it never clips a wall or a stair.
            if (sameFloor && flat <= freeApproachRange && !nodes[_at].onStairs)
            {
                if (flat <= stopDistance) return false;
                Vector3 dir = Flat(target - transform.position).normalized;
                Vector3 stop = target - dir * stopDistance;
                stop.y = transform.position.y;
                if (!Blocked(transform.position, stop))
                {
                    StepFree(dir, Mathf.Min(trotSpeed * dt, flat - stopDistance));
                    return true;
                }
            }

            // A wall (or a storey) in the way: hop to whichever neighbouring waypoint gets it closer,
            // which is what walks it round the bedroom partition instead of into it.
            int hop = BestNeighbour(_at, target);
            if (hop >= 0) Repath(hop);
        }
        return Advance(trotSpeed, dt);
    }

    bool StepRoam(float dt)
    {
        if (_pauseTimer > 0f) { _pauseTimer -= dt; return false; }

        if (_path.Count == 0)
        {
            Repath(PickRoamTarget(sameFloorBias));
            if (_path.Count == 0) { _pauseTimer = Random.Range(pause.x, pause.y); return false; }
        }

        bool moved = Advance(roamSpeed, dt);
        if (_path.Count == 0) _pauseTimer = Random.Range(pause.x, pause.y);   // arrived: stop and nose about
        return moved;
    }

    void EnterFollow()
    {
        _following = true;
        _modeTimer = Random.Range(followDuration.x, followDuration.y);
        _strayTimer = 0f;
        _pauseTimer = 0f;
        _goal = -1;                        // re-target the player next frame
    }

    void EnterRoam()
    {
        _following = false;
        _modeTimer = Random.Range(roamDuration.x, roamDuration.y);
        _path.Clear();
        _goal = -1;
    }
}
