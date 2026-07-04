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
/// <item><b>Third person</b> — plays the recolored <c>chop_male</c> / <c>chop_female</c> pose on the
/// player billboard (male vs female from the saved <see cref="CharacterLook"/>), bite on cell 2.</item>
/// <item><b>First person</b> — the <c>fp_axe</c> viewmodel is held in the bottom-right of the screen
/// the whole time you're in first person, and swings (bite on cell 3) when you chop.</item>
/// </list>
/// The first-person viewmodel canvas is built at runtime, so nothing needs wiring in the scene. The
/// shirt/sleeve red is recolored to the player's chosen shirt exactly as the base character is.
/// </summary>
public class AxeChopper : MonoBehaviour
{
    [Header("Master sheets (Read/Write enabled textures)")]
    public Texture2D chopMale;      // chop_male.png   160x32, 5 cells (0-3 swing, 4 hold-wood)
    public Texture2D chopFemale;    // chop_female.png 160x32, 5 cells
    public Texture2D fpAxe;         // fp_axe.png      640x128, 5 cells (0 Ready .. 4 Recoil)

    [Header("Scene refs")]
    public CameraRig cameraRig;
    public CharacterBillboardAnimator bodyAnim;    // its SpriteRenderer shows the third-person pose

    [Header("Tuning")]
    [Tooltip("How close (metres, on the ground plane) the player must be to a tree to chop it.")]
    public float reach = 3.0f;
    public float swingFps = 11f;
    public float fpSwingFps = 12f;
    public string promptText = "Press E to chop";

    // The player billboard is framed at PPU 16 with a feet pivot (see CharacterLookApplier); the chop
    // cells are the same 32x32 base pixels, so they MUST use the same framing to match the walk sprite.
    const float BodyPpu = 16f;
    static readonly Vector2 BodyPivot = new Vector2(0.5f, 0.09f);

    Sprite[] _tpFrames;   // 5 recolored chop cells
    Sprite[] _fpFrames;   // 5 recolored fp_axe cells
    SpriteRenderer _bodySr;
    Image _fpImage;       // runtime-built first-person viewmodel
    bool _busy;

    void Awake()
    {
#if UNITY_EDITOR
        // Self-wire from the known asset paths so the viewmodel works without a manual Inspector hookup.
        if (chopMale == null)   chopMale   = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Animation/chop_male.png");
        if (chopFemale == null) chopFemale = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Animation/chop_female.png");
        if (fpAxe == null)      fpAxe      = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Animation/fp_axe.png");
#endif
        var look = CharacterStore.Load();
        Texture2D bodyTex = (look.body == BodyType.Female && chopFemale != null) ? chopFemale : chopMale;
        if (bodyTex != null) _tpFrames = SliceRecolored(bodyTex, look, 32, 32, BodyPpu, BodyPivot);
        if (fpAxe != null)   _fpFrames = SliceRecolored(fpAxe, look, 128, 128, 128f, new Vector2(0.5f, 0.5f));

        if (bodyAnim != null) _bodySr = bodyAnim.GetComponent<SpriteRenderer>();
        BuildFpViewmodel();
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

        bool fp = cameraRig != null && cameraRig.IsFirstPerson;
        if (_fpImage != null) _fpImage.gameObject.SetActive(fp && _fpFrames != null);   // held while in first person

        if (_busy) return;

        var target = NearestTree();
        if (target == null) return;

        if (DialogUI.Instance != null) DialogUI.Instance.ShowPrompt(promptText);
        if (EPressed()) StartCoroutine(fp ? SwingFP(target) : SwingTP(target));
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

    // Third person: swing cells 0->1->2->3 on the player billboard, bite on cell 2.
    IEnumerator SwingTP(ChoppableTree target)
    {
        _busy = true;
        if (_tpFrames != null && _bodySr != null && bodyAnim != null)
        {
            bodyAnim.suspended = true;                    // stop the walk/idle animator overwriting the pose
            for (int i = 0; i < 4; i++)
            {
                _bodySr.sprite = _tpFrames[i];
                if (i == 2 && target != null) target.Chop();
                yield return new WaitForSeconds(1f / swingFps);
            }
            bodyAnim.suspended = false;
        }
        else if (target != null) target.Chop();
        _busy = false;
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
