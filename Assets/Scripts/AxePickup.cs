using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// The axe-in-stump world pickup (see AXE_STUMP.md). One sits by the player's main cabin: walk up and
/// press <b>E</b> to take it, which unlocks woodcutting (grants the axe to <see cref="AxeChopper"/>).
/// Press <b>E</b> again at the stump to put the axe back, which re-locks woodcutting.
///
/// <para>The art is the 4×3 <c>axe_stump.png</c> sheet (index <c>row*4 + col</c>): <b>cols 0–2</b> are a
/// slow idle glint loop, <b>col 3</b> is the empty stump left behind after the axe is taken; the three
/// <b>rows</b> are the front / side / back views, chosen from where the camera is (paired with a
/// <see cref="Billboard"/> so the quad also turns to face the camera).</para>
///
/// <para>Only one ever exists — extra instances remove themselves — and the taken state persists via
/// <see cref="CharacterStore"/>, so the stump stays empty and the player keeps their axe across doors
/// and reloads.</para>
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class AxePickup : MonoBehaviour
{
    [Header("Sheet (axe_stump.png — 128x96, 32px cells, 4 cols x 3 rows)")]
    public Texture2D sheet;                 // self-wired from the known asset path in the editor

    [Header("Facing (which authored row the camera sees)")]
    [Tooltip("Compass heading the stump's front faces: 0=N, 90=E, 180=S, 270=W. The front row shows when the camera looks from this side.")]
    public float noseYaw = 180f;
    [Tooltip("Fine-tune rotation if the chosen view is one step off.")]
    public float angleOffset = 0f;

    [Header("Pickup")]
    public float pickupRadius = 2.0f;       // how close the player must get before E takes it
    public float idleFps = 4f;              // slow, subtle blade glint
    public string promptText = "Press E to take the axe";
    public string takeMessage = "Took the axe";
    public string returnPromptText = "Press E to put the axe back";
    public string returnMessage = "Put the axe back";

    [Header("Place beside the cabin")]
    [Tooltip("On Start, snap next to the cabin object so the single stump always lands by the main cabin. Turn off to hand-place it.")]
    public bool snapBesideCabin = true;
    public string cabinObjectName = "__CabinShell";
    public Vector3 cabinOffset = new Vector3(3f, 0f, -4f);

    /// <summary>PlayerPrefs flag id for this stump having been claimed (kept separate from the axe flag).</summary>
    public string pickupId = "cabin_axe";

    const int Cell = 32;
    const float Ppu = 32f;
    const int FrontRow = 0, SideRow = 1, BackRow = 2, EmptyCol = 3;

    // Only one axe-in-stump at a time.
    static AxePickup _instance;

    Sprite[] _sprites;   // 12 sliced cells (row*4 + col)
    SpriteRenderer _sr;
    Transform _player;
    Camera _cam;
    int _row, _frame;
    float _t;
    bool _flip, _taken;

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;

        _sr = GetComponent<SpriteRenderer>();
#if UNITY_EDITOR
        if (sheet == null) sheet = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Animation/axe_stump.png");
#endif
        if (sheet != null) BuildSprites();

        _taken = CharacterStore.GetFlag(pickupId);        // already claimed -> start on the empty stump
        if (GetComponent<Billboard>() == null) gameObject.AddComponent<Billboard>();   // face the camera like other props
    }

    void OnDestroy() { if (_instance == this) _instance = null; }

    void Start()
    {
        // Keep the single stump planted by the main cabin. Only moves on the ground plane, and only
        // if the cabin is actually found, so a deliberately hand-placed stump is never teleported away.
        if (!snapBesideCabin) return;
        var cabin = GameObject.Find(cabinObjectName);
        if (cabin != null)
        {
            Vector3 p = cabin.transform.position + cabinOffset;
            transform.position = new Vector3(p.x, transform.position.y, p.z);
        }
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

        // Advance the glint loop only while the axe is sitting in the stump.
        if (!_taken)
        {
            _t += Time.deltaTime;
            if (idleFps > 0f && _t >= 1f / idleFps) { _t = 0f; _frame = (_frame + 1) % 3; }
        }

        if (_player == null)
        {
            var pc = FindAnyObjectByType<PlayerController3D>();
            if (pc != null) _player = pc.transform;
        }
        if (_player == null) return;

        Vector3 a = transform.position; a.y = 0f;
        Vector3 b = _player.position; b.y = 0f;
        if ((a - b).sqrMagnitude > pickupRadius * pickupRadius) return;

        // Walk up and press E to take the axe, or press E again at the stump to put it back.
        if (DialogUI.Instance != null) DialogUI.Instance.ShowPrompt(_taken ? returnPromptText : promptText);
        if (EPressed()) { if (_taken) Return(); else Take(); }
    }

    // Pick the front/side/back row from where the camera views the stump, and set the current cell.
    void LateUpdate()
    {
        if (_sprites == null) return;
        UpdateFacing();
        int col = _taken ? EmptyCol : _frame;
        _sr.sprite = _sprites[_row * 4 + col];
        _sr.flipX = _flip;
    }

    void Take()
    {
        _taken = true;
        _sr.sprite = _sprites[_row * 4 + EmptyCol];               // empty stump, left as decor
        CharacterStore.SetFlag(pickupId, true);                    // this stump stays claimed

        // Grant the axe so woodcutting unlocks. If the chopper isn't in this scene, still persist the
        // flag so it's equipped when the player next controls one.
        var chopper = FindAnyObjectByType<AxeChopper>();
        if (chopper != null) chopper.EquipAxe();
        else CharacterStore.SetFlag(AxeChopper.AxeFlag, true);

        if (DialogUI.Instance != null) DialogUI.Instance.ShowDialog(takeMessage, 2f);
    }

    // Put the axe back into the stump (mirror of Take): restart the glint, drop the claimed flag, and
    // take the axe off the player so woodcutting re-locks. LateUpdate paints the axe-present frame.
    void Return()
    {
        _taken = false;
        _frame = 0; _t = 0f;                                       // restart the idle glint loop
        CharacterStore.SetFlag(pickupId, false);                   // stump is no longer claimed

        var chopper = FindAnyObjectByType<AxeChopper>();
        if (chopper != null) chopper.UnequipAxe();
        else CharacterStore.SetFlag(AxeChopper.AxeFlag, false);

        if (DialogUI.Instance != null) DialogUI.Instance.ShowDialog(returnMessage, 2f);
    }

    void UpdateFacing()
    {
        if (_cam == null) { var c = Camera.main; if (c == null) return; _cam = c; }
        Vector3 d = _cam.transform.position - transform.position; d.y = 0f;
        if (d.sqrMagnitude < 1e-4f) return;

        // Bearing from the stump to the camera (compass 0=N, 90=E), turned into how the front appears.
        float bearing = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        float apparent = Mod(noseYaw - bearing + 180f + angleOffset, 360f);
        if (apparent > 135f && apparent < 225f) { _row = FrontRow; _flip = false; }        // camera in front
        else if (apparent < 45f || apparent > 315f) { _row = BackRow; _flip = false; }      // camera behind
        else { _row = SideRow; _flip = apparent >= 225f; }                                  // side (W mirrored)
    }

    void BuildSprites()
    {
        _sprites = new Sprite[12];
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 4; c++)
            {
                // Texture space is bottom-left origin; row 0 (front) is the TOP strip of the sheet.
                var rect = new Rect(c * Cell, sheet.height - (r + 1) * Cell, Cell, Cell);
                _sprites[r * 4 + c] = Sprite.Create(sheet, rect, new Vector2(0.5f, 0f), Ppu, 0, SpriteMeshType.FullRect);
            }
        _sr.sprite = _sprites[FrontRow * 4 + (_taken ? EmptyCol : 0)];
    }

    static float Mod(float a, float m) { float v = a % m; return v < 0f ? v + m : v; }

    bool EPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }
}
