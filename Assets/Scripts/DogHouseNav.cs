using System.Collections.Generic;
using UnityEngine;

/// <summary>One waypoint on the dog's run of the house.</summary>
[System.Serializable]
public class DogNavNode
{
    public string name;
    public Vector3 position;
    [Tooltip("Indices of the nodes this one connects to. Undirected — the builder writes both ends.")]
    public int[] links;
    [Tooltip("Mid-flight on the staircase: the dog climbs through here but never settles or " +
             "free-walks off it (its floor is a slope, not a plane).")]
    public bool onStairs;
}

/// <summary>
/// Gives <see cref="DogCompanion"/> the run of the two-storey cabin: it wanders the house on its own,
/// climbs and descends the stairwell flight, lets itself into the bedroom, and only comes looking for
/// the player every so often.
///
/// <para><b>Why a waypoint graph and not a NavMesh.</b> The interior is generated in code
/// (HorrorGame3DSetup.BuildCabinInterior) and every stick of furniture in it is a billboarded sprite
/// with no collider — a baked NavMesh would see one empty 20x20 box per storey and happily route the
/// dog straight through the sofa. The shell's real geometry is all constants in the builder, so the
/// graph is laid out from those same numbers instead: a loop of the ground floor that keeps clear of
/// the living-room group, the flight up the south-east stairwell, the landing, and the bedroom through
/// its door. See HorrorGame3DSetup.BuildDogNav.</para>
///
/// <para><b>Height comes from the graph, not from the floor.</b> The dog's y is interpolated along
/// whichever edge it is walking, so the stair edges (a straight constant-slope ramp) put it exactly on
/// the tread line without a single raycast.</para>
///
/// <para>Absent or unpopulated, <see cref="DogCompanion"/> falls back to its old flat beeline follow,
/// so this is safe to leave off any scene that is one storey of open floor.</para>
/// </summary>
[DisallowMultipleComponent]
public class DogHouseNav : MonoBehaviour
{
    [Tooltip("The house's walkable waypoints. Laid out by Tools > Horror Game > Build Cabin Interior.")]
    public List<DogNavNode> nodes = new List<DogNavNode>();

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

    [Header("Geometry")]
    public float arriveRadius = 0.35f;
    [Tooltip("Height difference under which two points count as the same storey (the slab is 3.2m up).")]
    public float floorEpsilon = 1.2f;
    [Tooltip("How much a storey's worth of height counts against a waypoint when picking the nearest " +
             "one, so the dog never targets the node directly above or below it.")]
    public float floorPenalty = 3f;
    [Tooltip("Height the clear-line test is taken at: over the thresholds, under the stair rail's cap.")]
    public float sightHeight = 0.45f;
    public LayerMask wallMask = ~0;

    /// <summary>True while it is coming to find the player rather than wandering on its own.</summary>
    public bool Following => _following;
    public bool Ready => nodes != null && nodes.Count >= 2;

    int _at = -1;                       // the waypoint it is standing at / last left
    int _goal = -1;
    readonly List<int> _path = new List<int>();
    Vector3 _segFrom;                   // where the current edge was entered (the y lerp's anchor)
    bool _following;
    float _modeTimer, _strayTimer, _pauseTimer;

    // BFS scratch, kept alive so pathing never allocates mid-wander
    int[] _prev;
    readonly Queue<int> _queue = new Queue<int>();
    readonly List<int> _bag = new List<int>();

    void Awake()
    {
        _segFrom = transform.position;
        _modeTimer = Random.Range(roamDuration.x, roamDuration.y);
    }

