using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Game.Characters;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Woodcutting on the player rig (see CHOPPING.md). Walk up to a <see cref="ChoppableTree"/> and a
/// prompt appears; press <b>E</b> to swing — this works in <b>both first and third person</b>.
/// <list type="bullet">
/// <item><b>Third person</b> — plays the recolored chop pose on the player billboard (male vs female
/// from the saved <see cref="CharacterLook"/>), bite on cell 2. The pose follows the camera side, so
/// with the camera behind the player you see the <c>chop_male_back</c> / <c>chop_female_back</c> back
/// view, and the front <c>chop_male</c> / <c>chop_female</c> sheets when you hold C to look at them.</item>
/// <item><b>First person</b> — the <c>fp_axe</c> viewmodel is raised in the bottom-right of the screen
/// <b>only when you're at a choppable tree</b> (not carried around the whole time), and swings
/// (bite on cell 3) when you chop.</item>
/// </list>
/// After felling a tree, walking over the dropped log shows the <b>hold-wood</b> carry pose (cell 4,
/// also facing-aware) for a short beat before returning to walk/idle — see <see cref="ShowCarry"/>.
/// The first-person viewmodel canvas is built at runtime, so nothing needs wiring in the scene. The
/// shirt/sleeve red is recolored to the player's chosen shirt exactly as the base character is.
/// </summary>
public class AxeChopper : MonoBehaviour
{
    [Header("Master sheets (Read/Write enabled textures)")]
    public Texture2D chopMale;      // chop_male.png        160x32, 5 cells (0-3 swing, 4 hold-wood) — front
    public Texture2D chopFemale;    // chop_female.png      160x32, 5 cells — front
    public Texture2D chopMaleBack;  // chop_male_back.png   160x32, 5 cells — camera-behind view
    public Texture2D chopFemaleBack;// chop_female_back.png 160x32, 5 cells — camera-behind view
    public Texture2D fpAxe;         // fp_axe.png           640x128, 5 cells (0 Ready .. 4 Recoil)

    [Header("Carry-walk sheets (walking while holding a felled log — Read/Write enabled)")]
    public Texture2D carryWalkMale;       // carry_walk_male.png        128x32, 4-frame walk cycle — front
    public Texture2D carryWalkFemale;     // carry_walk_female.png      128x32, 4 cells — front
    public Texture2D carryWalkMaleBack;   // carry_walk_male_back.png   128x32, 4 cells — camera-behind view
    public Texture2D carryWalkFemaleBack; // carry_walk_female_back.png 128x32, 4 cells — camera-behind view

    [Header("Scene refs")]
    public CameraRig cameraRig;
    public CharacterBillboardAnimator bodyAnim;    // its SpriteRenderer shows the third-person pose

    [Header("Tuning")]
    [Tooltip("How close (metres, on the ground plane) the player must be to a tree to chop it.")]
    public float reach = 3.0f;
    public float swingFps = 11f;
    public float fpSwingFps = 12f;
    public string promptText = "Press E to chop";
    [Tooltip("How long the hold-wood carry pose stays up after picking up a log, before the player returns to normal walk/idle.")]
    public float carrySeconds = 2f;
    [Tooltip("Frame rate of the carry-walk cycle shown while the player moves holding the log.")]
    public float carryWalkFps = 9f;

    [Header("Axe gating")]
    [Tooltip("If off, the player can't chop until they take the axe-in-stump by the cabin (AxePickup calls EquipAxe). The equipped state persists across scenes/sessions.")]
    public bool startWithAxe = false;

    /// <summary>PlayerPrefs flag id set once the player has picked up the axe.</summary>
    public const string AxeFlag = "has_axe";

    // The player billboard is framed at PPU 16 with a feet pivot (see CharacterLookApplier); the chop
    // cells are the same 32x32 base pixels, so they MUST use the same framing to match the walk sprite.
    const float BodyPpu = 16f;
    static readonly Vector2 BodyPivot = new Vector2(0.5f, 0.09f);

