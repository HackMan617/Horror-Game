// WindowWatcher.cs — the bathroom window's scare: two glowing red eyes press against the frosted
// glass while the player showers, a body-shaped darkening behind them and breath fog blooming
// between, and then they are simply gone. There is no fade out. It is not there between blinks.
//
// Intermittent by design — it must never become reliable, or it stops working. ShowerStall owns
// the rolls (rare, and only while the player is actually in the stall with the water running);
// this owns what the window does once one comes up.
//
// Frames: bathroom_window_watch.png (96x32 — 3 frames of 32x32), the same rect as the window in
// bathroom_colddusk.png, so it is a straight swap on the same renderer. The window is a
// BathroomFixture that repaints itself every frame, so it is suspended for the duration rather
// than fought over.

using System.Collections;
using UnityEngine;
using Game.Interior;

public class WindowWatcher : MonoBehaviour
{
    [Header("The window it happens to")]
    public BathroomFixture window;

    [Header("Watcher frames — 3: [0] dim, [1] bright pulse, [2] drifted")]
    [Tooltip("bathroom_window_watch.png — Read/Write ON · Point · Compression None.")]
    public Texture2D watchSheet;
    public float pixelsPerUnit = 16f;
    public Vector2 pivot = new Vector2(0.5f, 0f);

    [Header("Timing")]
    [Tooltip("How long it lingers. Short: long enough to be sure, not long enough to study.")]
    public Vector2 holdSeconds = new Vector2(1.2f, 3.5f);
    public float frameFps = 4f;
    public bool fadeIn = true;
    public float fadeSeconds = 0.5f;

    [Header("Reaction")]
    [Tooltip("A low sub hit, not a jumpscare screech.")]
    public AudioSource stingSfx;
    [Tooltip("Fires when the eyes appear — hook dread/sanity systems here.")]
    public UnityEngine.Events.UnityEvent onAppear;
    [Tooltip("Fires only if the player was looking at the window when it appeared, so the dread hit " +
             "lands when it is earned.")]
    public UnityEngine.Events.UnityEvent onSeen;
    public Transform playerCamera;
    public float seenDotThreshold = 0.6f;

    public bool Active { get; private set; }

    Sprite[] _watch;
    SpriteRenderer _sr;

    void Awake()
    {
        _sr = window != null ? window.Renderer : GetComponent<SpriteRenderer>();
        _watch = BathroomAtlas.SliceWatch(watchSheet, pixelsPerUnit, pivot);
    }

    public IEnumerator Appear()
    {
        if (Active || _sr == null || _watch == null || _watch.Length < BathroomAtlas.WATCH_FRAMES) yield break;
        Active = true;
        if (window) window.suspended = true;          // stop the window repainting itself over this
        onAppear?.Invoke();
        if (stingSfx) stingSfx.Play();
        if (LookingAtWindow()) onSeen?.Invoke();

        float hold = Random.Range(holdSeconds.x, holdSeconds.y);
        float t = 0f, ft = 0f; int f = 0;
        var c = _sr.color;

        while (t < hold)
        {
            t += Time.deltaTime; ft += Time.deltaTime;
            if (ft >= 1f / frameFps) { ft = 0f; f = (f + 1) % _watch.Length; }
            _sr.sprite = _watch[f];
            if (fadeIn)                                // the eyes brighten in, then hold
            {
                c.a = Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(t / fadeSeconds));
                _sr.color = c;
            }
            yield return null;
        }

        c.a = 1f; _sr.color = c;
        if (window) { window.suspended = false; window.SetFrame(0); }
        Active = false;
    }

    bool LookingAtWindow()
    {
        if (playerCamera == null) return false;
        Vector3 to = (transform.position - playerCamera.position).normalized;
        return Vector3.Dot(playerCamera.forward, to) > seenDotThreshold;
    }
}
