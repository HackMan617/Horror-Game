using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A fellable spruce driven by axe hits (the <c>tree_chop</c> sheet, see CHOPPING.md / REDWOOD.md).
/// Eight stages: 0 intact → 1-3 deepening notch → 4-7 auto-played fall. Each connecting swing calls
/// <see cref="Chop"/>; the notch deepens one stage every <see cref="hitsPerStage"/> hits, and once past
/// the deep cut the tree topples, then <b>disappears and drops a log pickup</b> in its place.
/// Registers itself in <see cref="Active"/> so the player's <see cref="AxeChopper"/> can find nearby
/// trees to prompt against.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class ChoppableTree : MonoBehaviour
{
    [Tooltip("8 sliced stages of tree_chop (0 intact .. 7 felled).")]
    public Sprite[] stages;
    [Tooltip("Axe swings needed to deepen the notch by one stage.")]
    public int hitsPerStage = 3;
    [Tooltip("Seconds per frame of the automatic fall animation (stages 4->7).")]
    public float fallFrameSec = 0.18f;
    [Tooltip("Log pickup spawned when the tree finishes falling (log_pickup).")]
    public GameObject logPickupPrefab;

    /// <summary>Every live, standing choppable tree — the player's axe searches this for a target.</summary>
    public static readonly List<ChoppableTree> Active = new List<ChoppableTree>();

    SpriteRenderer _sr;
    Collider _trunk;
    int _stage, _hits;
    bool _falling, _felled;

    /// <summary>Standing and choppable (not mid-fall, not gone).</summary>
    public bool Available => !_falling && !_felled;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _trunk = GetComponent<Collider>();
        if (stages != null && stages.Length > 0) _sr.sprite = stages[0];
    }

    void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
    void OnDisable() { Active.Remove(this); }

    /// <summary>Land one axe hit on this tree (called from the swing's impact frame).</summary>
    public void Chop()
    {
        if (!Available || stages == null || stages.Length < 8) return;

        if (_stage < 3)                                   // deepen the notch, chips fly
        {
            if (++_hits >= hitsPerStage) { _hits = 0; _sr.sprite = stages[++_stage]; }
        }
        else                                              // past the heartwood -> it goes
        {
            StartCoroutine(Fall());
        }
    }

    IEnumerator Fall()
    {
        _falling = true;
        if (_trunk != null) _trunk.enabled = false;       // stop blocking the player as it comes down
        for (int s = 4; s <= 7; s++) { _sr.sprite = stages[s]; yield return new WaitForSeconds(fallFrameSec); }
        _felled = true;
        yield return new WaitForSeconds(0.2f);            // a beat resting felled before it clears

        if (logPickupPrefab != null)
        {
            var log = Instantiate(logPickupPrefab, transform.position, Quaternion.identity);
            log.SetActive(true);
        }
        Destroy(gameObject);                              // the tree disappears, leaving the log
    }
}
