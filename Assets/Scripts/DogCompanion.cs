using UnityEngine;

/// <summary>
/// Apricot dog companion for the overworld. Trails the player on the XZ plane,
/// stopping a short distance away so it stays on-screen, plays a walk cycle while
/// moving and a sitting idle loop while stopped. Hidden during the nightmare
/// (overworld only for now). A Billboard keeps it facing the camera.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class DogCompanion : MonoBehaviour
{
    public Transform player;
    public NightmareController nightmare;   // hide while the nightmare is active
    public Sprite[] idleFrames;
    public Sprite[] walkFrames;
    public float fps = 6f;
    public float followDistance = 2.5f;     // stop this far from the player (stays visible)
    public float speed = 4.5f;              // a little faster than the walk so it can keep up

    SpriteRenderer _sr;
    float _t;
    bool _hidden;

    void Awake() { _sr = GetComponent<SpriteRenderer>(); }

    void Update()
    {
        // Overworld only: vanish during the nightmare.
        if (nightmare != null && nightmare.IsNightmare)
        {
            if (!_hidden) { _sr.enabled = false; _hidden = true; }
            return;
        }
        if (_hidden) { _sr.enabled = true; _hidden = false; }

        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;
        if (player == null) return;

        // Follow on the ground plane, stopping short so the player can see the dog.
        Vector3 to = player.position - transform.position;
        to.y = 0f;
        float dist = to.magnitude;

        bool walking = dist > followDistance;
        if (walking)
        {
            float move = Mathf.Min(speed * Time.deltaTime, dist - followDistance);
            transform.position += (to / dist) * move;
        }

        // Both states animate (walk cycle / sitting pant); pick the set by movement.
        var frames = walking ? walkFrames : idleFrames;
        if (frames != null && frames.Length > 0)
        {
            _t += Time.deltaTime * fps;
            _sr.sprite = frames[((int)_t) % frames.Length];
        }
    }
}
