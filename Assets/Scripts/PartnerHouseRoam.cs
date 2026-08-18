using UnityEngine;

/// <summary>
/// Gives the partner the run of the house, the way the dog already has it. They amble the same
/// waypoint graph the dog walks (<see cref="HouseNavWalker"/>) — round the ground floor, up and down
/// the stairwell flight, out onto the landing and into the bedroom — stopping now and then to stand
/// about, and every so often settling in at the hearth to cook for a good while.
///
/// <para>Unlike the dog they never come looking for you: a partner keeping house is somewhere in it,
/// and you find them. <see cref="PartnerController"/> reads <see cref="Current"/> and
/// <see cref="HouseNavWalker.Heading"/> each frame to pick the animation — the 8-way walk cycle while
/// moving, the cooking loop at the hearth, the resting stance while stood still — and holds them in
/// place while you are talking to them.</para>
/// </summary>
[DisallowMultipleComponent]
public class PartnerHouseRoam : HouseNavWalker
{
    /// <summary>What they are doing while stopped. Drives which loop <see cref="PartnerController"/> plays.</summary>
    public enum Activity { Walking, Resting, Cooking }

    [Header("Wandering")]
    [Tooltip("An unhurried indoor walk — slower than the dog's amble, which is what makes the dog read " +
             "as the one dashing about.")]
    public float roamSpeed = 1.5f;
    [Tooltip("Seconds they stand about on reaching a waypoint (min, max).")]
    public Vector2 pause = new Vector2(3f, 9f);
    [Range(0f, 1f)]
    [Tooltip("Chance a new target is on the storey they are already on. The rest of the time they " +
             "deliberately pick the other storey, which is what takes them up and down the stairs.")]
    public float sameFloorBias = 0.7f;

    [Header("Cooking")]
    [Tooltip("The waypoint that counts as the stove — the hearth on the ground floor. Named by the " +
             "builder (HorrorGame3DSetup.BuildHouseNav); blank or unmatched simply means they never cook.")]
    public string cookNodeName = "Hearth";
    [Range(0f, 1f)]
    [Tooltip("Chance they settle in to cook on arriving at the hearth rather than just passing through.")]
    public float cookChance = 0.65f;
    [Tooltip("Seconds spent over the pot once they do (min, max) — long enough that you can walk in on it.")]
    public Vector2 cookDuration = new Vector2(14f, 26f);
    [Tooltip("Seconds between the times they make a POINT of going to cook (min, max). Left purely to " +
             "chance the hearth is one waypoint in twenty-odd and, at walking pace, comes up perhaps " +
             "once in ten minutes — so you would never once walk in on it. This is the errand that " +
             "actually takes them there.")]
    public Vector2 cookEvery = new Vector2(75f, 150f);

    /// <summary>What they are doing right now.</summary>
    public Activity Current { get; private set; } = Activity.Resting;

    float _pauseTimer, _cookTimer;
    int _cookNode = -2;               // -2 = not looked up yet, -1 = no such waypoint

    protected override void Awake()
    {
        base.Awake();
        _cookTimer = Random.Range(cookEvery.x, cookEvery.y);
    }

    /// <summary>
    /// Drive the partner for one frame. Returns true if they actually moved, which is what
    /// <see cref="PartnerController"/> advances the walk cycle off.
    /// </summary>
    public bool Step(float dt, bool holdStill)
    {
        if (!Ready) { Current = Activity.Resting; return false; }

        EnsureOnGraph();

        // Talking to you outranks the housework: they stop where they are and keep the route they were
        // walking, so when you are done they carry on to wherever they were going.
        if (holdStill) { Current = Activity.Resting; return false; }

        _cookTimer -= dt;                     // counts down through walking and standing alike

        if (_pauseTimer > 0f)
        {
            _pauseTimer -= dt;
            return false;                     // Current already holds Resting / Cooking
        }

        if (_path.Count == 0)
        {
            Repath(NextTarget());
            if (_path.Count == 0) { Settle(); return false; }
        }

        bool moved = Advance(roamSpeed, dt);
        Current = Activity.Walking;
        if (_path.Count == 0) Settle();       // arrived: stand about, or get the pot on
        return moved;
    }

    // Where to head next: the hearth when they are due a turn at the stove, otherwise anywhere.
    int NextTarget()
    {
        ResolveCookNode();
        if (_cookTimer <= 0f && _cookNode >= 0 && _cookNode != _at) return _cookNode;
        return PickRoamTarget(sameFloorBias);
    }

    // Decide what to do with the stop that has just started.
    void Settle()
    {
        ResolveCookNode();

        // At the hearth: cook if they came here to, and sometimes even if they only wandered by.
        if (_at >= 0 && _at == _cookNode && (_cookTimer <= 0f || Random.value < cookChance))
        {
            Current = Activity.Cooking;
            _pauseTimer = Random.Range(cookDuration.x, cookDuration.y);
            _cookTimer = Random.Range(cookEvery.x, cookEvery.y);
            return;
        }

        Current = Activity.Resting;
        _pauseTimer = Random.Range(pause.x, pause.y);
    }

    void ResolveCookNode()
    {
        if (_cookNode == -2) _cookNode = string.IsNullOrEmpty(cookNodeName) ? -1 : NodeNamed(cookNodeName);
    }
}
