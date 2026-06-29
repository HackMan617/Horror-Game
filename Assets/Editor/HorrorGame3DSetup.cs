using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Stage 1 of the 2.5D pivot: ensures a 3D Universal Renderer exists (added to the
/// URP asset as a second renderer so the existing 2D scenes keep working), then
/// builds an isolated 3D sandbox scene with a floor, walls, lighting, and the
/// billboard player rig (mouse-look + V toggles first/third person). The player
/// uses the new character: the back sheet when viewed from behind, the front sheet
/// when viewed from the front (hold C to look behind), both animated from movement.
/// Menu: Tools > Horror Game > Build 3D Sandbox. Also auto-runs once (version-gated).
/// </summary>
public static class HorrorGame3DSetup
{
    const string Urp        = "Assets/Settings/UniversalRP.asset";
    const string Renderer3D = "Assets/Settings/Renderer3D.asset";
    const string MatDir     = "Assets/Settings/3D";
    const string PlayerPng  = "Assets/Art/Player/Player.png";
    const string FloorTex   = "Assets/Art/Environment/LobbyFloor.png";
    const string BlobPng    = "Assets/Art/Environment/Blob.png";
    const string BackSheet  = "Assets/Animation/character_sprite_sheet_back.png";
    const string FrontSheet = "Assets/Animation/character_sprite_sheet.png";
    const string BedSheet   = "Assets/Animation/bed_sprite_sheet.png";
    const string SceneOut   = "Assets/Scenes/Sandbox3D.unity";
    const int SetupVersion  = 4;   // bump to force the auto-run to rebuild the sandbox

    static int _renderer3DIndex = 1;

    [InitializeOnLoadMethod]
    static void AutoRun()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorPrefs.GetInt("HG3D_SetupVersion", 0) >= SetupVersion) return;
            EditorPrefs.SetInt("HG3D_SetupVersion", SetupVersion);
            try { BuildSandbox3D(); }
            catch (System.Exception e) { Debug.LogError("[HorrorGame] 3D auto-build failed: " + e); }
        };
    }

    [MenuItem("Tools/Horror Game/Build 3D Sandbox")]
    public static void BuildSandbox3D()
    {
        EnsureRenderer3D();
        SliceStrip(BackSheet, "back_", 5, 32, 32, 16f, 0.09f);
        SliceStrip(FrontSheet, "front_", 5, 32, 32, 16f, 0.09f);
        SliceStrip(BedSheet, "bed_", 6, 64, 32, 32f, 0.08f);
        var backSprites = LoadSheetSprites(BackSheet, "back_");
        var frontSprites = LoadSheetSprites(FrontSheet, "front_");
        var bedSprites = LoadSheetSprites(BedSheet, "bed_");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.5f);

        var sun = new GameObject("Directional Light").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.1f;
        sun.color = new Color(1f, 0.97f, 0.9f);
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // floor (tiled pixel texture) + walls
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.localScale = new Vector3(2f, 1f, 2f);          // 10x10 plane -> 20x20
        floor.GetComponent<Renderer>().sharedMaterial =
            LitMaterial("FloorMat3D", Color.white, FloorTex, new Vector2(8f, 8f), repeat: true);

        var wallMat = LitMaterial("WallMat3D", new Color(0.42f, 0.30f, 0.20f), null, Vector2.one, false);
        MakeWall("Wall_N", new Vector3(0f, 1.5f, 10f),  new Vector3(20f, 3f, 0.5f), wallMat);
        MakeWall("Wall_S", new Vector3(0f, 1.5f, -10f), new Vector3(20f, 3f, 0.5f), wallMat);
        MakeWall("Wall_E", new Vector3(10f, 1.5f, 0f),  new Vector3(0.5f, 3f, 20f), wallMat);
        MakeWall("Wall_W", new Vector3(-10f, 1.5f, 0f), new Vector3(0.5f, 3f, 20f), wallMat);

        var spriteMat = SpriteMaterial();

        // ---- player rig ----
        var player = new GameObject("Player");
        player.transform.position = new Vector3(0f, 0.1f, -5f);
        var cc = player.AddComponent<CharacterController>();
        cc.height = 2f; cc.radius = 0.3f; cc.center = new Vector3(0f, 1f, 0f);
        player.AddComponent<PlayerController3D>();

        var spriteGo = new GameObject("Sprite");
        spriteGo.transform.SetParent(player.transform, false);
        spriteGo.transform.localPosition = Vector3.zero;              // feet pivot at the player's feet
        var sr = spriteGo.AddComponent<SpriteRenderer>();
        sr.sprite = backSprites.Length > 0 ? backSprites[0] : LoadSprite(PlayerPng, "player_idle_0");
        sr.sharedMaterial = spriteMat;
        spriteGo.AddComponent<Billboard>();
        var charAnim = spriteGo.AddComponent<CharacterBillboardAnimator>();
        charAnim.backFrames = backSprites;
        charAnim.frontFrames = frontSprites;
        charAnim.player = player.GetComponent<PlayerController3D>();
        charAnim.fps = 8f;

        var pivot = new GameObject("CameraPivot");
        pivot.transform.SetParent(player.transform, false);
        pivot.transform.localPosition = new Vector3(0f, 1.6f, 0f);    // head height
        var rig = pivot.AddComponent<CameraRig>();

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        camGo.transform.SetParent(pivot.transform, false);
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.05f;
        cam.GetUniversalAdditionalCameraData().SetRenderer(_renderer3DIndex);
        rig.cam = cam;
        rig.playerSprite = sr;
        charAnim.cameraTransform = camGo.transform;

        // a couple of billboard blobs for reference
        MakeBlob3D(new Vector3(-4f, 0f, 3f), new Color(0.82f, 0.58f, 0.35f), spriteMat);
        MakeBlob3D(new Vector3(3f, 0f, 5f),  new Color(0.47f, 0.66f, 0.90f), spriteMat);

        // ---- nightmare transition + the bed that triggers it ----
        var nightmare = new GameObject("Nightmare").AddComponent<NightmareController>();
        nightmare.sun = sun;

        var bed = new GameObject("Bed");
        bed.transform.position = new Vector3(5f, 0f, 8.2f);    // across the room, by the north wall
        var bedSr = bed.AddComponent<SpriteRenderer>();
        bedSr.sprite = bedSprites.Length > 0 ? bedSprites[0] : null;
        bedSr.sharedMaterial = spriteMat;
        bed.AddComponent<Billboard>();
        var bedAnim = bed.AddComponent<LoopSpriteAnimator>();
        bedAnim.frames = bedSprites;
        bedAnim.fps = 6f;
        var portal = bed.AddComponent<BedPortal>();
        portal.player = player.transform;
        portal.nightmare = nightmare;

        EditorSceneManager.SaveScene(scene, SceneOut);
        AddSceneToBuild(SceneOut);
        Debug.Log("[HorrorGame] 3D Sandbox built at " + SceneOut + " with the new character (back " +
                  backSprites.Length + " / front " + frontSprites.Length + " frames) and the bed (" +
                  bedSprites.Length + " frames). Walk to the bed + press E to enter the nightmare. " +
                  "Play: WASD + mouse, V = first/third, hold C = look behind.");
    }

    // -------------------------------------------------------------- renderer
    static void EnsureRenderer3D()
    {
        var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Renderer3D);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(data, Renderer3D);
            AssetDatabase.SaveAssets();
        }

        var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Urp);
        var so = new SerializedObject(urp);
        var list = so.FindProperty("m_RendererDataList");

        int found = -1;
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == data) { found = i; break; }

        if (found < 0)
        {
            found = list.arraySize;
            list.InsertArrayElementAtIndex(found);
            list.GetArrayElementAtIndex(found).objectReferenceValue = data;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(urp);
            AssetDatabase.SaveAssets();
        }
        _renderer3DIndex = found;
    }

    // -------------------------------------------------------------- sprite sheets
    // Slices a horizontal strip into `count` cells (cellW x cellH) with a bottom-centre
    // pivot, so billboards share a consistent quad size and ground point. PPU sets the
    // world size: characters use 16 (32px -> 2 units tall), the bed uses 32.
    static void SliceStrip(string path, string prefix, int count, int cellW, int cellH, float ppu, float pivotY)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter imp)) return;
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Multiple;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.spritePixelsPerUnit = ppu;
        imp.mipmapEnabled = false;
        imp.wrapMode = TextureWrapMode.Clamp;

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var dp = factory.GetSpriteEditorDataProviderFromObject(imp);
        dp.InitSpriteEditorDataProvider();

        var rects = new List<SpriteRect>();
        for (int i = 0; i < count; i++)
            rects.Add(new SpriteRect
            {
                name = prefix + i,
                spriteID = GUID.Generate(),
                rect = new Rect(i * cellW, 0, cellW, cellH),
                pivot = new Vector2(0.5f, pivotY),    // bottom-centre (feet / base)
                alignment = SpriteAlignment.Custom,
                border = Vector4.zero,
            });
        dp.SetSpriteRects(rects.ToArray());
        try
        {
            var nid = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (nid != null) nid.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        }
        catch { }
        dp.Apply();
        imp.SaveAndReimport();
    }

    static Sprite[] LoadSheetSprites(string path, string prefix) =>
        AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
            .Where(s => s.name.StartsWith(prefix)).OrderBy(s => s.name).ToArray();

    // -------------------------------------------------------------- helpers
    static Material LitMaterial(string name, Color color, string texPath, Vector2 tiling, bool repeat)
    {
        EnsureFolder(MatDir);
        string path = MatDir + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.color = color;
        if (!string.IsNullOrEmpty(texPath))
        {
            if (repeat && AssetImporter.GetAtPath(texPath) is TextureImporter imp && imp.wrapMode != TextureWrapMode.Repeat)
            {
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.SaveAndReimport();
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            mat.SetTexture("_BaseMap", tex);
            mat.mainTexture = tex;
            mat.mainTextureScale = tiling;
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material SpriteMaterial()
    {
        EnsureFolder(MatDir);
        string path = MatDir + "/SpriteBillboard3D.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Sprites/Default"));   // unlit, renders under any pipeline
            AssetDatabase.CreateAsset(mat, path);
        }
        return mat;
    }

    static void MakeWall(string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
        w.name = name;
        w.transform.position = pos;
        w.transform.localScale = scale;
        w.GetComponent<Renderer>().sharedMaterial = mat;
    }

    static void MakeBlob3D(Vector3 pos, Color color, Material mat)
    {
        var go = new GameObject("Blob");
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = LoadSpriteAt(BlobPng);
        sr.sharedMaterial = mat;
        sr.color = color;
        go.AddComponent<Billboard>();
        go.AddComponent<BlobAnimator>();
    }

    static Sprite LoadSprite(string path, string name) =>
        AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault(s => s.name == name);

    static Sprite LoadSpriteAt(string path) =>
        AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        var leaf = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    static void AddSceneToBuild(string scenePath)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == scenePath)) return;
        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
