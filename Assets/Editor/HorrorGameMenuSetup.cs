using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;   // new Input System UI module
using UnityEngine.UI;

/// <summary>
/// Builds the "Game Test" start screen (Hercules Pixel font, Play / Quit) and
/// registers it as the first scene in the build. Run via
/// Tools > Horror Game > Build Start Menu.
/// </summary>
public static class HorrorGameMenuSetup
{
    const string FontReg   = "Assets/Fonts/HerculesPixelFontRegular.otf";
    const string MenuScene = "Assets/Scenes/MainMenu.unity";
    const string GameScene = "Assets/Scenes/Lobby.unity";

    [MenuItem("Tools/Horror Game/Build Start Menu")]
    public static void Build()
    {
        // crisper pixels for a pixel font
        if (AssetImporter.GetAtPath(FontReg) is TrueTypeFontImporter fi)
        {
            fi.fontRenderingMode = FontRenderingMode.HintedRaster;
            fi.SaveAndReimport();
        }
        var font = AssetDatabase.LoadAssetAtPath<Font>(FontReg);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Canvas
        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // (No EventSystem here — GameManager creates one persistent EventSystem
        //  at runtime that serves every scene's UI, avoiding duplicates.)

        // camera so the scene renders and clears to a calm, "innocent" menu colour
        var camGo = new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" };
        var cam = camGo.GetComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.09f, 0.12f);
        camGo.transform.position = new Vector3(0f, 0f, -10f);

        // title
        var title = NewUI("Title", canvasGo.transform);
        var t = title.AddComponent<Text>();
        t.font = font; t.text = "Game Test"; t.fontSize = 120;
        t.alignment = TextAnchor.MiddleCenter; t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var trt = title.GetComponent<RectTransform>();
        trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0.5f, 1f);
        trt.sizeDelta = new Vector2(1400, 260);
        trt.anchoredPosition = new Vector2(0f, -240f);

        // controller + buttons
        var ctrl = new GameObject("MenuController").AddComponent<MainMenuController>();
        var play = CreateButton("PlayButton", canvasGo.transform, font, "Play", new Vector2(0f, -20f));
        var quit = CreateButton("QuitButton", canvasGo.transform, font, "Quit", new Vector2(0f, -150f));
        UnityEventTools.AddPersistentListener(play.onClick, ctrl.Play);
        UnityEventTools.AddPersistentListener(quit.onClick, ctrl.Quit);

        EditorSceneManager.SaveScene(scene, MenuScene);

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MenuScene, true),
            new EditorBuildSettingsScene(GameScene, true),
            new EditorBuildSettingsScene("Assets/Scenes/Nightmare.unity", true),
        };

        Debug.Log("[HorrorGame] Start menu built at " + MenuScene +
                  ". It's build scene 0; Play loads " + GameScene + ".");
    }

    static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static Button CreateButton(string name, Transform parent, Font font, string label, Vector2 pos)
    {
        var go = NewUI(name, parent);
        var img = go.AddComponent<Image>();
        img.color = Color.white;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.normalColor      = new Color(1f, 1f, 1f, 0.10f);
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.28f);
        colors.pressedColor     = new Color(1f, 1f, 1f, 0.45f);
        colors.selectedColor    = new Color(1f, 1f, 1f, 0.18f);
        btn.colors = colors;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(440f, 96f);
        rt.anchoredPosition = pos;

        var label2 = NewUI("Text", go.transform);
        var lt = label2.AddComponent<Text>();
        lt.font = font; lt.text = label; lt.fontSize = 54;
        lt.alignment = TextAnchor.MiddleCenter; lt.color = Color.white;
        lt.horizontalOverflow = HorizontalWrapMode.Overflow;
        lt.verticalOverflow = VerticalWrapMode.Overflow;
        StretchFull(label2.GetComponent<RectTransform>());
        return btn;
    }
}
