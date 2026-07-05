using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A fellable spruce built from the <c>tree_chop</c> sheet (896x544 — 112x272 cells, 8 cols x 2 rows,
/// see CHOPPING.md / REDWOOD.md):
/// <list type="bullet">
/// <item><b>Bottom row</b> is an 8-frame <b>idle sway</b> that loops while the tree stands untouched.</item>
/// <item><b>Top row</b> is the 8 discrete <b>chop/fall stages</b>: 0 intact → 1-3 deepening notch →
/// 4-7 auto-played fall.</item>
/// </list>
/// The moment the player lands the first notch the idle sway stops and the tree holds its wound; each
/// connecting swing calls <see cref="Chop"/> and deepens the notch one stage every
/// <see cref="hitsPerStage"/> hits. Past the deep cut it topples, disappears and drops a log pickup.
/// Both rows are sliced from the texture at runtime (like <see cref="AxeChopper"/>), so nothing needs
/// hand-wiring per instance. Registers in <see cref="Active"/> so the player's <see cref="AxeChopper"/>
/// can find nearby trees to prompt against.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class ChoppableTree : MonoBehaviour
{
    [Header("Sheet (tree_chop.png — 896x544, 112x272 cells, 8 cols x 2 rows)")]
    [Tooltip("Top row = chop/fall stages, bottom row = idle sway. Self-wired from the asset path in the editor.")]
    public Texture2D sheet;
    [Tooltip("Fallback chop stages (0 intact .. 7 felled) used only if no sheet is wired.")]
    public Sprite[] stages;

    [Header("Tuning")]
    [Tooltip("Axe swings needed to deepen the notch by one stage.")]
    public int hitsPerStage = 3;
    [Tooltip("Frame rate of the standing idle sway (bottom row). 0 = hold a static tree.")]
    public float idleFps = 4f;
    [Tooltip("Seconds per frame of the automatic fall animation (stages 4->7).")]
    public float fallFrameSec = 0.18f;
    [Tooltip("Log pickup spawned when the tree finishes falling (log_pickup).")]
    public GameObject logPickupPrefab;

    /// <summary>Every live, standing choppable tree — the player's axe searches this for a target.</summary>
    public static readonly List<ChoppableTree> Active = new List<ChoppableTree>();

    // Sheet geometry — must match the tree_chop.png import (PPU 32, feet pivot) so the runtime-sliced
    // sprites are the exact size/pivot of the hand-placed trees.
    const int Cols = 8;
    const int CellW = 112, CellH = 272;
    const float Ppu = 32f;
    static readonly Vector2 Pivot = new Vector2(0.5f, 0.051470588f);

    Sprite[] _chop;             // 8 chop/fall stages (top row of the sheet)
    Sprite[] _idle;             // 8 idle sway frames (bottom row)
    SpriteRenderer _sr;
    Collider _trunk;
    int _stage, _hits, _idleFrame;
    float _idleT;
    bool _falling, _felled;

    /// <summary>Standing and choppable (not mid-fall, not gone).</summary>
    public bool Available => !_falling && !_felled;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _trunk = GetComponent<Collider>();
#if UNITY_EDITOR
        if (sheet == null) sheet = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Animation/tree_chop.png");
#endif
        BuildSprites();

        if (_idle != null && _idle.Length > 0) _sr.sprite = _idle[0];        // start on the idle loop
        else if (_chop != null && _chop.Length > 0) _sr.sprite = _chop[0];
        else if (stages != null && stages.Length > 0) _sr.sprite = stages[0];
    }

    void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
    void OnDisable() { Active.Remove(this); }

    void Update()
    {
        // Idle sway plays only while the tree stands untouched (stage 0). Once notched it holds its
        // wound; while falling/felled the Fall coroutine owns the sprite.
        if (_idle == null || _idle.Length == 0 || idleFps <= 0f) return;
        if (_stage != 0 || _falling || _felled) return;

        _idleT += Time.deltaTime;
        if (_idleT >= 1f / idleFps)
        {
            _idleT = 0f;
            _idleFrame = (_idleFrame + 1) % _idle.Length;
            _sr.sprite = _idle[_idleFrame];
        }
    }

    /// <summary>Land one axe hit on this tree (called from the swing's impact frame).</summary>
    public void Chop()
    {
        if (!Available) return;
        var chop = _chop ?? stages;
        if (chop == null || chop.Length < 8) return;

        if (_stage < 3)                                   // deepen the notch (this ends the idle sway)
        {
            if (++_hits >= hitsPerStage) { _hits = 0; _sr.sprite = chop[++_stage]; }
        }
        else                                              // past the heartwood -> it goes
        {
            StartCoroutine(Fall());
        }
    }

    IEnumerator Fall()
    {
        var chop = _chop ?? stages;
        _falling = true;
        if (_trunk != null) _trunk.enabled = false;       // stop blocking the player as it comes down
        for (int s = 4; s <= 7; s++) { _sr.sprite = chop[s]; yield return new WaitForSeconds(fallFrameSec); }
        _felled = true;
        yield return new WaitForSeconds(0.2f);            // a beat resting felled before it clears

        if (logPickupPrefab != null)
        {
            var log = Instantiate(logPickupPrefab, transform.position, Quaternion.identity);
            log.SetActive(true);
        }
        Destroy(gameObject);                              // the tree disappears, leaving the log
    }

    // Slice both rows straight from the texture. Top image row (chop) is the UPPER texture half;
    // bottom image row (idle) is the LOWER half — texture space is bottom-left origin.
    void BuildSprites()
    {
        if (sheet == null) return;
        _chop = SliceRow(0);   // top of the image  -> chop/fall stages
        _idle = SliceRow(1);   // bottom of the image -> idle sway
    }

    Sprite[] SliceRow(int row)
    {
        var arr = new Sprite[Cols];
        int y = sheet.height - (row + 1) * CellH;
        for (int c = 0; c < Cols; c++)
            arr[c] = Sprite.Create(sheet, new Rect(c * CellW, y, CellW, CellH), Pivot, Ppu, 0, SpriteMeshType.FullRect);
        return arr;
    }
}
