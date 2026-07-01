using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;   // Light2D
using UnityEngine.UI;

/// <summary>
/// Builds the two top-down gameplay scenes (camera follows the player) and wires
/// build settings:
///   Tools > Horror Game > Build Lobby      -> large bird's-eye "paradise" room
///   Tools > Horror Game > Build Nightmare  -> horror lighting + survival clock
/// </summary>
public static class HorrorGameWorldSetup
{
    const string PlayerPng  = "Assets/Art/Player/Player.png";
    const string Controller = "Assets/Animation/Player/Player.controller";
    const string Bg         = "Assets/Art/Environment/Background.png";
    const string RoomPng    = "Assets/Art/Environment/LobbyRoom.png";
    const string BedPng     = "Assets/Art/Environment/BedTopDown.png";
    const string BlobPng    = "Assets/Art/Environment/Blob.png";
    const string FontPath   = "Assets/Fonts/HerculesPixelFontRegular.otf";

    const string MenuScene      = "Assets/Scenes/MainMenu.unity";
    const string LobbyScene     = "Assets/Scenes/Lobby.unity";
    const string NightmareScene = "Assets/Scenes/Nightmare.unity";

    // LobbyRoom.png is 712x400 @ 20 PPU -> 35.6 x 20 world units, centred on origin.
    static readonly Vector2 LobbyHalf = new Vector2(17.8f, 10f);
    // Background.png tiles 40 x 30 in the nightmare, centred on origin.
    static readonly Vector2 NightHalf = new Vector2(20f, 15f);

    [MenuItem("Tools/Horror Game/Build Lobby")]
    public static void BuildLobby()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cam = MakeCamera(new Color(0.43f, 0.29f, 0.18f));
        AttachFollow(cam, -LobbyHalf, LobbyHalf);
        MakeGlobalLight(1.0f);

        MakeSprite("Room", RoomPng, 20f, Vector3.zero, -100);

        var player = MakePlayer();
        player.transform.position = new Vector3(0f, 0f, 0f);
        var pc = player.GetComponent<PlayerController2D>();
        pc.clampToArea = true;
        pc.areaMin = new Vector2(-16.5f, -8.8f);
        pc.areaMax = new Vector2( 16.5f,  8.8f);

        // blob NPCs (pets / people) gathered across the left + centre
        var cols = new[]
        {
            new Color(0.82f, 0.58f, 0.35f), new Color(0.47f, 0.66f, 0.90f),
            new Color(0.90f, 0.58f, 0.70f), new Color(0.58f, 0.82f, 0.55f),
            new Color(0.92f, 0.80f, 0.47f), new Color(0.70f, 0.58f, 0.90f),
            new Color(0.55f, 0.80f, 0.85f),
        };
        var pos = new[]
        {
            new Vector2(-12f, 4.5f), new Vector2(-9f, 6.5f), new Vector2(-6.5f, 2.5f),
            new Vector2(-13f, -3.5f), new Vector2(-8f, -5.5f), new Vector2(-4f, -1.5f),
            new Vector2(-10.5f, 0.5f),
        };
        var scl = new[] { 1.1f, 0.85f, 1.0f, 0.8f, 1.15f, 0.9f, 1.0f };
        for (int i = 0; i < cols.Length; i++) MakeBlob(i, pos[i], scl[i], cols[i]);

        // bed on the far right (slightly angled) + sleep interaction
        var bed = MakeSprite("Bed", BedPng, 20f, new Vector3(14f, 3f, 0f), 0);
        bed.transform.rotation = Quaternion.Euler(0f, 0f, -12f);
        var bi = bed.AddComponent<BedInteraction>();
        bi.interactRange = 2.6f;

        var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        var canvas = MakeUICanvas();
        bi.prompt = MakeUIText(canvas.transform, font, "Press E to sleep", 40,
            new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(900f, 80f), Color.white);

        EditorSceneManager.SaveScene(scene, LobbyScene);
        SetBuildSettings();
        Debug.Log("[HorrorGame] Top-down Lobby built (camera-follow) at " + LobbyScene);
    }

    [MenuItem("Tools/Horror Game/Build Nightmare")]
    public static void BuildNightmare()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cam = MakeCamera(Color.black);
        AttachFollow(cam, -NightHalf, NightHalf);
        MakeGlobalLight(0.06f);

        var player = MakePlayer();
        var pc = player.GetComponent<PlayerController2D>();
        pc.clampToArea = true;
        pc.areaMin = new Vector2(-18f, -13f);
        pc.areaMax = new Vector2( 18f,  13f);

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

        MakeTiledSprite("Background", Bg, new Vector2(40f, 30f), Vector3.zero, -100);

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
        Debug.Log("[HorrorGame] Top-down Nightmare built (camera-follow) at " + NightmareScene);
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
        go.transform.position = new Vector3(0f, 0f, -10f);
        return cam;
    }

    static void AttachFollow(Camera cam, Vector2 worldMin, Vector2 worldMax)
    {
        var f = cam.gameObject.AddComponent<CameraFollow2D>();
        f.clampToBounds = true;
        f.worldMin = worldMin;
        f.worldMax = worldMax;
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
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = LoadSprite(PlayerPng, "player_idle_0");
        sr.sortingOrder = 10;
        var an = go.AddComponent<Animator>();
        an.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
        go.AddComponent<PlayerController2D>();
        return go;
    }

    static void MakeBlob(int idx, Vector2 pos, float scale, Color color)
    {
        var go = new GameObject("Blob" + idx);
        go.transform.position = new Vector3(pos.x, pos.y, 0f);
        go.transform.localScale = Vector3.one * scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = ConfigureSprite(BlobPng, 24f);
        sr.color = color;
        sr.sortingOrder = 5;
        go.AddComponent<BlobAnimator>();
    }

    static GameObject MakeSprite(string name, string path, float ppu, Vector3 pos, int order)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = ConfigureSprite(path, ppu);
        sr.sortingOrder = order;
        return go;
    }

    static Sprite ConfigureSprite(string path, float ppu)
    {
        if (AssetImporter.GetAtPath(path) is TextureImporter imp)
        {
            bool changed = false;
            if (imp.textureType != TextureImporterType.Sprite) { imp.textureType = TextureImporterType.Sprite; changed = true; }
            if (imp.spriteImportMode != SpriteImportMode.Single) { imp.spriteImportMode = SpriteImportMode.Single; changed = true; }
            if (imp.filterMode != FilterMode.Point) { imp.filterMode = FilterMode.Point; changed = true; }
            if (imp.textureCompression != TextureImporterCompression.Uncompressed) { imp.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
            if (!Mathf.Approximately(imp.spritePixelsPerUnit, ppu)) { imp.spritePixelsPerUnit = ppu; changed = true; }
            if (changed) imp.SaveAndReimport();
        }
        return LoadSprite(path);
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
        // Ensure these scenes are present WITHOUT dropping any others — e.g. the 3D Exterior /
        // Sandbox3D scenes registered by HorrorGame3DSetup, which the menu -> character select ->
        // exterior -> house flow needs. (Previously this overwrote the whole list and wiped them.)
        var wanted = new[] { MenuScene, LobbyScene, NightmareScene };
        var scenes = EditorBuildSettings.scenes.ToList();
        foreach (var path in wanted)
            if (!scenes.Any(s => s.path == path))
                scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
