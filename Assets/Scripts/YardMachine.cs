using UnityEngine;

/// <summary>
/// One of Robert's yard machines (dish, CRT, server tower, scope, hacksaw, cables, battery). Loops its
/// short robotic idle strip on a SpriteRenderer at the machine's own rate so the yard never ticks in
/// lockstep. Holds BOTH the day and the "wrong" nightmare frame sets and swaps between them on the
/// same dread flag as Robert, the dog, the mountain and the houses — day now, nightmare wired for later
/// (drive DreadProgress past the threshold to turn the yard wrong). Wander/creep is not handled here.
/// See Assets/Animation/ROBERT_PROPS.md.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class YardMachine : MonoBehaviour
{
    public Sprite[] dayFrames;
    public Sprite[] nightFrames;
    [Tooltip("Milliseconds per idle frame (per-machine so the yard doesn't pulse in sync).")]
    public float frameMs = 300f;

    [Header("Dread flag (same source as Robert / dog / houses)")]
    [Range(0f, 1f)] public float DreadProgress = 0f;
    public float nightmareThreshold = 0.5f;

    public bool randomStartPhase = true;   // stagger instances so strips are out of phase

    SpriteRenderer _sr;
    float _t;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        if (randomStartPhase) _t = Random.value * 10000f;
    }

    Sprite[] Active()
    {
        bool nm = DreadProgress >= nightmareThreshold && nightFrames != null && nightFrames.Length > 0;
        return nm ? nightFrames : dayFrames;
    }

    void Update()
    {
        var frames = Active();
        if (frames == null || frames.Length == 0) return;
        _t += Time.deltaTime * 1000f;
        int i = frameMs > 0f ? ((int)(_t / frameMs)) % frames.Length : 0;
        _sr.sprite = frames[i];
    }
}
