using UnityEngine;

/// <summary>
/// Smooth top-down camera follow. Tracks the target's X/Y (keeps its own Z and
/// rotation, so it stays a bird's-eye view) and clamps so the view never shows
/// past the world bounds. Snaps to the target on start.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraFollow2D : MonoBehaviour
{
    public Transform target;
    public float smoothTime = 0.15f;

    [Header("World bounds (camera view stays inside)")]
    public bool clampToBounds = true;
    public Vector2 worldMin = new Vector2(-18f, -10f);
    public Vector2 worldMax = new Vector2( 18f,  10f);

    Camera _cam;
    Vector3 _vel;

    void Awake() { _cam = GetComponent<Camera>(); }

    void Start()
    {
        if (target == null)
        {
            var p = GameObject.Find("Player");
            if (p != null) target = p.transform;
        }
        if (target != null) transform.position = Resolve(target.position); // snap, no easing
    }

    void LateUpdate()
    {
        if (target == null) return;
        Vector3 desired = Resolve(target.position);
        transform.position = Vector3.SmoothDamp(transform.position, desired, ref _vel, smoothTime);
    }

    Vector3 Resolve(Vector3 targetPos)
    {
        Vector3 p = new Vector3(targetPos.x, targetPos.y, transform.position.z);
        if (!clampToBounds) return p;

        float hh = _cam.orthographicSize;
        float hw = hh * _cam.aspect;
        float minX = worldMin.x + hw, maxX = worldMax.x - hw;
        float minY = worldMin.y + hh, maxY = worldMax.y - hh;
        p.x = (minX <= maxX) ? Mathf.Clamp(p.x, minX, maxX) : (worldMin.x + worldMax.x) * 0.5f;
        p.y = (minY <= maxY) ? Mathf.Clamp(p.y, minY, maxY) : (worldMin.y + worldMax.y) * 0.5f;
        return p;
    }
}
