using UnityEngine;

/// <summary>
/// A second ambient layer that drifts in and out over a steadier bed — e.g. wind rustling the trees
/// washing over the constant low wind rumble. The clip loops quietly underneath, but its level is
/// gated by a slow random envelope: it holds silent for a stretch (you hear only the bed), then fades
/// up and holds for a spell so it blends with whatever else is playing, then fades back out. So at
/// some moments this layer sits on top of the bed and at others the bed plays alone — never a
/// constant drone. Optionally reads a <see cref="SkyController"/> so, like the rumble, it swells at
/// night and eases off by day.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class OccasionalAmbience : MonoBehaviour
{
    public AudioClip clip;
    public SkyController sky;                        // optional: louder at night, quieter by day

    [Header("Level")]
    [Tooltip("Peak level of an audible window in daylight.")]
    [Range(0f, 1f)] public float dayVolume = 0.3f;
    [Tooltip("Peak level of an audible window at full night.")]
    [Range(0f, 1f)] public float nightVolume = 0.45f;
    [Tooltip("Seconds spent fading in and out at the edges of each audible window (a gentle swell, " +
             "never an abrupt cut).")]
    public float fade = 4f;

    [Header("Timing (seconds)")]
    [Tooltip("Quiet stretch between audible windows — only the bed plays through here.")]
    public float minSilence = 20f;
    public float maxSilence = 55f;
    [Tooltip("How long an audible window is held (at peak) once it has faded in.")]
    public float minAudible = 10f;
    public float maxAudible = 25f;

    AudioSource _src;
    float _target;      // 0 while silent, 1 while an audible window is held
    float _env;         // eased toward _target — the envelope value
    float _phaseTimer;  // seconds left in the current silent / audible phase

    void Awake()
    {
        _src = GetComponent<AudioSource>();
        _src.clip = clip;
        _src.loop = true;
        _src.playOnAwake = false;
        _src.spatialBlend = 0f;     // ambient bed: 2D
        _src.volume = 0f;
        _target = 0f;
        // Begin partway through a quiet stretch so the trees don't come in the instant the scene loads.
        _phaseTimer = Random.Range(minSilence * 0.3f, maxSilence);
        if (clip != null) _src.Play();
    }

    void Update()
    {
        if (clip == null) return;

        _phaseTimer -= Time.deltaTime;
        if (_phaseTimer <= 0f)
        {
            if (_target > 0.5f) { _target = 0f; _phaseTimer = Random.Range(minSilence, maxSilence); }
            else                { _target = 1f; _phaseTimer = Random.Range(minAudible, maxAudible); }
        }

        float rate = fade > 0.01f ? 1f / fade : 100f;   // envelope units per second
        _env = Mathf.MoveTowards(_env, _target, rate * Time.deltaTime);
        _src.volume = _env * PeakLevel();
    }

    // The peak an audible window reaches right now, tracking day/night like the rumble bed.
    float PeakLevel()
    {
        float day = sky != null ? 1f - sky.Darkness : 1f;   // 1 in daylight, 0 at full night
        return Mathf.Lerp(nightVolume, dayVolume, day);
    }
}
