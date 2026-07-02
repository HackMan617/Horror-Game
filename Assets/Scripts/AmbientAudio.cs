using UnityEngine;

/// <summary>
/// A looping ambient sound bed whose volume tracks the day/night cycle: full by day, fading to
/// (near) silence at night. If a <see cref="SkyController"/> is assigned it reads its Darkness
/// (0 = day, 1 = night); with none it just holds the day volume. Used for the daytime birdsong in
/// the exterior — birds sing while it's light and hush after dark.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AmbientAudio : MonoBehaviour
{
    public AudioClip clip;
    public SkyController sky;                       // optional: fade with day/night
    [Range(0f, 1f)] public float dayVolume = 0.5f;
    [Range(0f, 1f)] public float nightVolume = 0f;  // birds go quiet after dark
    [Tooltip("How fast the volume eases toward its target (volume units per second).")]
    public float fadeSpeed = 0.4f;

    AudioSource _src;

    void Awake()
    {
        _src = GetComponent<AudioSource>();
        _src.clip = clip;
        _src.loop = true;
        _src.playOnAwake = true;
        _src.spatialBlend = 0f;                     // ambient bed: 2D
        _src.volume = TargetVolume();               // start at the right level (no fade-in pop)
        if (clip != null && !_src.isPlaying) _src.Play();
    }

    void Update()
    {
        _src.volume = Mathf.MoveTowards(_src.volume, TargetVolume(), fadeSpeed * Time.deltaTime);
    }

    float TargetVolume()
    {
        float day = sky != null ? 1f - sky.Darkness : 1f;   // 1 in daylight, 0 at full night
        return Mathf.Lerp(nightVolume, dayVolume, day);
    }
}
