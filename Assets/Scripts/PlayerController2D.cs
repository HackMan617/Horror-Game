using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Top-down player movement:
///   W A S D / arrows .... move in all 4 directions
///   hold Shift .......... run
///   J ................... Attack 1
///   K ................... Attack 2
/// Movement is clamped to a configurable area so the player stays in the room.
/// The sprite still flips on the X axis to face left/right.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Animator))]
public class PlayerController2D : MonoBehaviour
{
    [Header("Speeds (units / second)")]
    public float walkSpeed = 2.5f;
    public float runSpeed  = 5f;

    [Header("Movement bounds (top-down room)")]
    public bool clampToArea = true;
    public Vector2 areaMin = new Vector2(-7.5f, -3.6f);
    public Vector2 areaMax = new Vector2( 7.5f,  3.6f);

    /// <summary>Raw movement intent this frame (x,y in [-1,1]); the camera reads it to resume following.</summary>
    public Vector2 MoveInput { get; private set; }

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
        float x = 0f, y = 0f;
        bool running = false, attack = false, attack2 = false;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  y -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    y += 1f;
            running = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            attack  = kb.jKey.wasPressedThisFrame;
            attack2 = kb.kKey.wasPressedThisFrame;
        }
#else
        x = Input.GetAxisRaw("Horizontal");
        y = Input.GetAxisRaw("Vertical");
        running = Input.GetKey(KeyCode.LeftShift);
        attack  = Input.GetKeyDown(KeyCode.J);
        attack2 = Input.GetKeyDown(KeyCode.K);
#endif

        MoveInput = new Vector2(x, y);
        Vector2 dir = MoveInput;
        if (dir.sqrMagnitude > 1f) dir.Normalize();   // diagonals aren't faster
        float speed = running ? runSpeed : walkSpeed;

        Vector3 p = transform.position + (Vector3)(dir * (speed * Time.deltaTime));
        if (clampToArea)
        {
            p.x = Mathf.Clamp(p.x, areaMin.x, areaMax.x);
            p.y = Mathf.Clamp(p.y, areaMin.y, areaMax.y);
        }
        transform.position = p;

        if (x > 0.01f) _facing = 1;
        else if (x < -0.01f) _facing = -1;
        _sr.flipX = _facing < 0;

        _anim.SetFloat(SpeedHash, dir.magnitude * speed);
        if (attack)  _anim.SetTrigger(AttackHash);
        if (attack2) _anim.SetTrigger(Attack2Hash);
    }
}
