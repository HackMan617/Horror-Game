using UnityEngine;

/// <summary>
/// Drives the 2.5D character billboard from gameplay: cycles a walk strip while
/// the player moves, idles on frame 0 when still. Built to support both facing
/// sets — once front frames are added it shows the FRONT sheet when the player
/// faces the camera and the BACK sheet when they face away. With back only, it
/// always uses the back sheet (which is what the behind-the-player camera sees).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class CharacterBillboardAnimator : MonoBehaviour
{
    public Sprite[] backFrames;
    public Sprite[] frontFrames;          // optional; wired in later
    public float fps = 8f;
    public PlayerController3D player;
    public Transform cameraTransform;     // used for front/back selection

    /// <summary>While true, another system (e.g. AxeChopper) is driving the sprite — don't touch it.</summary>
    [System.NonSerialized] public bool suspended;

    SpriteRenderer _sr;
    float _t;
    int _frame;

    void Awake() { _sr = GetComponent<SpriteRenderer>(); }

    void Update()
    {
        if (suspended) return;
        var frames = SelectFrames();
        if (frames == null || frames.Length == 0) return;

        bool moving = player != null && player.MoveInput.sqrMagnitude > 0.01f;
        if (moving)
        {
            _t += Time.deltaTime * fps;
            _frame = ((int)_t) % frames.Length;
        }
        else
        {
            _t = 0f;
            _frame = 0;
        }
        _sr.sprite = frames[Mathf.Clamp(_frame, 0, frames.Length - 1)];
    }

    Sprite[] SelectFrames()
    {
        if (frontFrames != null && frontFrames.Length > 0 && player != null && cameraTransform != null)
        {
            Vector3 toCam = cameraTransform.position - player.transform.position;
            bool facingCamera = Vector3.Dot(player.transform.forward, toCam) > 0f;
            return facingCamera ? frontFrames : backFrames;
        }
        return backFrames;
    }
}
