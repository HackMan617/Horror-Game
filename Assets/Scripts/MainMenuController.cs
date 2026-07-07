using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Start-screen buttons. Play loads the game environment; Quit exits
/// (stops Play mode in the editor). Also adds a runtime Settings button that opens the shared
/// <see cref="SettingsManager"/> panel, so options are reachable from the main menu as well as
/// the in-game pause menu.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Tooltip("Scene loaded by the Play button.")]
    public string gameSceneName = "Sandbox3D";

    // The authored menu (Play/Quit) is built by an editor tool; add the Settings entry at runtime so
    // it stays in sync with the code-built settings panel without re-running that tool.
    void Start()
    {
        var font = Resources.Load<Font>("HerculesPixelFontRegular");

        var canvasGo = new GameObject("MainMenuSettingsCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var sc = canvasGo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920f, 1080f);
        sc.matchWidthOrHeight = 0.5f;

        var go = new GameObject("SettingsButton", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(canvasGo.transform, false);
        var img = go.GetComponent<Image>(); img.color = new Color(1f, 1f, 1f, 0.12f);
        var btn = go.GetComponent<Button>(); btn.targetGraphic = img;
        var colors = btn.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0.12f);
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.30f);
        colors.pressedColor = new Color(1f, 1f, 1f, 0.50f);
        btn.colors = colors;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(360f, 84f);
        rt.anchoredPosition = new Vector2(0f, 80f);   // bottom-centre, below the authored Play/Quit

        var t = new GameObject("Text", typeof(RectTransform), typeof(Text));
        t.transform.SetParent(go.transform, false);
        var txt = t.GetComponent<Text>();
        txt.font = font; txt.text = "Settings"; txt.fontSize = 40;
        txt.alignment = TextAnchor.MiddleCenter; txt.color = Color.white;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        var trt = t.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

        btn.onClick.AddListener(() => { if (SettingsManager.Instance != null) SettingsManager.Instance.Open(); });
    }

    public void Play()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
