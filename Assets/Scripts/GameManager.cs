using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum GameState { Lobby, Nightmare }

/// <summary>
/// Persistent coordinator: game state, faded scene transitions, a shared
/// EventSystem (so UI works in every scene), and an Escape pause menu
/// ("Game Test" + Retry / Quit). Self-bootstraps before the first scene loads.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public GameState State { get; private set; } = GameState.Lobby;
    public bool LastNightmareSurvived { get; private set; }

    const string LobbyScene = "Lobby";
    const string NightmareScene = "Nightmare";
    const string MenuScene = "MainMenu";

    Font _font;
    CanvasGroup _fade;
    GameObject _pauseRoot;
    bool _transitioning;
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
        BuildFadeOverlay();
        BuildPauseMenu();
    }

    void Update()
    {
        if (_transitioning) return;
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame &&
            SceneManager.GetActiveScene().name != MenuScene)
            SetPaused(!_paused);
    }

    // ---------------------------------------------------------------- state
    public void EnterNightmare()
    {
        if (_transitioning) return;
        State = GameState.Nightmare;
        StartCoroutine(Transition(NightmareScene));
    }

    public void ReturnToLobby(bool survived)
    {
        if (_transitioning) return;
        LastNightmareSurvived = survived;
        State = GameState.Lobby;
        StartCoroutine(Transition(LobbyScene));
    }

    IEnumerator Transition(string scene)
    {
        _transitioning = true;
        if (_paused) SetPaused(false);
        yield return Fade(1f, 0.5f);
        yield return SceneManager.LoadSceneAsync(scene);
        yield return Fade(0f, 0.5f);
        _transitioning = false;
    }

    IEnumerator Fade(float target, float dur)
    {
        float start = _fade.alpha, t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            _fade.alpha = Mathf.Lerp(start, target, t / dur);
            yield return null;
        }
        _fade.alpha = target;
    }

    // ---------------------------------------------------------------- pause
    void SetPaused(bool p)
    {
        _paused = p;
        _pauseRoot.SetActive(p);
        Time.timeScale = p ? 0f : 1f;
    }

    public void Retry()
    {
        SetPaused(false);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void QuitToMenu()
    {
        SetPaused(false);
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

    void BuildFadeOverlay()
    {
        var go = new GameObject("FadeCanvas", typeof(Canvas), typeof(CanvasGroup));
        DontDestroyOnLoad(go);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        _fade = go.GetComponent<CanvasGroup>();
        _fade.alpha = 0f;
        _fade.blocksRaycasts = false;
        _fade.interactable = false;
        var img = NewUI("Black", go.transform).AddComponent<Image>();
        img.color = Color.black; img.raycastTarget = false;
        Stretch(img.GetComponent<RectTransform>());
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
        trt.anchoredPosition = new Vector2(0f, 190f); trt.sizeDelta = new Vector2(1300f, 240f);

        MakeButton("Retry", new Vector2(0f, 0f), Retry);
        MakeButton("Quit", new Vector2(0f, -130f), QuitToMenu);

        _pauseRoot.SetActive(false);
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
