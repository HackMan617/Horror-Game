using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Smooth top-down camera follow with mouse drag-to-peek.
///   - Normally follows the player (X/Y only, so the view stays bird's-eye),
///     clamped so the view never shows past the world bounds.
///   - Left-click + drag pans the camera to look around the map (grab-and-pull:
///     the grabbed point stays under the cursor). The view stays where you left
///     it after releasing.
///   - The instant the player moves, the camera snaps back to following them.
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
    PlayerController2D _pc;
    Vector3 _vel;
    bool _freeLook;        // peeking with the mouse instead of following
    Vector3 _grabWorld;    // world point grabbed on mouse-down

    void Awake() { _cam = GetComponent<Camera>(); }

    void Start()
    {
        if (target == null)
        {
            var p = GameObject.Find("Player");
            if (p != null) target = p.transform;
        }
        if (target != null)
        {
            _pc = target.GetComponent<PlayerController2D>();
            transform.position = ClampXY(target.position);   // snap, no easing
        }
    }

    void LateUpdate()
    {
        if (target == null) return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

        // Moving always wins — resume following the player.
        bool moving = _pc != null && _pc.MoveInput.sqrMagnitude > 0.0001f;
        if (moving) _freeLook = false;
        else HandleMousePan();

        if (_freeLook) return;   // hold the peeked position until the player moves

        transform.position = Vector3.SmoothDamp(transform.position, ClampXY(target.position), ref _vel, smoothTime);
    }

    void HandleMousePan()
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse == null) return;
        bool down = mouse.leftButton.wasPressedThisFrame;
        bool held = mouse.leftButton.isPressed;
        Vector2 screen = mouse.position.ReadValue();
#else
        bool down = Input.GetMouseButtonDown(0);
        bool held = Input.GetMouseButton(0);
        Vector2 screen = Input.mousePosition;
#endif
        if (down) { _freeLook = true; _grabWorld = ScreenToWorld(screen); }
        if (_freeLook && held)
        {
            // Move the camera so the grabbed world point stays under the cursor.
            Vector3 now = ScreenToWorld(screen);
            transform.position = ClampXY(transform.position + (_grabWorld - now));
        }
    }

    Vector3 ScreenToWorld(Vector2 screen)
    {
        var sp = new Vector3(screen.x, screen.y, -transform.position.z);
        var w = _cam.ScreenToWorldPoint(sp);
        w.z = transform.position.z;
        return w;
    }

    Vector3 ClampXY(Vector3 p)
    {
        float z = transform.position.z;
        if (!clampToBounds) return new Vector3(p.x, p.y, z);
        float hh = _cam.orthographicSize, hw = hh * _cam.aspect;
        float minX = worldMin.x + hw, maxX = worldMax.x - hw;
        float minY = worldMin.y + hh, maxY = worldMax.y - hh;
        float x = (minX <= maxX) ? Mathf.Clamp(p.x, minX, maxX) : (worldMin.x + worldMax.x) * 0.5f;
        float y = (minY <= maxY) ? Mathf.Clamp(p.y, minY, maxY) : (worldMin.y + worldMax.y) * 0.5f;
        return new Vector3(x, y, z);
    }
}
