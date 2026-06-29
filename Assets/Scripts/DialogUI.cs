using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared, screen-scaled gameplay UI for interaction prompts and partner dialog, using the
/// game's pixel font at consistent, readable sizes (so prompts no longer look tiny next to
/// the menus). Prompts are immediate-mode: an interactable calls ShowPrompt every frame it is
/// in range, and the prompt auto-hides when nothing requests it. Dialog lines show at the
/// bottom of the screen over a transparent background (outlined so they stay legible).
/// </summary>
public class DialogUI : MonoBehaviour
{
    public static DialogUI Instance { get; private set; }

    Font _font;
    Text _prompt;
    Text _dialog;
    float _dialogTimer;
    bool _promptRequested;

    void Awake()
    {
        Instance = this;
        _font = Resources.Load<Font>("HerculesPixelFontRegular");
        if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildUI();
    }

    /// <summary>Show an interaction prompt this frame (call every frame while in range).</summary>
    public void ShowPrompt(string text)
    {
        _prompt.text = text;
        _prompt.enabled = true;
        _promptRequested = true;
    }

    /// <summary>Show a line of dialog at the bottom of the screen for `duration` seconds.</summary>
    public void ShowDialog(string text, float duration)
    {
        _dialog.text = text;
        _dialog.enabled = true;
        _dialogTimer = duration;
    }

    void Update()
    {
        if (_dialogTimer > 0f)
        {
            _dialogTimer -= Time.deltaTime;
            if (_dialogTimer <= 0f) _dialog.enabled = false;
        }
    }

    void LateUpdate()
    {
        // auto-hide the prompt if nobody requested it this frame
        if (!_promptRequested && _prompt.enabled) _prompt.enabled = false;
        _promptRequested = false;
    }

    void BuildUI()
    {
        var canvasGo = new GameObject("DialogCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;                 // above the world, below the pause menu (1000)
        var sc = canvasGo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920f, 1080f);
        sc.matchWidthOrHeight = 0.5f;

        _prompt = MakeText(canvasGo.transform, new Vector2(0f, -300f), new Vector2(1600f, 90f), 42);
        _prompt.enabled = false;
        _dialog = MakeText(canvasGo.transform, new Vector2(0f, -430f), new Vector2(1500f, 160f), 46);
        _dialog.enabled = false;
    }

    Text MakeText(Transform parent, Vector2 pos, Vector2 size, int fontSize)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = _font;
        t.fontSize = fontSize;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;

        var outline = go.AddComponent<Outline>();   // keeps text legible over any background
        outline.effectColor = new Color(0f, 0f, 0f, 0.92f);
        outline.effectDistance = new Vector2(3f, -3f);

        var rt = t.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return t;
    }
}
