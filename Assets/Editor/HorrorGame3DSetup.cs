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
/// The scene also holds the animated bed (press E to enter the nightmare) and an
/// apricot dog companion that follows the player in the overworld.
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
    const string DogSheet   = "Assets/Animation/dog_apricot.png";
    const string PartnerBoy = "Assets/Animation/partner_boy.png";
    const string PartnerGirl = "Assets/Animation/partner_girl.png";
    const string HouseSheet = "Assets/Animation/house.png";
    const string HouseBack  = "Assets/Animation/house_back.png";
    const string HouseSide  = "Assets/Animation/house_side.png";
    const string HouseSideMirror = "Assets/Animation/house_side_mirror.png";
    const string GreenTree  = "Assets/Animation/tree_spruce.png";
    const string WinterTree = "Assets/Animation/tree_spruce_winter.png";
    const string GrassSheet = "Assets/Animation/grass_tufts.png";
    const string InteriorFloorTex = "Assets/Art/Environment/interior_floor.png";
    const string InteriorWallTex  = "Assets/Art/Environment/interior_wall.png";
    const string SceneOut   = "Assets/Scenes/Sandbox3D.unity";
    const string ExteriorSceneOut = "Assets/Scenes/Exterior.unity";
    const int SetupVersion  = 17;  // bump to force the auto-run to rebuild the scenes

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
            try { BuildSandbox3D(); BuildExterior(); }
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
        SliceGrid(DogSheet, 32f, 0.06f, 32, 32, 6, new[] { "dog_idle_", "dog_walk_", "dog_run_", "dog_heart_" });
        SliceGrid(PartnerBoy, 16f, 0.09f, 32, 32, 6, new[] { "idle_", "speak_", "wave_", "talk_", "smile_" });
        SliceGrid(PartnerGirl, 16f, 0.09f, 32, 32, 6, new[] { "idle_", "speak_", "wave_", "talk_", "smile_" });
        var backSprites = LoadSheetSprites(BackSheet, "back_");
        var frontSprites = LoadSheetSprites(FrontSheet, "front_");
        var bedSprites = LoadSheetSprites(BedSheet, "bed_");
        var dogIdle = LoadSheetSprites(DogSheet, "dog_idle_");
        var dogWalk = LoadSheetSprites(DogSheet, "dog_walk_");
        var dogHeart = LoadSheetSprites(DogSheet, "dog_heart_");
        var boyIdle = LoadSheetSprites(PartnerBoy, "idle_");
        var girlIdle = LoadSheetSprites(PartnerGirl, "idle_");
        var boySmile = LoadSheetSprites(PartnerBoy, "smile_");
        var girlSmile = LoadSheetSprites(PartnerGirl, "smile_");
        var boySpeak = LoadSheetSprites(PartnerBoy, "speak_");
        var girlSpeak = LoadSheetSprites(PartnerGirl, "speak_");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.5f);

        var sun = new GameObject("Directional Light").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.1f;
        sun.color = new Color(1f, 0.97f, 0.9f);
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // floor + walls (interior tiles)
        EnsurePixelTexture(InteriorFloorTex);
        EnsurePixelTexture(InteriorWallTex);
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.localScale = new Vector3(2f, 1f, 2f);          // 10x10 plane -> 20x20
        floor.GetComponent<Renderer>().sharedMaterial =
            LitMaterial("FloorMat3D", Color.white, InteriorFloorTex, new Vector2(10f, 10f), repeat: true);

        var wallMat = LitMaterial("WallMat3D", Color.white, InteriorWallTex, new Vector2(8f, 1.5f), repeat: true);
        MakeWall("Wall_N", new Vector3(0f, 1.5f, 10f),  new Vector3(20f, 3f, 0.5f), wallMat);
        MakeWall("Wall_S", new Vector3(0f, 1.5f, -10f), new Vector3(20f, 3f, 0.5f), wallMat);
        MakeWall("Wall_E", new Vector3(10f, 1.5f, 0f),  new Vector3(0.5f, 3f, 20f), wallMat);
        MakeWall("Wall_W", new Vector3(-10f, 1.5f, 0f), new Vector3(0.5f, 3f, 20f), wallMat);

        var spriteMat = SpriteMaterial();

        // ---- player rig (recolored to the chosen look) ----
        var player = BuildPlayerRig(new Vector3(0f, 0.1f, -5f), spriteMat);

        // the dog + partner companions fill the two former blob slots
        MakePartner(new Vector3(-4f, 0f, 3f), player.transform, boyIdle, girlIdle, boySmile, girlSmile, boySpeak, girlSpeak, spriteMat);

        // ---- nightmare transition + the bed that triggers it ----
        var nightmare = new GameObject("Nightmare").AddComponent<NightmareController>();
        nightmare.sun = sun;

        new GameObject("DialogUI").AddComponent<DialogUI>();   // shared interaction prompt + dialog

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

        // ---- apricot dog companion (overworld only; hides during the nightmare) ----
        MakeDog(new Vector3(3f, 0f, 5f), player.transform, nightmare, dogIdle, dogWalk, dogHeart, spriteMat);

        EditorSceneManager.SaveScene(scene, SceneOut);
        AddSceneToBuild(SceneOut);
        Debug.Log("[HorrorGame] 3D Sandbox built at " + SceneOut + " with the character (back " +
                  backSprites.Length + "/front " + frontSprites.Length + "), the bed (" + bedSprites.Length +
                  "), the apricot dog (idle " + dogIdle.Length + "/walk " + dogWalk.Length +
                  "), and the partner (boy idle " + boyIdle.Length + "/girl idle " + girlIdle.Length + "). " +
                  "Walk to the bed + press E to enter the nightmare (the dog hides). " +
                  "Play: WASD + mouse, V = first/third, hold C = look behind.");
    }

    // -------------------------------------------------------------- player rig
    // The billboard player + camera rig, recolored to the saved look. Shared by the
    // interior and the exterior so the chosen character appears in both.
    static GameObject BuildPlayerRig(Vector3 spawnPos, Material spriteMat)
    {
        SliceStrip(BackSheet, "back_", 5, 32, 32, 16f, 0.09f);
        SliceStrip(FrontSheet, "front_", 5, 32, 32, 16f, 0.09f);
        var back = LoadSheetSprites(BackSheet, "back_");
        var front = LoadSheetSprites(FrontSheet, "front_");

        var player = new GameObject("Player");
        player.transform.position = spawnPos;
        var cc = player.AddComponent<CharacterController>();
        cc.height = 2f; cc.radius = 0.3f; cc.center = new Vector3(0f, 1f, 0f);
        player.AddComponent<PlayerController3D>();

        var spriteGo = new GameObject("Sprite");
        spriteGo.transform.SetParent(player.transform, false);
        spriteGo.transform.localPosition = Vector3.zero;
        var sr = spriteGo.AddComponent<SpriteRenderer>();
        sr.sprite = back.Length > 0 ? back[0] : LoadSprite(PlayerPng, "player_idle_0");
        sr.sharedMaterial = spriteMat;
        spriteGo.AddComponent<Billboard>();
        var charAnim = spriteGo.AddComponent<CharacterBillboardAnimator>();
        charAnim.backFrames = back;
        charAnim.frontFrames = front;
        charAnim.player = player.GetComponent<PlayerController3D>();
        charAnim.fps = 8f;

        var applier = spriteGo.AddComponent<CharacterLookApplier>();
        applier.masterFront = CharacterSelectSetup.ConfigureMaster(CharacterSelectSetup.MasterFront);
        applier.masterBack = CharacterSelectSetup.ConfigureMaster(CharacterSelectSetup.MasterBack);
        applier.masterFrontLong = CharacterSelectSetup.ConfigureMaster(CharacterSelectSetup.MasterFrontLong);
        applier.masterBackLong = CharacterSelectSetup.ConfigureMaster(CharacterSelectSetup.MasterBackLong);
        applier.animator = charAnim;

        var pivot = new GameObject("CameraPivot");
        pivot.transform.SetParent(player.transform, false);
        pivot.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        var rig = pivot.AddComponent<CameraRig>();

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        camGo.transform.SetParent(pivot.transform, false);
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.05f;
        cam.GetUniversalAdditionalCameraData().SetRenderer(_renderer3DIndex);
        rig.cam = cam;
        rig.playerSprite = sr;
        charAnim.cameraTransform = camGo.transform;

        return player;
    }

    // -------------------------------------------------------------- exterior (house)
    [MenuItem("Tools/Horror Game/Build Exterior")]
    public static void BuildExterior()
    {
        EnsureRenderer3D();
        SliceGrid(GreenTree, 18f, 0.05f, 80, 152, 6, new[] { "green0_", "green1_" });   // 12-frame sway
        SliceGrid(WinterTree, 18f, 0.05f, 80, 152, 6, new[] { "winter0_", "winter1_" });
        SliceGrid(GrassSheet, 32f, 0f, 32, 32, 6, new[] { "grassa_", "grassb_", "grassc_" });
        var greenFrames = LoadSheetSprites(GreenTree, "green");
        var winterFrames = LoadSheetSprites(WinterTree, "winter");
        var grass = LoadSheetSprites(GrassSheet, "grass");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.5f, 0.52f, 0.55f);

        var sun = new GameObject("Directional Light").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.15f;
        sun.color = new Color(1f, 0.97f, 0.88f);
        sun.transform.rotation = Quaternion.Euler(55f, -25f, 0f);

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(8f, 1f, 8f);        // 80x80 yard
        ground.GetComponent<Renderer>().sharedMaterial =
            LitMaterial("YardMat3D", new Color(0.36f, 0.5f, 0.28f), null, Vector2.one, false);

        var spriteMat = SpriteMaterial();

        // The house: a real 3D log cabin built from house_tiles.png — tiled-siding walls, interlocking
        // corner logs and a closed gable roof (CabinShellBuilder), all anchored on the ground.
        var housePortal = BuildCabin(new Vector3(0f, 0f, 10f));

        var player = BuildPlayerRig(new Vector3(0f, 0.1f, 0f), spriteMat);   // spawns facing the cabin
        housePortal.player = player.transform;

        new GameObject("DialogUI").AddComponent<DialogUI>();

        // ---- dense forest ring: green spruces near the house, snowy winter farther out ----
        Vector3 forest = new Vector3(0f, 0f, 6f);
        int treeCount = 0;
        treeCount += PlaceTreeRing(forest, 15f,   11, greenFrames,  spriteMat, 1);
        treeCount += PlaceTreeRing(forest, 19f,   14, greenFrames,  spriteMat, 2);
        treeCount += PlaceTreeRing(forest, 23.5f, 17, winterFrames, spriteMat, 3);
        treeCount += PlaceTreeRing(forest, 28f,   21, winterFrames, spriteMat, 4);

        Vector3[] grassPos = {
            new Vector3(-3f, 0f, 3f), new Vector3(3f, 0f, 5f), new Vector3(-5f, 0f, -2f),
            new Vector3(5f, 0f, 1f), new Vector3(-2f, 0f, 6f), new Vector3(4f, 0f, 7f),
            new Vector3(-6f, 0f, 4f), new Vector3(6f, 0f, -3f), new Vector3(2f, 0f, -4f),
            new Vector3(-4f, 0f, 9f),
        };
        for (int i = 0; i < grassPos.Length && grass.Length > 0; i++)
            MakeProp("Grass", grassPos[i], grass[(i * 3) % grass.Length], spriteMat);

        EditorSceneManager.SaveScene(scene, ExteriorSceneOut);
        AddSceneToBuild(ExteriorSceneOut);
        Debug.Log("[HorrorGame] Exterior built at " + ExteriorSceneOut + " with the log cabin (tiled walls + " +
                  "corner logs + gable roof), " + treeCount + " animated trees (green + winter), " +
                  grassPos.Length + " grass tufts. Walk up to the cabin; from the front press E to enter.");
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
                spriteID = StableGuid(path + "#" + prefix + i),
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

    // Slices a grid sheet, giving each visual row (top -> bottom) its own name prefix,
    // e.g. {"dog_idle_","dog_walk_","dog_run_"} for the 3-row dog sheet.
    static void SliceGrid(string path, float ppu, float pivotY, int cellW, int cellH, int cols, string[] rowPrefixes)
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

        int rows = rowPrefixes.Length;
        var rects = new List<SpriteRect>();
        for (int r = 0; r < rows; r++)
        {
            int yUnity = (rows - 1 - r) * cellH;   // visual row r (0 = top) -> Unity y (origin bottom-left)
            for (int c = 0; c < cols; c++)
                rects.Add(new SpriteRect
                {
                    name = rowPrefixes[r] + c,
                    spriteID = StableGuid(path + "#" + rowPrefixes[r] + c),
                    rect = new Rect(c * cellW, yUnity, cellW, cellH),
                    pivot = new Vector2(0.5f, pivotY),
                    alignment = SpriteAlignment.Custom,
                    border = Vector4.zero,
                });
        }
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

    // Deterministic sprite id from the sheet path + frame name, so re-slicing yields the
    // same ids each build (no meta churn, scene sprite references stay valid).
    static GUID StableGuid(string key)
    {
        using (var md5 = System.Security.Cryptography.MD5.Create())
        {
            var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(key));
            var sb = new System.Text.StringBuilder(32);
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return new GUID(sb.ToString());
        }
    }

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

    // Import a small pixel-art texture for crisp tiling (point filter, repeat wrap, no compression).
    static void EnsurePixelTexture(string path)
    {
        if (AssetImporter.GetAtPath(path) is TextureImporter imp)
        {
            bool dirty = imp.filterMode != FilterMode.Point || imp.wrapMode != TextureWrapMode.Repeat ||
                         imp.textureCompression != TextureImporterCompression.Uncompressed || imp.mipmapEnabled;
            if (dirty)
            {
                imp.filterMode = FilterMode.Point;
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.mipmapEnabled = false;
                imp.SaveAndReimport();
            }
        }
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

    static void MakeProp(string name, Vector3 pos, Sprite sprite, Material mat)
    {
        if (sprite == null) return;
        var go = new GameObject(name);
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sharedMaterial = mat;
        go.AddComponent<Billboard>();
    }

    // ---------------------------------------------------------------- cabin (tile house)
    // Builds a 3D log cabin from house_tiles.png at `origin`: tiled-siding walls, a door, and (via
    // CabinShellBuilder) interlocking corner logs + a closed gable roof. Returns its HousePortal.
    static HousePortal BuildCabin(Vector3 origin)
    {
        const float W = 6f, D = 7f, Hh = 3.5f, tile = 0.5f;
        float hd = D * 0.5f;
        var atlasMat = CabinAtlasMaterial();

        var house = new GameObject("House");
        house.transform.position = origin;

        // tiled-siding wall box, base at local y = 0 (planted on the ground)
        var walls = new GameObject("CabinWalls");
        walls.transform.SetParent(house.transform, false);
        walls.AddComponent<MeshFilter>().sharedMesh = BuildCabinWallMesh(W, Hh, D, tile);
        walls.AddComponent<MeshRenderer>().sharedMaterial = atlasMat;

        // door on the front gable end (faces -Z, toward the approaching player)
        var door = MakeTileQuad("CabinDoor", house.transform, new Vector3(0f, 1.2f, -hd - 0.03f),
                                Quaternion.identity, 1.5f, 2.4f, 6, 0, atlasMat);

        // interlocking corner logs + closed gable roof (real geometry, sized from the wall bounds)
        var shell = walls.AddComponent<CabinShellBuilder>();
        shell.atlasMaterial = atlasMat;
        shell.ridgeAlongZ = true;          // front/back are the gable ends (the door side)
        shell.worldUnitsPerTile = tile;
        shell.cornerPostSize = 0.4f;
        shell.cornerOverhang = 0.08f;
        shell.ridgeHeight = 1.8f;
        shell.eaveOverhang = 0.4f;
        shell.gableOverhang = 0.35f;
        shell.buildEaveFascia = true;
        shell.Build();

        // solid footprint so the player circles it and enters from the front
        var col = house.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, Hh * 0.5f, 0f);
        col.size = new Vector3(W, Hh, D);

        var hp = door.AddComponent<HousePortal>();
        hp.interiorScene = "Sandbox3D";
        return hp;
    }

    // URP/Unlit material sampling house_tiles.png (point, alpha-clipped, double-sided).
    static Material CabinAtlasMaterial()
    {
        const string atlasPath = "Assets/Animation/house_tiles.png";
        if (AssetImporter.GetAtPath(atlasPath) is TextureImporter imp)
        {
            bool dirty = imp.textureType != TextureImporterType.Default || imp.filterMode != FilterMode.Point ||
                         imp.textureCompression != TextureImporterCompression.Uncompressed ||
                         imp.mipmapEnabled || !imp.isReadable;
            if (dirty)
            {
                imp.textureType = TextureImporterType.Default;
                imp.filterMode = FilterMode.Point;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.mipmapEnabled = false;
                imp.isReadable = true;
                imp.SaveAndReimport();
            }
        }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
        EnsureFolder(MatDir);
        string matPath = MatDir + "/HouseTiles3D.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (mat == null) { mat = new Material(sh); AssetDatabase.CreateAsset(mat, matPath); }
        else mat.shader = sh;
        mat.mainTexture = tex;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 1f);
        if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", 0.5f);
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
        mat.EnableKeyword("_ALPHATEST_ON");
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // 8x6 atlas of 24px tiles; UV rect for tile (col,row) with a half-texel inset.
    static Rect CabinTileUV(int col, int row)
    {
        const float aw = 192f, ah = 144f, tp = 24f;
        float u0 = col * tp / aw, u1 = (col + 1) * tp / aw;
        float v1 = 1f - row * tp / ah, v0 = 1f - (row + 1) * tp / ah;
        float eu = 0.5f / aw, ev = 0.5f / ah;
        return Rect.MinMaxRect(u0 + eu, v0 + ev, u1 - eu, v1 - ev);
    }

    // Four siding walls (tile 0,0) tiled at `tile` world units, base at y = 0.
    static Mesh BuildCabinWallMesh(float W, float Hh, float D, float tile)
    {
        var v = new List<Vector3>();
        var uv = new List<Vector2>();
        var tri = new List<int>();
        Rect s = CabinTileUV(0, 0);
        float hw = W * 0.5f, hd = D * 0.5f;
        int nW = Mathf.Max(1, Mathf.RoundToInt(W / tile));
        int nD = Mathf.Max(1, Mathf.RoundToInt(D / tile));
        int nH = Mathf.Max(1, Mathf.RoundToInt(Hh / tile));
        AddTiledWall(v, uv, tri, new Vector3(-hw, 0f, -hd), new Vector3(hw, 0f, -hd), new Vector3(-hw, Hh, -hd), s, nW, nH); // front
        AddTiledWall(v, uv, tri, new Vector3(-hw, 0f,  hd), new Vector3(hw, 0f,  hd), new Vector3(-hw, Hh,  hd), s, nW, nH); // back
        AddTiledWall(v, uv, tri, new Vector3(-hw, 0f, -hd), new Vector3(-hw, 0f, hd), new Vector3(-hw, Hh, -hd), s, nD, nH); // left
        AddTiledWall(v, uv, tri, new Vector3( hw, 0f, -hd), new Vector3( hw, 0f, hd), new Vector3( hw, Hh, -hd), s, nD, nH); // right
        var m = new Mesh { name = "CabinWalls" };
        m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
        m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    // Fills the parallelogram a->b (u) and a->d (v) with nu x nv copies of tile rect r.
    static void AddTiledWall(List<Vector3> v, List<Vector2> uv, List<int> tri,
                             Vector3 a, Vector3 b, Vector3 d, Rect r, int nu, int nv)
    {
        Vector3 c = b + (d - a);
        for (int i = 0; i < nu; i++)
        for (int j = 0; j < nv; j++)
        {
            float u0 = i / (float)nu, u1 = (i + 1) / (float)nu, w0 = j / (float)nv, w1 = (j + 1) / (float)nv;
            Vector3 p00 = CabinBilerp(a, b, d, c, u0, w0);
            Vector3 p10 = CabinBilerp(a, b, d, c, u1, w0);
            Vector3 p11 = CabinBilerp(a, b, d, c, u1, w1);
            Vector3 p01 = CabinBilerp(a, b, d, c, u0, w1);
            int sIdx = v.Count;
            v.Add(p00); v.Add(p10); v.Add(p11); v.Add(p01);
            uv.Add(new Vector2(r.xMin, r.yMin)); uv.Add(new Vector2(r.xMax, r.yMin));
            uv.Add(new Vector2(r.xMax, r.yMax)); uv.Add(new Vector2(r.xMin, r.yMax));
            tri.Add(sIdx); tri.Add(sIdx + 1); tri.Add(sIdx + 2);
            tri.Add(sIdx); tri.Add(sIdx + 2); tri.Add(sIdx + 3);
        }
    }

    static Vector3 CabinBilerp(Vector3 a, Vector3 b, Vector3 d, Vector3 c, float u, float w)
        => a * (1 - u) * (1 - w) + b * u * (1 - w) + d * (1 - u) * w + c * u * w;

    // A single flat quad showing atlas tile (col,row), centred on its local origin.
    static GameObject MakeTileQuad(string name, Transform parent, Vector3 localPos, Quaternion localRot,
                                   float w, float h, int col, int row, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        Rect r = CabinTileUV(col, row);
        float hw = w * 0.5f, hh = h * 0.5f;
        var m = new Mesh { name = name };
        m.SetVertices(new List<Vector3> {
            new Vector3(-hw, -hh, 0f), new Vector3(hw, -hh, 0f), new Vector3(hw, hh, 0f), new Vector3(-hw, hh, 0f) });
        m.SetUVs(0, new List<Vector2> {
            new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax) });
        m.SetTriangles(new int[] { 0, 1, 2, 0, 2, 3 }, 0);
        m.RecalculateNormals(); m.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = m;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    static void MakeTree(Vector3 pos, Sprite[] frames, Material mat)
    {
        if (frames == null || frames.Length == 0) return;
        var go = new GameObject("Tree");
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = frames[0];
        sr.sharedMaterial = mat;
        go.AddComponent<Billboard>();
        var anim = go.AddComponent<LoopSpriteAnimator>();
        anim.frames = frames;
        anim.fps = 5f;                  // gentle sway
        anim.randomStartPhase = true;   // out of sync between trees
    }

    // Rings of trees around a centre with deterministic jitter (so rebuilds don't churn the scene).
    static int PlaceTreeRing(Vector3 center, float radius, int count, Sprite[] frames, Material mat, int seed)
    {
        for (int i = 0; i < count; i++)
        {
            float a = (i + 0.5f) / count * Mathf.PI * 2f + (Hash01(seed * 131 + i) - 0.5f) * 0.4f;
            float r = radius + (Hash01(seed * 197 + i) - 0.5f) * 4f;
            MakeTree(center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r), frames, mat);
        }
        return count;
    }

    static float Hash01(int n)
    {
        float s = Mathf.Sin(n * 12.9898f) * 43758.5453f;
        return s - Mathf.Floor(s);
    }

    static void MakeDog(Vector3 pos, Transform player, NightmareController nightmare,
                        Sprite[] idle, Sprite[] walk, Sprite[] heart, Material mat)
    {
        var go = new GameObject("Dog");
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = idle.Length > 0 ? idle[0] : null;
        sr.sharedMaterial = mat;
        go.AddComponent<Billboard>();
        var dog = go.AddComponent<DogCompanion>();
        dog.player = player;
        dog.nightmare = nightmare;
        dog.idleFrames = idle;
        dog.walkFrames = walk;
        dog.heartFrames = heart;
        dog.fps = 6f;
    }

    static void MakePartner(Vector3 pos, Transform player, Sprite[] boyIdle, Sprite[] girlIdle,
                            Sprite[] boySmile, Sprite[] girlSmile, Sprite[] boySpeak, Sprite[] girlSpeak, Material mat)
    {
        var go = new GameObject("Partner");
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = boyIdle.Length > 0 ? boyIdle[0] : null;
        sr.sharedMaterial = mat;
        go.AddComponent<Billboard>();
        var anim = go.AddComponent<LoopSpriteAnimator>();
        anim.frames = boyIdle;                 // overridden at runtime by the chosen partner
        anim.fps = 6f;
        var pc = go.AddComponent<PartnerController>();
        pc.player = player;
        pc.boyIdle = boyIdle;
        pc.girlIdle = girlIdle;
        pc.boySmile = boySmile;
        pc.girlSmile = girlSmile;
        pc.boySpeak = boySpeak;
        pc.girlSpeak = girlSpeak;
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