    Sprite[] _tpFrames;      // 5 recolored chop cells — front view
    Sprite[] _tpFramesBack;  // 5 recolored chop cells — camera-behind (back) view
    Sprite[] _carryFrames;      // 4 recolored carry-walk cells — front view
    Sprite[] _carryFramesBack;  // 4 recolored carry-walk cells — camera-behind (back) view
    Sprite[] _fpFrames;      // 5 recolored fp_axe cells
    PlayerController3D _player;  // read for movement, to pick carry-walk vs static hold-wood pose
    float _carryWalkT;          // carry-walk cycle clock
    SpriteRenderer _bodySr;
    Image _fpImage;          // runtime-built first-person viewmodel
    bool _busy;
    bool _carrying;          // true while the player holds a felled log (static hold-wood carry pose)
    float _carryT;           // seconds left on the carry pose before it releases
    bool _hasAxe;            // gates all chopping — granted by taking the axe-in-stump (EquipAxe)

    void Awake()
    {
#if UNITY_EDITOR
        // Self-wire from the known asset paths so the viewmodel works without a manual Inspector hookup.
        if (chopMale == null)     chopMale       = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Animation/chop_male.png");
        if (chopFemale == null)   chopFemale     = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Animation/chop_female.png");
        if (chopMaleBack == null) chopMaleBack   = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Animation/chop_male_back.png");
        if (chopFemaleBack == null) chopFemaleBack = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Animation/chop_female_back.png");
        if (fpAxe == null)        fpAxe          = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Animation/fp_axe.png");
        if (carryWalkMale == null)       carryWalkMale       = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Animation/carry_walk_male.png");
        if (carryWalkFemale == null)     carryWalkFemale     = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Animation/carry_walk_female.png");
        if (carryWalkMaleBack == null)   carryWalkMaleBack   = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Animation/carry_walk_male_back.png");
        if (carryWalkFemaleBack == null) carryWalkFemaleBack = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Animation/carry_walk_female_back.png");
#endif
        var look = CharacterStore.Load();
        bool female = look.body == BodyType.Female;
        Texture2D bodyTex     = (female && chopFemale != null)     ? chopFemale     : chopMale;
        Texture2D bodyTexBack = (female && chopFemaleBack != null) ? chopFemaleBack : chopMaleBack;
        if (bodyTex != null)     _tpFrames     = SliceRecolored(bodyTex, look, 32, 32, BodyPpu, BodyPivot);
        if (bodyTexBack != null) _tpFramesBack = SliceRecolored(bodyTexBack, look, 32, 32, BodyPpu, BodyPivot);
        if (fpAxe != null)       _fpFrames     = SliceRecolored(fpAxe, look, 128, 128, 128f, new Vector2(0.5f, 0.5f));

        // Same base pixels, same framing as the chop sheets — recolor to the chosen look so the
        // carried-log walk matches the customized character in both front and back views.
        Texture2D carryTex     = (female && carryWalkFemale != null)     ? carryWalkFemale     : carryWalkMale;
        Texture2D carryTexBack = (female && carryWalkFemaleBack != null) ? carryWalkFemaleBack : carryWalkMaleBack;
        if (carryTex != null)     _carryFrames     = SliceRecolored(carryTex, look, 32, 32, BodyPpu, BodyPivot);
        if (carryTexBack != null) _carryFramesBack = SliceRecolored(carryTexBack, look, 32, 32, BodyPpu, BodyPivot);

        if (bodyAnim != null) _bodySr = bodyAnim.GetComponent<SpriteRenderer>();
        _player = (bodyAnim != null ? bodyAnim.player : null) ?? FindAnyObjectByType<PlayerController3D>();
        _hasAxe = startWithAxe || CharacterStore.GetFlag(AxeFlag);
        BuildFpViewmodel();
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

        bool fp = cameraRig != null && cameraRig.IsFirstPerson;

        // A swing coroutine owns the sprite / viewmodel while it runs — leave both alone until it ends.
        if (_busy) return;

        // Hold the carry pose (facing-aware) for a beat after picking up a log, then release it back
        // to the normal walk/idle animator.
        if (_carrying)
        {
            _carryT -= Time.deltaTime;
            if (_carryT <= 0f) ShowCarry(false);
            else ApplyCarryPose();
        }

        // No axe, no woodcutting — the viewmodel stays hidden and trees give no prompt until the
        // player takes the axe from the stump by the cabin (see AxePickup / EquipAxe).
        if (!_hasAxe)
        {
            if (_fpImage != null) _fpImage.gameObject.SetActive(false);
            return;
        }

        var target = NearestTree();

        // The first-person axe is only raised when the player is actually at a choppable tree —
        // it is not carried in-hand the whole time they're in first person.
        if (_fpImage != null) _fpImage.gameObject.SetActive(fp && _fpFrames != null && target != null);

        if (target == null) return;

        if (DialogUI.Instance != null) DialogUI.Instance.ShowPrompt(promptText);
        if (EPressed()) StartCoroutine(fp ? SwingFP(target) : SwingTP(target));
    }

