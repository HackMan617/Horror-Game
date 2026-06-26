using UnityEngine;

/// <summary>
/// Cheap "alive" animation for a placeholder blob NPC: a continuous squish plus
/// a small bob, each with a random phase so a roomful of blobs isn't synced.
/// </summary>
public class BlobAnimator : MonoBehaviour
{
    public float speed  = 3.2f;
    public float squish = 0.14f;
    public float bob    = 0.08f;

    Vector3 _baseScale;
    Vector3 _basePos;
    float _phase;

    void Start()
    {
        _baseScale = transform.localScale;
        _basePos   = transform.localPosition;
        _phase     = Random.value * 6.2831853f;
    }

    void Update()
    {
        float t = Time.time * speed + _phase;
        float s = Mathf.Sin(t);
        transform.localScale = new Vector3(_baseScale.x * (1f + squish * s),
                                           _baseScale.y * (1f - squish * s),
                                           _baseScale.z);
        transform.localPosition = _basePos + Vector3.up * (Mathf.Abs(Mathf.Sin(t * 0.5f)) * bob);
    }
}
