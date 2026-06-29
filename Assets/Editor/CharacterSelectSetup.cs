using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Game.Characters;

/// <summary>
/// Builds the CharacterSelect scene — a dark room with a live, recolorable character
/// preview (the pack's CharacterAnimator) plus the CharacterSelectController UI — and
/// configures the master sheets for runtime recolor (Read/Write on, point, uncompressed).
/// Menu: Tools > Horror Game > Build Character Select. Also auto-runs once (version-gated).
/// </summary>
public static class CharacterSelectSetup
{
    public const string MasterFront = "Assets/Animation/2.5D Retro Character Sprite Sheet/unity/character_master.png";
    public const string MasterBack  = "Assets/Animation/2.5D Retro Character Sprite Sheet/unity/character_master_back.png";
    const string SceneOut = "Assets/Scenes/CharacterSelect.unity";
    const int SetupVersion = 1;

    [InitializeOnLoadMethod]
    static void AutoRun()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorPrefs.GetInt("HGSelect_SetupVersion", 0) >= SetupVersion) return;
            EditorPrefs.SetInt("HGSelect_SetupVersion", SetupVersion);
            try { BuildCharacterSelect(); }
            catch (System.Exception e) { Debug.LogError("[HorrorGame] Character Select auto-build failed: " + e); }
        };
    }

    [MenuItem("Tools/Horror Game/Build Character Select")]
    public static void BuildCharacterSelect()
    {
        var front = ConfigureMaster(MasterFront);
        var back  = ConfigureMaster(MasterBack);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 3.5f;
        cam.transform.position = new Vector3(0f, 0f, -10f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.07f, 0.07f, 0.10f);

        // live preview character (recolored + animated by CharacterAnimator)
        var go = new GameObject("Preview");
        go.transform.position = new Vector3(-3.1f, -2.0f, 0f);
        go.AddComponent<SpriteRenderer>();
        var anim = go.AddComponent<CharacterAnimator>();
        anim.masterFront = front;
        anim.masterBack = back;
        anim.pixelsPerUnit = 12f;                       // ~2.7 units tall in the preview
        anim.pivot = new Vector2(0.5f, 0f);

        // UI controller
        var ctrl = new GameObject("CharacterSelect").AddComponent<CharacterSelectController>();
        ctrl.preview = anim;
        ctrl.gameScene = "Sandbox3D";

        EditorSceneManager.SaveScene(scene, SceneOut);
        AddSceneToBuild(SceneOut);
        Debug.Log("[HorrorGame] Character Select scene built at " + SceneOut +
                  ". Flow: MainMenu > Play > CharacterSelect > Enter > Sandbox3D.");
    }

    /// <summary>Ensure a master sheet is import-configured for the runtime palette swap.</summary>
    public static Texture2D ConfigureMaster(string path)
    {
        if (AssetImporter.GetAtPath(path) is TextureImporter imp)
        {
            bool dirty = imp.textureType != TextureImporterType.Sprite ||
                         imp.spriteImportMode != SpriteImportMode.Single ||
                         imp.filterMode != FilterMode.Point ||
                         imp.textureCompression != TextureImporterCompression.Uncompressed ||
                         !imp.isReadable || imp.mipmapEnabled ||
                         imp.spritePixelsPerUnit != 32f;
            if (dirty)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.filterMode = FilterMode.Point;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.isReadable = true;
                imp.mipmapEnabled = false;
                imp.spritePixelsPerUnit = 32f;
                imp.SaveAndReimport();
            }
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static void AddSceneToBuild(string scenePath)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == scenePath)) return;
        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
