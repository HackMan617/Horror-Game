using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 2.5D first/third-person movement. Mouse X yaws the player; WASD moves relative
/// to facing; Shift runs. Pitch (mouse Y) and the 1st/3rd toggle live on CameraRig.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController3D : MonoBehaviour
{
    public float walkSpeed = 4f;
    public float runSpeed = 7f;
    public float mouseSensitivity = 0.12f;
    public float gravity = -20f;

    public Vector2 MoveInput { get; private set; }

    CharacterController _cc;
    float _yaw;
    float _vSpeed;

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _yaw = transform.eulerAngles.y;
    }

    void Update()
    {
        float mouseX = 0f, x = 0f, z = 0f;
        bool run = false;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (mouse != null) mouseX = mouse.delta.ReadValue().x * mouseSensitivity;
        if (kb != null)
        {
            if (kb.aKey.isPressed) x -= 1f;
            if (kb.dKey.isPressed) x += 1f;
            if (kb.sKey.isPressed) z -= 1f;
            if (kb.wKey.isPressed) z += 1f;
            run = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
        }
#else
        mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * 10f;
        x = Input.GetAxisRaw("Horizontal");
        z = Input.GetAxisRaw("Vertical");
        run = Input.GetKey(KeyCode.LeftShift);
#endif

        MoveInput = new Vector2(x, z);

        _yaw += mouseX;
        transform.rotation = Quaternion.Euler(0f, _yaw, 0f);

        Vector3 move = transform.right * x + transform.forward * z;
        if (move.sqrMagnitude > 1f) move.Normalize();
        float speed = run ? runSpeed : walkSpeed;

        if (_cc.isGrounded && _vSpeed < 0f) _vSpeed = -2f;
        _vSpeed += gravity * Time.deltaTime;

        Vector3 velocity = move * speed + Vector3.up * _vSpeed;
        _cc.Move(velocity * Time.deltaTime);
    }
}
