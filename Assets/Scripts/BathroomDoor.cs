// BathroomDoor.cs — the bathroom's plank door and its hook-and-eye latch: shut, then the hook
// lifted clear of the eye, then out of the way. The latch going FIRST is the whole point of the
// piece — holding that frame for a beat is what makes the door read as fastened rather than merely
// closed.
//
// 3D port of the kit's original (bathroom_kit/SHOWER.md), and the leaf is deliberately NOT a sprite.
// A door has one job besides looking like a door: hiding the room behind it. The game's sprite
// shader is ZWrite Off, so a sprite door writes no depth and occludes nothing — it can only be
// ordered against other sprites, and anything in the bathroom ordered above it (the shower curtain,
// most visibly) paints straight through a shut door. So the leaf is an alpha-clipped quad, the same
// depth-writing geometry the cabin's own front door and windows are made of, and the frames are
// selected by rewriting its UVs.
//
// Only the two SHUT frames are ever drawn. The kit's open frames paint the opening as a black void
// with light spilling across it — a complete little picture of a doorway, which is right in a flat
// 2D room and wrong in a real one, where the far side of the doorway is a bathroom you can walk
// into. Opening therefore ends with the leaf simply gone, the way a door swung flat to the wall is.

using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class BathroomDoor : MonoBehaviour
{
    [Header("Door leaf — an opaque, depth-writing quad")]
    public MeshRenderer leaf;
    public MeshFilter leafMesh;
    [Tooltip("Atlas UV rect per frame: [0] shut+latched, [1] latch lifted, [2] half, [3] wide. " +
             "Frames 2 and 3 are never shown — the leaf is hidden instead (see the file header).")]
    public Rect[] frameUV = new Rect[4];

    [Header("Timing")]
    [Tooltip("The hold on frame 1 while the hook clears the eye. Shorter than this and the latch " +
             "stops reading as a latch.")]
    public float latchSeconds = 0.22f;
    public float swingStep = 0.07f;
    [Tooltip("Blocks the doorway while the door is shut.")]
    public Collider blocker;

    [Header("Player")]
    public Transform player;
    public float range = 2.2f;
    public string openPrompt = "Press E to open the bathroom door";
    public string closePrompt = "Press E to close the bathroom door";

    [Header("Sound")]
    public AudioSource latchSfx, hingeCreakSfx, shutSfx;

    public bool IsOpen { get; private set; }
    bool _busy;
    Mesh _mesh;
    readonly Vector2[] _uv = new Vector2[4];

    void Start() { Set(0); if (blocker) blocker.enabled = true; }

    void Update()
    {
        if (_busy || player == null) return;
        if (Vector3.Distance(player.position, transform.position) > range) return;
        if (DialogUI.Instance != null) DialogUI.Instance.ShowPrompt(IsOpen ? closePrompt : openPrompt);
        if (EPressed()) Toggle();
    }

    public void Toggle() { if (!_busy) StartCoroutine(IsOpen ? Close() : Open()); }

    public IEnumerator Open()
    {
        _busy = true;
        if (latchSfx) latchSfx.Play();
        Set(1); yield return new WaitForSeconds(latchSeconds);      // the hook lifts before anything swings
        if (hingeCreakSfx) hingeCreakSfx.Play();
        Set(2); yield return new WaitForSeconds(swingStep);
        Set(3);
        if (blocker) blocker.enabled = false;
        IsOpen = true; _busy = false;
    }

    public IEnumerator Close()
    {
        _busy = true;
        Set(2); yield return new WaitForSeconds(swingStep);
        Set(1); yield return new WaitForSeconds(swingStep);
        if (shutSfx) shutSfx.Play();
        Set(0);
        if (blocker) blocker.enabled = true;
        IsOpen = false; _busy = false;
    }

    void Set(int i)
    {
        bool shut = i <= 1;                                          // 2 and 3 are "out of the way"
        if (leaf) leaf.enabled = shut;
        if (!shut || leafMesh == null || i >= frameUV.Length) return;

        if (_mesh == null) _mesh = leafMesh.mesh;                    // an instance, so the shared quad is untouched
        var r = frameUV[i];
        _uv[0] = new Vector2(r.xMin, r.yMin);
        _uv[1] = new Vector2(r.xMax, r.yMin);
        _uv[2] = new Vector2(r.xMax, r.yMax);
        _uv[3] = new Vector2(r.xMin, r.yMax);
        _mesh.uv = _uv;
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
