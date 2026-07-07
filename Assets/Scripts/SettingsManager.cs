using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Owns the in-game Settings screen and applies its values at runtime. Self-bootstraps into a
/// persistent (<see cref="DontDestroyOnLoad"/>) singleton before the first scene loads, so the panel
/// is available from both the pause menu (<see cref="GameManager"/>) and the main menu
/// (<see cref="MainMenuController"/>), and settings re-apply on every scene load.
///
/// <para>Adjustable now: <b>Sound</b> (drives <see cref="AudioListener.volume"/> — the game has no
/// AudioMixer, so this is the single global bus) and <b>Mouse sensitivity</b> (pushed into both
/// <see cref="CameraRig.mouseSensitivity"/> and <see cref="PlayerController3D.mouseSensitivity"/>).
/// Controls are shown read-only for now (see CHOPPING-style hardcoded key polling across scripts).</para>
///
/// <para><b>Lockable sound</b> (for a future horror beat where the player loses control of their
/// volume): call <see cref="LockSound"/> to force an effective volume the player can't change, and
/// <see cref="UnlockSound"/> to hand control back. While locked the Sound row is disabled and the
/// stored preference is preserved untouched, so unlocking restores exactly what they had.</para>
/// </summary>
public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    /// <summary>True while the Settings panel is on screen.</summary>
    public bool IsOpen => _root != null && _root.activeSelf;

    // ---- lockable sound (horror mechanic hook) ----
    /// <summary>When true the player cannot change sound; <see cref="EffectiveVolume"/> is forced.</summary>
    public bool SoundLocked { get; private set; }
    float _forcedVolume = 1f;
    /// <summary>The volume actually applied to the listener — the forced value while locked, else the player's.</summary>
    public float EffectiveVolume => SoundLocked ? _forcedVolume : SettingsStore.SoundVolume;

    // Lets GameManager tell that the same Escape press just closed the panel (so it doesn't also unpause).
    int _closedByEscFrame = -1;
    public bool ConsumedEscapeThisFrame => _closedByEscFrame == Time.frameCount;

    const float SoundStepSize = 0.05f;   // 5% per click
    const float SensStepSize  = 0.01f;   // shown as a whole number (value * 100)

    Font _font;
    GameObject _root;
    Text _soundValue, _sensValue;
    Button _soundLeft, _soundRight;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        new GameObject("SettingsManager").AddComponent<SettingsManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _font = Resources.Load<Font>("HerculesPixelFontRegular");
        BuildPanel();
        ApplyVolume();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        // AudioListener.volume is global (persists), but re-assert it and push sensitivity into the
        // freshly-spawned CameraRig / PlayerController3D of the new scene.
        ApplyVolume();
        ApplySensitivity();
    }

    void Update()
    {
        if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Close();
            _closedByEscFrame = Time.frameCount;
        }
    }

    // ---------------------------------------------------------------- apply
    /// <summary>Push the effective volume onto the global audio listener.</summary>
    public void ApplyVolume() => AudioListener.volume = EffectiveVolume;

    /// <summary>Push the chosen sensitivity into both mouse-look components in the active scene.</summary>
    public void ApplySensitivity()
    {
        float s = SettingsStore.MouseSensitivity;
        var rig = FindAnyObjectByType<CameraRig>();
        if (rig != null) rig.mouseSensitivity = s;
        var pc = FindAnyObjectByType<PlayerController3D>();
        if (pc != null) pc.mouseSensitivity = s;
    }

    // ---------------------------------------------------------------- lockable sound API
    /// <summary>Seize sound control for a scripted moment: force <paramref name="forcedVolume"/> (0..1)
    /// and disable the player's Sound control until <see cref="UnlockSound"/>. Their saved preference
    /// is left intact.</summary>
    public void LockSound(float forcedVolume)
    {
        SoundLocked = true;
        _forcedVolume = Mathf.Clamp01(forcedVolume);
        ApplyVolume();
        RefreshValues();
    }

    /// <summary>Hand sound control back to the player and restore their chosen volume.</summary>
    public void UnlockSound()
    {
        SoundLocked = false;
        ApplyVolume();
        RefreshValues();
    }

    // ---------------------------------------------------------------- open / close
    public void Open()
    {
        if (_root == null) return;
        EnsureEventSystem();
        RefreshValues();
        _root.SetActive(true);
        Cursor.lockState = CursorLockMode.None;   // panel is clickable regardless of the scene's mouse-look
        Cursor.visible = true;
    }

    public void Close()
    {
        if (_root != null) _root.SetActive(false);
        // Opened only from the (paused) pause menu or the main menu, so a free cursor is correct on the
        // way out. If somehow closed during live gameplay, re-lock so mouse-look resumes cleanly.
        bool paused = GameManager.Instance != null && GameManager.Instance.IsPaused;
        if (!paused && FindAnyObjectByType<CameraRig>() != null)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // ---------------------------------------------------------------- value steppers
    void SoundStep(int dir)
    {
        if (SoundLocked) return;
        SettingsStore.SoundVolume = SettingsStore.SoundVolume + dir * SoundStepSize;
        ApplyVolume();
        RefreshValues();
    }

    void SensStep(int dir)
    {
        SettingsStore.MouseSensitivity = SettingsStore.MouseSensitivity + dir * SensStepSize;
        ApplySensitivity();
        RefreshValues();
    }

    void RestoreDefaults()
    {
        SettingsStore.ResetToDefaults();
        ApplyVolume();
        ApplySensitivity();
        RefreshValues();
    }

    void RefreshValues()
    {
        if (_soundValue != null)
            _soundValue.text = SoundLocked ? "LOCKED" : Mathf.RoundToInt(SettingsStore.SoundVolume * 100f) + "%";
        if (_sensValue != null)
            _sensValue.text = Mathf.RoundToInt(SettingsStore.MouseSensitivity * 100f).ToString();

        if (_soundLeft != null) _soundLeft.interactable = !SoundLocked;
        if (_soundRight != null) _soundRight.interactable = !SoundLocked;
    }

    // ---------------------------------------------------------------- UI construction (runtime, code-built)
    void BuildPanel()
    {
        _root = new GameObject("SettingsCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        DontDestroyOnLoad(_root);
        var canvas = _root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1100;   // above the pause menu (1000) so it covers it when opened in-game
        var sc = _root.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920f, 1080f);
        sc.matchWidthOrHeight = 0.5f;

        var dim = NewUI("Dim", _root.transform).AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.88f);
        Stretch(dim.GetComponent<RectTransform>());

        MakeTitle("Settings", 440f, 84);

        // ---- Audio ----
        MakeHeader("Audio", 330f);
        _soundValue = MakeCyclerRow("Sound", 260f, out _soundLeft, out _soundRight, () => SoundStep(-1), () => SoundStep(+1));

        // ---- Mouse ----
        MakeHeader("Mouse", 140f);
        _sensValue = MakeCyclerRow("Sensitivity", 70f, out _, out _, () => SensStep(-1), () => SensStep(+1));

        // ---- Controls (read-only for now) ----
        MakeHeader("Controls", -60f);
        var controls = MakeText(
            "Move        W A S D\n" +
            "Run         Shift\n" +
            "Interact    E\n" +
            "Look behind C  (hold)\n" +
            "Toggle view V\n" +
            "Pause       Esc",
            new Vector2(0f, -230f), new Vector2(720f, 300f), 34, TextAnchor.UpperCenter);
        controls.color = new Color(0.82f, 0.82f, 0.86f);
        var note = MakeText("Rebinding coming soon", new Vector2(0f, -390f), new Vector2(720f, 40f), 26, TextAnchor.MiddleCenter);
        note.color = new Color(0.6f, 0.6f, 0.64f);
        note.fontStyle = FontStyle.Italic;

        // ---- footer buttons ----
        MakeButton("Defaults", new Vector2(-150f, -470f), new Vector2(260f, 84f), 34, RestoreDefaults);
        MakeButton("Back", new Vector2(150f, -470f), new Vector2(260f, 84f), 40, Close);

        _root.SetActive(false);
    }

    // A "label   < value >" stepper row centred at y. Returns the value Text; hands back the two
    // arrow buttons so the caller can disable them (used to lock the Sound row).
    Text MakeCyclerRow(string label, float y, out Button left, out Button right,
                       UnityEngine.Events.UnityAction onLeft, UnityEngine.Events.UnityAction onRight)
    {
        var lbl = MakeText(label, new Vector2(-250f, y), new Vector2(320f, 70f), 40, TextAnchor.MiddleLeft);
        lbl.color = Color.white;

        left = MakeButton("<", new Vector2(90f, y), new Vector2(70f, 70f), 44, onLeft);
        var value = MakeText("-", new Vector2(210f, y), new Vector2(180f, 70f), 40, TextAnchor.MiddleCenter);
        value.color = new Color(1f, 0.92f, 0.6f);
        right = MakeButton(">", new Vector2(330f, y), new Vector2(70f, 70f), 44, onRight);
        return value;
    }

    void MakeTitle(string text, float y, int size)
    {
        var t = MakeText(text, new Vector2(0f, y), new Vector2(1300f, 200f), size, TextAnchor.MiddleCenter);
        t.color = Color.white;
    }

    void MakeHeader(string text, float y)
    {
        var t = MakeText(text, new Vector2(-360f, y), new Vector2(720f, 60f), 44, TextAnchor.MiddleLeft);
        t.color = new Color(0.95f, 0.55f, 0.45f);   // warm accent to separate sections
    }

    Text MakeText(string text, Vector2 pos, Vector2 size, int fontSize, TextAnchor anchor)
    {
        var t = NewUI("Text", _root.transform).AddComponent<Text>();
        t.font = _font; t.text = text; t.fontSize = fontSize;
        t.alignment = anchor; t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var rt = t.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return t;
    }

    Button MakeButton(string label, Vector2 pos, Vector2 size, int fontSize, UnityEngine.Events.UnityAction action)
    {
        var go = NewUI(label + "Button", _root.transform);
        var img = go.AddComponent<Image>(); img.color = Color.white;
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        var colors = btn.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0.12f);
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.30f);
        colors.pressedColor = new Color(1f, 1f, 1f, 0.50f);
        colors.selectedColor = new Color(1f, 1f, 1f, 0.18f);
        colors.disabledColor = new Color(1f, 1f, 1f, 0.05f);
        btn.colors = colors;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size; rt.anchoredPosition = pos;

        var t = NewUI("Text", go.transform).AddComponent<Text>();
        t.font = _font; t.text = label; t.fontSize = fontSize;
        t.alignment = TextAnchor.MiddleCenter; t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        Stretch(t.GetComponent<RectTransform>());

        btn.onClick.AddListener(action);
        return btn;
    }

    void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem", typeof(EventSystem));
        es.AddComponent<InputSystemUIInputModule>();
        DontDestroyOnLoad(es);
    }

    static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}
