using UnityEngine;

/// <summary>
/// Loops a sprite-sheet animation on a SpriteRenderer at a fixed frame rate.
/// Used for ambient props such as the bed (always playing, unlike the movement-
/// driven CharacterBillboardAnimator).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class LoopSpriteAnimator : MonoBehaviour
{
    public Sprite[] frames;
    public float fps = 6f;

    SpriteRenderer _sr;
    float _t;

    void Awake() { _sr = GetComponent<SpriteRenderer>(); }

    void Update()
    {
        if (frames == null || frames.Length == 0) return;
        _t += Time.deltaTime * fps;
        _sr.sprite = frames[((int)_t) % frames.Length];
    }
}
