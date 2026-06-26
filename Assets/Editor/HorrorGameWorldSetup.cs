using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;   // Light2D
using UnityEngine.UI;

/// <summary>
/// Builds the two gameplay scenes from scratch and wires build settings:
///   Tools > Horror Game > Build Lobby      -> bright "paradise" + bed
///   Tools > Horror Game > Build Nightmare  -> horror lighting + survival clock
/// Build order set to MainMenu(0), Lobby(1), Nightmare(2).
/// </summary>
public static class HorrorGameWorldSetup
{
    const string PlayerPng  = "Assets/Art/Player/Player.png";
    const string Controller = "Assets/Animation/Player/Player.controller";
    const string Bg         = "Assets/Art/Environment/Background.png";
    const string Floor      = "Assets/Art/Environment/LobbyFloor.png";
    const string BedPng     = "Assets/Art/Environment/Bed.png";
    const string FontPath   = "Assets/Fonts/HerculesPixelFontRegular.otf";

    const string MenuScene      = "Assets/Scenes/MainMenu.unity";
    const string LobbyScene     = "Assets/Scenes/Lobby.unity";
    const string NightmareScene = "Assets/Scenes/Nightmare.unity";

    [MenuItem("Tools/Horror Game/Build Nightmare")]
    public static void BuildNightmare()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        MakeCamera(Color.black);
        MakeGlobalLight(0.06f);

        var player = MakePlayer();
        var lightGo = new GameObject("PlayerLight");
        lightGo.transform.SetParent(player.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        var pl = lightGo.AddComponent<Light2D>();
        pl.lightType = Light2D.LightType.Point;
        pl.color = new Color(1f, 0.93f, 0.78f);
        pl.intensity = 1.25f;
        pl.pointLightInnerRadius = 0.4f;
        pl.pointLightOuterRadius = 4.2f;
        pl.pointLightInnerAngle = 360f;
        pl.pointLightOuterAngle = 360f;
        pl.falloffIntensity = 0.6f;

        MakeTiledSprite("Background", Bg, new Vector2(40f, 30f), new Vector3(0f, 2f, 0f), -100);

        var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        var canvas = MakeUICanvas();
        var clockText = MakeUIText(canvas.transform, font, "Hour 1 / 8", 54,
            new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(700f, 100f),
            new Color(0.86f, 0.86f, 0.92f));
        var clock = new GameObject("NightmareClock").AddComponent<NightmareClock>();
        clock.survivalSeconds = 300f;
        clock.totalHours = 8;
        clock.display = clockText;

        EditorSceneManager.SaveScene(scene, NightmareScene);
        SetBuildSettings();
        Debug.Log("[HorrorGame] Nightmare scene built at " + NightmareScene);
    }

    [MenuItem("Tools/Horror Game/Build Lobby")]
    public static void BuildLobby()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        MakeCamera(new Color(0.62f, 0.78f, 0.90f)); // soft daylight sky
        MakeGlobalLight(1.0f);                        // bright paradise

        MakePlayer();                                 // no point light in the lobby

        MakeTiledSprite("Floor", Floor, new Vector2(40f, 30f), new Vector3(0f, 0f, 0f), -100);

        var bedGo = new GameObject("Bed");
        bedGo.transform.position = new Vector3(4f, 0.5f, 0f);
        var bedSr = bedGo.AddComponent<SpriteRenderer>();
        bedSr.sprite = LoadSprite(BedPng);
        bedSr.sortingOrder = 0;
        var bed = bedGo.AddComponent<BedInteraction>();
        bed.interactRange = 2.2f;

        var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        var canvas = MakeUICanvas();
        var promptText = MakeUIText(canvas.transform, font, "Press E to sleep", 40,
            new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(900f, 80f), Color.white);
        bed.prompt = promptText;

        EditorSceneManager.SaveScene(scene, LobbyScene);
        SetBuildSettings();
        Debug.Log("[HorrorGame] Lobby scene built at " + LobbyScene);
    }

    // ---------------------------------------------------------------- helpers
    static Camera MakeCamera(Color bg)
    {
        var go = new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" };
        var cam = go.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = bg;
        go.transform.position = new Vector3(0f, 1f, -10f);
        return cam;
    }

    static void MakeGlobalLight(float intensity)
    {
        var l = new GameObject("Global Light 2D").AddComponent<Light2D>();
        l.lightType = Light2D.LightType.Global;
        l.intensity = intensity;
    }

    static GameObject MakePlayer()
    {
        var go = new GameObject("Player");
        go.transform.position = Vector3.zero;
        var sr = go.AddComponent<SpriteRenderer>();      // defaults to Sprite-Lit-Default in this URP 2D project
        sr.sprite = LoadSprite(PlayerPng, "player_idle_0");
        sr.sortingOrder = 10;
        var an = go.AddComponent<Animator>();
        an.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
        go.AddComponent<PlayerController2D>();
        return go;
    }

    static Sprite LoadSprite(string path, string name = null)
    {
        var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>();
        return name == null ? sprites.FirstOrDefault() : sprites.FirstOrDefault(s => s.name == name);
    }

    static void MakeTiledSprite(string name, string path, Vector2 size, Vector3 pos, int order)
    {
        if (AssetImporter.GetAtPath(path) is TextureImporter imp)
        {
            bool changed = false;
            if (imp.spriteImportMode != SpriteImportMode.Single) { imp.spriteImportMode = SpriteImportMode.Single; changed = true; }
            if (imp.wrapMode != TextureWrapMode.Repeat) { imp.wrapMode = TextureWrapMode.Repeat; changed = true; }
            var s = new TextureImporterSettings();
            imp.ReadTextureSettings(s);
            if (s.spriteMeshType != SpriteMeshType.FullRect) { s.spriteMeshType = SpriteMeshType.FullRect; imp.SetTextureSettings(s); changed = true; }
            if (changed) imp.SaveAndReimport();
        }
        var go = new GameObject(name);
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = LoadSprite(path);
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.size = size;
        sr.sortingOrder = order;
    }

    static GameObject MakeUICanvas()
    {
        var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var sc = go.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920f, 1080f);
        sc.matchWidthOrHeight = 0.5f;
        return go;
    }

    static Text MakeUIText(Transform parent, Font font, string text, int size,
                           Vector2 anchor, Vector2 pos, Vector2 sizeDelta, Color color)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = font; t.text = text; t.fontSize = size; t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos; rt.sizeDelta = sizeDelta;
        return t;
    }

    static void SetBuildSettings()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MenuScene, true),
            new EditorBuildSettingsScene(LobbyScene, true),
            new EditorBuildSettingsScene(NightmareScene, true),
        };
    }
}
