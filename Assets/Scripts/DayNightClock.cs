// DayNightClock.cs
// The single source of truth for the time of day (0 = dawn … 1 = deep night), and the one thing in
// the game that keeps running while you are indoors.
//
// The clock used to live on SkyController, which is a problem the moment anything but the sky wants
// to know the hour: SkyController exists only in the Exterior scene, and its timeOfDay is an
// ordinary serialised field, so stepping through the cabin door stopped time and stepping back out
// restarted it from wherever the scene asset happened to be saved. The interior windows made that
// visible — a window has to show the same evening the yard did a moment ago.
//
// So the value moves here, onto a DontDestroyOnLoad singleton that survives the door, exactly like
// <see cref="DreadDirector"/>. SkyController keeps every one of its pacing knobs and hands them to
// the clock when it wakes (see SkyController.Update), so the exterior stays where the tuning lives
// and nothing that already sets those knobs — Setup Exterior Birds And Night Pacing, say — has to
// change. Indoors there is no sky at all, and the clock simply runs on the pacing it was last given.

using UnityEngine;

public class DayNightClock : MonoBehaviour
{
    public static DayNightClock Instance { get; private set; }

    [Header("The hour")]
    [Tooltip("0 = dawn · 0.4 = noon · 0.78 = sunset · 1 = deep night.")]
    [Range(0f, 1f)] public float timeOfDay = 0.35f;
    [Tooltip("Advance the clock. Off freezes the hour everywhere — the sky, the windows and the light.")]
    public bool advance = true;

    [Header("Pacing (mirrored from the scene's SkyController when there is one)")]
    [Tooltip("Give the day and the night their own real-time budgets instead of one even sweep.")]
    public bool splitDayNight = false;
    [Range(0f, 1f)] public float nightStartT = 0.80f;
    public float dayLengthSeconds = 120f;
    public float dayDurationSeconds = 60f;
    public float nightDurationSeconds = 120f;
    public bool loop = true;

    /// <summary>The hour, 0..1. Falls back to deep night when no clock exists, because every room in
    /// this game is written for the dark — a missing clock should not turn the house to noon.</summary>
    public static float Value01 => Instance != null ? Mathf.Clamp01(Instance.timeOfDay) : 1f;

    public static bool Exists => Instance != null;

    void Awake()
    {
        // A re-loaded scene's copy self-destructs, so the hour carries through the door.
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update()
    {
        if (!advance || !Application.isPlaying) return;
        timeOfDay = Advance(timeOfDay, Time.deltaTime);
    }

    /// <summary>One tick of the clock. Kept public and pure so SkyController can share the exact
    /// pacing maths rather than keeping a second copy of it that drifts.</summary>
    public float Advance(float t, float dt)
    {
        if (splitDayNight)
        {
            // Move faster through the day span and slower through the night span, so each consumes
            // its own real-time budget (night can be much longer than day).
            float ns = Mathf.Clamp01(nightStartT);
            float rate = (t < ns) ? ns / Mathf.Max(1f, dayDurationSeconds)
                                  : (1f - ns) / Mathf.Max(1f, nightDurationSeconds);
            t += dt * rate;
        }
        else t += dt / Mathf.Max(1f, dayLengthSeconds);

        if (t > 1f) t = loop ? t - 1f : 1f;
        return t;
    }

    /// <summary>Copy a SkyController's pacing onto the clock. Called by the sky as it wakes, so the
    /// exterior's tuning is what everything else runs on.</summary>
    public void AdoptPacing(bool split, float nightStart, float dayLen, float dayDur, float nightDur, bool doLoop)
    {
        splitDayNight = split;
        nightStartT = nightStart;
        dayLengthSeconds = dayLen;
        dayDurationSeconds = dayDur;
        nightDurationSeconds = nightDur;
        loop = doLoop;
    }

    /// <summary>Jump the hour (0 = dawn … 1 = night).</summary>
    public void SetTime(float t) => timeOfDay = Mathf.Clamp01(t);
}
