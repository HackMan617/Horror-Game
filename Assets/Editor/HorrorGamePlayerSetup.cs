using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// One-click (and auto-on-import) setup for the preliminary swordsman sprite:
///   1. slices Assets/Art/Player/Player.png into a 190x171 grid (feet pivot)
///   2. builds idle / walk / run / attack1 / attack2 AnimationClips
///   3. wires an AnimatorController (Speed float + Attack / Attack2 triggers)
///   4. drops a playable "Player" object into the active scene
/// Re-running it rebuilds everything cleanly.
/// </summary>
public static class HorrorGamePlayerSetup
{
    const string Png            = "Assets/Art/Player/Player.png";
    const string OutDir         = "Assets/Animation/Player";
    const string ControllerPath = OutDir + "/Player.controller";

    // Grid produced by sprite-tools/process.py
    const int CellW = 190;
    const int CellH = 171;
    static readonly Vector2 Pivot = new Vector2(0.43684f, 0.04678f); // feet anchor

    struct Anim
    {
        public string name; public int count; public float fps; public bool loop;
        public Anim(string n, int c, float f, bool l) { name = n; count = c; fps = f; loop = l; }
    }

    // Order MUST match the rows top->bottom in Player.png
    static readonly Anim[] Anims =
    {
        new Anim("idle",    5,  8f, true),
        new Anim("walk",    7, 10f, true),
        new Anim("run",     8, 14f, true),
        new Anim("attack1", 5, 12f, false),
        new Anim("attack2", 3, 10f, false),
    };

    // ---------------------------------------------------------------- auto-run
    // Defer to delayCall (fires once the editor is idle and the REAL scene is
    // loaded) instead of polling update() mid-domain-reload, which ran scene
    // mutation against a transient backup scene and threw.
    static int _ticks;

    [InitializeOnLoadMethod]
    static void AutoRun()
    {
        _ticks = 0;
        EditorApplication.delayCall += TryBuild;
    }

