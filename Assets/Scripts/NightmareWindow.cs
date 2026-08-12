using UnityEngine;

/// <summary>
/// The rotted twin of a window, stacked over the waking one and revealed by the dread flag. Same
/// flicker language as the room's furniture and decor (<c>InteriorObject</c> / <c>InteriorProp</c>):
/// dark at dread 0, strobing in more and more as it climbs, and at 1 mostly wrong with brief lucid
/// blinks back to the real night outside.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class NightmareWindow : MonoBehaviour
{
    [Tooltip("Leave empty to follow the game-wide DreadDirector.")]
    [Range(0f, 1f)] public float DreadProgress = -1f;

    SpriteRenderer _sr;
    float _t;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _sr.enabled = false;
    }

    void Update()
    {
        float d = DreadProgress >= 0f ? DreadProgress : DreadDirector.Value01;

        _t -= Time.deltaTime;
        if (_t > 0f) return;

        if (d <= 0f) { _sr.enabled = false; _t = 0.25f; }
        else if (d >= 1f)
        {
            _sr.enabled = !(Random.value < 0.14f);
            _t = _sr.enabled ? Random.Range(0.16f, 0.34f) : Random.Range(0.04f, 0.11f);
        }
        else
        {
            _sr.enabled = Random.value < d;
            _t = _sr.enabled ? Random.Range(0.05f, 0.05f + 0.15f * d)
                             : Random.Range(0.14f, 0.14f + 0.52f * (1f - d));
        }
    }
}
