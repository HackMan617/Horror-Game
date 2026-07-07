using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Persistent coordinator: a shared EventSystem (so UI works in every scene),
/// cursor lock/visibility for mouse-look scenes, and an Escape pause menu
/// ("Game Test" + Retry / Quit). Self-bootstraps before the first scene loads.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public bool IsPaused => _paused;

    const string MenuScene = "MainMenu";

    Font _font;
    GameObject _pauseRoot;
    bool _paused;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        new GameObject("GameManager").AddComponent<GameManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _font = Resources.Load<Font>("HerculesPixelFontRegular");
        EnsureEventSystem();
        BuildPauseMenu();
    }

    void Update()
    {
        var kb = Keyboard.current;
        // Don't toggle pause on the same Escape that just closed the Settings panel (or while it's open).
        bool settingsBusy = SettingsManager.Instance != null &&
                            (SettingsManager.Instance.IsOpen || SettingsManager.Instance.ConsumedEscapeThisFrame);
        if (kb != null && kb.escapeKey.wasPressedThisFrame && !settingsBusy &&
            SceneManager.GetActiveScene().name != MenuScene)
            SetPaused(!_paused);
    }

    // ---------------------------------------------------------------- pause
    void SetPaused(bool p)
    {
        _paused = p;
        _pauseRoot.SetActive(p);
        Time.timeScale = p ? 0f : 1f;
        ApplyCursor(gameplay: !p);   // free the cursor while paused so buttons are clickable
    }

    // Lock + hide the cursor for mouse-look scenes (a CameraRig is present); otherwise free it.
    void ApplyCursor(bool gameplay)
    {
        bool mouseLook = gameplay && FindAnyObjectByType<CameraRig>() != null;
        Cursor.lockState = mouseLook ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !mouseLook;
    }

    public void Retry()
    {
        _paused = false; _pauseRoot.SetActive(false); Time.timeScale = 1f;
        ApplyCursor(gameplay: false);   // free now; the reloaded scene's CameraRig re-locks if 3D
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void QuitToMenu()
    {
        _paused = false; _pauseRoot.SetActive(false); Time.timeScale = 1f;
        ApplyCursor(gameplay: false);   // the menu has no mouse-look
        SceneManager.LoadScene(MenuScene);
    }

    // ---------------------------------------------------------------- UI build
    void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem", typeof(EventSystem));
        es.AddComponent<InputSystemUIInputModule>();
        DontDestroyOnLoad(es);
    }

    void BuildPauseMenu()
    {
        _pauseRoot = new GameObject("PauseCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        DontDestroyOnLoad(_pauseRoot);
        var canvas = _pauseRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var sc = _pauseRoot.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920f, 1080f);
        sc.matchWidthOrHeight = 0.5f;

        var dim = NewUI("Dim", _pauseRoot.transform).AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.72f);
        Stretch(dim.GetComponent<RectTransform>());

        var title = NewUI("Title", _pauseRoot.transform).AddComponent<Text>();
        title.font = _font; title.text = "Game Test"; title.fontSize = 110;
        title.alignment = TextAnchor.MiddleCenter; title.color = Color.white;
        title.horizontalOverflow = HorizontalWrapMode.Overflow;
        title.verticalOverflow = VerticalWrapMode.Overflow;
        var trt = title.GetComponent<RectTransform>();
        trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0.5f, 0.5f);
        trt.anchoredPosition = new Vector2(0f, 300f); trt.sizeDelta = new Vector2(1300f, 240f);

        MakeButton("Settings", new Vector2(0f, 130f), OpenSettings);
        MakeButton("Retry", new Vector2(0f, 0f), Retry);
        MakeButton("Quit", new Vector2(0f, -130f), QuitToMenu);

        _pauseRoot.SetActive(false);
    }

    void OpenSettings()
    {
        if (SettingsManager.Instance != null) SettingsManager.Instance.Open();
    }

    void MakeButton(string label, Vector2 pos, UnityEngine.Events.UnityAction action)
    {
        var go = NewUI(label + "Button", _pauseRoot.transform);
        var img = go.AddComponent<Image>(); img.color = Color.white;
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        var colors = btn.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0.12f);
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.30f);
        colors.pressedColor = new Color(1f, 1f, 1f, 0.50f);
        colors.selectedColor = new Color(1f, 1f, 1f, 0.18f);
        btn.colors = colors;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(440f, 96f); rt.anchoredPosition = pos;

        var t = NewUI("Text", go.transform).AddComponent<Text>();
        t.font = _font; t.text = label; t.fontSize = 48;
        t.alignment = TextAnchor.MiddleCenter; t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        Stretch(t.GetComponent<RectTransform>());

        btn.onClick.AddListener(action);
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
