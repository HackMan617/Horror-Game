// ShowerStall.cs — the cabin bathroom's walk-in shower: the valve, the falling water, the steam,
// the curtain drawing itself closed, the floor going slick, the mirror fogging over, and the rolls
// for the thing at the window.
//
// This is the 3D rewrite of the kit's ShowerController (bathroom_kit/SHOWER.md), and it drops two
// things that only made sense in the flat original:
//
//   - No stand points, no teleport. The stall is REAL SPACE upstairs — 3m across and 1.3m deep —
//     so the player walks into it. "Inside" is simply the trigger box containing them, which is
//     also what gates the watcher.
//   - No sprite arrays to wire. Every piece is a BathroomFixture that already sliced itself from
//     the atlas; this only ever tells one which frame to hold.
//
// One key, one loop, the way an actual shower goes: step in, E turns the water on, E turns it off.
// The curtain follows the water rather than the player, so the moment you commit is the moment the
// room closes around you — which is the moment the window is worth looking at.

using System.Collections;
using UnityEngine;
using Game.Interior;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ShowerStall : MonoBehaviour
{
    [Header("Who is in here")]
    public Transform player;
    [Tooltip("The stall's floor space. The player is 'in the shower' while this contains them.")]
    public BoxCollider stallVolume;
    [Tooltip("How far outside the stall the valve can still be reached.")]
    public float reach = 1.2f;

    [Header("Curtain — 4 frames: closed, 1/3, 2/3, bunched open")]
    public BathroomFixture curtain;
    public float curtainSlideStep = 0.06f;

    [Header("Head + valve — 2 frames each: [0] off, [1] on")]
    public BathroomFixture showerHead, valveHandle;

    [Header("Water / steam / wet floor")]
    public BathroomFixture waterStream;
    public BathroomFixture[] steamEmitters;
    [Tooltip("The sheen overlay laid on the tile around the stall — fades in while the water runs.")]
    public BathroomFixture[] wetFloorTiles;
    public BathroomFixture puddle;
    [Tooltip("2 frames: [0] clear, [1] fogged.")]
    public BathroomFixture mirror;
    public float fogDelay = 6f;
    [Tooltip("How fast the tile goes slick, and how much slower it dries again.")]
    public float wetRate = 0.5f, dryRate = 0.08f;

    [Tooltip("A faint bloom behind the steam while the water runs — killed the moment the valve closes.")]
    public Light steamGlow;

    [Header("Sound")]
    public AudioSource waterLoop, valveSquealSfx, curtainRingsSfx;

    [Header("Prompts")]
    public string turnOnPrompt = "Press E to turn the water on";
    public string turnOffPrompt = "Press E to turn the water off";

    [Header("Watcher (window scare)")]
    [Tooltip("Leave EMPTY to keep the eyes out of the house. Assigning this is the whole of the " +
             "nightmare hookup — the rolls below already only ever run while the player is showering.")]
    public WindowWatcher watcher;
    [Range(0f, 1f)] public float watcherChance = 0.18f;
    public Vector2 watcherCheckInterval = new Vector2(8f, 20f);

    public bool WaterOn { get; private set; }
    public bool PlayerInside { get; private set; }

    int _curtainIdx = 3;                  // starts bunched open
    float _runTime;
    Coroutine _slide;

    void Start()
    {
        SetCurtain(3, instant: true);
        ApplyWater(false, instant: true);
        if (mirror) mirror.SetFrame(0);
        StartCoroutine(WatcherLoop());
    }

    void Update()
    {
        PlayerInside = Occupied();

        if (PlayerInside)
        {
            if (DialogUI.Instance != null) DialogUI.Instance.ShowPrompt(WaterOn ? turnOffPrompt : turnOnPrompt);
            if (EPressed()) SetWater(!WaterOn);
        }
        else if (WaterOn) SetWater(false);   // walking out shuts it off — nobody leaves it running

        Animate(Time.deltaTime);
    }

    // The stall box, grown by the valve's reach so you can start it with a hand through the curtain.
    bool Occupied()
    {
        if (player == null || stallVolume == null) return false;
        var b = stallVolume.bounds;
        b.Expand(reach);
        return b.Contains(player.position);
    }

    // ---------- curtain ----------
    public void SetCurtain(int idx, bool instant = false)
    {
        idx = Mathf.Clamp(idx, 0, 3);
        if (curtain == null) { _curtainIdx = idx; return; }
        // Paint on an instant set OR a no-op set. A fixture wakes on frame 0 — a CLOSED curtain — so
        // the opening state has to be written even though _curtainIdx already claims to be there,
        // or the shower starts life with the curtain drawn and no way to have drawn it.
        if (instant || _curtainIdx == idx) { _curtainIdx = idx; curtain.SetFrame(idx); return; }
        if (_slide != null) StopCoroutine(_slide);
        _slide = StartCoroutine(SlideTo(idx));
        if (curtainRingsSfx) curtainRingsSfx.Play();      // iron rings scraping the rod
    }

    IEnumerator SlideTo(int target)
    {
        int step = target > _curtainIdx ? 1 : -1;
        while (_curtainIdx != target)
        {
            _curtainIdx += step;
            curtain.SetFrame(_curtainIdx);
            yield return new WaitForSeconds(curtainSlideStep);
        }
        _slide = null;
    }

    public bool CurtainOpen => _curtainIdx >= 2;

    // ---------- water ----------
    public void SetWater(bool on)
    {
        if (WaterOn == on) return;
        ApplyWater(on, false);
        if (on && valveSquealSfx) valveSquealSfx.Play();  // the old valve complains
    }
    public void ToggleWater() => SetWater(!WaterOn);

    void ApplyWater(bool on, bool instant)
    {
        WaterOn = on; _runTime = 0f;
        if (showerHead)  showerHead.SetFrame(on ? 1 : 0);
        if (valveHandle) valveHandle.SetFrame(on ? 1 : 0);
        if (waterStream) waterStream.Renderer.enabled = on;
        if (puddle)      puddle.Renderer.enabled = on;
        foreach (var s in steamEmitters) if (s) s.Renderer.enabled = on;
        if (steamGlow) steamGlow.enabled = on;
        if (waterLoop) { if (on) waterLoop.Play(); else waterLoop.Stop(); }
        if (!on && mirror) mirror.SetFrame(0);
        SetCurtain(on ? 0 : 3, instant);                  // the room closes round you when you commit
        if (instant) foreach (var t in wetFloorTiles) if (t) SetAlpha(t.Renderer, on ? 1f : 0f);
    }

    void Animate(float dt)
    {
        if (WaterOn)
        {
            _runTime += dt;
            foreach (var t in wetFloorTiles) if (t) SetAlpha(t.Renderer, Mathf.MoveTowards(t.Renderer.color.a, 1f, dt * wetRate));
            if (_runTime > fogDelay && mirror) mirror.SetFrame(1);
        }
        else
        {
            // the tile dries a good deal slower than it wet — the room stays used for a while
            foreach (var t in wetFloorTiles) if (t) SetAlpha(t.Renderer, Mathf.MoveTowards(t.Renderer.color.a, 0f, dt * dryRate));
        }
    }

    static void SetAlpha(SpriteRenderer sr, float a) { if (!sr) return; var c = sr.color; c.a = a; sr.color = c; }

    // ---------- the watcher: only ever while showering ----------
    // Rare and random by design. It must never become reliable, or it stops working.
    IEnumerator WatcherLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(watcherCheckInterval.x, watcherCheckInterval.y));
            if (PlayerInside && WaterOn && watcher && Random.value < watcherChance)
                yield return watcher.Appear();
        }
    }

    bool EPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }
}
