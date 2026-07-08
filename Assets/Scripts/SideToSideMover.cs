using UnityEngine;

/// <summary>
/// Glides a billboard prop back and forth along world X around its start position, so it reads as
/// TRAVELLING rather than hovering in place. Used for the roadside crows: paired with a
/// <see cref="LoopSpriteAnimator"/> flap they cross side-to-side over the road instead of floating
/// in the air just cycling their wing frames. The Y (height) and Z are left untouched.
/// </summary>
public class SideToSideMover : MonoBehaviour
{
    [Tooltip("Half the sweep width in world units — the crow drifts start.x ± amplitude along X.")]
    public float amplitude = 2.5f;
    [Tooltip("Sweep speed in radians/second (a full left-right-left cycle every 2*PI/speed seconds).")]
    public float speed = 0.8f;
    [Tooltip("Start at a random point in the sweep so multiple crows aren't in lock-step.")]
    public bool randomPhase = true;

    float _centerX;
    float _phase;

    void Start()
    {
        _centerX = transform.position.x;
        if (randomPhase) _phase = Random.Range(0f, Mathf.PI * 2f);
    }

    void Update()
    {
        _phase += Time.deltaTime * speed;
        var p = transform.position;
        p.x = _centerX + Mathf.Sin(_phase) * amplitude;
        transform.position = p;
    }
}