    /// <summary>
    /// Drive the dog for one frame. Returns true if it actually moved, which is what
    /// <see cref="DogCompanion"/> animates the walk cycle off.
    /// </summary>
    public bool Step(Transform player, float dt, float stopDistance, float trotSpeed, bool holdStill)
    {
        if (!Ready || player == null) return false;

        // Snap onto the graph without teleporting: it simply walks on from wherever it was placed.
        if (_at < 0) { _at = Nearest(transform.position); _segFrom = transform.position; }

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
                    transform.position += dir * Mathf.Min(trotSpeed * dt, flat - stopDistance);
                    _segFrom = transform.position;
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
            Repath(PickRoamTarget());
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

    // Walk one frame along the current edge. y is interpolated across the edge rather than sampled off
    // the floor, so the stair edges (one straight constant-slope run) sit the dog exactly on the treads.
    bool Advance(float speed, float dt)
    {
        if (_path.Count == 0) return false;

        Vector3 to = nodes[_path[0]].position;
        Vector3 here = Flat(transform.position);
        Vector3 d = Flat(to) - here;
        float dist = d.magnitude;
        float step = speed * dt;

        if (dist <= Mathf.Max(step, arriveRadius))
        {
            _at = _path[0];
            _path.RemoveAt(0);
            transform.position = to;
            _segFrom = to;
            return true;
        }

        here += d / dist * step;
        float span = Vector3.Distance(Flat(_segFrom), Flat(to));
        float t = span > 0.001f ? Mathf.Clamp01(Vector3.Distance(Flat(_segFrom), here) / span) : 1f;
        transform.position = new Vector3(here.x, Mathf.Lerp(_segFrom.y, to.y, t), here.z);
        return true;
    }

    // Breadth-first over the graph: fewest hops, which on a graph this evenly spaced is also the
    // shortest way round. Runs only when the goal changes, not every frame.
    void Repath(int goal)
    {
        _goal = goal;
        _path.Clear();
        if (goal < 0 || goal == _at || _at < 0) return;

        int n = nodes.Count;
        if (_prev == null || _prev.Length != n) _prev = new int[n];
        for (int i = 0; i < n; i++) _prev[i] = Unvisited;
        _prev[_at] = -1;

        _queue.Clear();
        _queue.Enqueue(_at);
        while (_queue.Count > 0)
        {
            int cur = _queue.Dequeue();
            if (cur == goal) break;
            var links = nodes[cur].links;
            if (links == null) continue;
            foreach (int next in links)
            {
                if (next < 0 || next >= n || _prev[next] != Unvisited) continue;
                _prev[next] = cur;
                _queue.Enqueue(next);
            }
        }

        if (_prev[goal] == Unvisited) { _goal = -1; return; }     // nothing links there
        for (int c = goal; c != _at; c = _prev[c]) _path.Insert(0, c);
        _segFrom = transform.position;

        // Every edge in the graph is a vetted straight line, but the dog is not always standing on one:
        // a free approach walks it off-node toward the player. Setting off for the next waypoint from
        // there is an UNVETTED line, and at the foot of the stairs it cuts the corner straight through
        // the banister. So if that first leg is not clear, retrace to the waypoint it left first — the
        // way it came out is the one line off the graph known to be walkable.
        if (_path.Count > 0 && Blocked(transform.position, nodes[_path[0]].position))
            _path.Insert(0, _at);
    }

    const int Unvisited = -2;

    // The waypoint nearest a point, with height counted heavily so it never picks the one on the
    // storey above or below just because it is closer in plan.
    int Nearest(Vector3 p)
    {
        int best = -1;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] == null) continue;
            Vector3 d = nodes[i].position - p;
            float dy = d.y * floorPenalty;
            float sqr = d.x * d.x + d.z * d.z + dy * dy;
            if (sqr < bestSqr) { bestSqr = sqr; best = i; }
        }
        return best;
    }

    // The neighbour of 'from' that gets meaningfully closer to the target — the greedy step that walks
    // the dog round an obstruction. The margin keeps it from ping-ponging between two equal waypoints.
    int BestNeighbour(int from, Vector3 target)
    {
        if (from < 0 || nodes[from].links == null) return -1;
        float here = Flat(nodes[from].position - target).magnitude;
        int best = -1;
        float bestDist = here - 0.5f;
        foreach (int next in nodes[from].links)
        {
            if (next < 0 || next >= nodes.Count || nodes[next] == null) continue;
            float d = Flat(nodes[next].position - target).magnitude
                      + Mathf.Abs(nodes[next].position.y - target.y) * floorPenalty;
            if (d < bestDist) { bestDist = d; best = next; }
        }
        return best;
    }

    // A wander target: never where it already is, never mid-flight on the stairs, and biased toward
    // (or deliberately away from) its current storey so it takes itself up and down on its own.
    int PickRoamTarget()
    {
        float y = transform.position.y;
        bool preferSameFloor = Random.value < sameFloorBias;

        for (int pass = 0; pass < 2; pass++)
        {
            _bag.Clear();
            for (int i = 0; i < nodes.Count; i++)
            {
                if (i == _at || nodes[i] == null || nodes[i].onStairs) continue;
                if (pass == 0)
                {
                    bool same = Mathf.Abs(nodes[i].position.y - y) <= floorEpsilon;
                    if (same != preferSameFloor) continue;
                }
                _bag.Add(i);
            }
            if (_bag.Count > 0) return _bag[Random.Range(0, _bag.Count)];
        }
        return -1;
    }

    // Is there a wall between these two floor points? The shell, the partitions and the stair rails are
    // all box colliders, so this is a real wall test; the furniture is collider-less sprites, which is
    // exactly why the graph routes round it by hand instead.
    bool Blocked(Vector3 a, Vector3 b)
    {
        a.y += sightHeight;
        b.y += sightHeight;
        return Physics.Linecast(a, b, wallMask, QueryTriggerInteraction.Ignore);
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

#if UNITY_EDITOR
    // The graph is invisible otherwise, and a waypoint dropped inside the sofa is only obvious on sight.
    void OnDrawGizmosSelected()
    {
        if (nodes == null) return;
        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            if (node == null) continue;
            Gizmos.color = node.onStairs ? new Color(1f, 0.7f, 0.2f) : new Color(0.4f, 0.9f, 1f);
            Gizmos.DrawSphere(node.position, 0.18f);
            if (node.links == null) continue;
            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.45f);
            foreach (int next in node.links)
                if (next > i && next < nodes.Count && nodes[next] != null)
                    Gizmos.DrawLine(node.position, nodes[next].position);
        }
    }
#endif
}
