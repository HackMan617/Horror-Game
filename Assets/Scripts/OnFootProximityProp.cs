using UnityEngine;

/// <summary>
/// Gates a roadside billboard's animation (and optionally its visibility) to when the player is ON FOOT
/// and NEARBY. While driving, or when far away, the flip-book (<see cref="LoopSpriteAnimator"/>) is frozen
/// on frame 0 — so the roadside isn't a wall of wiggling sprites blurring past the windscreen. Props with
/// <see cref="hideWhenAway"/> (crows flapping across the view, blowing debris) are hidden entirely until
/// you park and walk up to them; landmark props (dead trees, signs) stay visible but static while driving.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class OnFootProximityProp : MonoBehaviour
{
    [Tooltip("On-foot distance (XZ) within which the prop animates / appears.")]
    public float showRange = 14f;
    [Tooltip("Hide the sprite entirely unless the player is on foot and nearby (crows, debris). " +
             "Off = always visible, but only animates when you're on foot nearby (trees, signs).")]
    public bool hideWhenAway = false;

    SpriteRenderer _sr;
    LoopSpriteAnimator _anim;
    TruckDriver _truck;
    Transform _player;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _anim = GetComponent<LoopSpriteAnimator>();
    }

    void Update()
    {
        if (_truck == null) _truck = FindObjectOfType<TruckDriver>();
        if (_player == null) { var p = GameObject.Find("Player"); if (p != null) _player = p.transform; }

        bool driving = _truck != null && _truck.IsDriving;
        bool near = false;
        if (_player != null)
        {
            Vector3 d = _player.position - transform.position; d.y = 0f;
            near = d.sqrMagnitude <= showRange * showRange;
        }
        bool active = !driving && near;   // on foot AND close

        if (_anim != null) _anim.enabled = active;
        if (!active && _anim != null && _anim.frames != null && _anim.frames.Length > 0)
            _sr.sprite = _anim.frames[0];         // rest on the parked frame
        _sr.enabled = hideWhenAway ? active : true;
    }
}
