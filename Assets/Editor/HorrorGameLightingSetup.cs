using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;   // Light2D
using UnityEngine.SceneManagement;

/// <summary>
/// Horror lighting rig:
///   - a Point Light 2D parented to the Player (the lit pool follows the player)
///   - the scene's Global Light 2D dimmed to near-dark
///   - camera background set to black
///   - a dark, lit backdrop so the moving light has something to reveal
/// Auto-applies once (when the Player exists but the light doesn't); also under
/// Tools > Horror Game > Setup Horror Lighting.
/// </summary>
public static class HorrorGameLightingSetup
{
    const string BgPng = "Assets/Art/Environment/Background.png";
    static int _ticks;

    [InitializeOnLoadMethod]
    static void AutoRun()
    {
        _ticks = 0;
        EditorApplication.delayCall += Try;
    }

    static void Try()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        // Only auto-light the game scene — never the menu or other scenes.
        if (SceneManager.GetActiveScene().name != "SampleScene") return;

        var player = GameObject.Find("Player");
        if (player == null)
        {
            if (++_ticks < 50) EditorApplication.delayCall += Try; // player setup may be pending
            return;
        }
        if (player.transform.Find("PlayerLight") != null) return; // already lit

        try { Setup(); }
        catch (Exception e) { Debug.LogError("[HorrorGame] Lighting setup failed: " + e); }
    }

    [MenuItem("Tools/Horror Game/Setup Horror Lighting")]
    public static void Setup()
    {
        var player = GameObject.Find("Player");
        if (player == null) { Debug.LogError("[HorrorGame] No Player in scene; run the player setup first."); return; }
        var playerSR = player.GetComponent<SpriteRenderer>();

        // --- light that follows the player ---
        var t = player.transform.Find("PlayerLight");
        var lgo = t != null ? t.gameObject : new GameObject("PlayerLight");
        lgo.transform.SetParent(player.transform, false);
        lgo.transform.localPosition = new Vector3(0f, 0.9f, 0f); // centre on the torso, not the feet

        var light = lgo.GetComponent<Light2D>();
        if (light == null) light = lgo.AddComponent<Light2D>();
        light.lightType             = Light2D.LightType.Point;
        light.color                 = new Color(1f, 0.93f, 0.78f); // warm lantern
        light.intensity             = 1.25f;
        light.pointLightInnerRadius = 0.4f;
        light.pointLightOuterRadius = 4.2f;
        light.pointLightInnerAngle  = 360f;
        light.pointLightOuterAngle  = 360f;
        light.falloffIntensity      = 0.6f;

        // --- dim the global light so everything off-pool is near black ---
        var glGo = GameObject.Find("Global Light 2D");
        if (glGo != null)
        {
            var gl = glGo.GetComponent<Light2D>();
            if (gl != null) gl.intensity = 0.06f;
        }

        // --- black camera background ---
        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
        }

        // --- lit backdrop so the light reveals the world around the player ---
        SetupBackdrop(playerSR != null ? playerSR.sharedMaterial : null);

        var scene = player.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        if (scene.path != null && scene.path.StartsWith("Assets/"))
            EditorSceneManager.SaveScene(scene);

        Debug.Log("[HorrorGame] Horror lighting applied. Enter Play and move with A/D — the lit pool follows the player.");
    }

    static void SetupBackdrop(Material litMaterial)
    {
        var imp = AssetImporter.GetAtPath(BgPng) as TextureImporter;
        if (imp == null) { Debug.LogWarning("[HorrorGame] " + BgPng + " not found; skipping backdrop."); return; }

        imp.textureType        = TextureImporterType.Sprite;
        imp.spriteImportMode   = SpriteImportMode.Single;
        imp.filterMode         = FilterMode.Point;
        imp.wrapMode           = TextureWrapMode.Repeat;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.spritePixelsPerUnit = 100f;
        var s = new TextureImporterSettings();
        imp.ReadTextureSettings(s);
        s.spriteMeshType = SpriteMeshType.FullRect;   // required for tiled draw mode
        imp.SetTextureSettings(s);
        imp.SaveAndReimport();

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BgPng);

        var bg = GameObject.Find("Background");
        if (bg == null) bg = new GameObject("Background");
        bg.transform.position = new Vector3(0f, 2f, 0f);

        var sr = bg.GetComponent<SpriteRenderer>();
        if (sr == null) sr = bg.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.size = new Vector2(40f, 30f);
        sr.sortingOrder = -100;
        if (litMaterial != null) sr.sharedMaterial = litMaterial; // be affected by 2D lights
    }
}
