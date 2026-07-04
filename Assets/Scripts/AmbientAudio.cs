using UnityEngine;

/// <summary>
/// A looping ambient sound bed whose volume tracks the day/night cycle. If a
/// <see cref="SkyController"/> is assigned it reads its Darkness (0 = day, 1 = night) and lerps
/// between <see cref="dayVolume"/> and <see cref="nightVolume"/>; with none it just holds the day
/// volume. Set dayVolume &gt; nightVolume for a daytime bed (e.g. birdsong that hushes after dark),
/// or nightVolume &gt; dayVolume for a night bed (e.g. a low wind rumble that swells in at dusk).
///
/// The day/night change is eased (<see cref="fadeSpeed"/>). An optional slow <see cref="gustDepth"/>
/// gust modulates the level on top so a wind "breathes" — fades in and out — as it rises toward
/// night; leave gustDepth at 0 for a steady bed.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AmbientAudio : MonoBehaviour
{
    public AudioClip clip;
    public SkyController sky;                       // optional: fade with day/night
    [Range(0f, 1f)] public float dayVolume = 0.5f;
    [Range(0f, 1f)] public float nightVolume = 0f;  // e.g. birds go quiet after dark
    [Tooltip("How fast the day/night level eases toward its target (volume units per second).")]
    public float fadeSpeed = 0.4f;
    [Tooltip("Swell up from silence when the scene starts (a gentle 'begins now' onset) instead of " +
             "starting already at its day/night level.")]
    public bool fadeInOnStart = false;

    [Header("Gust (optional swell — 0 = steady)")]
    [Range(0f, 1f)] public float gustDepth = 0f;    // how deeply the level dips between gusts
    [Tooltip("Speed of the gust swell (Perlin oscillation).")]
    public float gustSpeed = 0.1f;

    AudioSource _src;
    float _env;        // eased day/night level, before the gust is applied
    float _gustSeed;

    void Awake()
    {
        _src = GetComponent<AudioSource>();
        _src.clip = clip;
        _src.loop = true;
        _src.playOnAwake = true;
        _src.spatialBlend = 0f;                     // ambient bed: 2D
        _gustSeed = (transform.position.x + transform.position.z) * 0.1f;   // per-object gust phase
        _env = fadeInOnStart ? 0f : EnvelopeTarget();   // swell in from silence, or start at level
        _src.volume = _env * GustMul();
        if (clip != null && !_src.isPlaying) _src.Play();
    }

    void Update()
    {
        _env = Mathf.MoveTowards(_env, EnvelopeTarget(), fadeSpeed * Time.deltaTime);
        _src.volume = _env * GustMul();
    }

    // The day/night level this bed is easing toward (before the gust).
    float EnvelopeTarget()
    {
        float day = sky != null ? 1f - sky.Darkness : 1f;   // 1 in daylight, 0 at full night
        return Mathf.Lerp(nightVolume, dayVolume, day);
    }

    // Slow multiplicative swell in [1 - gustDepth, 1]; 1 (no effect) when gustDepth is 0.
    float GustMul()
    {
        if (gustDepth <= 0f) return 1f;
        float g = Mathf.PerlinNoise(Time.time * gustSpeed, _gustSeed);
        return Mathf.Lerp(1f - gustDepth, 1f, g);
    }
}
