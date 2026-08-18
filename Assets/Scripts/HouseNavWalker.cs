using System.Collections.Generic;
using UnityEngine;

/// <summary>One waypoint on a companion's run of the house.</summary>
[System.Serializable]
public class HouseNavNode
{
    public string name;
    public Vector3 position;
    [Tooltip("Indices of the nodes this one connects to. Undirected — the builder writes both ends.")]
    public int[] links;
    [Tooltip("Mid-flight on the staircase: a companion climbs through here but never settles or " +
             "free-walks off it (its floor is a slope, not a plane).")]
    public bool onStairs;
}

/// <summary>
/// The walk itself, shared by everyone who has the run of the two-storey cabin — the dog
/// (<see cref="DogHouseNav"/>) and the partner (<see cref="PartnerHouseRoam"/>). Holds the waypoint
/// graph and knows how to cross it; what each companion *wants* (trail the player, cook at the hearth)
/// lives in the subclass.
///
/// <para><b>Why a waypoint graph and not a NavMesh.</b> The interior is generated in code
/// (HorrorGame3DSetup.BuildCabinInterior) and every stick of furniture in it is a billboarded sprite
/// with no collider — a baked NavMesh would see one empty 20x20 box per storey and happily route a
/// companion straight through the sofa. The shell's real geometry is all constants in the builder, so
/// the graph is laid out from those same numbers instead: a loop of the ground floor that keeps clear
/// of the living-room group, the flight up the south-east stairwell, the landing, and the bedroom
/// through its door. See HorrorGame3DSetup.BuildHouseNav.</para>
///
/// <para><b>Height comes from the graph, not from the floor.</b> y is interpolated along whichever
/// edge is being walked, so the stair edges (a straight constant-slope ramp) land the walker exactly
/// on the tread line without a single raycast.</para>
///
/// <para>The one rule when moving a waypoint: an edge is a straight line someone will walk down, so
/// both ends being clear is not enough. Select the object to see the whole graph in the Scene view.</para>
/// </summary>
public abstract class HouseNavWalker : MonoBehaviour
{
    [Tooltip("The house's walkable waypoints. Laid out by Tools > Horror Game > Build Cabin Interior.")]
    public List<HouseNavNode> nodes = new List<HouseNavNode>();

    [Header("Geometry")]
    public float arriveRadius = 0.35f;
    [Tooltip("Height difference under which two points count as the same storey (the slab is 3.2m up).")]
    public float floorEpsilon = 1.2f;
    [Tooltip("How much a storey's worth of height counts against a waypoint when picking the nearest " +
             "one, so a walker never targets the node directly above or below it.")]
    public float floorPenalty = 3f;
    [Tooltip("Height the clear-line test is taken at: over the thresholds, under the stair rail's cap.")]
    public float sightHeight = 0.45f;
    public LayerMask wallMask = ~0;

    public bool Ready => nodes != null && nodes.Count >= 2;

    /// <summary>
    /// Flat direction of the last step that actually moved. Never zero — it holds the last heading
    /// while stopped, so a walker that stops keeps facing the way it was going.
    /// </summary>
    public Vector3 Heading => _heading;

    /// <summary>The waypoint it is standing at (or last left); -1 until it joins the graph.</summary>
    public int AtNode => _at;

    protected int _at = -1;                       // the waypoint it is standing at / last left
    protected int _goal = -1;
    protected readonly List<int> _path = new List<int>();
    protected Vector3 _segFrom;                   // where the current edge was entered (the y lerp's anchor)

    Vector3 _heading = Vector3.forward;

    // BFS scratch, kept alive so pathing never allocates mid-walk
    int[] _prev;
    readonly Queue<int> _queue = new Queue<int>();
    readonly List<int> _bag = new List<int>();

    const int Unvisited = -2;

    protected virtual void Awake() => _segFrom = transform.position;

    /// <summary>Snap onto the graph without teleporting: walk on from wherever the builder dropped us.</summary>
    protected void EnsureOnGraph()
    {
        if (_at >= 0) return;
        _at = Nearest(transform.position);
        _segFrom = transform.position;
    }

    // Walk one frame along the current edge. y is interpolated across the edge rather than sampled off
    // the floor, so the stair edges (one straight constant-slope run) sit the walker on the treads.
    protected bool Advance(float speed, float dt)
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
            if (dist > 0.0001f) _heading = d / dist;
            transform.position = to;
            _segFrom = to;
            return true;
        }

        _heading = d / dist;
        here += _heading * step;
        float span = Vector3.Distance(Flat(_segFrom), Flat(to));
        float t = span > 0.001f ? Mathf.Clamp01(Vector3.Distance(Flat(_segFrom), here) / span) : 1f;
        transform.position = new Vector3(here.x, Mathf.Lerp(_segFrom.y, to.y, t), here.z);
        return true;
    }

    /// <summary>Step straight at a point, off the graph. Only ever call across a line you have tested.</summary>
    protected void StepFree(Vector3 dir, float distance)
    {
        _heading = dir;
        transform.position += dir * distance;
        _segFrom = transform.position;
    }

    // Breadth-first over the graph: fewest hops, which on a graph this evenly spaced is also the
    // shortest way round. Runs only when the goal changes, not every frame.
    protected void Repath(int goal)
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

        // Every edge in the graph is a vetted straight line, but a walker is not always standing on
        // one: a free approach walks it off-node. Setting off for the next waypoint from there is an
        // UNVETTED line, and at the foot of the stairs it cuts the corner straight through the
        // banister. So if that first leg is not clear, retrace to the waypoint it left first — the way
        // it came out is the one line off the graph known to be walkable.
        if (_path.Count > 0 && Blocked(transform.position, nodes[_path[0]].position))
            _path.Insert(0, _at);
    }

    // The waypoint nearest a point, with height counted heavily so it never picks the one on the
    // storey above or below just because it is closer in plan.
    protected int Nearest(Vector3 p)
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
    // a companion round an obstruction. The margin keeps it from ping-ponging between two waypoints.
    protected int BestNeighbour(int from, Vector3 target)
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
    protected int PickRoamTarget(float sameFloorBias)
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

    /// <summary>Index of the first waypoint with this name, or -1. Names come from the builder.</summary>
    protected int NodeNamed(string name)
    {
        for (int i = 0; i < nodes.Count; i++)
            if (nodes[i] != null && nodes[i].name == name) return i;
        return -1;
    }

    // Is there a wall between these two floor points? The shell, the partitions and the stair rails are
    // all box colliders, so this is a real wall test; the furniture is collider-less sprites, which is
    // exactly why the graph routes round it by hand instead.
    protected bool Blocked(Vector3 a, Vector3 b)
    {
        a.y += sightHeight;
        b.y += sightHeight;
        return Physics.Linecast(a, b, wallMask, QueryTriggerInteraction.Ignore);
    }

    protected static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

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
