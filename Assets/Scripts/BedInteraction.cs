using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// The lobby bed: walking near it shows a hint; pressing E enters the nightmare.
/// </summary>
public class BedInteraction : MonoBehaviour
{
    public float interactRange = 2.2f;
    public Text prompt;          // "Press E to sleep" hint, hidden until near

    Transform _player;

    void Start()
    {
        var p = GameObject.Find("Player");
        if (p != null) _player = p.transform;
        if (prompt != null) prompt.enabled = false;
    }

    void Update()
    {
        if (_player == null) return;

        bool near = Vector2.Distance(_player.position, transform.position) <= interactRange;
        if (prompt != null && prompt.enabled != near) prompt.enabled = near;
        if (!near) return;

        bool pressed;
#if ENABLE_INPUT_SYSTEM
        pressed = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        pressed = Input.GetKeyDown(KeyCode.E);
#endif
        if (pressed && GameManager.Instance != null)
            GameManager.Instance.EnterNightmare();
    }
}
