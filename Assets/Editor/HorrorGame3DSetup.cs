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
    static readonly string[] DogSheets = {
        "Assets/Animation/dog_apricot.png",
        "Assets/Animation/dog_chocolate.png",
        "Assets/Animation/dog_cream.png",
    };
    const string PartnerBoy = "Assets/Animation/partner_boy.png";
    const string PartnerGirl = "Assets/Animation/partner_girl.png";
    const string HouseSheet = "Assets/Animation/house.png";
    const string HouseBack  = "Assets/Animation/house_back.png";
    const string HouseSide  = "Assets/Animation/house_side.png";
    const string HouseSideMirror = "Assets/Animation/house_side_mirror.png";
    const string GreenTree  = "Assets/Animation/tree_spruce.png";
    const string WinterTree = "Assets/Animation/tree_spruce_winter.png";
    const string GrassSheet = "Assets/Animation/grass_tufts.png";
    const string SmokePuffPng = "Assets/Animation/smoke_puff.png";
    const string PropsAutumn = "Assets/Animation/props_autumn.png";
    const string PathCobble  = "Assets/Animation/path_cobble.png";
    const string RangeBackdrop = "Assets/Animation/range_backdrop.png";
    const string SunPng     = "Assets/Animation/sun.png";
    const string MoonPng    = "Assets/Animation/moon.png";
    const string FootstepDir = "Assets/Sound Effects/Footsteps";
    const string BirdsWav   = "Assets/Sound Effects/Birds Singing.wav";
    const string WoodStepsWav = "Assets/Sound Effects/Footsteps on Wooden Floor.wav";
    const string DoorSfx    = "Assets/Sound Effects/door opening.mp3";
    const string InteriorFloorTex = "Assets/Art/Environment/interior_floor.png";
    const string InteriorWallTex  = "Assets/Art/Environment/interior_wall.png";
    const string SceneOut   = "Assets/Scenes/Sandbox3D.unity";
    const string ExteriorSceneOut = "Assets/Scenes/Exterior.unity";
    const int SetupVersion  = 33;  // bump to force the auto-run to rebuild the scenes

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
        SliceGrid(PartnerBoy, 16f, 0.09f, 32, 32, 6, new[] { "idle_", "speak_", "wave_", "talk_", "smile_" });
        SliceGrid(PartnerGirl, 16f, 0.09f, 32, 32, 6, new[] { "idle_", "speak_", "wave_", "talk_", "smile_" });
        var backSprites = LoadSheetSprites(BackSheet, "back_");
        var frontSprites = LoadSheetSprites(FrontSheet, "front_");
        var bedSprites = LoadSheetSprites(BedSheet, "bed_");
        var dogBreeds = BuildDogBreeds();      // apricot / chocolate / cream, one chosen at runtime
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
        var player = BuildPlayerRig(new Vector3(0f, 0.1f, -5f), spriteMat, grassFill: false);   // interior: no grass fill
        AttachWoodFloorFootsteps(player);   // wooden interior floor: looping wood footfalls instead of grass steps

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

        // ---- dog companion (breed randomised at character select; overworld only, hides in the nightmare) ----
        MakeDog(new Vector3(3f, 0f, 5f), player.transform, nightmare, dogBreeds, spriteMat);

        EditorSceneManager.SaveScene(scene, SceneOut);
        AddSceneToBuild(SceneOut);
        Debug.Log("[HorrorGame] 3D Sandbox built at " + SceneOut + " with the character (back " +
                  backSprites.Length + "/front " + frontSprites.Length + "), the bed (" + bedSprites.Length +
                  "), the dog (" + dogBreeds.Length + " breeds, randomised on character select" +
                  "), and the partner (boy idle " + boyIdle.Length + "/girl idle " + girlIdle.Length + "). " +
                  "Walk to the bed + press E to enter the nightmare (the dog hides). " +
                  "Play: WASD + mouse, V = first/third, hold C = look behind.");
    }

    // -------------------------------------------------------------- player rig
    // The billboard player + camera rig, recolored to the saved look. Shared by the
    // interior and the exterior so the chosen character appears in both.
    static GameObject BuildPlayerRig(Vector3 spawnPos, Material spriteMat, bool grassFill = true)
    {
        SliceStrip(BackSheet, "back_", 5, 32, 32, 16f, 0.09f);
        SliceStrip(FrontSheet, "front_", 5, 32, 32, 16f, 0.09f);
        var back = LoadSheetSprites(BackSheet, "back_");
        var front = LoadSheetSprites(FrontSheet, "front_");

        var player = new GameObject("Player");
        player.transform.position = spawnPos;
        var cc = player.AddComponent<CharacterController>();
        cc.height = 2f; cc.radius = 0.3f; cc.center = new Vector3(0f, 1f, 0f);
        var pc3d = player.AddComponent<PlayerController3D>();

        // Footsteps: a sliced step clip per footfall (dirt on the grass, gravel on the cobble road).
        var footSrc = player.AddComponent<AudioSource>();
        footSrc.playOnAwake = false;
        footSrc.spatialBlend = 0f;
        var footsteps = player.AddComponent<FootstepAudio>();
        footsteps.player = pc3d;
        footsteps.controller = cc;
        footsteps.defaultSteps = LoadFootstepClips("grass_");   // grassy yard = default surface
        footsteps.surfaces = new[]
        {
            new FootstepAudio.Surface { colliderName = "Pathway", clips = LoadFootstepClips("gravel_") },
        };

        var spriteGo = new GameObject("Sprite");
        spriteGo.transform.SetParent(player.transform, false);
        spriteGo.transform.localPosition = new Vector3(0f, 0.14f, 0f);   // lift the feet above the ground-detail planes (grass 0.02 / path 0.04 / leaves 0.06) so the upright billboard isn't sheared off at the ankles by them
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
        camGo.AddComponent<AudioListener>();   // scene is built from EmptyScene; without this nothing is audible
        rig.cam = cam;
        rig.playerSprite = sr;
        rig.grassFillEnabled = grassFill;   // interiors pass false — no open sky to green when looking up
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

        // Sporadic grass/dirt tiles laid just above the base ground. GroundTiler builds a
        // patchwork mesh (weighted-random tile per cell) at runtime, so tuning the mix only
        // needs a re-Play; Build() here also bakes it so it shows in the Scene view.
        var grassTiles = new GameObject("GrassTiles", typeof(MeshFilter), typeof(MeshRenderer));
        grassTiles.transform.position = new Vector3(0f, 0.02f, 0f);
        var tiler = grassTiles.AddComponent<GroundTiler>();
        tiler.material = GrassAtlasMaterial();
        tiler.worldSize = 80f;
        tiler.tileWorldSize = 2f;
        tiler.Build();

        var spriteMat = SpriteMaterial();

        // The house: a real 3D log cabin built from house_tiles.png — tiled-siding walls, interlocking
        // corner logs and a closed gable roof (CabinShellBuilder), all anchored on the ground.
        var housePortal = BuildCabin(new Vector3(0f, 0f, 10f));

        var player = BuildPlayerRig(new Vector3(0f, 0.1f, 0f), spriteMat, grassFill: false);   // spawns facing the cabin; no green fill — looking up reveals the sky
        housePortal.player = player.transform;

        // Let the player crane right up at the sky (the old grass "curtain" is gone now that there's a real sky).
        var exteriorRig = player.GetComponentInChildren<CameraRig>();
        if (exteriorRig != null) exteriorRig.minPitch = -88f;   // ~straight up (negative pitch = looking up)

        new GameObject("DialogUI").AddComponent<DialogUI>();

        // Cobble road first, so the forest can open up a corridor for it (trees skip the road cells).
        BuildPathway();                  // double-wide route to the door, branching out to neighbouring plots

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
            if (!NearPath(grassPos[i], 1.2f))
                MakeProp("Grass", grassPos[i], grass[(i * 3) % grass.Length], spriteMat);

        ScatterAutumnProps(spriteMat);   // autumn dressing: bare/hollow trees, bench, mushrooms, crow, leaves...
        BuildMountainBackdrop(sun);      // far mountain range surrounding the yard + the day→night sky system

        // Daytime birdsong: a looping ambient bed that fades out at night, driven by the sky's Darkness.
        var birds = AssetDatabase.LoadAssetAtPath<AudioClip>(BirdsWav);
        if (birds != null)
        {
            var ambGo = new GameObject("Ambient_Birds");
            ambGo.AddComponent<AudioSource>();
            var amb = ambGo.AddComponent<AmbientAudio>();
            amb.clip = birds;
            amb.sky = GameObject.Find("Sky")?.GetComponent<SkyController>();
            amb.dayVolume = 0.5f;
            amb.nightVolume = 0f;
        }

        EditorSceneManager.SaveScene(scene, ExteriorSceneOut);
        AddSceneToBuild(ExteriorSceneOut);
        Debug.Log("[HorrorGame] Exterior built at " + ExteriorSceneOut + " with the log cabin (tiled walls + " +
                  "corner logs + gable roof), " + treeCount + " animated trees (green + winter), " +
                  grassPos.Length + " grass tufts + autumn props. Walk up to the cabin; from the front press E to enter.");
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

    // props_autumn.png atlas (128x96, top-left origin). [x, topY, w, h] of frame 0; extra frames
    // run horizontally. See Assets/Animation/README.md.
    struct PropCell
    {
        public string name; public int x, y, w, h, frames;
        public PropCell(string n, int x, int y, int w, int h, int f) { name = n; this.x = x; this.y = y; this.w = w; this.h = h; frames = f; }
    }
    const int PropsAtlasH = 96;
    static readonly PropCell[] PropAtlas =
    {
        new PropCell("bareTree",     0,  0, 32, 48, 3),   // animated sway
        new PropCell("hollowTree",  96,  0, 32, 48, 1),
        new PropCell("fallenLog",    0, 48, 32, 16, 1),
        new PropCell("woodpile",    32, 48, 32, 16, 1),
        new PropCell("bench",       64, 48, 32, 16, 1),
        new PropCell("fence",       96, 48, 16, 16, 1),
        new PropCell("gate",       112, 48, 16, 16, 1),
        new PropCell("mushHomey",    0, 64, 16, 16, 1),
        new PropCell("mushSickly",  16, 64, 16, 16, 1),
        new PropCell("planks",      32, 64, 16, 16, 1),
        new PropCell("acorns",      48, 64, 16, 16, 1),
        new PropCell("crow",        64, 64, 16, 16, 2),   // animated blink
        new PropCell("rock",        96, 64, 16, 16, 1),
        new PropCell("leaves",       0, 80, 16, 16, 4),   // animated skitter
    };

    // Slices props_autumn.png into named per-frame sprites (bareTree_0.., crow_0.., etc.) from the
    // mixed-size PropAtlas rects; top-left atlas coords are flipped to Unity's bottom-left origin.
    static void SlicePropsAtlas(string path, float ppu, float pivotY)
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
        foreach (var p in PropAtlas)
            for (int f = 0; f < p.frames; f++)
                rects.Add(new SpriteRect
                {
                    name = p.name + "_" + f,
                    spriteID = StableGuid(path + "#" + p.name + f),
                    rect = new Rect(p.x + f * p.w, PropsAtlasH - (p.y + p.h), p.w, p.h),
                    pivot = new Vector2(0.5f, pivotY),
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

    // Indoors the floor is wood: silence the per-step (grass) FootstepAudio and add a looping wood-floor
    // footfall bed (a child so it owns its own AudioSource) that rises while the player walks.
    static void AttachWoodFloorFootsteps(GameObject player)
    {
        var fs = player.GetComponent<FootstepAudio>();
        if (fs != null) fs.enabled = false;

        var go = new GameObject("WalkLoop");
        go.transform.SetParent(player.transform, false);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        var walk = go.AddComponent<WalkLoopAudio>();
        walk.player = player.GetComponent<PlayerController3D>();
        walk.controller = player.GetComponent<CharacterController>();
        walk.loopClip = AssetDatabase.LoadAssetAtPath<AudioClip>(WoodStepsWav);
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

    // Scatter autumn props (props_autumn.png) around the clearing: animated bare trees + a static
    // hollow tree with a perched blinking crow, bench/woodpile/log, mushrooms, acorns, rocks and
    // planks, plus leaves skittering flat on the grass. 16 px = 1 world unit, bottom-pivoted.
    static void ScatterAutumnProps(Material mat)
    {
        SlicePropsAtlas(PropsAutumn, 16f, 0f);
        var sprites = LoadSheetSprites(PropsAutumn, "");
        Sprite[] Fr(string n) => sprites.Where(s => s.name.StartsWith(n + "_")).OrderBy(s => s.name).ToArray();
        Sprite One(string n) { var a = Fr(n); return a.Length > 0 ? a[0] : null; }

        _placed.Clear();   // fresh scatter each rebuild (positions are still deterministic per seed)

        // Built, man-made props (bench + fence + gate) read as staged, not wild, so they get a
        // deliberate "garden edge" well south of the cabin rather than being flung around the yard.
        // Reserved first so the scattered natural props keep clear of it.
        BuildGardenEdge(One("fence"), One("gate"), One("bench"), mat);

        // Big animated bare trees next so their large footprint is reserved before the small
        // props fill the gaps. 240 ms sway loop, out-of-phase per instance (README props table).
        var bare = Fr("bareTree");
        for (int i = 0; i < 4; i++)
            MakeAnimProp("BareTree", ScatterPoint(100 + i, 5.5f), bare, 1000f / 240f, mat);

        // Hollow trees, with the blinking crow perched on the first one (README: perch on a
        // branch/rail, never floating) rather than sitting in empty air.
        var crow = Fr("crow");
        Vector3 hollow = ScatterPoint(200, 5.5f);
        MakeProp("HollowTree", hollow, One("hollowTree"), mat);
        MakeProp("HollowTree", ScatterPoint(201, 5.5f), One("hollowTree"), mat);
        MakeCrow(hollow + new Vector3(0f, 2.6f, 0f), crow.Length > 0 ? crow[0] : null, crow.Length > 1 ? crow[1] : null, mat);

        // Rustic yard clutter scattered across the clearing (logs + woodpiles stay wild-looking).
        MakeProp("Woodpile",  ScatterPoint(310, 3.5f), One("woodpile"), mat);
        MakeProp("Woodpile",  ScatterPoint(311, 3.5f), One("woodpile"), mat);
        MakeProp("FallenLog", ScatterPoint(320, 3.5f), One("fallenLog"), mat);
        MakeProp("FallenLog", ScatterPoint(321, 3.5f), One("fallenLog"), mat);

        // Small dressing dotted sporadically over the yard.
        MakeProp("Mushrooms", ScatterPoint(500, 2.5f), One("mushSickly"), mat);
        MakeProp("Mushrooms", ScatterPoint(501, 2.5f), One("mushSickly"), mat);
        MakeProp("Mushrooms", ScatterPoint(502, 2.5f), One("mushHomey"), mat);
        MakeProp("Mushrooms", ScatterPoint(503, 2.5f), One("mushHomey"), mat);
        MakeProp("Acorns",    ScatterPoint(510, 2.5f), One("acorns"), mat);
        MakeProp("Acorns",    ScatterPoint(511, 2.5f), One("acorns"), mat);
        MakeProp("Acorns",    ScatterPoint(512, 2.5f), One("acorns"), mat);
        MakeProp("Rock",      ScatterPoint(520, 2.5f), One("rock"), mat);
        MakeProp("Rock",      ScatterPoint(521, 2.5f), One("rock"), mat);
        MakeProp("Rock",      ScatterPoint(522, 2.5f), One("rock"), mat);
        MakeProp("Rock",      ScatterPoint(523, 2.5f), One("rock"), mat);
        MakeProp("Planks",    ScatterPoint(530, 2.5f), One("planks"), mat);
        MakeProp("Planks",    ScatterPoint(531, 2.5f), One("planks"), mat);

        // Leaves skitter flat on the grass (4-frame, 190 ms loop — README), also spread out.
        var leaves = Fr("leaves");
        for (int i = 0; i < 4; i++)
            MakeGroundAnim("Leaves", ScatterPoint(600 + i, 3f) + new Vector3(0f, 0.06f, 0f), leaves, 1000f / 190f, mat);
    }

    // -------------------------------------------------------------- cobblestone pathway
    // Footprint of the laid path (world XZ), so scattered props keep off the cobbles (see InKeepClear).
    static readonly List<Vector3> _pathCells = new List<Vector3>();

    // Lays path_cobble.png (PathTiler) as a flat DOUBLE-WIDE cobble road over the grass:
    //   * a 2-wide trunk from the yard (spawn, z=0) up to the cabin door (thresholds meet the
    //     house at z=6.5); the door is at x=0, so the road is shifted -0.5 to straddle it.
    //   * a 2-wide east/west avenue at z=3-4 that runs far out THROUGH the tree rings, then
    //   * narrows to a single-wide spur that climbs to a neighbouring house nestled in the trees.
    // Puddle/water tiles sit out on the far spurs (well away from the main house). Authored as
    // 1-unit grid cells (16 px world grid); PathTiler auto-selects each routing tile.
    static void BuildPathway()
    {
        _pathCells.Clear();
        var cells = new HashSet<Vector2Int>();
        var houses = new List<Vector2Int>();    // cells that meet a building -> threshold tile
        var puddles = new List<Vector2Int>();   // single-wide vertical straights drawn as animated puddles

        void Strip(int x0, int z0, int x1, int z1)
        {
            for (int x = Mathf.Min(x0, x1); x <= Mathf.Max(x0, x1); x++)
            for (int z = Mathf.Min(z0, z1); z <= Mathf.Max(z0, z1); z++)
                cells.Add(new Vector2Int(x, z));
        }

        Strip(0, 0, 1, 6);                       // 2-wide trunk: spawn -> door
        houses.Add(new Vector2Int(0, 6));        // doorstep (two thresholds side by side)
        houses.Add(new Vector2Int(1, 6));

        Strip(2, 3, 23, 4);                      // east avenue (out through the trees)
        Strip(-23, 3, -1, 4);                    // west avenue

        // Single-wide spurs climbing north to houses tucked in the winter-tree ring (~radius 24).
        for (int z = 5; z <= 9; z++) { cells.Add(new Vector2Int(23, z)); cells.Add(new Vector2Int(-23, z)); }
        houses.Add(new Vector2Int(23, 9));       // east neighbour's doorstep
        houses.Add(new Vector2Int(-23, 9));      // west neighbour's doorstep
        puddles.Add(new Vector2Int(23, 7));      // water far out on the east spur
        puddles.Add(new Vector2Int(-23, 7));     // water far out on the west spur

        const float shiftX = -0.5f;              // centre the 2-wide trunk on the door (x = 0)
        var go = new GameObject("Pathway", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.position = new Vector3(shiftX, 0f, 0f);
        var pt = go.AddComponent<PathTiler>();
        pt.material = PathAtlasMaterial();
        pt.tileWorldSize = 1f;
        pt.pathY = 0.04f;
        pt.cells = new List<Vector2Int>(cells);
        pt.houseCells = houses;
        pt.puddleCells = puddles;
        pt.Build();

        // Give the road a collider so footsteps can tell they're on gravel (raycast hits "Pathway").
        // Flat and only ~4 cm proud of the grass — well under the controller's step offset, so it
        // doesn't jolt movement.
        var pathCol = go.AddComponent<MeshCollider>();
        pathCol.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;

        foreach (var c in cells) _pathCells.Add(new Vector3(c.x + shiftX, 0f, c.y));   // world footprint
    }

    // Alpha-clipped (cutout) unlit material for the cobble atlas: the tiles have real alpha (they
    // overlay the grass), cut out at a 0.5 threshold so the road silhouette still shows grass around
    // it while WRITING DEPTH — so upright billboards (the player) sort correctly over the road instead
    // of being painted through by it. Import is forced to crisp pixel settings.
    static Material PathAtlasMaterial()
    {
        if (AssetImporter.GetAtPath(PathCobble) is TextureImporter imp)
        {
            bool dirty = imp.textureType != TextureImporterType.Default || imp.filterMode != FilterMode.Point ||
                         imp.textureCompression != TextureImporterCompression.Uncompressed ||
                         imp.mipmapEnabled || imp.wrapMode != TextureWrapMode.Clamp || !imp.alphaIsTransparency;
            if (dirty)
            {
                imp.textureType = TextureImporterType.Default;
                imp.filterMode = FilterMode.Point;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.mipmapEnabled = false;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.alphaSource = TextureImporterAlphaSource.FromInput;
                imp.alphaIsTransparency = true;
                imp.SaveAndReimport();
            }
        }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(PathCobble);
        EnsureFolder(MatDir);
        string matPath = MatDir + "/PathCobble3D.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (mat == null) { mat = new Material(sh); AssetDatabase.CreateAsset(mat, matPath); }
        else mat.shader = sh;

        // Alpha-CLIP (cutout) rather than alpha-blend, so the road WRITES DEPTH. As a transparent
        // ZWrite-off mesh it shared the Transparent queue with the player billboard (also ZWrite-off),
        // and with neither writing depth the two only sorted by draw order — so the road painted over
        // the player's legs from certain positions/facings (the "sunk into the cobbles" clip). Writing
        // depth lets the character sort correctly per-pixel against the ground. The cobble tiles are
        // hard-edged pixel art, so a 0.5 cutout keeps the grass showing through the road silhouette.
        mat.SetFloat("_Surface", 0f);        // opaque surface...
        mat.SetFloat("_AlphaClip", 1f);      // ...with alpha clipping (cutout road silhouette over grass)
        mat.SetFloat("_Cutoff", 0.5f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
        mat.SetFloat("_ZWrite", 1f);
        mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;   // 2450, writes depth
        mat.mainTexture = tex;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // -------------------------------------------------------------- mountain backdrop
    // Builds the surrounding "far range": range_backdrop.png is a pre-sliced atlas (range_backdrop_0..5
    // are the ridge strips far->near, _6 the hero peak). MountainBackdrop wraps each strip into a ring
    // around the yard so it reads in every direction. Radii/heights grow with distance; the far snow
    // peaks loom highest and farthest. Face/wanderer/fog from the README are deferred.
    static void BuildMountainBackdrop(Light sun)
    {
        EnsureRangeImport();
        var byName = new Dictionary<string, Sprite>();
        foreach (var s in AssetDatabase.LoadAllAssetsAtPath(RangeBackdrop).OfType<Sprite>()) byName[s.name] = s;
        Sprite S(int i) => byName.TryGetValue("range_backdrop_" + i, out var sp) ? sp : null;

        var root = new GameObject("MountainBackdrop");
        var mb = root.AddComponent<MountainBackdrop>();
        mb.material = BackdropMaterial();
        mb.center = Vector3.zero;
        // radius grows with distance; height scaled by the strip's pixel height; `copies` chosen so each
        // strip copy keeps the source aspect (≈ circumference / (height·pixelAspect)) — too many copies
        // squish the silhouette horizontally (that's what crushed the spruce line before).
        mb.layers = new[]
        {
            new MountainBackdrop.Layer { name = "snowFar",   sprite = S(0), radius = 108f, height = 42f, copies = 4, segmentsPerCopy = 8 },
            new MountainBackdrop.Layer { name = "snowRock",  sprite = S(1), radius = 96f,  height = 36f, copies = 3, segmentsPerCopy = 8 },
            new MountainBackdrop.Layer { name = "purple",    sprite = S(2), radius = 84f,  height = 29f, copies = 3, segmentsPerCopy = 8 },
            new MountainBackdrop.Layer { name = "dirtRidge", sprite = S(3), radius = 74f,  height = 22f, copies = 3, segmentsPerCopy = 8 },
            new MountainBackdrop.Layer { name = "nearDirt",  sprite = S(4), radius = 66f,  height = 12f, copies = 2, segmentsPerCopy = 10 },
            new MountainBackdrop.Layer { name = "trees",     sprite = S(5), radius = 60f,  height = 15f, copies = 2, segmentsPerCopy = 10 },
        };
        mb.heroSprite = S(6);
        mb.heroAzimuthDeg = 90f;    // +Z, north — behind the cabin
        mb.heroRadius = 102f;       // between snowRock and snowFar
        mb.heroWidth = 53f;         // ~1.16 aspect of the 122x105 hero sprite
        mb.heroHeight = 46f;
        mb.buildSky = false;   // SkyController owns the sky now (animated gradient + sun/moon/stars)
        mb.Build();

        BuildSkySystem(sun);
    }

    // The day→night sky (SKY_README): a gradient dome behind the mountains, a sun that arcs and sets,
    // a moon that rises, and a twinkling star field — all driven by SkyController.timeOfDay. Also lets
    // the sky dim the scene's directional light + ambient so the world darkens with it.
    static void BuildSkySystem(Light sun)
    {
        var sunSprite = EnsureSkySprite(SunPng);
        var moonSprite = EnsureSkySprite(MoonPng);

        var skyGo = new GameObject("Sky");
        var sky = skyGo.AddComponent<SkyController>();
        sky.sunSprite = sunSprite;
        sky.moonSprite = moonSprite;
        sky.sunLight = sun;
        sky.glow = false;             // no halo / second sun+moon sprite
        // Sky faces +Z (north): with that basis the tangent runs +X(east)→-X(west), so the sun rises in
        // the EAST and sets in the WEST, and the moon rises in the WEST (nx = 0.70).
        sky.skyYawDeg = 90f;
        sky.dayLengthSeconds = 120f;  // a full dawn→dark cycle every 2 minutes
        sky.loop = true;
        sky.timeOfDay = 0f;           // start at dawn so a fresh scene opens on the sunrise
        sky.Build();
    }

    // Import sun.png / moon.png per SKY_README: single sprite, PPU 24, point filter, no compression.
    static Sprite EnsureSkySprite(string path)
    {
        if (AssetImporter.GetAtPath(path) is TextureImporter imp)
        {
            bool dirty = imp.textureType != TextureImporterType.Sprite ||
                         imp.spriteImportMode != SpriteImportMode.Single ||
                         imp.filterMode != FilterMode.Point ||
                         imp.spritePixelsPerUnit != 24f ||
                         imp.textureCompression != TextureImporterCompression.Uncompressed ||
                         imp.mipmapEnabled;
            if (dirty)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.filterMode = FilterMode.Point;
                imp.spritePixelsPerUnit = 24f;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.mipmapEnabled = false;
                imp.SaveAndReimport();
            }
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // Loads the sliced footstep clips (dirt_* / gravel_*) baked under FootstepDir, in name order.
    static AudioClip[] LoadFootstepClips(string prefix)
    {
        if (!AssetDatabase.IsValidFolder(FootstepDir)) return new AudioClip[0];
        return AssetDatabase.FindAssets("t:AudioClip", new[] { FootstepDir })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => System.IO.Path.GetFileName(p).StartsWith(prefix))
            .OrderBy(p => p)
            .Select(AssetDatabase.LoadAssetAtPath<AudioClip>)
            .Where(c => c != null)
            .ToArray();
    }

    // Alpha-clipped unlit material for the range atlas: silhouettes are cut out (not blended) so the
    // rings depth-sort correctly by radius, and it's double-sided since we view the rings from inside.
    static Material BackdropMaterial()
    {
        EnsureRangeImport();
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(RangeBackdrop);
        EnsureFolder(MatDir);
        string matPath = MatDir + "/RangeBackdrop3D.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (mat == null) { mat = new Material(sh); AssetDatabase.CreateAsset(mat, matPath); }
        else mat.shader = sh;

        mat.SetFloat("_Surface", 0f);        // opaque surface...
        mat.SetFloat("_AlphaClip", 1f);      // ...with alpha clipping (cutout silhouettes)
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.SetFloat("_Cutoff", 0.5f);
        mat.SetFloat("_Cull", 0f);           // double-sided
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        mat.mainTexture = tex;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // Crisp pixel import for the range atlas (keeps the existing multi-sprite slicing intact).
    static void EnsureRangeImport()
    {
        if (AssetImporter.GetAtPath(RangeBackdrop) is TextureImporter imp)
        {
            bool dirty = imp.filterMode != FilterMode.Point ||
                         imp.textureCompression != TextureImporterCompression.Uncompressed ||
                         imp.mipmapEnabled || !imp.alphaIsTransparency;
            if (dirty)
            {
                imp.filterMode = FilterMode.Point;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.mipmapEnabled = false;
                imp.alphaIsTransparency = true;
                imp.SaveAndReimport();
            }
        }
    }

    // -------------------------------------------------------------- prop scatter
    // The walkable exterior is the clearing inside the innermost tree ring (PlaceTreeRing at
    // r=15 around forest centre 0,0,6). Props scatter across this whole disc rather than
    // clustering by the cabin door, so the yard reads as sporadically dressed.
    static readonly Vector3 ClearingCentre = new Vector3(0f, 0f, 6f);
    const float ClearingRadius = 12f;
    static readonly List<Vector3> _placed = new List<Vector3>();

    // True inside a zone that must stay clear: the cabin footprint (centre 0,0,10, 6x7, with a
    // margin), the spawn->front-door approach corridor, and breathing room around the spawn.
    static bool InKeepClear(Vector3 p)
    {
        if (p.x > -4f && p.x < 4f && p.z > 5.5f && p.z < 14f) return true;     // cabin
        if (p.x > -2.5f && p.x < 2.5f && p.z > -1.5f && p.z < 6.5f) return true; // door approach
        if (p.x * p.x + p.z * p.z < 4f) return true;                           // player spawn
        if (NearPath(p, 1.4f)) return true;                                    // keep props off the cobbles
        return false;
    }

    // Deterministic natural scatter: hashed polar samples over the clearing disc (sqrt radius =
    // uniform area), rejecting keep-clear zones and anything closer than `spacing` to an already
    // placed prop. Falls back to the most-isolated valid candidate if none clears the spacing.
    static Vector3 ScatterPoint(int seed, float spacing)
    {
        Vector3 best = ClearingCentre; float bestGap = -1f;
        for (int t = 0; t < 24; t++)
        {
            float a = Hash01(seed * 73 + t * 19 + 7) * Mathf.PI * 2f;
            float r = Mathf.Sqrt(Hash01(seed * 131 + t * 29 + 3)) * ClearingRadius;
            var p = ClearingCentre + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            if (InKeepClear(p)) continue;
            float gap = float.MaxValue;
            foreach (var q in _placed) gap = Mathf.Min(gap, (q - p).sqrMagnitude);
            if (gap > spacing * spacing) { _placed.Add(p); return p; }
            if (gap > bestGap) { bestGap = gap; best = p; }
        }
        _placed.Add(best);
        return best;
    }

    // A deliberate "garden edge" set far south of the cabin (house is at z=10): a fence line
    // capped by a gate, with the bench sitting just inside it facing the yard. Grouping the built
    // props here keeps them from looking out of place scattered across the wild clearing.
    static void BuildGardenEdge(Sprite fence, Sprite gate, Sprite bench, Material mat)
    {
        Vector3 anchor = new Vector3(-6.5f, 0f, -2.5f);   // south-west corner of the clearing
        MakeFenceRun(anchor, 6, fence, gate, mat);        // fence run east, gate caps the far end
        if (bench != null)
        {
            var b = anchor + new Vector3(2.5f, 0f, 1.4f); // just inside the fence line
            MakeProp("Bench", b, bench, mat);
            _placed.Add(b);
        }
    }

    // A short fence run: fence tiles repeat horizontally (16 px = 1 unit), capped by a gate.
    static void MakeFenceRun(Vector3 start, int tiles, Sprite fence, Sprite gate, Material mat)
    {
        if (fence == null) return;
        for (int i = 0; i < tiles; i++)
        {
            var p = start + new Vector3(i, 0f, 0f);
            MakeProp("Fence", p, fence, mat);
            _placed.Add(p);
        }
        if (gate != null)
        {
            var g = start + new Vector3(tiles, 0f, 0f);
            MakeProp("Gate", g, gate, mat);
            _placed.Add(g);
        }
    }

    // Billboard sprite that loops a set of frames (bare-tree sway, etc.).
    static void MakeAnimProp(string name, Vector3 pos, Sprite[] frames, float fps, Material mat)
    {
        if (frames == null || frames.Length == 0) return;
        var go = new GameObject(name);
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = frames[0];
        sr.sharedMaterial = mat;
        go.AddComponent<Billboard>();
        var anim = go.AddComponent<LoopSpriteAnimator>();
        anim.frames = frames;
        anim.fps = fps;
        anim.randomStartPhase = true;
    }

    // Billboard crow that holds still and blinks on a slow cycle (CrowBlink).
    static void MakeCrow(Vector3 pos, Sprite open, Sprite blink, Material mat)
    {
        if (open == null) return;
        var go = new GameObject("Crow");
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = open;
        sr.sharedMaterial = mat;
        go.AddComponent<Billboard>();
        var cb = go.AddComponent<CrowBlink>();
        cb.openFrame = open;
        cb.blinkFrame = blink;
    }

    // Animated sprite laid FLAT on the ground (leaves skittering) instead of billboarded upright.
    static void MakeGroundAnim(string name, Vector3 pos, Sprite[] frames, float fps, Material mat)
    {
        if (frames == null || frames.Length == 0) return;
        var go = new GameObject(name);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // lie flat on the grass
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = frames[0];
        sr.sharedMaterial = mat;
        var anim = go.AddComponent<LoopSpriteAnimator>();
        anim.frames = frames;
        anim.fps = fps;
        anim.randomStartPhase = true;
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

        // lit windows: a shadow figure crosses the pane on one side of the door, candles gutter
        // in the rest. Front windows flank the door; one sits on each side wall so the cabin looks
        // occupied from any approach.
        var figureFrames = new[] {
            new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(2, 2),
            new Vector2Int(3, 2), new Vector2Int(4, 2), new Vector2Int(5, 2),
        };
        var candleFrames = new[] {
            new Vector2Int(0, 3), new Vector2Int(1, 3), new Vector2Int(2, 3), new Vector2Int(3, 3),
        };
        const float winSize = 1.1f, winY = 2.15f;
        float front = -hd - 0.03f, sideX = W * 0.5f + 0.03f;
        MakeWindow(house.transform, new Vector3(-1.9f, winY, front), Quaternion.identity,
                   winSize, figureFrames, 4.5f, flicker: false, phase: 0f, mat: atlasMat);   // passing shadow
        MakeWindow(house.transform, new Vector3(1.9f, winY, front), Quaternion.identity,
                   winSize, candleFrames, 7f, flicker: true, phase: 0.3f, mat: atlasMat);     // flickering candle
        MakeWindow(house.transform, new Vector3(-sideX, winY, 1.4f), Quaternion.Euler(0f, 90f, 0f),
                   winSize, candleFrames, 7f, flicker: true, phase: 0.6f, mat: atlasMat);
        MakeWindow(house.transform, new Vector3(sideX, winY, -1.4f), Quaternion.Euler(0f, -90f, 0f),
                   winSize, candleFrames, 7f, flicker: true, phase: 0.9f, mat: atlasMat);

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

        // brick chimney poking through the back slope of the roof, smoke curling from the top.
        // It billboards like the trees so it always reads; the smoke rises in world space.
        var chimney = MakeTileQuad("Chimney", house.transform, new Vector3(1.1f, 5.05f, 1.4f),
                                   Quaternion.identity, 1.0f, 1.4f, 5, 1, atlasMat);
        chimney.AddComponent<Billboard>();
        var smokeGo = new GameObject("ChimneySmoke");
        smokeGo.transform.SetParent(chimney.transform, false);
        smokeGo.transform.localPosition = new Vector3(0f, 0.72f, 0f);   // the chimney mouth
        var smoke = smokeGo.AddComponent<ChimneySmoke>();
        smoke.puffSprite = SmokePuffSprite();
        smoke.material = SpriteMaterial();

        // solid footprint so the player circles it and enters from the front
        var col = house.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, Hh * 0.5f, 0f);
        col.size = new Vector3(W, Hh, D);

        var hp = door.AddComponent<HousePortal>();
        hp.interiorScene = "Sandbox3D";
        hp.openSound = AssetDatabase.LoadAssetAtPath<AudioClip>(DoorSfx);
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

    // URP/Unlit material sampling grass_tiles.png (point-filtered, opaque) for the tiled ground.
    static Material GrassAtlasMaterial()
    {
        const string atlasPath = "Assets/Animation/grass_tiles.png";
        if (AssetImporter.GetAtPath(atlasPath) is TextureImporter imp)
        {
            bool dirty = imp.textureType != TextureImporterType.Default || imp.filterMode != FilterMode.Point ||
                         imp.textureCompression != TextureImporterCompression.Uncompressed ||
                         imp.mipmapEnabled || imp.wrapMode != TextureWrapMode.Clamp;
            if (dirty)
            {
                imp.textureType = TextureImporterType.Default;
                imp.filterMode = FilterMode.Point;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.mipmapEnabled = false;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.SaveAndReimport();
            }
        }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
        EnsureFolder(MatDir);
        string matPath = MatDir + "/GrassTiles3D.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (mat == null) { mat = new Material(sh); AssetDatabase.CreateAsset(mat, matPath); }
        else mat.shader = sh;
        mat.mainTexture = tex;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
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

    // A lit window quad on a wall face that cycles atlas cells via TileSheetAnimator
    // (a crossing shadow, or a flickering candle).
    static void MakeWindow(Transform parent, Vector3 localPos, Quaternion localRot, float size,
                           Vector2Int[] frames, float fps, bool flicker, float phase, Material mat)
    {
        var win = MakeTileQuad("Window", parent, localPos, localRot, size, size, frames[0].x, frames[0].y, mat);
        var anim = win.AddComponent<TileSheetAnimator>();
        anim.frames = frames;
        anim.fps = fps;
        anim.flicker = flicker;
        anim.startPhase = phase;
    }

    // Soft round smoke puff, generated once into Assets/Animation and imported as a Sprite. The
    // sheet's own puff tiles read as snow and sit off-centre in their cells, so a clean radial
    // puff gives the chimney a believable rising column that can fade per-instance.
    static Sprite SmokePuffSprite()
    {
        if (!File.Exists(SmokePuffPng))
        {
            const int S = 64;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            var px = new Color32[S * S];
            float cx = (S - 1) * 0.5f, cy = (S - 1) * 0.5f, radius = 0.48f * S;
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / radius;
                float a = Mathf.Pow(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - d)), 1.3f);   // soft cloudy edge
                px[y * S + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(SmokePuffPng, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(SmokePuffPng);
        }
        if (AssetImporter.GetAtPath(SmokePuffPng) is TextureImporter imp)
        {
            bool dirty = imp.textureType != TextureImporterType.Sprite ||
                         imp.spriteImportMode != SpriteImportMode.Single ||
                         imp.filterMode != FilterMode.Bilinear ||
                         imp.textureCompression != TextureImporterCompression.Uncompressed ||
                         imp.mipmapEnabled || !imp.alphaIsTransparency;
            if (dirty)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.filterMode = FilterMode.Bilinear;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.mipmapEnabled = false;
                imp.alphaIsTransparency = true;
                imp.SaveAndReimport();
            }
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(SmokePuffPng);
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

        // Thin trunk collider: gives the third-person camera something to pull in front of (so the
        // arm no longer clips through the forest) and stops the player walking through trunks. Kept
        // slim so it doesn't snag movement; ring trees are already skipped near the road.
        var col = go.AddComponent<CapsuleCollider>();
        col.radius = 0.35f;
        col.height = 5f;
        col.center = new Vector3(0f, 2.5f, 0f);
    }

    // Rings of trees around a centre with deterministic jitter (so rebuilds don't churn the scene).
    // Trees that land on the cobble road are skipped so the path stays walkable through the woods.
    static int PlaceTreeRing(Vector3 center, float radius, int count, Sprite[] frames, Material mat, int seed)
    {
        int placed = 0;
        for (int i = 0; i < count; i++)
        {
            float a = (i + 0.5f) / count * Mathf.PI * 2f + (Hash01(seed * 131 + i) - 0.5f) * 0.4f;
            float r = radius + (Hash01(seed * 197 + i) - 0.5f) * 4f;
            var pos = center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            if (NearPath(pos, 1.6f)) continue;
            MakeTree(pos, frames, mat);
            placed++;
        }
        return placed;
    }

    // True if a world point sits within `radius` of any cobble-road cell (see _pathCells).
    static bool NearPath(Vector3 p, float radius)
    {
        float r2 = radius * radius;
        foreach (var pc in _pathCells)
            if ((p.x - pc.x) * (p.x - pc.x) + (p.z - pc.z) * (p.z - pc.z) < r2) return true;
        return false;
    }

    static float Hash01(int n)
    {
        float s = Mathf.Sin(n * 12.9898f) * 43758.5453f;
        return s - Mathf.Floor(s);
    }

    // Slice all three dog sheets (identical 6x4 grids of 32px cells) and bundle each breed's
    // idle/walk/heart frames; DogCompanion picks one at runtime from CharacterStore.LoadDog().
    static DogCompanion.BreedFrames[] BuildDogBreeds()
    {
        var breeds = new DogCompanion.BreedFrames[DogSheets.Length];
        for (int i = 0; i < DogSheets.Length; i++)
        {
            string sheet = DogSheets[i];
            SliceGrid(sheet, 32f, 0.06f, 32, 32, 6, new[] { "dog_idle_", "dog_walk_", "dog_run_", "dog_heart_" });
            breeds[i] = new DogCompanion.BreedFrames
            {
                name  = CharacterStore.DogNames[i],
                idle  = LoadSheetSprites(sheet, "dog_idle_"),
                walk  = LoadSheetSprites(sheet, "dog_walk_"),
                heart = LoadSheetSprites(sheet, "dog_heart_"),
            };
        }
        return breeds;
    }

    static void MakeDog(Vector3 pos, Transform player, NightmareController nightmare,
                        DogCompanion.BreedFrames[] breeds, Material mat)
    {
        var first = (breeds != null && breeds.Length > 0) ? breeds[0] : null;   // apricot = editor-time default
        var go = new GameObject("Dog");
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = (first != null && first.idle != null && first.idle.Length > 0) ? first.idle[0] : null;
        sr.sharedMaterial = mat;
        go.AddComponent<Billboard>();
        var dog = go.AddComponent<DogCompanion>();
        dog.player = player;
        dog.nightmare = nightmare;
        dog.breeds = breeds;
        if (first != null) { dog.idleFrames = first.idle; dog.walkFrames = first.walk; dog.heartFrames = first.heart; }
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
