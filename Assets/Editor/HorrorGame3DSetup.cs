using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Game.Interior;

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
    const string RobertPropsDay   = "Assets/Animation/props_robert.png";
    const string RobertPropsNight = "Assets/Animation/props_robert_nightmare.png";
    const string BirdsPng    = "Assets/Animation/birds_flock.png";
    const string NoteSignPng = "Assets/Animation/note_sign.png";
    const string PathCobble  = "Assets/Animation/path_cobble.png";
    const string RangeBackdrop = "Assets/Animation/range_backdrop.png";
    const string SunPng     = "Assets/Animation/sun.png";
    const string MoonPng    = "Assets/Animation/moon.png";
    const string FootstepDir = "Assets/Sound Effects/Footsteps";
    const string BirdsWav   = "Assets/Sound Effects/Birds Singing.wav";
    const string WoodStepsWav = "Assets/Sound Effects/Footsteps on Wooden Floor.wav";
    const string AsphaltWav = "Assets/Sound Effects/Walking on Asphalt.wav";
    const string RoadSignPng = "Assets/Animation/Car Atlas/roadside_pack/road_sign.png";  // TOWN / MILL RD directional post
    const string RedwoodDir = "Assets/Big Tree Updates/redwood_kit/sprites";               // giant idle-sway redwoods
    const string DoorSfx    = "Assets/Sound Effects/door opening.mp3";
    const string PaperSfx   = "Assets/Sound Effects/Paper Crumple.wav";
    const string DogPantWav = "Assets/Sound Effects/Dog Panting.wav";
    const string InteriorFloorTex = "Assets/Art/Environment/interior_floor.png";
    const string InteriorWallTex  = "Assets/Art/Environment/interior_wall.png";
    // Updated 256x128 atlases with FRONT/BACK/SIDE facings (Assets/Animation/INTERIOR_UPDATE.md).
    const string FurnitureDusk      = "Assets/Animation/interior_furniture_dusk.png";
    const string FurnitureLavender  = "Assets/Animation/interior_furniture_lavender.png";
    const string FurnitureNightmare = "Assets/Animation/interior_furniture_nightmare.png";
    const string SceneOut   = "Assets/Scenes/Sandbox3D.unity";
    const string ExteriorSceneOut = "Assets/Scenes/Exterior.unity";
    const string OutOfTownSceneOut = "Assets/Scenes/OutOfTown.unity";
    const string CockpitDir = "Assets/Animation/Updated Car POV/cockpit_kit/sprites";
    const string RoadTiles  = "Assets/Animation/Car/roadside_pack/road_tiles.png";
    const int SetupVersion  = 33;  // bump to force the auto-run to rebuild the scenes
    const int DrivingSetupVersion = 8;  // bump to re-install the in-world driving setup (truck + road + OutOfTown)

    static int _renderer3DIndex = 1;

    [InitializeOnLoadMethod]
    static void AutoRun()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;

            // Version-gated full rebuild of the two hand-tended scenes (wipes + regenerates them).
            if (EditorPrefs.GetInt("HG3D_SetupVersion", 0) < SetupVersion)
            {
                EditorPrefs.SetInt("HG3D_SetupVersion", SetupVersion);
                try { BuildSandbox3D(); BuildExterior(); }
                catch (System.Exception e) { Debug.LogError("[HorrorGame] 3D auto-build failed: " + e); }
            }

            // In-world driving self-installs once: build the OutOfTown stub + augment the hand-placed
            // truck with the driving components and the road. Gated by its own pref (does NOT bump
            // SetupVersion, so it never wipes Exterior/Sandbox — the hand-placed truck stays safe).
            if (EditorPrefs.GetInt("HG3D_DrivingSetup", 0) < DrivingSetupVersion)
            {
                EditorPrefs.SetInt("HG3D_DrivingSetup", DrivingSetupVersion);
                try { BuildOutOfTown(); SetupExteriorDriving(); }
                catch (System.Exception e) { Debug.LogError("[HorrorGame] Driving setup failed: " + e); }
            }
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
        bed.transform.position = new Vector3(5f, 0f, 8.2f);    // across the room, head to the north wall
        var bedSr = bed.AddComponent<SpriteRenderer>();
        bedSr.sharedMaterial = spriteMat;
        // Directional four-view bed (BED.md): shows the front/back/left/right oblique view for the side the
        // camera is on, drawn on an AXIS-ALIGNED quad (never a free billboard). A billboard would rotate
        // the long bed so its far end sweeps THROUGH the wall behind it; the fixed per-view facing keeps it
        // parallel to the wall (the sprite depth-bias covers the rest). See Bed.cs.
        var bedComp = bed.AddComponent<Bed>();
        bedComp.homeForward = new Vector3(0f, 0f, -1f);        // foot faces the room (south); head at the wall
        const string BedKit = "Assets/Animation/Updated Bed/bed_kit/sprites/";
        Texture2D BedTex(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>(BedKit + n + ".png");
        bedComp.frontDay = BedTex("bed_front");             bedComp.backDay  = BedTex("bed_back");
        bedComp.leftDay  = BedTex("bed_left");              bedComp.rightDay = BedTex("bed_right");
        bedComp.frontNight = BedTex("bed_front_nightmare"); bedComp.backNight  = BedTex("bed_back_nightmare");
        bedComp.leftNight  = BedTex("bed_left_nightmare");  bedComp.rightNight = BedTex("bed_right_nightmare");
        var portal = bed.AddComponent<BedPortal>();
        portal.player = player.transform;
        portal.nightmare = nightmare;

        // ---- dog companion (breed randomised at character select; overworld only, hides in the nightmare) ----
        MakeDog(new Vector3(3f, 0f, 5f), player.transform, nightmare, dogBreeds, spriteMat);

        // ---- the cozy living room: the last comfort before the nightmare (interior_furniture_kit) ----
        BuildLivingRoom(spriteMat);

        // ---- exit door back out to the yard, on the south wall behind the spawn ----
        BuildInteriorExitDoor(player.transform);

        EditorSceneManager.SaveScene(scene, SceneOut);
        AddSceneToBuild(SceneOut);
        Debug.Log("[HorrorGame] 3D Sandbox built at " + SceneOut + " with the character (back " +
                  backSprites.Length + "/front " + frontSprites.Length + "), the bed (" + bedSprites.Length +
                  "), the dog (" + dogBreeds.Length + " breeds, randomised on character select" +
                  "), and the partner (boy idle " + boyIdle.Length + "/girl idle " + girlIdle.Length + "). " +
                  "Walk to the bed + press E to enter the nightmare (the dog hides). " +
                  "Play: WASD + mouse, V = first/third, hold C = look behind.");
    }

    // -------------------------------------------------------------- living room (interior furniture)
    // Dresses the interior with the interior_furniture_kit (256x128 FRONT/BACK/SIDE atlas, see
    // Assets/Animation/INTERIOR_UPDATE.md). Unlike the outdoor props these pieces do NOT billboard —
    // each carries a fixed facing chosen for the wall it sits against, so it reads as real furniture
    // in the room instead of spinning to face the player: TV + bookshelf FRONT along the far (north)
    // wall, the occupied sofa + coffee table on the rug, and the two side chairs turned in profile
    // toward the group. Each InteriorObject slices its own frames at runtime (TV shimmer, dog breathing)
    // and flickers to its nightmare skin on the room-wide DreadProgress. Kept clear of bed/partner/dog.
    static void BuildLivingRoom(Material spriteMat)
    {
        var day   = EnsureFurnitureAtlas(FurnitureDusk);        // warm rust furniture in a teal room
        var night = EnsureFurnitureAtlas(FurnitureNightmare);   // the dream-rot skin (flickers in on dread)
        if (day == null) { Debug.LogWarning("[HorrorGame] interior furniture atlas missing: " + FurnitureDusk); return; }

        var room = new GameObject("LivingRoom");
        // A point 4u "in front" of a piece = the direction its FRONT faces (homeForward). Solid pieces
        // read the camera's angle against this to pick front/back/side as the player walks around them.
        Vector3 S(Vector3 p) => new Vector3(p.x, 0f, p.z - 4f);   // faces south / the room & entrance

        // Rug first, flat on the boards, anchoring the grouping; everything else stands on top of it.
        MakeRug(room.transform, new Vector3(-3.2f, 0.03f, 5f), new Vector3(1.8f, 1.8f, 1f), day, night, spriteMat);

        // Far (north) wall: TV (on) + bookshelf, their FRONT toward the room. Solid pieces, so their
        // side/back show as you circle them.
        var tvPos = new Vector3(-6f, 0f, 9.4f);
        MakeFurniture(room.transform, "TV", InteriorObject.Piece.Tv,
                      tvPos, S(tvPos), day, night, spriteMat, startsOn: true);        // lit shimmer; snaps to static in the nightmare
        var bsPos = new Vector3(-8.6f, 0f, 9.2f);
        MakeFurniture(room.transform, "Bookshelf", InteriorObject.Piece.Bookshelf,
                      bsPos, S(bsPos), day, night, spriteMat);

        // The occupied sofa (dog asleep on it — FRONT only, so it just billboards) faces the room,
        // coffee table in front of it on the rug. Sofa sits east of the TV so it doesn't hide it.
        var couchPos = new Vector3(-3.2f, 0f, 6.4f);
        MakeFurniture(room.transform, "SofaWithDog", InteriorObject.Piece.CouchDog,
                      couchPos, S(couchPos), day, night, spriteMat);                  // the dog naps here; opens an eye in the nightmare
        var tablePos = new Vector3(-3.2f, 0f, 4.4f);
        MakeFurniture(room.transform, "CoffeeTable", InteriorObject.Piece.CoffeeTable,
                      tablePos, S(tablePos), day, night, spriteMat);

        // Two side chairs whose FRONT faces the coffee table, so from the entrance you see them in
        // profile and see their front as you step into the group.
        var armPos = new Vector3(-6.8f, 0f, 4.6f);
        MakeFurniture(room.transform, "Armchair", InteriorObject.Piece.Armchair,
                      armPos, new Vector3(-3.2f, 0f, 4.4f), day, night, spriteMat);
        var lovePos = new Vector3(0.3f, 0f, 5.2f);
        MakeFurniture(room.transform, "Loveseat", InteriorObject.Piece.Couch,
                      lovePos, new Vector3(-3.2f, 0f, 4.4f), day, night, spriteMat);

        // A warm floor lamp in the north-west corner (single sprite; billboards to the camera).
        var lampPos = new Vector3(-8.9f, 0f, 6.6f);
        MakeFurniture(room.transform, "FloorLamp", InteriorObject.Piece.FloorLamp,
                      lampPos, S(lampPos), day, night, spriteMat, startsOn: true);    // warm pool of light (sick green when wrong)
    }

    // One upright furniture piece. Every upright piece BILLBOARDS to the camera (2.5D, never edge-on);
    // solid pieces (sofa/loveseat/armchair/bookshelf/tv) are additionally DIRECTIONAL — they swap
    // front/back/side from the view angle around 'homeForward' (= toward 'faceToward'), like the
    // neighbours. The sprite fills in at runtime (InteriorObject slices the atlas), blank in the editor.
    static GameObject MakeFurniture(Transform parent, string name, InteriorObject.Piece piece,
                                    Vector3 pos, Vector3 faceToward,
                                    Texture2D day, Texture2D night, Material mat, bool startsOn = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sharedMaterial = mat;

        var io = go.AddComponent<InteriorObject>();
        io.piece = piece;
        io.dayAtlas = day;
        io.nightmareAtlas = night;
        io.pixelsPerUnit = 16f;
        io.pivot = new Vector2(0.5f, 0f);          // bottom-centre keeps the piece planted on the floor line
        io.startsOn = startsOn;
        io.billboard = true;                       // all upright pieces face the camera
        if (InteriorSolid(piece))
        {
            io.directional = true;                 // ...and solid ones turn front/back/side with the view
            Vector3 fwd = faceToward - pos; fwd.y = 0f;
            io.homeForward = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.back;
        }

        AddFurnitureShadow(parent, pos, ShadowWidth(piece), mat);   // ground the billboard so it doesn't float
        return go;
    }

    static bool InteriorSolid(InteriorObject.Piece p) =>
        p == InteriorObject.Piece.Sofa || p == InteriorObject.Piece.Couch || p == InteriorObject.Piece.Armchair
        || p == InteriorObject.Piece.Bookshelf || p == InteriorObject.Piece.Tv;

    static float ShadowWidth(InteriorObject.Piece p)
    {
        switch (p)
        {
            case InteriorObject.Piece.Sofa:
            case InteriorObject.Piece.CouchDog:  return 3f;
            case InteriorObject.Piece.FloorLamp: return 1f;
            default:                             return 2f;
        }
    }

    // A soft dark ellipse laid flat on the floor under a billboarded piece, so it reads as planted
    // rather than floating (the piece's own base is already at y=0; this just gives it ground contact).
    // Fixed, not billboarded; drawn just above the floor and beneath the furniture.
    static void AddFurnitureShadow(Transform parent, Vector3 pos, float width, Material mat)
    {
        var sh = new GameObject("Shadow");
        sh.transform.SetParent(parent, false);
        sh.transform.position = new Vector3(pos.x, 0.02f, pos.z);
        sh.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        const float baseSize = 0.64f;              // SmokePuff sprite ≈ 64 px @ 100 ppu
        sh.transform.localScale = new Vector3(width * 1.25f / baseSize, width * 0.55f / baseSize, 1f);
        var sr = sh.AddComponent<SpriteRenderer>();
        sr.sprite = SmokePuffSprite();
        sr.sharedMaterial = mat;
        sr.color = new Color(0f, 0f, 0f, 0.33f);
        sr.sortingOrder = -2;                      // under the furniture + rug
    }

    // The floor rug: an InteriorObject laid FLAT on the boards (like the skittering leaves outside),
    // centre-pivoted and scaled up to anchor the grouping. Sits just above the floor; no billboard.
    static void MakeRug(Transform parent, Vector3 pos, Vector3 scale, Texture2D day, Texture2D night, Material mat)
    {
        var go = new GameObject("Rug");
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localScale = scale;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sharedMaterial = mat;

        var io = go.AddComponent<InteriorObject>();
        io.piece = InteriorObject.Piece.Rug;
        io.dayAtlas = day;
        io.nightmareAtlas = night;
        io.pixelsPerUnit = 16f;
        io.pivot = new Vector2(0.5f, 0.5f);
    }

    // Import a furniture atlas for InteriorObject: raw texture it can slice — Read/Write ON, point-
    // filtered, uncompressed, no mips, alpha as transparency (matches INTERIOR_FURNITURE.md install).
    static Texture2D EnsureFurnitureAtlas(string path)
    {
        if (AssetImporter.GetAtPath(path) is TextureImporter imp)
        {
            bool dirty = imp.textureType != TextureImporterType.Default || imp.filterMode != FilterMode.Point ||
                         imp.textureCompression != TextureImporterCompression.Uncompressed ||
                         imp.mipmapEnabled || !imp.isReadable || imp.wrapMode != TextureWrapMode.Clamp ||
                         !imp.alphaIsTransparency;
            if (dirty)
            {
                imp.textureType = TextureImporterType.Default;
                imp.filterMode = FilterMode.Point;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.mipmapEnabled = false;
                imp.isReadable = true;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.alphaSource = TextureImporterAlphaSource.FromInput;
                imp.alphaIsTransparency = true;
                imp.SaveAndReimport();
            }
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // A working front door on the interior's south wall (behind the spawn): the same house_tiles door
    // quad + HousePortal as the cabin outside, but the player approaches it from the +Z (inside) side
    // and it loads the Exterior scene. Walk up, press E — it swings open, fades, and you're back out.
    static void BuildInteriorExitDoor(Transform player)
    {
        var mat = CabinAtlasMaterial();
        var door = MakeTileQuad("ExitDoor", null, new Vector3(0f, 1.2f, -9.72f),
                                Quaternion.identity, 1.5f, 2.4f, 6, 0, mat);
        var hp = door.AddComponent<HousePortal>();
        hp.player = player;
        hp.interiorScene = "Exterior";
        hp.openSound = AssetDatabase.LoadAssetAtPath<AudioClip>(DoorSfx);
        hp.approachFromNegativeZ = false;               // the player is inside, on the +Z side of this door
        hp.promptText = "Press E to step outside";
        hp.overrideArrival = true;                      // step out into the yard just in front of the cabin door...
        hp.arrivalPosition = new Vector3(0f, 0.1f, 2.8f);   // ...not back at the far default spawn (door is at z≈6.5; 2.8 keeps clear of its re-enter range)
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
        player.AddComponent<PlayerArrival>();   // drops the player at a door's arrival point after a scene load (else default spawn)

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
        // On the asphalt road tiles (the "Road" by the cabin + the "DriveRoad" running out of town) play a
        // continuous walking-on-asphalt loop instead of the grass per-step clips. Matched by name-contains
        // so both road objects trigger it.
        footsteps.loopSurfaceName = "Road";
        footsteps.loopClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AsphaltWav);
        footsteps.loopVolume = 0.7f;

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

        // Distant ambient flock crossing the daytime sky (roosts / fades out at night).
        BuildBirds(spriteMat, GameObject.Find("Sky")?.GetComponent<SkyController>());

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

    // -------------------------------------------------------------- in-world driving
    // Augments the HAND-PLACED truck in Exterior with the driving components + lays the road running off
    // the map to the OutOfTown trigger. Opens/saves Exterior but does NOT regenerate it, so the truck (and
    // everything else hand-tended in the scene) survives.
    [MenuItem("Tools/Horror Game/Setup Exterior Driving")]
    public static void SetupExteriorDriving()
    {
        EnsureRenderer3D();
        EnsureCockpitImport();

        var scene = EditorSceneManager.OpenScene(ExteriorSceneOut, OpenSceneMode.Single);
        var truck = GameObject.Find("Truck");
        if (truck == null) { Debug.LogError("[HorrorGame] Setup Exterior Driving: no 'Truck' in Exterior."); return; }

        if (truck.GetComponent<DrivingRig>() == null) truck.AddComponent<DrivingRig>();
        var cockpit = truck.GetComponent<CockpitController>();
        if (cockpit == null) cockpit = truck.AddComponent<CockpitController>();
        WireCockpit(cockpit);
        if (truck.GetComponent<TruckDriver>() == null) truck.AddComponent<TruckDriver>();

        // Walking on the asphalt road tiles plays the continuous "Walking on Asphalt" loop (matched by
        // name-contains "Road", covering both the cabin "Road" and this "DriveRoad"). Applied here — the
        // safe in-place augment path — since a full BuildExterior would wipe the hand-placed truck.
        var playerGo = GameObject.Find("Player");
        var fs = playerGo != null ? playerGo.GetComponent<FootstepAudio>() : null;
        if (fs != null)
        {
            fs.loopSurfaceName = "Road";
            fs.loopClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AsphaltWav);
            fs.loopVolume = 0.7f;
        }

        // Road: continue the entrance line (world x≈0) SOUTH, off the map, from the doorstep past spawn.
        var roadGo = GameObject.Find("DriveRoad") ?? new GameObject("DriveRoad", typeof(MeshFilter), typeof(MeshRenderer));
        roadGo.transform.position = Vector3.zero;
        var rt = roadGo.GetComponent<RoadTiler>() ?? roadGo.AddComponent<RoadTiler>();
        rt.material = RoadAtlasMaterial();
        rt.tileWorldSize = 1f; rt.roadY = 0.03f;
        rt.originX = -1; rt.width = 3;          // cells x -1,0,1 -> centred on the door line
        rt.originZ = -46; rt.length = 52;       // z -46..6: from near the boundary up to the doorstep
        rt.surfaceRow = 0;                      // asphalt — same tiles as the "Road" the truck is parked on by the house
        rt.dashedCentre = true;
        rt.Build();

        // A TOWN / MILL RD sign on the road shoulder pointing back toward the cabin and the other houses.
        if (GameObject.Find("TownSign") == null)
            MakeTownSign(new Vector3(3f, 0f, -8f), EnsureRoadsideSprites(), SpriteMaterial());

        // Trigger just inside the r58 boundary wall so the truck transitions before it can hit it.
        var trig = GameObject.Find("OutOfTownTrigger") ?? new GameObject("OutOfTownTrigger");
        trig.transform.position = new Vector3(0f, 0f, -42f);
        var sx = trig.GetComponent<SceneExitTrigger>() ?? trig.AddComponent<SceneExitTrigger>();
        sx.targetScene = "OutOfTown"; sx.halfExtents = new Vector2(4f, 3f); sx.arriveOnFoot = false;

        EditorSceneManager.SaveScene(scene, ExteriorSceneOut);
        AddSceneToBuild(OutOfTownSceneOut);
        Debug.Log("[HorrorGame] Exterior driving set up: the truck is drivable; the road runs south to the OutOfTown trigger.");
    }

    // A minimal drivable "out of town" stub: ground + sky + a straight road. You arrive already driving and
    // reach a return trigger at the far end that lands you back on foot at the cabin (closing the loop home).
    [MenuItem("Tools/Horror Game/Build Out Of Town")]
    public static void BuildOutOfTown()
    {
        EnsureRenderer3D();
        EnsureCockpitImport();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.5f, 0.52f, 0.55f);

        var sun = new GameObject("Directional Light").AddComponent<Light>();
        sun.type = LightType.Directional; sun.intensity = 1.1f;
        sun.color = new Color(1f, 0.96f, 0.86f);
        sun.transform.rotation = Quaternion.Euler(55f, -25f, 0f);

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(20f, 1f, 20f);   // ±100 units — fits the long looping road
        ground.GetComponent<Renderer>().sharedMaterial =
            LitMaterial("YardMat3D", new Color(0.36f, 0.5f, 0.28f), null, Vector2.one, false);   // same base green as the yard

        // Same grass/dirt patchwork the exterior yard uses (GroundTiler + grass_tiles atlas), sized to cover
        // the whole drive so the road and its roadside props sit on the same ground as home.
        var grassTiles = new GameObject("GrassTiles", typeof(MeshFilter), typeof(MeshRenderer));
        grassTiles.transform.position = new Vector3(0f, 0.02f, 0f);
        var tiler = grassTiles.AddComponent<GroundTiler>();
        tiler.material = GrassAtlasMaterial();
        tiler.worldSize = 200f;
        tiler.tileWorldSize = 2f;
        tiler.Build();

        var spriteMat = SpriteMaterial();

        // A long straight asphalt road down the middle. z −86 (far end) .. 46 (town end); you start near
        // the town end and drive away south. Reaching the far end wraps you back to the start (DriveLoopTrigger).
        const float zStart = 38f, zHome = 44f, zSignZ = 32f, zFar = -82f, zNorth = 46f, zSouth = -86f;
        var roadGo = new GameObject("DriveRoad", typeof(MeshFilter), typeof(MeshRenderer));
        var rt = roadGo.AddComponent<RoadTiler>();
        rt.material = RoadAtlasMaterial();
        rt.tileWorldSize = 1f; rt.roadY = 0.03f;
        rt.originX = -1; rt.width = 3;
        rt.originZ = Mathf.RoundToInt(zSouth); rt.length = Mathf.RoundToInt(zNorth - zSouth);
        rt.surfaceRow = 0; rt.dashedCentre = true;   // asphalt, matching the paved road home (not dirt)
        rt.Build();

        // Range-backdrop ridgelines (same sprites as the exterior yard) that FOLLOW the camera so the peaks
        // stay far off however far you drive — the "travelling far away" feel — over a big static day/night sky.
        BuildMountainRings().AddComponent<BackdropFollow>();
        BuildSkySystem(sun, 260f);
        new GameObject("DialogUI").AddComponent<DialogUI>();

        // Town sign + roadside scenery (dead trees, stop signs, debris, crows) down both shoulders.
        var road = EnsureRoadsideSprites();
        MakeTownSign(new Vector3(3.1f, 0f, zSignZ), road, spriteMat);
        ScatterRoadside(road, spriteMat, zFrom: zFar + 6f, zTo: zStart - 6f);
        // Dense giant-redwood forest walling both sides behind the roadside scenery.
        ScatterForest(EnsureForestSprites(), spriteMat, zFrom: zFar + 4f, zTo: zStart - 2f);

        // Player rig at the town edge — its camera is what the truck borrows when auto-entering drive.
        BuildPlayerRig(new Vector3(0f, 0.1f, zStart), spriteMat, grassFill: false);

        // A full, re-enterable truck (body + door + lights), auto-entering drive on load facing south — so
        // getting out on the road leaves a truck you can walk back to and climb into again.
        var truck = BuildDrivableTruck(new Vector3(0f, 0.84f, zStart), spriteMat);
        var td = truck.GetComponent<TruckDriver>();
        td.autoEnterOnStart = true; td.startHeadingYaw = 180f;

        // Loop wrap: rolling onto the far (south) end teleports the truck back to the start by the sign,
        // so continuing to drive endlessly returns you to the town sign.
        var loop = new GameObject("LoopWrap").AddComponent<DriveLoopTrigger>();
        loop.transform.position = new Vector3(0f, 0f, zFar);
        loop.halfExtents = new Vector2(6f, 3f);
        loop.returnPosition = new Vector3(0f, 0.84f, zStart);

        // Follow the TOWN sign home: driving north to the town end returns you to Exterior on foot at the cabin.
        var trig = new GameObject("HomeTrigger");
        trig.transform.position = new Vector3(0f, 0f, zHome);
        var sx = trig.AddComponent<SceneExitTrigger>();
        sx.targetScene = "Exterior"; sx.halfExtents = new Vector2(4f, 2.5f);
        sx.arriveOnFoot = true; sx.arrivalPosition = new Vector3(5f, 0.1f, 8.5f);

        EditorSceneManager.SaveScene(scene, OutOfTownSceneOut);
        AddSceneToBuild(OutOfTownSceneOut);
        Debug.Log("[HorrorGame] OutOfTown stub built at " + OutOfTownSceneOut + ".");
    }

    // A full, visible, re-enterable truck (the same component set as the hand-placed Exterior truck):
    // billboarded 8-way body, headlights/tail-lights, a walk-up door, and the drive rig. Used in OutOfTown
    // so getting out on the road leaves a truck you can walk back to and climb into again.
    static GameObject BuildDrivableTruck(Vector3 pos, Material spriteMat)
    {
        const string dir = "Assets/Animation/Car/roadside_pack/";   // ppu-16 sheets, same as the hand-placed Exterior truck
        Sprite[] L(string sheet) => LoadSheetSprites(dir + sheet + ".png", sheet + "_");
        var front = L("truck_front"); var back = L("truck_back"); var side = L("truck_side");
        var f3q = L("truck_front3q"); var b3q = L("truck_back3q");

        var truck = new GameObject("Truck");
        truck.transform.position = pos;

        var sr = truck.AddComponent<SpriteRenderer>();
        sr.sharedMaterial = spriteMat;
        sr.sprite = front.Length > 0 ? front[0] : null;

        truck.AddComponent<Billboard>();

        var ds = truck.AddComponent<DirectionalSprite>();   // 8-way facing (parked pointing south)
        ds.noseYaw = 180f;
        if (front.Length > 0) { ds.front = front[0]; ds.frontFrames = front; }
        if (f3q.Length > 0)   { ds.front3q = f3q[0]; ds.front3qFrames = f3q; }
        if (side.Length > 0)  { ds.side = side[0];   ds.sideFrames = side; }
        if (b3q.Length > 0)   { ds.back3q = b3q[0];  ds.back3qFrames = b3q; }
        if (back.Length > 0)  { ds.back = back[0];   ds.backFrames = back; }

        truck.AddComponent<CarLights>();

        var carDoor = truck.AddComponent<CarDoor>();
        carDoor.openCloseClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound Effects/Open and Close Door.wav");
        carDoor.carStartClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound Effects/Car Start and Rumble.wav");

        truck.AddComponent<DrivingRig>();
        WireCockpit(truck.AddComponent<CockpitController>());
        truck.AddComponent<TruckDriver>();
        return truck;
    }

    // Sliced world sprites for the roadside scenery (DRIVING.md §5b) + the TOWN sign (CAR.md §3).
    class RoadsideKit { public Sprite[] deadtree, stopsign, debris, crow, sign; }

    static RoadsideKit EnsureRoadsideSprites()
    {
        string dir = CockpitDir;   // road_deadtree/stopsign/crow/debris live beside the cockpit sheets
        SliceStrip(dir + "/road_deadtree.png", "deadtree_", 4, 40, 56, 16f, 0f);
        SliceStrip(dir + "/road_stopsign.png", "stopsign_", 4, 24, 44, 16f, 0f);
        SliceStrip(dir + "/road_debris.png",   "debris_",   4, 16, 12, 16f, 0f);
        SliceStrip(dir + "/road_crow.png",     "crow_",     4, 16, 16, 16f, 0.5f);   // flying: centre pivot
        SliceStrip(RoadSignPng,                "roadsign_", 2, 32, 48, 16f, 0f);
        return new RoadsideKit
        {
            deadtree = LoadSheetSprites(dir + "/road_deadtree.png", "deadtree_"),
            stopsign = LoadSheetSprites(dir + "/road_stopsign.png", "stopsign_"),
            debris   = LoadSheetSprites(dir + "/road_debris.png",   "debris_"),
            crow     = LoadSheetSprites(dir + "/road_crow.png",     "crow_"),
            sign     = LoadSheetSprites(RoadSignPng,                "roadsign_"),
        };
    }

    // A billboard sprite with an always-playing flip-book (dead trees sway, signs creak, crows flap).
    static GameObject MakeAnimProp(string name, Vector3 pos, Sprite[] frames, Material mat, float fps,
                                   bool randomPhase, bool gateOnFoot = false, bool hideWhenAway = false)
    {
        if (frames == null || frames.Length == 0) return null;
        var go = new GameObject(name);
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = frames[0];
        sr.sharedMaterial = mat;
        go.AddComponent<Billboard>();
        var anim = go.AddComponent<LoopSpriteAnimator>();
        anim.frames = frames; anim.fps = fps; anim.randomStartPhase = randomPhase;
        // Gate the flip-book (and, for crows/debris, visibility) to when the player is on foot nearby, so
        // the roadside doesn't wiggle/flit past the windscreen while driving.
        if (gateOnFoot) go.AddComponent<OnFootProximityProp>().hideWhenAway = hideWhenAway;
        return go;
    }

    // The weathered TOWN / MILL RD directional post at the road's east shoulder. A REAL directional sign:
    // it does NOT billboard (a billboard would spin the whole post so the TOWN arrow never points at town).
    // It faces west toward the road so you read it as you drive, and the arrow is flipped to point north —
    // back toward the town/cabin — and you see it turn to profile as you pass, like a real sign.
    static void MakeTownSign(Vector3 pos, RoadsideKit kit, Material mat)
    {
        if (kit.sign == null || kit.sign.Length == 0) return;
        var go = new GameObject("TownSign");
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, -90f, 0f);   // face west, across the road toward the driver
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = kit.sign[0];
        sr.sharedMaterial = mat;
        sr.flipX = true;                                          // arrow points north — toward town
        var anim = go.AddComponent<LoopSpriteAnimator>();
        anim.frames = kit.sign; anim.fps = 1.1f;
    }

    // Slice the three giant idle-sway redwoods (8 frames each, 224×480, bottom pivot) into world sprites.
    static Sprite[][] EnsureForestSprites()
    {
        string[] names = { "giant_elder", "giant_gnarl", "giant_winter" };
        var result = new Sprite[names.Length][];
        for (int i = 0; i < names.Length; i++)
        {
            string path = RedwoodDir + "/" + names[i] + ".png";
            SliceStrip(path, names[i] + "_", 8, 224, 480, 40f, 0f);   // ppu 40 -> ~12 units tall, towering
            result[i] = LoadSheetSprites(path, names[i] + "_");
        }
        return result;
    }

    // A dense stand of giant redwoods walling both sides of the road (3 depth rows per side, staggered down
    // the length) for a thick forest. Big + far, so they're frozen while driving (OnFootProximityProp) and
    // only sway when you park and walk in among them.
    static void ScatterForest(Sprite[][] forest, Material mat, float zFrom, float zTo)
    {
        if (forest.Length == 0 || forest[0] == null || forest[0].Length == 0) return;
        float[] rowX = { 9f, 18f, 28f };
        int idx = 0;
        for (int side = -1; side <= 1; side += 2)
            for (int r = 0; r < rowX.Length; r++)
                for (float z = zFrom; z <= zTo; z += 9f, idx++)
                {
                    var frames = forest[System.Math.Abs(idx * 7 + r) % forest.Length];
                    if (frames == null || frames.Length == 0) continue;
                    float jx = ((idx * 5) % 5 - 2) * 1.2f;
                    float jz = ((idx * 13) % 7 - 3) * 0.8f;
                    float x = side * (rowX[r] + jx);
                    MakeAnimProp("Redwood", new Vector3(x, 0f, z + jz), frames, mat, 8f,
                                 randomPhase: true, gateOnFoot: true, hideWhenAway: false);
                }
    }

    // Scatter roadside scenery down both shoulders between zFrom and zTo: mostly bare dead trees, some
    // leaning stop signs, the odd blowing debris, and a handful of crows flapping over the road.
    static void ScatterRoadside(RoadsideKit kit, Material mat, float zFrom, float zTo)
    {
        int i = 0;
        for (float z = zFrom; z <= zTo; z += 7f, i++)
        {
            PlaceShoulderProp(kit, mat, i,     z,        side: -1);
            PlaceShoulderProp(kit, mat, i + 5, z + 3.5f, side: +1);
        }
        for (int c = 0; c < 7; c++)
        {
            float z = Mathf.Lerp(zFrom, zTo, (c + 0.5f) / 7f);
            MakeAnimProp("Crow", new Vector3(((c % 2) * 2 - 1) * 1.3f, 3.0f + (c % 3) * 0.7f, z),
                         kit.crow, mat, 7f, randomPhase: true, gateOnFoot: true, hideWhenAway: true);
        }
    }

    static void PlaceShoulderProp(RoadsideKit kit, Material mat, int i, float z, int side)
    {
        int pick = (i * 3 + 1) % 10;                 // deterministic mix: ~half trees, some signs, some debris
        Sprite[] frames; float fps; string name; float xOff; bool hide;
        if (pick < 5)      { frames = kit.deadtree; fps = 2.5f; name = "DeadTree"; xOff = 3.4f + (i % 3) * 0.9f; hide = false; }
        else if (pick < 8) { frames = kit.stopsign; fps = 2.0f; name = "StopSign"; xOff = 2.9f;                 hide = false; }
        else               { frames = kit.debris;   fps = 6.0f; name = "Debris";   xOff = 1.7f;                 hide = true;  }
        // Trees/signs stay visible while driving (frozen); debris only appears when you park and walk up.
        MakeAnimProp(name, new Vector3(side * xOff, 0f, z), frames, mat, fps, randomPhase: true, gateOnFoot: true, hideWhenAway: hide);
    }

    // Assign the cockpit sheet textures (home + _nightmare twins) so they serialize into the scene and
    // survive into a player build; CockpitController slices them at runtime by the DRIVING.md anchor table.
    static void WireCockpit(CockpitController c)
    {
        Texture2D T(string file) => AssetDatabase.LoadAssetAtPath<Texture2D>(CockpitDir + "/" + file);
        c.shellTex = T("cockpit_shell.png");          c.shellNm = T("cockpit_shell_nightmare.png");
        c.gaugeSpeedTex = T("gauge_speed.png");       c.gaugeSpeedNm = T("gauge_speed_nightmare.png");
        c.gaugeFuelTex = T("gauge_fuel.png");         c.gaugeFuelNm = T("gauge_fuel_nightmare.png");
        c.needleTex = T("needle.png");                c.needleNm = T("needle_nightmare.png");
        c.warningTex = T("warning_lights.png");       c.warningNm = T("warning_lights_nightmare.png");
        c.odometerTex = T("odometer_digits.png");     c.odometerNm = T("odometer_digits_nightmare.png");
        c.odoTenthsTex = T("odometer_tenths.png");    c.odoTenthsNm = T("odometer_tenths_nightmare.png");
        c.gearTex = T("gear_indicator.png");          c.gearNm = T("gear_indicator_nightmare.png");
        c.mirrorTex = T("mirror.png");                c.mirrorNm = T("mirror_nightmare.png");
        c.charmTex = T("charm.png");                  c.charmNm = T("charm_nightmare.png");
        c.wheelTex = T("steering_wheel.png");         c.wheelNm = T("steering_wheel_nightmare.png");
        c.passengerNm = T("mirror_passenger_nightmare.png");
    }

    // Cockpit pixel art: Point filter, no compression, no mips (DRIVING.md §2). We slice at runtime, so
    // the meta's own (auto) slicing is irrelevant — only the display filtering/compression matters here.
    static void EnsureCockpitImport()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { CockpitDir }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) continue;
            bool changed = false;
            if (ti.filterMode != FilterMode.Point) { ti.filterMode = FilterMode.Point; changed = true; }
            if (ti.textureCompression != TextureImporterCompression.Uncompressed)
            { ti.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
            if (ti.mipmapEnabled) { ti.mipmapEnabled = false; changed = true; }
            if (changed) ti.SaveAndReimport();
        }
    }

    // Alpha-clipped (cutout) unlit material for road_tiles.png, same recipe as PathAtlasMaterial: writes
    // depth so upright billboards (the parked truck, the player) sort correctly over the road.
    static Material RoadAtlasMaterial()
    {
        if (AssetImporter.GetAtPath(RoadTiles) is TextureImporter imp)
        {
            bool dirty = imp.filterMode != FilterMode.Point ||
                         imp.textureCompression != TextureImporterCompression.Uncompressed ||
                         imp.mipmapEnabled || imp.wrapMode != TextureWrapMode.Clamp || !imp.alphaIsTransparency;
            if (dirty)
            {
                imp.filterMode = FilterMode.Point;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.mipmapEnabled = false;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.alphaIsTransparency = true;
                imp.SaveAndReimport();
            }
        }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(RoadTiles);
        EnsureFolder(MatDir);
        string matPath = MatDir + "/RoadTiles3D.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (mat == null) { mat = new Material(sh); AssetDatabase.CreateAsset(mat, matPath); }
        else mat.shader = sh;

        mat.SetFloat("_Surface", 0f);
        mat.SetFloat("_AlphaClip", 1f);
        mat.SetFloat("_Cutoff", 0.5f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
        mat.SetFloat("_ZWrite", 1f);
        mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        mat.mainTexture = tex;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        EditorUtility.SetDirty(mat);
        return mat;
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
        // Depth-bias variant of the sprite shader: nudges billboards toward the camera in the depth
        // buffer so they stop clipping into walls they stand against, while still being occluded by
        // geometry clearly in front (see SpriteBillboardDepthBias.shader). Falls back to Sprites/Default
        // if the shader is missing so builds never break.
        var biased = Shader.Find("Sprites/BillboardDepthBias");
        if (biased != null)
        {
            if (mat.shader != biased) mat.shader = biased;
            mat.SetFloat("_DepthBias", 0.8f);
            EditorUtility.SetDirty(mat);
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

    // -------------------------------------------------------------- birds (ambient flock)
    // birds_flock.png atlas (64x24, top-left origin): three sizes, 4 flap frames each. See BIRDS.md.
    const int BirdsAtlasH = 24;
    static readonly PropCell[] BirdAtlas =
    {
        new PropCell("birdFar",  0,  0,  8,  6, 4),
        new PropCell("birdMid",  0,  6, 12,  8, 4),
        new PropCell("birdNear", 0, 14, 16, 10, 4),
    };

    // Slices birds_flock.png into centre-pivoted per-frame sprites (birdFar_0.., etc.).
    static void SliceBirdsAtlas(string path, float ppu)
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
        foreach (var p in BirdAtlas)
            for (int f = 0; f < p.frames; f++)
                rects.Add(new SpriteRect
                {
                    name = p.name + "_" + f,
                    spriteID = StableGuid(path + "#" + p.name + f),
                    rect = new Rect(p.x + f * p.w, BirdsAtlasH - (p.y + p.h), p.w, p.h),
                    pivot = new Vector2(0.5f, 0.5f),
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

    // Builds the ambient bird flock (spawns/animates at runtime via BirdFlock), wired to the sky so it
    // fades out at night. Uses the shared unlit sprite material — the silhouettes read over any sky.
    public static GameObject BuildBirds(Material spriteMat, SkyController sky)
    {
        SliceBirdsAtlas(BirdsPng, 16f);
        var far = LoadSheetSprites(BirdsPng, "birdFar_");
        var mid = LoadSheetSprites(BirdsPng, "birdMid_");
        var near = LoadSheetSprites(BirdsPng, "birdNear_");
        if (far.Length + mid.Length + near.Length == 0)
        {
            Debug.LogWarning("[HorrorGame] birds atlas missing / failed to slice: " + BirdsPng);
            return null;
        }

        var go = new GameObject("Birds");
        var flock = go.AddComponent<BirdFlock>();
        flock.farFrames = far;
        flock.midFrames = mid;
        flock.nearFrames = near;
        flock.material = spriteMat;
        flock.sky = sky;
        return go;
    }

    // Applies the two exterior tweaks to the CURRENTLY-OPEN Exterior scene (so we don't have to rebuild
    // the whole scene and lose hand edits): the longer-night pacing on its SkyController, and the bird
    // flock. Call, then save the scene. Idempotent — skips the flock if one already exists.
    public static void SetupExteriorBirdsAndNightPacing()
    {
        var sky = Object.FindObjectOfType<SkyController>();
        if (sky != null)
        {
            sky.splitDayNight = true;
            sky.nightStartT = 0.80f;
            sky.dayDurationSeconds = 60f;
            sky.nightDurationSeconds = 120f;
            EditorUtility.SetDirty(sky);
        }
        if (GameObject.Find("Birds") == null)
            BuildBirds(SpriteMaterial(), sky);
    }

    // -------------------------------------------------------------- clouds (ambient sky backdrop)
    // clouds_atmo.png atlas (120x54, top-left origin): five silhouettes, 2 shimmer frames each. See CLOUDS.md.
    const string CloudsPng = "Assets/Animation/clouds_atmo.png";
    const int CloudsAtlasH = 54;
    static readonly PropCell[] CloudAtlas =
    {
        new PropCell("cloudWisp",  0,  0, 14,  5, 2),
        new PropCell("cloudSmall", 0,  5, 20,  8, 2),
        new PropCell("cloudMed",   0, 13, 30, 11, 2),
        new PropCell("cloudLarge", 0, 24, 42, 14, 2),
        new PropCell("cloudHero",  0, 38, 60, 16, 2),
    };

    // Slices clouds_atmo.png into centre-pivoted per-frame sprites (cloudWisp_0.., etc.).
    static void SliceCloudsAtlas(string path, float ppu)
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
        foreach (var p in CloudAtlas)
            for (int f = 0; f < p.frames; f++)
                rects.Add(new SpriteRect
                {
                    name = p.name + "_" + f,
                    spriteID = StableGuid(path + "#" + p.name + f),
                    rect = new Rect(p.x + f * p.w, CloudsAtlasH - (p.y + p.h), p.w, p.h),
                    pivot = new Vector2(0.5f, 0.5f),
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

    // Builds the ambient cloud backdrop (spawns/animates at runtime via CloudLayer), wired to the sky
    // so the clouds ride its rect just behind the sun/moon. Uses the shared unlit sprite material.
    public static GameObject BuildClouds(Material spriteMat, SkyController sky)
    {
        SliceCloudsAtlas(CloudsPng, 16f);
        var wisp  = LoadSheetSprites(CloudsPng, "cloudWisp_");
        var small = LoadSheetSprites(CloudsPng, "cloudSmall_");
        var med   = LoadSheetSprites(CloudsPng, "cloudMed_");
        var large = LoadSheetSprites(CloudsPng, "cloudLarge_");
        var hero  = LoadSheetSprites(CloudsPng, "cloudHero_");
        if (wisp.Length + small.Length + med.Length + large.Length + hero.Length == 0)
        {
            Debug.LogWarning("[HorrorGame] clouds atlas missing / failed to slice: " + CloudsPng);
            return null;
        }

        var go = new GameObject("Clouds");
        var layer = go.AddComponent<CloudLayer>();
        layer.wispFrames  = wisp;
        layer.smallFrames = small;
        layer.medFrames   = med;
        layer.largeFrames = large;
        layer.heroFrames  = hero;
        layer.material    = spriteMat;
        layer.sky         = sky;
        return go;
    }

    // Adds the ambient cloud backdrop to the CURRENTLY-OPEN Exterior scene without rebuilding it (so
    // hand edits survive). Call, then save the scene. Idempotent — skips if a Clouds layer exists.
    public static void SetupExteriorClouds()
    {
        var sky = Object.FindObjectOfType<SkyController>();
        if (GameObject.Find("Clouds") == null)
            BuildClouds(SpriteMaterial(), sky);
    }

    // note_sign.png (176x80, top-left origin): the small illegible "far" note that pins to the door,
    // and the readable "near" close-up shown on interact. Each holds 2 slow droop frames. See NOTE.md.
    const int NoteAtlasH = 80;
    static readonly PropCell[] NoteAtlas =
    {
        new PropCell("noteFar",  0,  0, 16, 22, 2),   // pinned on the door — writing illegible
        new PropCell("noteNear", 0, 22, 88, 58, 2),   // the read view — the actual text
    };

    static void SliceNoteAtlas(string path, float ppu)
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
        foreach (var p in NoteAtlas)
            for (int f = 0; f < p.frames; f++)
                rects.Add(new SpriteRect
                {
                    name = p.name + "_" + f,
                    spriteID = StableGuid(path + "#" + p.name + f),
                    rect = new Rect(p.x + f * p.w, NoteAtlasH - (p.y + p.h), p.w, p.h),
                    pivot = new Vector2(0.5f, 0.5f),
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

    // Pins the "on vacation" note to House B's front door in the currently-open Exterior scene (House B
    // is a hand-placed scene object). The small illegible FAR note mounts on the door, fixed-facing the
    // approaching player (no billboard, so it can't flip to a backwards view); press E near it to read
    // the NEAR close-up with the actual text (NoteSign). Re-running replaces any existing note.
    public static GameObject AddHouseBNote()
    {
        SliceNoteAtlas(NoteSignPng, 32f);
        var far = LoadSheetSprites(NoteSignPng, "noteFar_");
        var near = LoadSheetSprites(NoteSignPng, "noteNear_");
        if (far.Length == 0 || near.Length == 0) { Debug.LogWarning("[HorrorGame] note_sign slice failed: " + NoteSignPng); return null; }

        var existing = GameObject.Find("HouseB_Note");
        if (existing != null) Object.DestroyImmediate(existing);

        var door = GameObject.Find("NeighborHouse_B/DoorTop");
        Vector3 pos = door != null
            ? new Vector3(door.transform.position.x, door.transform.position.y, door.transform.position.z - 0.03f)
            : new Vector3(30f, 1.65f, 23.69f);

        var player = GameObject.Find("Player");
        var go = new GameObject("HouseB_Note");
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);   // face south / outward, toward the approaching player
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = far[0];
        sr.sharedMaterial = SpriteMaterial();
        var ns = go.AddComponent<NoteSign>();
        ns.player = player != null ? player.transform : null;
        ns.farFrames = far;
        ns.nearFrames = near;
        ns.paperSound = AssetDatabase.LoadAssetAtPath<AudioClip>(PaperSfx);
        return go;
    }

    // Wires the paper-crumple SFX onto the already-placed door note (NoteSign) without moving/rebuilding
    // it — plays when the player opens or closes the note. Run after adding Paper Crumple.wav.
    [MenuItem("Tools/Horror Game/House B/Add Note Crumple Sound")]
    public static void AddNoteCrumpleSound()
    {
        var note = GameObject.Find("HouseB_Note");
        if (note == null) { Debug.LogWarning("[HorrorGame] HouseB_Note not found — open the Exterior scene first."); return; }
        var ns = note.GetComponent<NoteSign>();
        if (ns == null) { Debug.LogWarning("[HorrorGame] HouseB_Note has no NoteSign component."); return; }
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(PaperSfx);
        if (clip == null) { Debug.LogWarning("[HorrorGame] Paper Crumple.wav missing: " + PaperSfx); return; }
        Undo.RecordObject(ns, "Add Note Crumple Sound");
        ns.paperSound = clip;
        EditorUtility.SetDirty(ns);
        EditorSceneManager.MarkSceneDirty(note.scene);
        Debug.Log("[HorrorGame] Wired Paper Crumple.wav to the door note. Save (Ctrl+S) to keep it.");
    }

    // ----------------------------------------------------------------- board up House B (vacant)
    // Turns Neighbor House B derelict per BOARDED_UP.md — the horror of a house no one lives in.
    // Non-destructive & reversible: it only repoints House B's existing opening quads (TileStripQuad)
    // at boarded_up_tiles.png and adds a few dead-ivy overlays; the 3D shell and layout are untouched.
    // Windows become smashed/boarded panes (flies orbiting the hole, a moth at the lit upstairs slit),
    // the door is barricaded with a condemned notice, the chimney goes dead cold (no smoke), and dead
    // ivy creeps the siding. Vacancy is the whole state (no home/nightmare split — both point at the
    // one boarded sheet). Run "Restore (Occupied)" to revert.
    const string BoardedTiles   = "Assets/Animation/boarded_up_tiles.png";
    const string NeighborBTiles = "Assets/Animation/Neighbor Houses Modified/neighbor_houses_kit/neighbor_B_tiles.png";
    const string NeighborBNight = "Assets/Animation/Neighbor Houses Modified/neighbor_houses_kit/neighbor_B_tiles_nightmare.png";

    [MenuItem("Tools/Horror Game/House B/Board Up (Vacant)")]
    public static void BoardUpHouseB()
    {
        var houseB = GameObject.Find("NeighborHouse_B");
        if (houseB == null) { Debug.LogWarning("[HorrorGame] NeighborHouse_B not found — open the Exterior scene first."); return; }

        var boarded = ImportBoardedTiles();
        if (boarded == null) { Debug.LogWarning("[HorrorGame] boarded_up_tiles.png missing: " + BoardedTiles); return; }

        Undo.RegisterFullObjectHierarchyUndo(houseB, "Board Up House B");

        foreach (var q in houseB.GetComponentsInChildren<Game.Houses.TileStripQuad>(true))
        {
            switch (q.kind)
            {
                case Game.Houses.TileStripQuad.Kind.WinSil:     // Win_L / Win_R -> smashed pane + flies
                case Game.Houses.TileStripQuad.Kind.WinCandle:  // Win_Up -> moth at the lit slit
                    q.tilesHome = boarded; q.tilesNightmare = boarded;
                    break;
                case Game.Houses.TileStripQuad.Kind.DoorTop:    // -> planks + condemned notice (static)
                    q.tilesHome = boarded; q.tilesNightmare = boarded;
                    q.kind = Game.Houses.TileStripQuad.Kind.BoardedDoorTop;
                    break;
                case Game.Houses.TileStripQuad.Kind.DoorBottom: // -> plank brace + threshold weeds (static)
                    q.tilesHome = boarded; q.tilesNightmare = boarded;
                    q.kind = Game.Houses.TileStripQuad.Kind.BoardedDoorBottom;
                    break;
                case Game.Houses.TileStripQuad.Kind.Smoke:      // dead chimney — no smoke
                    q.gameObject.SetActive(false);
                    break;
            }
            EditorUtility.SetDirty(q);
        }

        // The shell itself (rotting warped siding, missing shingles, cracked foundation, crumbling
        // chimney): House B's walls/roof are textured at runtime by HouseDreadSwap via a property
        // block, and the boarded atlas is cell-aligned with neighbor_B_tiles, so pointing home & night
        // at the boarded sheet swaps every wall/roof/foundation/chimney cell for its derelict version.
        var swap = houseB.GetComponent<HouseDreadSwap>();
        if (swap != null) { swap.home = boarded; swap.night = boarded; EditorUtility.SetDirty(swap); }

        // The "on vacation" note was mounted 0.22 m proud of the door, so it read as floating off the
        // wall from the side — pull it flush against the boarded door.
        var note = GameObject.Find("HouseB_Note");
        var doorT = houseB.transform.Find("DoorTop");
        if (note != null && doorT != null)
        {
            Undo.RecordObject(note.transform, "Board Up House B");
            note.transform.position = new Vector3(doorT.position.x, doorT.position.y, doorT.position.z - 0.03f);
            note.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        }

        // Boarded detail overlays around the WHOLE house — the shell siding is already boarded via
        // HouseDreadSwap; these add the tile details (boarded/smashed windows, a moth at a slit, dead
        // ivy, loose planks) on the sides and back too, not just the front. Fresh quads sliced from the
        // boarded atlas, placed per wall face: front/back sit in the XY plane, the sides rotate 90° into
        // the ZY plane. NeighborHouseTiles3D is double-sided, so a wall-proud quad reads from outside.
        foreach (var t in houseB.GetComponentsInChildren<Transform>(true).ToArray())
            if (t != null && (t.name.StartsWith("Ivy_") || t.name.StartsWith("Boarded_")))
                Undo.DestroyObjectImmediate(t.gameObject);

        var decalMat = AssetDatabase.LoadAssetAtPath<Material>(NeighborHouseMat);
        var prim = GameObject.CreatePrimitive(PrimitiveType.Quad);
        var quadMesh = prim.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(prim);
        var decalPlayer = GameObject.Find("Player");

        var Kbw    = Game.Houses.TileStripQuad.Kind.BoardedWindow; // (7,0) static planks over glass
        var Kbroke = Game.Houses.TileStripQuad.Kind.WinSil;        // row2 smashed pane + flies
        var Kmoth  = Game.Houses.TileStripQuad.Kind.WinCandle;     // row3 moth at the lit slit
        var Kivy   = Game.Houses.TileStripQuad.Kind.Overgrowth;    // row4 dead ivy
        var Kplank = Game.Houses.TileStripQuad.Kind.LoosePlank;    // row5 rattling board

        int decalN = 0;
        void Decal(Game.Houses.TileStripQuad.Kind kind, int col, int row, Vector3 pos, float yRot, Vector2 size, bool preview)
        {
            var q = new GameObject("Boarded_" + (decalN++), typeof(MeshFilter), typeof(MeshRenderer), typeof(Game.Houses.TileStripQuad));
            q.transform.SetParent(houseB.transform, false);
            q.transform.localPosition = pos;
            q.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);
            q.transform.localScale = new Vector3(size.x, size.y, 1f);
            q.GetComponent<MeshFilter>().sharedMesh = quadMesh;
            var mr = q.GetComponent<MeshRenderer>();
            mr.sharedMaterial = decalMat;
            var t = q.GetComponent<Game.Houses.TileStripQuad>();
            t.kind = kind; t.tilesHome = boarded; t.tilesNightmare = boarded;
            t.atlasWidth = 192; t.atlasHeight = 144; t.tilePx = 24;
            if (decalPlayer != null) t.player = decalPlayer.transform;
            if (preview)   // static cell only — an MPB on an animated strip would freeze its frames
            {
                float sx = 24f / 192f, sy = 24f / 144f;
                var mpb = new MaterialPropertyBlock(); mr.GetPropertyBlock(mpb);
                mpb.SetTexture("_BaseMap", boarded); mpb.SetTexture("_MainTex", boarded);
                mpb.SetVector("_BaseMap_ST", new Vector4(sx, sy, col * sx, 1f - (row + 1) * sy));
                mr.SetPropertyBlock(mpb);
            }
            Undo.RegisterCreatedObjectUndo(q, "Board Up House B");
        }

        // Footprint 7.7 (X) x 6.5 (Z): walls at x=±3.85, z=±3.25; decals sit ~0.03 proud of each face.
        const float FR = -3.28f, BK = 3.28f, LF = -3.88f, RT = 3.88f;
        var win  = new Vector2(1.2f, 1.2f);
        var tall = new Vector2(1.2f, 1.3f);
        var wide = new Vector2(1.35f, 1.0f);
        // FRONT (z-) — keep dead ivy + a loose plank alongside the boarded windows/door already converted
        Decal(Kivy,   0, 4, new Vector3(-2.9f, 0.95f, FR), 0f, tall, false);
        Decal(Kivy,   0, 4, new Vector3( 2.9f, 1.35f, FR), 0f, tall, false);
        Decal(Kplank, 0, 5, new Vector3(-1.15f, 2.6f, FR), 0f, wide, false);
        // BACK (z+)
        Decal(Kbw,    7, 0, new Vector3(-2.0f, 1.7f, BK), 0f, win,  true);
        Decal(Kbroke, 0, 2, new Vector3( 2.0f, 1.7f, BK), 0f, win,  false);
        Decal(Kmoth,  0, 3, new Vector3( 0.0f, 3.5f, BK), 0f, win,  false);
        Decal(Kivy,   0, 4, new Vector3(-3.0f, 0.9f, BK), 0f, tall, false);
        Decal(Kplank, 0, 5, new Vector3( 1.0f, 2.7f, BK), 0f, wide, false);
        // LEFT (x-) — rotated into the ZY plane
        Decal(Kbw,    7, 0, new Vector3(LF, 1.7f, -1.2f), 90f, win,  true);
        Decal(Kbroke, 0, 2, new Vector3(LF, 1.7f,  1.4f), 90f, win,  false);
        Decal(Kivy,   0, 4, new Vector3(LF, 0.9f,  0.1f), 90f, tall, false);
        Decal(Kplank, 0, 5, new Vector3(LF, 2.9f, -1.6f), 90f, wide, false);
        // RIGHT (x+)
        Decal(Kbw,    7, 0, new Vector3(RT, 1.7f, -1.2f), 90f, win,  true);
        Decal(Kmoth,  0, 3, new Vector3(RT, 3.4f,  1.0f), 90f, win,  false);
        Decal(Kivy,   0, 4, new Vector3(RT, 1.0f,  1.7f), 90f, tall, false);
        Decal(Kplank, 0, 5, new Vector3(RT, 2.6f, -1.7f), 90f, wide, false);

        EditorSceneManager.MarkSceneDirty(houseB.scene);
        Debug.Log("[HorrorGame] House B boarded up — save the scene (Ctrl+S) to keep it. Restore via Tools > Horror Game > House B > Restore.");
    }

    [MenuItem("Tools/Horror Game/House B/Restore (Occupied)")]
    public static void RestoreHouseB()
    {
        var houseB = GameObject.Find("NeighborHouse_B");
        if (houseB == null) { Debug.LogWarning("[HorrorGame] NeighborHouse_B not found."); return; }
        var home  = AssetDatabase.LoadAssetAtPath<Texture2D>(NeighborBTiles);
        var night = AssetDatabase.LoadAssetAtPath<Texture2D>(NeighborBNight);
        if (home == null) { Debug.LogWarning("[HorrorGame] neighbor_B_tiles.png missing: " + NeighborBTiles); return; }

        Undo.RegisterFullObjectHierarchyUndo(houseB, "Restore House B");
        foreach (var t in houseB.GetComponentsInChildren<Transform>(true).ToArray())
            if (t != null && (t.name.StartsWith("Ivy_") || t.name.StartsWith("Boarded_"))) Undo.DestroyObjectImmediate(t.gameObject);

        foreach (var q in houseB.GetComponentsInChildren<Game.Houses.TileStripQuad>(true))
        {
            if (q.kind == Game.Houses.TileStripQuad.Kind.BoardedDoorTop)    q.kind = Game.Houses.TileStripQuad.Kind.DoorTop;
            if (q.kind == Game.Houses.TileStripQuad.Kind.BoardedDoorBottom) q.kind = Game.Houses.TileStripQuad.Kind.DoorBottom;
            q.tilesHome = home; q.tilesNightmare = night;
            if (q.kind == Game.Houses.TileStripQuad.Kind.Smoke && !q.gameObject.activeSelf) q.gameObject.SetActive(true);
            EditorUtility.SetDirty(q);
        }
        var swap = houseB.GetComponent<HouseDreadSwap>();
        if (swap != null) { swap.home = home; swap.night = night; EditorUtility.SetDirty(swap); }
        EditorSceneManager.MarkSceneDirty(houseB.scene);
        Debug.Log("[HorrorGame] House B restored to occupied — save the scene (Ctrl+S) to keep it.");
    }

    // ----------------------------------------------------------------- Robert's chimney smoke
    // Robert's house (NeighborHouse_C, the saltbox he stands in front of) had no visible chimney. This
    // builds a fieldstone chimney from neighbor_B_tiles.png (the ChimneyBody + ChimneyTop cells, so it
    // matches the neighbours' stone look) on the FRONT of C's ridge, plus the player cabin's pooled
    // ChimneySmoke plume tuned LARGE — a warm, lived-in chimney rising above Robert's roof (a deliberate
    // contrast to the dead, boarded House B). Re-runnable: replaces any prior build.
    const string PlayerChimneyPath = "House/Chimney";
    const string NeighborHouseMat  = "Assets/Settings/3D/NeighborHouseTiles3D.mat";

    [MenuItem("Tools/Horror Game/Add Robert Chimney Smoke")]
    public static void AddRobertChimneySmoke()
    {
        var houseC = GameObject.Find("NeighborHouse_C");
        if (houseC == null) { Debug.LogWarning("[HorrorGame] NeighborHouse_C (Robert's house) not found — open the Exterior scene first."); return; }
        var neighborB = AssetDatabase.LoadAssetAtPath<Texture2D>(NeighborBTiles);
        var tilesMat  = AssetDatabase.LoadAssetAtPath<Material>(NeighborHouseMat);
        if (neighborB == null || tilesMat == null) { Debug.LogWarning("[HorrorGame] neighbor_B_tiles.png / NeighborHouseTiles3D.mat missing."); return; }

        // Replace any previous build so this is idempotent.
        var prev = houseC.transform.Find("RobertChimney");
        if (prev != null) Undo.DestroyObjectImmediate(prev.gameObject);

        // Root on the FRONT of C's ridge so the player (who approaches from the south / -z, past Robert)
        // sees it — the back slope was hiding the old stack. Walls top ~y5, saltbox ridge ~6.8.
        var root = new GameObject("RobertChimney");
        root.transform.SetParent(houseC.transform, false);
        root.transform.localPosition = new Vector3(1.0f, 6.6f, -0.6f);

        // Shared unit quad; TileStripQuad expects a 0-1 UV quad and scrolls the atlas UVs itself.
        var prim = GameObject.CreatePrimitive(PrimitiveType.Quad);
        var quadMesh = prim.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(prim);
        var playerT = GameObject.Find("Player");

        // Build one chimney face from a neighbor_B cell. TileStripQuad only textures the material at
        // play, so also push the cell into a property block for an in-editor preview.
        void MakeFace(string nm, Game.Houses.TileStripQuad.Kind kind, int col, int row, Vector3 lp, Vector3 ls)
        {
            var q = new GameObject(nm, typeof(MeshFilter), typeof(MeshRenderer), typeof(Billboard), typeof(Game.Houses.TileStripQuad));
            q.transform.SetParent(root.transform, false);
            q.transform.localPosition = lp;
            q.transform.localScale = ls;
            q.GetComponent<MeshFilter>().sharedMesh = quadMesh;
            var mr = q.GetComponent<MeshRenderer>();
            mr.sharedMaterial = tilesMat;
            q.GetComponent<Billboard>().yAxisOnly = true;
            var t = q.GetComponent<Game.Houses.TileStripQuad>();
            t.kind = kind; t.tilesHome = neighborB; t.tilesNightmare = neighborB;
            t.atlasWidth = 192; t.atlasHeight = 144; t.tilePx = 24;
            if (playerT != null) t.player = playerT.transform;

            float sx = 24f / 192f, sy = 24f / 144f;
            var mpb = new MaterialPropertyBlock();
            mr.GetPropertyBlock(mpb);
            mpb.SetTexture("_BaseMap", neighborB);
            mpb.SetTexture("_MainTex", neighborB);
            mpb.SetVector("_BaseMap_ST", new Vector4(sx, sy, col * sx, 1f - (row + 1) * sy));
            mr.SetPropertyBlock(mpb);
        }
        // Fieldstone stack + capped top (with flue pots), stacked to poke above the front roofline.
        MakeFace("Stack", Game.Houses.TileStripQuad.Kind.ChimneyBody, 4, 1, new Vector3(0f, 0f,   0f), new Vector3(0.95f, 1.3f,  1f));
        MakeFace("Cap",   Game.Houses.TileStripQuad.Kind.ChimneyTop,  5, 1, new Vector3(0f, 0.9f, 0f), new Vector3(1.05f, 0.65f, 1f));

        // Plume: clone the player cabin's pooled ChimneySmoke emitter and tune it large & billowy.
        var smokeSrc = GameObject.Find(PlayerChimneyPath + "/ChimneySmoke");
        if (smokeSrc != null)
        {
            var smokeGo = Object.Instantiate(smokeSrc, root.transform);
            smokeGo.name = "ChimneySmoke";
            smokeGo.transform.localPosition = new Vector3(0f, 1.25f, 0f);
            var smoke = smokeGo.GetComponent<ChimneySmoke>();
            if (smoke != null)
            {
                smoke.poolSize     = 14;
                smoke.emitInterval = 0.4f;
                smoke.riseSpeed    = 1.15f;
                smoke.driftAmount  = 0.4f;
                smoke.life         = 4.2f;
                smoke.startScale   = 0.45f;
                smoke.endScale     = 2.3f;
                smoke.tint         = new Color(0.82f, 0.82f, 0.86f, 0.7f);
                smoke.sortingOrder = 0;   // distance-sort with the tree/player billboards (no clip-through)
            }
        }
        else Debug.LogWarning("[HorrorGame] '" + PlayerChimneyPath + "/ChimneySmoke' not found — chimney built without a plume.");

        // Retire C's old tile-strip smoke quad (the one that wasn't reading) so there's no faint double.
        var oldSmoke = houseC.transform.Find("Smoke");
        if (oldSmoke != null) oldSmoke.gameObject.SetActive(false);

        Undo.RegisterCreatedObjectUndo(root, "Add Robert Chimney Smoke");
        EditorSceneManager.MarkSceneDirty(houseC.scene);
        Debug.Log("[HorrorGame] Built a neighbor-stone chimney + large plume on Robert's house (NeighborHouse_C). Enter Play to see it; save (Ctrl+S) to keep it.");
    }

    // Normalises every ChimneySmoke emitter in the open scene to sortingOrder 0 so the puffs
    // distance-sort with the trees/player billboards instead of drawing on top of them (the smoke was
    // clipping through nearer objects at order 30). Fixes the player cabin + Robert's chimney at once.
    [MenuItem("Tools/Horror Game/Fix Chimney Smoke Sorting")]
    public static void FixChimneySmokeSorting()
    {
        var smokes = Object.FindObjectsOfType<ChimneySmoke>(true);
        if (smokes.Length == 0) { Debug.LogWarning("[HorrorGame] No ChimneySmoke found in the open scene."); return; }
        foreach (var s in smokes)
        {
            Undo.RecordObject(s, "Fix Chimney Smoke Sorting");
            s.sortingOrder = 0;
            EditorUtility.SetDirty(s);
            if (s.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(s.gameObject.scene);
        }
        Debug.Log("[HorrorGame] Set " + smokes.Length + " ChimneySmoke emitter(s) to sortingOrder 0 (distance-sorts with trees/player). Enter Play to verify; save (Ctrl+S).");
    }

    // ----------------------------------------------------------------- Robert's yard props
    // Tech-junk machines around Robert's house (ROBERT_PROPS.md): sliced from props_robert.png (day) +
    // props_robert_nightmare.png (same 208x80 layout), each looping its idle strip at its own rate via
    // YardMachine, which also holds the nightmare frames for a later dread swap. Placed in the yard in
    // front of NeighborHouse_C, flanking the path, around where Robert stands. Re-runnable.
    struct YardDef
    {
        public string name; public int x, y, w, h, frames; public float ms; public Vector3 pos;
        public YardDef(string n, int x, int y, int w, int h, int f, float ms, Vector3 p)
        { name = n; this.x = x; this.y = y; this.w = w; this.h = h; frames = f; this.ms = ms; pos = p; }
    }
    const int RobertPropsAtlasH = 80;
    static readonly YardDef[] RobertYard =
    {
        //          name           x    y   w   h  fr   ms     world pos (feet on ground, y=0)
        new YardDef("dish",         0,   0, 32, 48, 3, 520f, new Vector3(20.0f, 0f, 8.0f)),
        new YardDef("crt",         96,   0, 32, 48, 3, 190f, new Vector3(25.8f, 0f, 8.0f)),
        new YardDef("serverTower",  0,  48, 16, 32, 3, 360f, new Vector3(26.5f, 0f, 7.0f)),
        new YardDef("scope",       48,  48, 32, 16, 4, 150f, new Vector3(20.5f, 0f, 6.5f)),
        new YardDef("hacksaw",     48,  64, 32, 16, 2, 600f, new Vector3(25.2f, 0f, 5.5f)),
        new YardDef("cables",     176,  48, 16, 16, 2, 430f, new Vector3(24.3f, 0f, 6.5f)),
        new YardDef("battery",    176,  64, 16, 16, 2, 560f, new Vector3(20.8f, 0f, 5.0f)),
    };

    // Slice a Robert-props atlas into named per-frame sprites (dish_0.., crt_0..) with feet pivots —
    // mirrors SlicePropsAtlas, using the RobertYard regions and the 208x80 atlas height.
    static void SliceRobertProps(string path, float ppu)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter imp)) return;
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Multiple;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.spritePixelsPerUnit = ppu;
        imp.mipmapEnabled = false;
        imp.wrapMode = TextureWrapMode.Clamp;

        var factory = new SpriteDataProviderFactories(); factory.Init();
        var dp = factory.GetSpriteEditorDataProviderFromObject(imp);
        dp.InitSpriteEditorDataProvider();

        var rects = new List<SpriteRect>();
        foreach (var m in RobertYard)
            for (int f = 0; f < m.frames; f++)
                rects.Add(new SpriteRect
                {
                    name = m.name + "_" + f,
                    spriteID = StableGuid(path + "#" + m.name + f),
                    rect = new Rect(m.x + f * m.w, RobertPropsAtlasH - (m.y + m.h), m.w, m.h),
                    pivot = new Vector2(0.5f, 0f),   // feet
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

    [MenuItem("Tools/Horror Game/Add Robert Yard Props")]
    public static void AddRobertYardProps()
    {
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(RobertPropsDay) == null)
        { Debug.LogWarning("[HorrorGame] props_robert.png missing: " + RobertPropsDay); return; }
        SliceRobertProps(RobertPropsDay, 16f);
        SliceRobertProps(RobertPropsNight, 16f);

        var mat = SpriteMaterial();
        var existing = GameObject.Find("RobertYard");
        if (existing != null) Undo.DestroyObjectImmediate(existing);
        var yard = new GameObject("RobertYard");
        Undo.RegisterCreatedObjectUndo(yard, "Add Robert Yard Props");

        int placed = 0;
        foreach (var m in RobertYard)
        {
            var day   = LoadSheetSprites(RobertPropsDay, m.name + "_");
            var night = LoadSheetSprites(RobertPropsNight, m.name + "_");
            if (day.Length == 0) { Debug.LogWarning("[HorrorGame] no day frames sliced for " + m.name); continue; }
            var go = new GameObject("Yard_" + m.name);
            go.transform.SetParent(yard.transform, false);
            go.transform.position = m.pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = day[0];
            sr.sharedMaterial = mat;
            sr.sortingOrder = 0;                 // distance-sort with the trees/Robert (no clip-through)
            go.AddComponent<Billboard>();
            var ym = go.AddComponent<YardMachine>();
            ym.dayFrames = day; ym.nightFrames = night; ym.frameMs = m.ms;
            placed++;
        }
        EditorSceneManager.MarkSceneDirty(yard.scene);
        Debug.Log("[HorrorGame] Placed " + placed + " Robert yard machines (day idle animating; nightmare frames wired for a later dread swap). Save (Ctrl+S).");
    }

    // Adds/updates the RobertGoesHome routine on Robert and wires it to House C's door halves + a door
    // point on the ground, so he occasionally walks inside (back sheet) and later back out (front sheet)
    // with the door swinging. Daylight + player-proximity gated.
    [MenuItem("Tools/Horror Game/Add Robert Goes Home")]
    public static void AddRobertGoesHome()
    {
        var robert = GameObject.Find("Robert");
        if (robert == null) { Debug.LogWarning("[HorrorGame] Robert not found — open the Exterior scene first."); return; }
        var houseC = GameObject.Find("NeighborHouse_C");
        if (houseC == null) { Debug.LogWarning("[HorrorGame] NeighborHouse_C (Robert's house) not found."); return; }

        var rgh = robert.GetComponent<RobertGoesHome>();
        if (rgh == null) rgh = Undo.AddComponent<RobertGoesHome>(robert);
        else Undo.RecordObject(rgh, "Add Robert Goes Home");

        var doors = new List<Game.Houses.TileStripQuad>();
        Transform doorT = null;
        foreach (var q in houseC.GetComponentsInChildren<Game.Houses.TileStripQuad>(true))
            if (q.kind == Game.Houses.TileStripQuad.Kind.DoorTop || q.kind == Game.Houses.TileStripQuad.Kind.DoorBottom)
            {
                doors.Add(q);
                if (q.kind == Game.Houses.TileStripQuad.Kind.DoorTop) doorT = q.transform;
            }
        rgh.doorQuads = doors.ToArray();
        rgh.doorPoint = doorT != null ? new Vector3(doorT.position.x, 0f, doorT.position.z) : new Vector3(23f, 0f, 9f);

        var player = GameObject.Find("Player");
        rgh.player = player != null ? player.transform : null;

        // Continuous short in/out cycle: a few seconds inside, a few outside, starting by going in.
        rgh.idleWait = new Vector2(2f, 5f);
        rgh.insideWait = new Vector2(3f, 6f);

        EditorUtility.SetDirty(rgh);
        EditorSceneManager.MarkSceneDirty(robert.scene);
        Debug.Log("[HorrorGame] Robert now cycles in/out of his house (" + doors.Count + " door halves wired, door at " + rgh.doorPoint + "). Enter Play to watch; save (Ctrl+S).");
    }

    // ----------------------------------------------------------------- world edge
    // The base ground plane only reached ±40 while the nearest mountain ring sits at r~60, so the
    // player could walk off the plane into the void. This extends the ground + grass/dirt tiling out
    // under the mountains, scatters autumn props through the new outer ring so it isn't bare, and rings
    // the world with an INVISIBLE collider wall just inside the mountains (the CharacterController stops
    // against it). Operates on the open Exterior scene — does not regenerate it.
    [MenuItem("Tools/Horror Game/Fix World Edge (Extend Yard + Boundary)")]
    public static void FixWorldEdge()
    {
        var ground = GameObject.Find("Ground");
        if (ground == null) { Debug.LogWarning("[HorrorGame] 'Ground' not found — open the Exterior scene first."); return; }
        Undo.RecordObject(ground.transform, "Fix World Edge");
        ground.transform.localScale = new Vector3(13f, 1f, 13f);   // 130x130 (±65): reaches under the r60 mountains
        EditorUtility.SetDirty(ground.transform);

        var grass = GameObject.Find("GrassTiles");
        if (grass != null && grass.GetComponent<GroundTiler>() is GroundTiler tiler)
        { Undo.RecordObject(tiler, "Fix World Edge"); tiler.worldSize = 130f; tiler.Build(); EditorUtility.SetDirty(tiler); }

        ScatterEdgeProps();          // fill the new outer ring with autumn dressing
        BuildWorldBoundary(58f, 6f); // invisible wall just inside the mountains

        EditorSceneManager.MarkSceneDirty(ground.scene);
        Debug.Log("[HorrorGame] Extended the ground/grass to the mountains, scattered edge props, and ringed the world with an invisible boundary at r58. Enter Play; save (Ctrl+S).");
    }

    // Autumn props (trees, rocks, logs, mushrooms) scattered through the annulus r42..56 so the newly
    // exposed ground between the old yard and the mountains reads as forest floor, not bare plane.
    static void ScatterEdgeProps()
    {
        SlicePropsAtlas(PropsAutumn, 16f, 0f);
        var sprites = LoadSheetSprites(PropsAutumn, "");
        Sprite[] Fr(string n) => sprites.Where(s => s.name.StartsWith(n + "_")).OrderBy(s => s.name).ToArray();
        Sprite One(string n) { var a = Fr(n); return a.Length > 0 ? a[0] : null; }
        var mat = SpriteMaterial();
        var bare = Fr("bareTree");

        var group = GameObject.Find("YardEdgeProps");
        if (group != null) Undo.DestroyObjectImmediate(group);
        group = new GameObject("YardEdgeProps");
        Undo.RegisterCreatedObjectUndo(group, "Fix World Edge");
        var housesXZ = new[] { new Vector2(-23f, 12f), new Vector2(30f, 27f), new Vector2(23f, 12f) };

        GameObject Spawn(string nm, Vector3 p, Sprite s)
        {
            if (s == null) return null;
            var g = new GameObject(nm); g.transform.SetParent(group.transform, false); g.transform.position = p;
            var sr = g.AddComponent<SpriteRenderer>(); sr.sprite = s; sr.sharedMaterial = mat; sr.sortingOrder = 0;
            g.AddComponent<Billboard>();
            return g;
        }

        for (int i = 0; i < 48; i++)
        {
            float a = Hash01(i * 97 + 5) * Mathf.PI * 2f;
            float r = 42f + Hash01(i * 131 + 11) * 14f;                 // 42..56, inside the r58 boundary
            var pos = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            bool nearHouse = false;
            foreach (var h in housesXZ) if ((new Vector2(pos.x, pos.z) - h).sqrMagnitude < 36f) nearHouse = true;
            if (nearHouse) continue;

            int k = Mathf.FloorToInt(Hash01(i * 211 + 3) * 100f);
            if (k < 46 && bare.Length > 0)                              // ~46% animated bare trees
            {
                var g = Spawn("EdgeTree", pos, bare[0]);
                if (g != null) { var an = g.AddComponent<LoopSpriteAnimator>(); an.frames = bare; an.fps = 1000f / 240f; an.randomStartPhase = true; }
            }
            else
            {
                string nm = k < 60 ? "rock" : k < 72 ? "fallenLog" : k < 82 ? "woodpile" : k < 90 ? "hollowTree" : k < 96 ? "mushSickly" : "acorns";
                Spawn("Edge_" + nm, pos, One(nm));
            }
        }
    }

    // A ring of invisible box colliders at `radius` (tall `height`) so the CharacterController is
    // stopped just inside the mountains and can't walk off the world. Segments overlap so there are
    // no gaps to slip through. No renderer -> invisible in play.
    static void BuildWorldBoundary(float radius, float height)
    {
        var existing = GameObject.Find("WorldBoundary");
        if (existing != null) Undo.DestroyObjectImmediate(existing);
        var root = new GameObject("WorldBoundary");
        Undo.RegisterCreatedObjectUndo(root, "Fix World Edge");

        const int segs = 40;
        float chord = 2f * radius * Mathf.Sin(Mathf.PI / segs);
        var center = new Vector3(0f, height * 0.5f, 0f);
        for (int i = 0; i < segs; i++)
        {
            float ang = (i / (float)segs) * Mathf.PI * 2f;
            var seg = new GameObject("Bound_" + i);
            seg.transform.SetParent(root.transform, false);
            seg.transform.position = new Vector3(Mathf.Cos(ang) * radius, height * 0.5f, Mathf.Sin(ang) * radius);
            seg.transform.LookAt(center);                              // local +Z faces the centre (radial)
            var box = seg.AddComponent<BoxCollider>();
            box.size = new Vector3(chord * 1.25f, height, 1f);        // X tangent (overlapped), Z radial thickness
        }
    }

    // Import boarded_up_tiles.png like the neighbor atlases: Point filter, no compression, no
    // mipmaps (crisp 24-px cells). Returns the loaded texture.
    static Texture2D ImportBoardedTiles()
    {
        var imp = AssetImporter.GetAtPath(BoardedTiles) as TextureImporter;
        if (imp != null)
        {
            bool dirty = imp.filterMode != FilterMode.Point ||
                         imp.textureCompression != TextureImporterCompression.Uncompressed ||
                         imp.mipmapEnabled;
            if (dirty)
            {
                imp.filterMode = FilterMode.Point;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.mipmapEnabled = false;
                imp.SaveAndReimport();
            }
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(BoardedTiles);
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
    // Exterior wrapper: the range-backdrop ridgeline rings + the day/night sky.
    static void BuildMountainBackdrop(Light sun)
    {
        BuildMountainRings();
        BuildSkySystem(sun);
    }

    // The range-backdrop ridgeline rings (no sky). Returns the root so callers can, e.g., make it follow
    // the camera on the long drive. Shared by the Exterior yard and the OutOfTown road.
    static GameObject BuildMountainRings()
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
        return root;
    }

    // The day→night sky (SKY_README): a gradient dome behind the mountains, a sun that arcs and sets,
    // a moon that rises, and a twinkling star field — all driven by SkyController.timeOfDay. Also lets
    // the sky dim the scene's directional light + ambient so the world darkens with it.
    static void BuildSkySystem(Light sun, float domeRadius = 150f)
    {
        var sunSprite = EnsureSkySprite(SunPng);
        var moonSprite = EnsureSkySprite(MoonPng);

        var skyGo = new GameObject("Sky");
        var sky = skyGo.AddComponent<SkyController>();
        sky.domeRadius = domeRadius;   // larger for the long drive so the following mountains stay inside it
        sky.sunSprite = sunSprite;
        sky.moonSprite = moonSprite;
        sky.sunLight = sun;
        sky.glow = false;             // no halo / second sun+moon sprite
        // Sky faces +Z (north): with that basis the tangent runs +X(east)→-X(west), so the sun rises in
        // the EAST and sets in the WEST, and the moon rises in the WEST (nx = 0.70).
        sky.skyYawDeg = 90f;
        sky.dayLengthSeconds = 120f;  // fallback (unused while splitDayNight is on)
        // Night lasts significantly longer than day: ~1 min of day, ~2 min of night.
        sky.splitDayNight = true;
        sky.nightStartT = 0.80f;
        sky.dayDurationSeconds = 60f;
        sky.nightDurationSeconds = 120f;
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

        // Intermittent panting: an AudioSource the dog plays a single pant through while trotting to
        // the player (random gaps) and occasionally on a pet — 2D so it reads as the companion by you.
        var pant = go.AddComponent<AudioSource>();
        pant.playOnAwake = false;
        pant.spatialBlend = 0f;

        var dog = go.AddComponent<DogCompanion>();
        dog.player = player;
        dog.nightmare = nightmare;
        dog.breeds = breeds;
        if (first != null) { dog.idleFrames = first.idle; dog.walkFrames = first.walk; dog.heartFrames = first.heart; }
        dog.fps = 6f;
        dog.pantClip = AssetDatabase.LoadAssetAtPath<AudioClip>(DogPantWav);
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
