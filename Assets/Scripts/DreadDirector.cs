using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// The single source of truth for the game's <b>dread</b> level (0 = calm … 1 = nightmare). The
/// Dread Detector HUD (<see cref="DreadDetector"/>) reads it, and it is the seam the rest of the
/// horror network (HouseDreadSwap, YardMachine, TileStripQuad, NeighborRobert, the mountain face)
/// will later subscribe to so the whole street goes "wrong" together.
///
/// For now dread is driven manually — the real escalation rules (days survived, story beats,
/// proximity, time-in-nightmare) are a later decision. Use the debug keys ' [ ' and ' ] ' to lower
/// / raise it while testing, or set <see cref="autoRisePerMinute"/> for a slow automatic climb.
/// </summary>
public class DreadDirector : MonoBehaviour
{
    public static DreadDirector Instance { get; private set; }

    [Range(0f, 1f)] public float dread = 0f;

    [Tooltip("Optional slow automatic rise, in dread units per minute (0 = off — manual/debug only).")]
    public float autoRisePerMinute = 0f;

    [Tooltip("Debug: '[' and ']' nudge dread down / up by 0.1 while testing.")]
    public bool debugKeys = true;

    /// <summary>Current dread 0..1 (0 if no director is present in the scene).</summary>
    public static float Value01 => Instance != null ? Mathf.Clamp01(Instance.dread) : 0f;

    void Awake()
    {
        // Persist across scene loads (a re-loaded scene's copy self-destructs) so dread carries
        // through the door between the exterior and interior.
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update()
    {
        if (autoRisePerMinute != 0f)
            dread = Mathf.Clamp01(dread + autoRisePerMinute / 60f * Time.deltaTime);

#if ENABLE_INPUT_SYSTEM
        if (debugKeys)
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.rightBracketKey.wasPressedThisFrame) dread = Mathf.Clamp01(dread + 0.1f);
                if (kb.leftBracketKey.wasPressedThisFrame)  dread = Mathf.Clamp01(dread - 0.1f);
            }
        }
#endif
    }
}
