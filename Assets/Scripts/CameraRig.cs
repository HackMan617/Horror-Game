using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Sits on a pivot at the player's head (child of the player, so it inherits yaw).
/// Mouse Y pitches it; V toggles first/third person. The child camera sits at the
/// pivot (first person) or behind it (third person), so pitch orbits in 3rd person.
/// Hold C to look behind: the camera arm swings 180 so it views the player's front
/// (and what's behind them) — this is what surfaces the front-facing sprite.
/// Hides the player billboard in first person. Locks the cursor for mouse-look.
/// Greens the view as it tilts upward (so you can't see past the grass) and draws a
/// small crosshair while in first person.
/// </summary>
public class CameraRig : MonoBehaviour
{
    public Camera cam;
    public SpriteRenderer playerSprite;   // hidden in first person
    public float thirdPersonDistance = 4.5f;
    public float mouseSensitivity = 0.12f;
    public float minPitch = -60f;
    public float maxPitch = 70f;
    public bool startFirstPerson = false;

    [Header("Grass fill (greens the view as you look up)")]
    public bool grassFillEnabled = true;
    public Color grassFillColor = new Color(0.40f, 0.53f, 0.30f);   // grassy green
    public float grassFillStartPitch = -20f;   // pitch (deg) where the green starts to appear
    public float grassFillFullPitch = -55f;    // pitch (deg) where the green is fully opaque
    [Range(0f, 1f)] public float grassFillMaxAlpha = 1f;

    [Header("First-person crosshair")]
    public bool showCrosshair = true;
    public Color crosshairColor = new Color(1f, 1f, 1f, 0.75f);
    public float crosshairArm = 8f;         // length of each arm, in pixels
    public float crosshairThickness = 2f;   // arm thickness, in pixels
    public float crosshairGap = 4f;         // gap between the centre and each arm

    bool _firstPerson;
    float _pitch;
    Texture2D _white;

    void Start()
    {
        _firstPerson = startFirstPerson;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        _white = new Texture2D(1, 1);
        _white.SetPixel(0, 0, Color.white);
        _white.Apply();

        Apply();
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

        float mouseY = 0f;
        bool toggle = false;
        bool lookBack = false;

#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        var kb = Keyboard.current;
        if (mouse != null) mouseY = mouse.delta.ReadValue().y * mouseSensitivity;
        if (kb != null) { toggle = kb.vKey.wasPressedThisFrame; lookBack = kb.cKey.isPressed; }
#else
        mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * 10f;
        toggle = Input.GetKeyDown(KeyCode.V);
        lookBack = Input.GetKey(KeyCode.C);
#endif

        if (toggle) { _firstPerson = !_firstPerson; Apply(); }

        _pitch = Mathf.Clamp(_pitch - mouseY, minPitch, maxPitch);
        // Hold C to look behind: swing the camera arm 180 so it views the player's
        // front (and the area behind them). The billboard animator then shows the
        // front frames, since the camera is now in front of the player.
        float lookYaw = lookBack ? 180f : 0f;
        transform.localRotation = Quaternion.Euler(_pitch, lookYaw, 0f);
    }

    void Apply()
    {
        if (playerSprite != null) playerSprite.enabled = !_firstPerson;
        if (cam != null)
        {
            cam.transform.localPosition = _firstPerson ? Vector3.zero : new Vector3(0f, 0f, -thirdPersonDistance);
            cam.transform.localRotation = Quaternion.identity;
        }
    }

    void OnGUI()
    {
        if (_white == null) return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

        // Grass closes over the view as the camera tilts up, so you can't see past it.
        // _pitch is negative when looking up (min = full up), positive when looking down.
        if (grassFillEnabled)
        {
            float up = Mathf.InverseLerp(grassFillStartPitch, grassFillFullPitch, _pitch);
            float a = up * grassFillMaxAlpha;
            if (a > 0.001f)
            {
                var gc = GUI.color;
                GUI.color = new Color(grassFillColor.r, grassFillColor.g, grassFillColor.b, a);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _white);
                GUI.color = gc;
            }
        }

        // A simple four-arm crosshair with a centre gap, drawn only in first person.
        if (showCrosshair && _firstPerson)
        {
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            float t = crosshairThickness, arm = crosshairArm, gap = crosshairGap;

            var prev = GUI.color;
            GUI.color = crosshairColor;
            GUI.DrawTexture(new Rect(cx - gap - arm, cy - t * 0.5f, arm, t), _white);   // left
            GUI.DrawTexture(new Rect(cx + gap,       cy - t * 0.5f, arm, t), _white);   // right
            GUI.DrawTexture(new Rect(cx - t * 0.5f,  cy - gap - arm, t, arm), _white);  // up
            GUI.DrawTexture(new Rect(cx - t * 0.5f,  cy + gap,       t, arm), _white);  // down
            GUI.color = prev;
        }
    }
}
