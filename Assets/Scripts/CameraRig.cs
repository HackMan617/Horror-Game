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

    bool _firstPerson;
    float _pitch;

    void Start()
    {
        _firstPerson = startFirstPerson;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
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
}
