using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Minimal driver so we can SEE the sprite animate:
///   A / D  (or Left / Right) ........ move + walk
///   hold Shift while moving .......... run
///   J or Space ....................... Attack 1
///   K ................................ Attack 2
/// Movement is clamped horizontally so the character stays on-screen.
/// This is a preliminary test harness, not the real game controller.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Animator))]
public class PlayerController2D : MonoBehaviour
{
    [Header("Speeds (units / second)")]
    public float walkSpeed = 2f;
    public float runSpeed  = 5f;

    [Header("Keep the demo on-screen")]
    public float clampX = 6f;

    static readonly int SpeedHash   = Animator.StringToHash("Speed");
    static readonly int AttackHash  = Animator.StringToHash("Attack");
    static readonly int Attack2Hash = Animator.StringToHash("Attack2");

    Animator _anim;
    SpriteRenderer _sr;
    int _facing = 1;

    void Awake()
    {
        _anim = GetComponent<Animator>();
        _sr   = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        float x = 0f;
        bool running = false, attack = false, attack2 = false;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
            running = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            attack  = kb.jKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
            attack2 = kb.kKey.wasPressedThisFrame;
        }
#else
        x        = Input.GetAxisRaw("Horizontal");
        running  = Input.GetKey(KeyCode.LeftShift);
        attack   = Input.GetKeyDown(KeyCode.J) || Input.GetKeyDown(KeyCode.Space);
        attack2  = Input.GetKeyDown(KeyCode.K);
#endif

        float speed = running ? runSpeed : walkSpeed;

        Vector3 p = transform.position;
        p.x = Mathf.Clamp(p.x + x * speed * Time.deltaTime, -clampX, clampX);
        transform.position = p;

        if (x > 0.01f) _facing = 1;
        else if (x < -0.01f) _facing = -1;
        _sr.flipX = _facing < 0;

        _anim.SetFloat(SpeedHash, Mathf.Abs(x) * speed);
        if (attack)  _anim.SetTrigger(AttackHash);
        if (attack2) _anim.SetTrigger(Attack2Hash);
    }
}