    /// <summary>
    /// Grant the axe so the player can chop, and persist it. Called by <see cref="AxePickup"/> when the
    /// player takes the axe out of the stump; harmless to call again if they already have it.
    /// </summary>
    public void EquipAxe()
    {
        _hasAxe = true;
        CharacterStore.SetFlag(AxeFlag, true);
    }

    /// <summary>
    /// Take the axe back off the player (mirror of <see cref="EquipAxe"/>): re-locks chopping and clears
    /// the persisted flag. Called by <see cref="AxePickup"/> when the player returns the axe to the stump.
    /// </summary>
    public void UnequipAxe()
    {
        _hasAxe = false;
        CharacterStore.SetFlag(AxeFlag, false);
    }

    // Closest standing choppable tree within reach, measured on the ground plane.
    ChoppableTree NearestTree()
    {
        ChoppableTree best = null; float bestSq = reach * reach;
        Vector3 me = transform.position; me.y = 0f;
        var list = ChoppableTree.Active;
        for (int i = 0; i < list.Count; i++)
        {
            var t = list[i];
            if (t == null || !t.Available) continue;
            Vector3 p = t.transform.position; p.y = 0f;
            float d = (p - me).sqrMagnitude;
            if (d < bestSq) { best = t; bestSq = d; }
        }
        return best;
    }

    // Third person: swing cells 0->1->2->3 on the player billboard, bite on cell 2. Uses the back sheet
    // when the camera is behind the player (the usual case) and the front sheet when looking at them.
    IEnumerator SwingTP(ChoppableTree target)
    {
        _busy = true;
        var frames = SelectTpFrames();
        if (frames != null && _bodySr != null && bodyAnim != null)
        {
            bodyAnim.suspended = true;                    // stop the walk/idle animator overwriting the pose
            for (int i = 0; i < 4; i++)
            {
                _bodySr.sprite = frames[i];
                if (i == 2 && target != null) target.Chop();
                yield return new WaitForSeconds(1f / swingFps);
            }
            // If we're carrying a log, drop back into the hold pose; otherwise hand control back to the
            // walk/idle animator. Update() re-applies the carry pose next frame either way.
            if (_carrying) ApplyCarryPose();
            else bodyAnim.suspended = false;
        }
        else if (target != null) target.Chop();
        _busy = false;
    }

    // Back sheet when the camera is behind the player (default), front sheet when it's looking at them.
    // Falls back to whichever set exists if only one was wired.
    Sprite[] SelectTpFrames()
    {
        bool facing = bodyAnim != null && bodyAnim.FacingCamera;
        if (!facing && _tpFramesBack != null) return _tpFramesBack;
        return _tpFrames ?? _tpFramesBack;
    }

    // Back carry-walk sheet when the camera is behind the player (default), front when looking at them.
    Sprite[] SelectCarryFrames()
    {
        bool facing = bodyAnim != null && bodyAnim.FacingCamera;
        if (!facing && _carryFramesBack != null) return _carryFramesBack;
        return _carryFrames ?? _carryFramesBack;
    }

    // Drives the billboard while a felled log is held: the facing-aware 4-frame carry-walk cycle when
    // the player moves, and the static hold-wood pose (chop cell 4) when they stand still. Both sheets
    // are recolored to the chosen look, so the carried-log character matches front and back.
    void ApplyCarryPose()
    {
        if (_bodySr == null || bodyAnim == null) return;
        bodyAnim.suspended = true;                        // the carry pose overrides walk/idle

        bool moving = _player != null && _player.MoveInput.sqrMagnitude > 0.01f;
        var walk = SelectCarryFrames();
        if (moving && walk != null && walk.Length > 0)
        {
            _carryWalkT += Time.deltaTime * carryWalkFps;
            _bodySr.sprite = walk[((int)_carryWalkT) % walk.Length];
            return;
        }

        _carryWalkT = 0f;                                 // restart the cycle on the next step
        var hold = SelectTpFrames();
        if (hold != null && hold.Length >= 5) _bodySr.sprite = hold[4];   // static hold-wood pose
    }

