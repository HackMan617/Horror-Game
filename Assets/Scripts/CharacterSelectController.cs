using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using Game.Characters;

/// <summary>
/// Character-select screen. Builds the UI at runtime: a live recolored preview
/// (the pack's CharacterAnimator), left/right cyclers for hair/skin/eyes/shirt/pants, a
/// Gender (Male/Female long-hair) choice, and a Partner (Boy/Girl) choice, preset buttons,
/// and an Enter button that saves the chosen look + partner and loads the game.
/// </summary>
public class CharacterSelectController : MonoBehaviour
{
    public CharacterAnimator preview;          // recolored preview character (assigned by the builder)
    public string gameScene = "Exterior";

    // attribute index: 0-4 = CharacterLook colors, 5 = body/gender, 6 = partner (boy/girl)
    static readonly string[] AttrNames = { "Hair", "Skin", "Eyes", "Shirt", "Pants", "Gender", "Partner" };
    static readonly string[] BodyNames = { "Male", "Female" };

    CharacterLook _look = CharacterLook.Default;
    int _partner;                              // 0 = Boy, 1 = Girl
    Font _font;
    readonly Text[] _values = new Text[7];

    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        EnsureEventSystem();

        _look = CharacterStore.Load();
        _partner = CharacterStore.LoadPartner();
        _font = Resources.Load<Font>("HerculesPixelFontRegular");
        if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        BuildUI();
        ApplyToPreview();
    }

    void Update()
    {
        if (preview != null) preview.SetMovement(Vector2.down);   // walk in place, front-facing
    }

    // ---------------- option access ----------------
    int OptionCount(int a)
    {
        switch (a)
        {
            case 0: return CharacterPalette.Hair.Length;
            case 1: return CharacterPalette.Skin.Length;
            case 2: return CharacterPalette.Eyes.Length;
            case 3: return CharacterPalette.Shirt.Length;
            case 4: return CharacterPalette.Pants.Length;
            case 5: return BodyNames.Length;
            default: return CharacterStore.PartnerNames.Length;
        }
    }
    int GetIndex(int a)
    {
        switch (a)
        {
            case 0: return _look.hair;
            case 1: return _look.skin;
            case 2: return _look.eyes;
            case 3: return _look.shirt;
            case 4: return _look.pants;
            case 5: return (int)_look.body;
            default: return _partner;
        }
    }
    void SetIndex(int a, int v)
    {
        switch (a)
        {
            case 0: _look.hair = v; break;
            case 1: _look.skin = v; break;
            case 2: _look.eyes = v; break;
            case 3: _look.shirt = v; break;
            case 4: _look.pants = v; break;
            case 5: _look.body = (BodyType)v; break;
            default: _partner = v; break;
        }
    }
    string OptionName(int a, int i)
    {
        switch (a)
        {
            case 0: return CharacterPalette.Hair[i].name;
            case 1: return CharacterPalette.Skin[i].name;
            case 2: return CharacterPalette.Eyes[i].name;
            case 3: return CharacterPalette.Shirt[i].name;
            case 4: return CharacterPalette.Pants[i].name;
            case 5: return BodyNames[i];
            default: return CharacterStore.PartnerNames[i];
        }
    }

    void Cycle(int a, int dir)
    {
        int n = OptionCount(a);
        int v = (GetIndex(a) + dir + n) % n;
        SetIndex(a, v);
        _values[a].text = OptionName(a, v);
        if (a < 6) ApplyToPreview();          // colors + gender change the preview; partner does not
    }

    void ApplyPreset(CharacterLook preset)
    {
        var body = _look.body;                // a preset is a colour combo; keep the chosen gender
        _look = preset;
        _look.body = body;
        for (int a = 0; a < 5; a++) _values[a].text = OptionName(a, GetIndex(a));
        ApplyToPreview();
    }

    void ApplyToPreview()
    {
        if (preview == null) return;
        preview.look = _look;
        preview.Rebuild();
    }

    void Confirm()
    {
        CharacterStore.Save(_look);
        CharacterStore.SavePartner(_partner);
        SceneManager.LoadScene(gameScene);
    }

    // ---------------- UI ----------------
    void BuildUI()
    {
        var canvasGo = new GameObject("SelectCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var sc = canvasGo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920f, 1080f);
        sc.matchWidthOrHeight = 0.5f;
        var root = canvasGo.transform;

        MakeText(root, "CHOOSE YOUR CHARACTER", new Vector2(0f, 470f), new Vector2(1500f, 100f), 66, TextAnchor.MiddleCenter);

        const float x = 470f, top = 300f, dy = 72f;
        for (int a = 0; a < 7; a++)
        {
            int attr = a;
            float y = top - a * dy;
            MakeText(root, AttrNames[a], new Vector2(x - 340f, y), new Vector2(200f, 60f), 33, TextAnchor.MiddleRight);
            MakeButton(root, "<", new Vector2(x - 130f, y), new Vector2(64f, 64f), 40, () => Cycle(attr, -1));
            _values[a] = MakeText(root, "", new Vector2(x + 95f, y), new Vector2(330f, 60f), 32, TextAnchor.MiddleCenter);
            MakeButton(root, ">", new Vector2(x + 320f, y), new Vector2(64f, 64f), 40, () => Cycle(attr, +1));
        }

        MakeText(root, "- or pick a preset look -", new Vector2(x, top - 7 * dy - 8f), new Vector2(620f, 52f), 28, TextAnchor.MiddleCenter);
        var presets = CharacterStore.Presets;
        for (int p = 0; p < presets.Length; p++)
        {
            var preset = presets[p];
            MakeButton(root, preset.name, new Vector2(x - 250f + p * 250f, top - 7 * dy - 72f), new Vector2(230f, 60f), 24,
                       () => ApplyPreset(preset.look));
        }

        MakeButton(root, "ENTER", new Vector2(x, -415f), new Vector2(360f, 88f), 40, Confirm);

        for (int a = 0; a < 7; a++) _values[a].text = OptionName(a, GetIndex(a));
    }

    Text MakeText(Transform parent, string txt, Vector2 pos, Vector2 size, int fontSize, TextAnchor anchor)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = _font; t.text = txt; t.fontSize = fontSize; t.alignment = anchor; t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var rt = t.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return t;
    }

    void MakeButton(Transform parent, string label, Vector2 pos, Vector2 size, int fontSize, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(label + "Button", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.14f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.32f);
        colors.pressedColor = new Color(1f, 1f, 1f, 0.5f);
        btn.colors = colors;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;

        var t = MakeText(go.transform, label, Vector2.zero, size, fontSize, TextAnchor.MiddleCenter);
        var trt = t.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

        btn.onClick.AddListener(onClick);
    }

    void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem", typeof(EventSystem));
        es.AddComponent<InputSystemUIInputModule>();
    }
}
