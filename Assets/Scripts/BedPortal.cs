using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// The bed that sends the player into the nightmare. When the player is within
/// range it shows a prompt; pressing E triggers NightmareController.EnterNightmare().
/// One-way for now: once the nightmare starts, the prompt/interaction stops.
/// </summary>
public class BedPortal : MonoBehaviour
{
    public Transform player;
    public NightmareController nightmare;
    public float range = 2.5f;
    public string prompt = "Press E to sleep";

    bool _inRange;

    void Update()
    {
        if (player == null || (nightmare != null && nightmare.IsNightmare)) { _inRange = false; return; }
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) { _inRange = false; return; }

        Vector3 a = player.position; a.y = 0f;
        Vector3 b = transform.position; b.y = 0f;
        _inRange = Vector3.Distance(a, b) <= range;
        if (!_inRange) return;

        if (DialogUI.Instance != null) DialogUI.Instance.ShowPrompt(prompt);

        bool pressed;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        pressed = kb != null && kb.eKey.wasPressedThisFrame;
#else
        pressed = Input.GetKeyDown(KeyCode.E);
#endif
        if (pressed && nightmare != null) nightmare.EnterNightmare();
    }
}
