using UnityEngine;

/// <summary>
/// The crow's "too still" idle from props_autumn.png: it holds the eye-open frame and blinks
/// (the second frame) for a short beat on a slow cycle, rather than looping evenly. Straight from
/// the sheet's README: crowFrame = (elapsed % blinkEvery) &lt; blinkHold ? blink : open.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class CrowBlink : MonoBehaviour
{
    public Sprite openFrame;
    public Sprite blinkFrame;
    public float blinkEvery = 3.4f;    // seconds between blinks
    public float blinkHold = 0.14f;    // seconds the blink frame shows

    SpriteRenderer _sr;
    float _phase;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _phase = Random.value * blinkEvery;   // desync multiple crows
    }

    void Update()
    {
        if (openFrame == null) return;
        float t = (Time.time + _phase) % blinkEvery;
        _sr.sprite = (blinkFrame != null && t < blinkHold) ? blinkFrame : openFrame;
    }
}
