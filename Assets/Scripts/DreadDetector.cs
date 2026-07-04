using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Dread Detector HUD face readout (Assets/Animation/DREAD.md). A vitals-monitor portrait that
/// degrades through six dread levels (0 Dormant · 1 Uneasy · 2 Worried · 3 Anxious · 4 Parasitic ·
/// 5 Nightmare), animating slowly when calm and faster with more frames as dread rises.
///
/// Driven off the 576×576 <c>dread_master_atlas</c> (144 sprites, 12×12): 3 variant columns
/// (parasite=0, melt=1, fracture=2) × 2 body rows (male=0, female=1) × 6 levels × 4 frames. By
/// default it picks the body to match the player (<see cref="CharacterStore"/>) and reads the dread
/// value from <see cref="DreadDirector"/>. It snaps (no lerp) when the level jumps, so the face
/// lurches into the worse state.
/// </summary>
[RequireComponent(typeof(Image))]
public class DreadDetector : MonoBehaviour
{
    [Tooltip("The 144 sliced master-atlas frames (dread_master_atlas_0 … _143, 12×12).")]
    public Sprite[] atlas;
    public Image image;

    [Header("Skin")]
    [Tooltip("parasite = 0, melt = 1, fracture = 2. Fixed per area/chapter.")]
    [Range(0, 2)] public int variant = 2;
    [Tooltip("Match the player's body from CharacterStore. Off = use the explicit body below.")]
    public bool useCharacterBody = true;
    [Tooltip("0 = male, 1 = female (used only when 'useCharacterBody' is off).")]
    [Range(0, 1)] public int body = 1;

    [Header("Source")]
    [Tooltip("Read the shared DreadDirector value each frame. Off = drive it yourself via SetDread/SetLevel.")]
    public bool readDreadDirector = true;

    // Per-level: usable frames, and seconds per frame (escalates with dread) — from DREAD.md.
    static readonly int[]   FrameCount = { 2, 2, 2, 3, 3, 4 };
    static readonly float[] FrameSec   = { 0.52f, 0.44f, 0.36f, 0.22f, 0.16f, 0.11f };

    int level, frame;
    float timer;

    void Reset() => image = GetComponent<Image>();

    void Awake()
    {
        if (image == null) image = GetComponent<Image>();
        if (useCharacterBody) body = (int)CharacterStore.Load().body;   // BodyType: Male=0, Female=1
    }

    /// <summary>Feed dread as 0..1; maps to the six discrete levels.</summary>
    public void SetDread(float dread01) => SetLevel(Mathf.RoundToInt(Mathf.Clamp01(dread01) * 5f));

    /// <summary>Set the level directly (0..5). Snaps the animation to the new level.</summary>
    public void SetLevel(int lvl)
    {
        int c = Mathf.Clamp(lvl, 0, 5);
        if (c == level) return;
        level = c; frame = 0; timer = 0f;   // snap, don't lerp
    }

    // Master-atlas index: variantCol*4+frame across, bodyRow*6+level down, row-major over 12 cols.
    int AtlasIndex(int lvl, int fr) => (body * 6 + lvl) * 12 + (variant * 4 + fr);

    void Update()
    {
        if (readDreadDirector) SetDread(DreadDirector.Value01);
        if (atlas == null || atlas.Length < 144 || image == null) return;

        timer += Time.deltaTime;
        if (timer >= FrameSec[level])
        {
            timer -= FrameSec[level];
            frame = (frame + 1) % FrameCount[level];
        }
        image.sprite = atlas[AtlasIndex(level, frame)];
    }
}
