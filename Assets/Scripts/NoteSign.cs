using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// A readable note pinned to a door / wall (Assets/Animation/NOTE.md). From a distance it shows the
/// small "far" note (writing illegible) on its own SpriteRenderer; walk up and press E to read it and
/// the "near" close-up (the actual text) fills the screen. Both views hold a slow 2-frame paper droop.
/// Press E again — or walk away — to put it back. The mounted note is fixed-facing (not billboarded),
/// so its paper never flips to a mirrored/backwards view.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class NoteSign : MonoBehaviour
{
    public Transform player;
    [Tooltip("Mounted-on-the-door view (illegible) — a slow 2-frame droop.")]
    public Sprite[] farFrames;
    [Tooltip("Readable close-up (the text) — shown fullscreen while reading.")]
    public Sprite[] nearFrames;
    public float range = 3.5f;
    public string prompt = "Press E to read";
    public string closePrompt = "Press E to close";
    [Range(0f, 1f)] public float dimAlpha = 0.82f;
    public float droopFps = 0.8f;                 // slow paper droop

    SpriteRenderer _sr;
    Texture2D _dim;
    bool _reading;
    float _t;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        if (_sr != null && farFrames != null && farFrames.Length > 0) _sr.sprite = farFrames[0];
        _dim = new Texture2D(1, 1);
        _dim.SetPixel(0, 0, Color.white);
        _dim.Apply();
    }

    int Droop(int len) => (len > 0) ? ((int)(_t * droopFps)) % len : 0;

    void Update()
    {
        _t += Time.deltaTime;
        if (_sr != null && farFrames != null && farFrames.Length > 0)
            _sr.sprite = farFrames[Droop(farFrames.Length)];       // slow droop on the mounted note

        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

        bool inRange = player != null &&
            Vector3.Distance(Flat(player.position), Flat(transform.position)) <= range;

        if (_reading)
        {
            if (DialogUI.Instance != null) DialogUI.Instance.ShowPrompt(closePrompt);
            if (EPressed() || !inRange) _reading = false;          // close on E, or when walking away
            return;
        }

        if (!inRange) return;
        if (DialogUI.Instance != null) DialogUI.Instance.ShowPrompt(prompt);
        if (EPressed() && nearFrames != null && nearFrames.Length > 0) _reading = true;
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    void OnGUI()
    {
        if (!_reading || nearFrames == null || nearFrames.Length == 0) return;
        var sp = nearFrames[Droop(nearFrames.Length)];
        if (sp == null) return;

        var prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, dimAlpha);               // dim the world behind the note
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _dim);
        GUI.color = Color.white;

        // Draw the near frame's sub-rect of the atlas, centred, preserving its aspect.
        var tex = sp.texture;
        Rect tr = sp.textureRect;                                  // pixels, bottom-left origin
        Rect uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
        float h = Screen.height * 0.6f;
        float w = h * (tr.width / tr.height);
        GUI.DrawTextureWithTexCoords(new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h), tex, uv);
        GUI.color = prev;
    }

    bool EPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }
}