    /// <summary>
    /// Enter/leave the "carrying a felled log" state. While carrying, the player billboard plays the
    /// facing-aware carry-walk cycle when moving and holds the static hold-wood pose (cell 4) when
    /// still, for <see cref="carrySeconds"/> before releasing back to the walk/idle animator. Both the
    /// carry-walk and hold sheets are recolored to the chosen look. Called by <see cref="LogPickup"/>.
    /// </summary>
    public void ShowCarry(bool on)
    {
        _carrying = on;
        if (on) { _carryT = carrySeconds; ApplyCarryPose(); }
        else if (bodyAnim != null) bodyAnim.suspended = false;
    }

    // First person: sweep cells 0->1->2->3->4 on the viewmodel, bite on cell 3, rest on Ready.
    IEnumerator SwingFP(ChoppableTree target)
    {
        _busy = true;
        if (_fpFrames != null && _fpImage != null)
        {
            for (int i = 0; i <= 4; i++)
            {
                _fpImage.sprite = _fpFrames[i];
                if (i == 3 && target != null) target.Chop();
                yield return new WaitForSeconds(1f / fpSwingFps);
            }
            _fpImage.sprite = _fpFrames[0];               // rest on Ready
        }
        else if (target != null) target.Chop();
        _busy = false;
    }

    // Builds the bottom-right first-person axe viewmodel on its own screen-space canvas.
    void BuildFpViewmodel()
    {
        if (_fpFrames == null || _fpFrames.Length == 0) return;

        // Clear any stale viewmodel canvas left in the scene (e.g. an earlier authoring pass) so we
        // never end up with a second, always-on axe overlay.
        var stale = GameObject.Find("FP_AxeCanvas");
        if (stale != null) Destroy(stale);

        var canvasGo = new GameObject("FP_AxeCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var imgGo = new GameObject("FP_Axe");
        imgGo.transform.SetParent(canvasGo.transform, false);
        _fpImage = imgGo.AddComponent<Image>();
        _fpImage.raycastTarget = false;
        _fpImage.preserveAspect = true;
        _fpImage.sprite = _fpFrames[0];
        var rt = _fpImage.rectTransform;
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.sizeDelta = new Vector2(760f, 760f);           // fist off the corner, blade reaching centre
        rt.anchoredPosition = new Vector2(130f, -90f);
        _fpImage.gameObject.SetActive(false);             // shown only in first person
    }

    bool EPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }

    // ---------- recolor (shirt = customization) + slice ----------
    static Sprite[] SliceRecolored(Texture2D master, CharacterLook look, int fw, int fh, float ppu, Vector2 pivot)
    {
        Texture2D rec = CharacterPalette.Recolor(master, look);   // skin/hair/eyes/pants + shirt base & shadow
        PatchExtraShirtShadows(rec, look);                        // #983018 shadow variant + deep sleeve shadow
        return CharacterPalette.Slice(rec, fw, fh, ppu, pivot);
    }

    // CharacterPalette maps the shirt base (#d83030) and shadow (#982018). The chop/fp sheets also
    // carry a slightly different shadow (#983018) and a deep sleeve shadow (#6a1e14) the base map
    // doesn't know — remap those to the chosen shirt so no red is left on a recolored sleeve.
    static void PatchExtraShirtShadows(Texture2D tex, CharacterLook look)
    {
        if (tex == null || !tex.isReadable) return;
        var shirt = CharacterPalette.Shirt[Mathf.Clamp(look.shirt, 0, CharacterPalette.Shirt.Length - 1)];
        Color32 sshadow = Hex(shirt.shadowHex);
        Color32 sdeep   = Mul(Hex(shirt.baseHex), 0.5f);
        var srcShadowAlt = new Color32(152, 48, 24, 255);   // #983018
        var srcDeep      = new Color32(106, 30, 20, 255);   // #6a1e14
        var px = tex.GetPixels32();
        bool changed = false;
        for (int i = 0; i < px.Length; i++)
        {
            if (px[i].a < 200) continue;
            if (Same(px[i], srcShadowAlt)) { sshadow.a = px[i].a; px[i] = sshadow; changed = true; }
            else if (Same(px[i], srcDeep)) { sdeep.a = px[i].a; px[i] = sdeep; changed = true; }
        }
        if (changed) { tex.SetPixels32(px); tex.Apply(); }
    }

    static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b;
    static Color32 Mul(Color32 c, float f) => new Color32((byte)(c.r * f), (byte)(c.g * f), (byte)(c.b * f), 255);
    static Color32 Hex(string h) { ColorUtility.TryParseHtmlString(h, out Color c); return (Color32)c; }
}