    static void TryBuild()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode)
            return; // never touch assets/scene mid-reload or in play mode

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Png);
        if (tex == null)
        {
            if (++_ticks < 50) EditorApplication.delayCall += TryBuild;
            else Debug.LogWarning("[HorrorGame] Player.png not imported. " +
                                  "Run Tools > Horror Game > Build Player Animation once it appears.");
            return;
        }

        // Strict completeness check: a leftover "Player" with no components (e.g.
        // from an earlier failed run, kept alive across reloads by Unity's scene
        // backup) must NOT count as "set up", or we'd never repair it.
        var p = GameObject.Find("Player");
        bool ready = p != null
                     && p.GetComponent<SpriteRenderer>() != null
                     && p.GetComponent<Animator>() != null
                     && p.GetComponent<PlayerController2D>() != null;
        if (ready) return;

        try { Build(); }
        catch (Exception e) { Debug.LogError("[HorrorGame] Auto-build failed: " + e); }
    }

    // ------------------------------------------------------------------- build
    [MenuItem("Tools/Horror Game/Build Player Animation")]
    public static void Build()
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Png);
        if (tex == null)
        {
            Debug.LogError("[HorrorGame] Missing " + Png);
            return;
        }

        SliceSheet(tex.height);
        var sprites = AssetDatabase.LoadAllAssetsAtPath(Png).OfType<Sprite>()
                                   .ToDictionary(s => s.name, s => s);

        EnsureFolder(OutDir);
        var clips = BuildClips(sprites);
        var controller = BuildController(clips);
        SetupSceneObject(controller, sprites["player_idle_0"]);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[HorrorGame] Player animation built. Press Play, then A/D to move, " +
                  "hold Shift to run, J/Space = Attack 1, K = Attack 2. (Ctrl+S to keep the scene.)");
    }

    // ------------------------------------------------------------------- slice
    static void SliceSheet(int texHeight)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(Png);
        importer.textureType         = TextureImporterType.Sprite;
        importer.spriteImportMode    = SpriteImportMode.Multiple;
        importer.filterMode          = FilterMode.Point;          // crisp pixels
        importer.textureCompression  = TextureImporterCompression.Uncompressed;
        importer.spritePixelsPerUnit = 100f;
        importer.mipmapEnabled       = false;
        importer.wrapMode            = TextureWrapMode.Clamp;
        importer.isReadable          = false;

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var dp = factory.GetSpriteEditorDataProviderFromObject(importer);
        dp.InitSpriteEditorDataProvider();

        var rects = new List<SpriteRect>();
        for (int r = 0; r < Anims.Length; r++)
        {
            for (int c = 0; c < Anims[r].count; c++)
            {
                rects.Add(new SpriteRect
                {
                    name      = $"player_{Anims[r].name}_{c}",
                    spriteID  = GUID.Generate(),
                    rect      = new Rect(c * CellW, texHeight - (r + 1) * CellH, CellW, CellH),
                    pivot     = Pivot,
                    alignment = SpriteAlignment.Custom,
                    border    = Vector4.zero,
                });
            }
        }

        dp.SetSpriteRects(rects.ToArray());
        try
        {
            var nameId = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (nameId != null)
                nameId.SetNameFileIdPairs(
                    rects.Select(sr => new SpriteNameFileIdPair(sr.name, sr.spriteID)));
        }
        catch { /* older/newer API variants — slicing still applies */ }

        dp.Apply();
        importer.SaveAndReimport();
    }

    // ------------------------------------------------------------------- clips
    static Dictionary<string, AnimationClip> BuildClips(Dictionary<string, Sprite> sprites)
    {
        var binding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer), path = "", propertyName = "m_Sprite"
        };

        var clips = new Dictionary<string, AnimationClip>();
        foreach (var a in Anims)
        {
            var clip = new AnimationClip { frameRate = a.fps };

            var keys = new ObjectReferenceKeyframe[a.count + 1];
            for (int i = 0; i < a.count; i++)
                keys[i] = new ObjectReferenceKeyframe
                { time = i / a.fps, value = sprites[$"player_{a.name}_{i}"] };
            // terminal key so the last frame holds for a full 1/fps before loop/exit
            keys[a.count] = new ObjectReferenceKeyframe
            { time = a.count / a.fps, value = sprites[$"player_{a.name}_{a.count - 1}"] };

            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

            var s = AnimationUtility.GetAnimationClipSettings(clip);
            s.loopTime = a.loop;
            AnimationUtility.SetAnimationClipSettings(clip, s);

            string path = $"{OutDir}/{a.name}.anim";
            DeleteIfExists(path);
            AssetDatabase.CreateAsset(clip, path);
            clips[a.name] = clip;
        }
        return clips;
    }

    // -------------------------------------------------------------- controller
    static AnimatorController BuildController(Dictionary<string, AnimationClip> clips)
    {
        DeleteIfExists(ControllerPath);
        var ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        ac.AddParameter("Speed",   AnimatorControllerParameterType.Float);
        ac.AddParameter("Attack",  AnimatorControllerParameterType.Trigger);
        ac.AddParameter("Attack2", AnimatorControllerParameterType.Trigger);

        var sm   = ac.layers[0].stateMachine;
        var idle = sm.AddState("Idle");    idle.motion = clips["idle"];
        var walk = sm.AddState("Walk");    walk.motion = clips["walk"];
        var run  = sm.AddState("Run");     run.motion  = clips["run"];
        var atk1 = sm.AddState("Attack1"); atk1.motion = clips["attack1"];
        var atk2 = sm.AddState("Attack2"); atk2.motion = clips["attack2"];
        sm.defaultState = idle;

        Loco(idle.AddTransition(walk), "Speed", AnimatorConditionMode.Greater, 0.1f);
        Loco(walk.AddTransition(idle), "Speed", AnimatorConditionMode.Less,    0.1f);
        Loco(walk.AddTransition(run),  "Speed", AnimatorConditionMode.Greater, 3.5f);
        Loco(run.AddTransition(walk),  "Speed", AnimatorConditionMode.Less,    3.5f);

        AnyTrigger(sm.AddAnyStateTransition(atk1), "Attack");
        AnyTrigger(sm.AddAnyStateTransition(atk2), "Attack2");
        Exit(atk1.AddTransition(idle));
        Exit(atk2.AddTransition(idle));

        EditorUtility.SetDirty(ac);
        return ac;
    }

    static void Loco(AnimatorStateTransition t, string p, AnimatorConditionMode m, float v)
    {
        t.hasExitTime = false; t.exitTime = 0f;
        t.hasFixedDuration = true; t.duration = 0.08f;
        t.AddCondition(m, v, p);
    }

    static void AnyTrigger(AnimatorStateTransition t, string trigger)
    {
        t.hasExitTime = false;
        t.hasFixedDuration = true; t.duration = 0.05f;
        t.canTransitionToSelf = false;
        t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
    }

    static void Exit(AnimatorStateTransition t)
    {
        t.hasExitTime = true; t.exitTime = 0.9f;
        t.hasFixedDuration = true; t.duration = 0.1f;
    }

    // ------------------------------------------------------------------- scene
    static void SetupSceneObject(AnimatorController controller, Sprite firstFrame)
    {
        // Destroy any prior "Player" (incl. a broken leftover) and build fresh, so
        // AddComponent runs on a clean GameObject in this idle context.
        var existing = GameObject.Find("Player");
        if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
        var go = new GameObject("Player");
        go.transform.position = Vector3.zero;

        var sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = firstFrame;
        sr.sortingOrder = 10;

        var an = go.GetComponent<Animator>();
        if (an == null) an = go.AddComponent<Animator>();
        an.runtimeAnimatorController = controller;

        if (go.GetComponent<PlayerController2D>() == null)
            go.AddComponent<PlayerController2D>();

        var cam = Camera.main;
        if (cam == null)
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 3f;
            cam.backgroundColor = Color.black;
            cam.transform.position = new Vector3(0f, 1f, -10f);
        }

        var scene = go.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        if (scene.path != null && scene.path.StartsWith("Assets/"))
            EditorSceneManager.SaveScene(scene);   // persist so it survives reloads
        Selection.activeGameObject = go;
    }

    // ------------------------------------------------------------------ helpers
    static void DeleteIfExists(string path)
    {
        if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
            AssetDatabase.DeleteAsset(path);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        var leaf   = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
