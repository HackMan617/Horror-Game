using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum GameState { Lobby, Nightmare }

/// <summary>
/// Persistent coordinator for the two game states. Self-bootstraps before the
/// first scene loads, so it always exists in Play regardless of entry scene.
/// Handles state + faded scene transitions between the Lobby and the Nightmare.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public GameState State { get; private set; } = GameState.Lobby;
    public bool LastNightmareSurvived { get; private set; }

    const string LobbyScene = "Lobby";
    const string NightmareScene = "Nightmare";

    CanvasGroup _fade;
    bool _transitioning;

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
        BuildFadeOverlay();
    }

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

    void BuildFadeOverlay()
    {
        var canvasGo = new GameObject("FadeCanvas", typeof(Canvas), typeof(CanvasGroup));
        DontDestroyOnLoad(canvasGo);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        _fade = canvasGo.GetComponent<CanvasGroup>();
        _fade.alpha = 0f;
        _fade.blocksRaycasts = false;
        _fade.interactable = false;

        var img = new GameObject("Black", typeof(RectTransform), typeof(Image));
        img.transform.SetParent(canvasGo.transform, false);
        img.GetComponent<Image>().color = Color.black;
        img.GetComponent<Image>().raycastTarget = false;
        var rt = img.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}
