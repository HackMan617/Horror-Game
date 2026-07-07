using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Walk-up interaction for the parked truck (Assets/Animation/Car/roadside_pack/CAR.md). When the
/// player is near, a prompt appears; press E to swing the near door open, E again to close it. The
/// door eases through stages 0→3 (open) / 3→0 (close) over time, and each stage maps to the sheet's
/// door frames [0,4,5,6] (CAR.md). The frame index is fed to the truck's <see cref="DirectionalSprite"/>
/// so whichever view is currently facing the camera animates its own door/tailgate.
///
/// First slice of the vehicle mechanics — enter / drive hooks will hang off this later.
/// </summary>
[RequireComponent(typeof(DirectionalSprite))]
public class CarDoor : MonoBehaviour
{
    [Tooltip("Player transform. Found by name (\"Player\") at Start when left null.")]
    public Transform player;
    public float range = 3.5f;
    public string openPrompt = "Press E to open door";
    public string closePrompt = "Press E to close door";
    [Tooltip("Seconds per door stage step (open 0→3, close 3→0). CAR.md ≈ 70 ms/step.")]
    public float stepSeconds = 0.07f;
    [Tooltip("Minimum seconds between toggles, so E-mashing can't spam the swing.")]
    public float interactCooldown = 0.4f;

    [Header("Audio — one 'Open and Close Door' clip, split into the two foley hits")]
    [Tooltip("The 'Open and Close Door.wav': an open creak, a silence, then a close slam. Self-wired in the editor.")]
    public AudioClip openCloseClip;
    [Range(0f, 1f)] public float soundVolume = 1f;
    [Tooltip("Seconds [start,end] of the OPEN foley inside the clip (leading silence trimmed for a snappy play).")]
    public float openStart = 0.70f, openEnd = 1.45f;
    [Tooltip("Seconds [start,end] of the CLOSE foley inside the clip.")]
    public float closeStart = 3.65f, closeEnd = 4.40f;

    [Header("Engine — start the truck (the door must be open)")]
    [Tooltip("Car Start and Rumble.wav — the starter turning over into a rough idle. Self-wired in the editor.")]
    public AudioClip carStartClip;
    [Range(0f, 1f)] public float engineVolume = 1f;
    [Tooltip("How long the engine runs before it cuts out on its own — the truck won't hold, you're not driving off.")]
    public float engineSeconds = 10f;
    [Tooltip("Seconds of fade at the very end as the engine dies, so it doesn't clip off abruptly.")]
    public float engineFade = 1f;
    [Tooltip("Prompt shown (with the close-door prompt) while the door is open and the engine is off.")]
    public string startPrompt = "Press F to start the truck";

    [Header("Get in & drive — once the engine is running (CAR POV / DRIVING.md)")]
    [Tooltip("Prompt shown while the engine runs, inviting the player to climb in and drive off. " +
             "Requires a TruckDriver on this truck (added by Tools/Horror Game/Setup Exterior Driving).")]
    public string getInPrompt = "Press G to get in and drive";
    [Tooltip("Drive the corrupted cockpit (lying gauges, passenger in the mirror). Normally off outside the nightmare.")]
    public bool nightmare = false;

    [Header("Exhaust smoke (puffs while the engine runs)")]
    [Tooltip("Local offset from the truck origin where the smoke rises from (the exhaust / engine bay).")]
    public Vector3 smokeLocalOffset = new Vector3(0f, 0.4f, 0f);
    [Tooltip("Smoke puff colour; its alpha sets how thick each puff reads.")]
    public Color smokeColor = new Color(0.24f, 0.24f, 0.26f, 0.55f);
    [Tooltip("Puffs emitted per second while the engine is running.")]
    public float smokeRate = 7f;

    // CAR.md: door stage 0..3 -> frame in the 7-frame view sheet (0 shut, 4/5/6 swing open).
    static readonly int[] StageToFrame = { 0, 4, 5, 6 };

    DirectionalSprite _ds;
    AudioClip _openClip, _closeClip;   // the two foley hits sliced out of openCloseClip
    int _stage;        // current door stage: 0 = shut .. 3 = fully open
    int _target;       // stage we're easing toward
    float _stepT;      // per-step timer
    float _cd;         // interaction cooldown remaining
    AudioSource _engine;   // dedicated, stoppable 3D source for the engine (PlayClipAtPoint can't be cut)
    bool _engineOn;        // true while the engine is running its brief spell
    float _engineT;        // seconds of engine run remaining before it dies
    ParticleSystem _smoke; // exhaust puffs, emitted only while the engine runs
    TruckDriver _driver;   // present once the truck is driving-enabled; owns the in-world drive
    CarLights _lights;     // headlights/tail-lights + idle rumble; woken while the engine runs

    /// <summary>True while the door is open or opening (enter/drive hooks can read this later).</summary>
    public bool IsOpen => _target > 0;

    void Awake()
    {
        _ds = GetComponent<DirectionalSprite>();
#if UNITY_EDITOR
        if (openCloseClip == null)
            openCloseClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound Effects/Open and Close Door.wav");
        if (carStartClip == null)
            carStartClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound Effects/Car Start and Rumble.wav");
#endif
        // One clip holds both foley hits (open creak, silence, close slam) — slice each into its own
        // clip once so opening plays the open half and closing plays the close half.
        if (openCloseClip != null)
        {
            if (openCloseClip.loadState != AudioDataLoadState.Loaded) openCloseClip.LoadAudioData();
            _openClip  = SubClip(openCloseClip, openStart, openEnd, "DoorOpen");
            _closeClip = SubClip(openCloseClip, closeStart, closeEnd, "DoorClose");
        }

        // A real, stoppable 3D source on the truck so the engine can be cut after a few seconds —
        // AudioSource.PlayClipAtPoint (used for the one-shot door foley) fires and forgets and can't stop.
        _engine = gameObject.AddComponent<AudioSource>();
        _engine.clip = carStartClip;
        _engine.playOnAwake = false;
        _engine.loop = false;
        _engine.spatialBlend = 1f;      // positional at the parked truck
        _engine.volume = engineVolume;

        BuildSmoke();
    }

    void Start()
    {
        if (player == null)
        {
            var p = GameObject.Find("Player");
            if (p != null) player = p.transform;
        }
        _driver = GetComponent<TruckDriver>();   // present once the truck is driving-enabled
        _lights = GetComponent<CarLights>();     // headlights/tail-lights + rumble, woken while the engine runs
    }

    void Update()
    {
        if (_driver != null && _driver.IsDriving) return;   // TruckDriver owns input while we're driving
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;
        if (_cd > 0f) _cd -= Time.deltaTime;

        // Ease the door toward its target, one stage per step.
        if (_stage != _target)
        {
            _stepT += Time.deltaTime;
            if (_stepT >= stepSeconds)
            {
                _stepT = 0f;
                _stage += _target > _stage ? 1 : -1;
            }
        }
        if (_ds != null) _ds.frame = StageToFrame[Mathf.Clamp(_stage, 0, 3)];

        // Engine: once started it rumbles for a few seconds then dies out on its own — the truck
        // won't actually take you anywhere. Fades over the final engineFade seconds so it doesn't clip
        // off, and keeps running even if the player walks away mid-idle.
        if (_engineOn)
        {
            _engineT -= Time.deltaTime;
            if (engineFade > 0f && _engineT < engineFade && _engine != null)
                _engine.volume = engineVolume * Mathf.Clamp01(_engineT / engineFade);
            if (_engineT <= 0f) StopEngine();
        }

        bool inRange = player != null &&
            Vector3.Distance(Flat(player.position), Flat(transform.position)) <= range;
        if (!inRange) return;   // out of range: prompt hides, but the door keeps whatever state it's in

        if (DialogUI.Instance != null)
        {
            // Engine running -> invite the player to climb in and drive (needs a TruckDriver). Otherwise
            // open/close the door, and offer to start the truck once the door is open.
            string prompt;
            if (IsOpen && _engineOn && _driver != null)
                prompt = getInPrompt + "   ·   " + closePrompt;
            else
            {
                prompt = IsOpen ? closePrompt : openPrompt;
                if (IsOpen && !_engineOn && carStartClip != null) prompt += "   ·   " + startPrompt;
            }
            DialogUI.Instance.ShowPrompt(prompt);
        }

        if (EPressed() && _cd <= 0f)
        {
            bool willOpen = !IsOpen;                 // currently shut -> this press opens it
            _target = willOpen ? 3 : 0;
            _cd = interactCooldown;

            var clip = willOpen ? _openClip : _closeClip;
            if (clip != null) AudioSource.PlayClipAtPoint(clip, transform.position, soundVolume);
        }

        // Start the truck: only with the door open, and not while it's already turning over.
        if (IsOpen && !_engineOn && carStartClip != null && FPressed())
            StartEngine();

        // Climb in and drive in place: only once the engine is turning over. Hands off to TruckDriver,
        // which hides the walking player, mounts the cab camera and shows the dashboard overlay.
        if (IsOpen && _engineOn && _driver != null && GPressed())
        {
            var rig = GetComponent<DrivingRig>();
            if (rig != null) rig.nightmare = nightmare;
            StopEngine();               // the idle rolls into the live drive
            _driver.EnterDrive();
        }
    }

    // Turn the engine over: play from the start of the recording (starter + rumble) and arm the timer
    // that cuts it out after a few seconds.
    void StartEngine()
    {
        if (_engine == null || carStartClip == null) return;
        _engine.volume = engineVolume;
        _engine.time = 0f;
        _engine.Play();
        _engineOn = true;
        _engineT = Mathf.Max(0.1f, engineSeconds);
        if (_smoke != null) _smoke.Play();
        if (_lights != null) _lights.engineRunning = true;   // rumble + headlights + tail/brake lights on
    }

    void StopEngine()
    {
        _engineOn = false;
        if (_engine != null) { _engine.Stop(); _engine.volume = engineVolume; }
        // Stop emitting but let the puffs already in the air rise and fade for a natural tail.
        if (_smoke != null) _smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (_lights != null) _lights.engineRunning = false;
    }

    // A small runtime particle system on the exhaust: grey puffs that rise, billow and fade, emitted
    // only while the engine runs. World-space so the puffs drift and dissipate in place rather than
    // riding the truck. Built here so nothing has to be authored per-truck in the scene.
    void BuildSmoke()
    {
        var go = new GameObject("ExhaustSmoke");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = smokeLocalOffset;

        _smoke = go.AddComponent<ParticleSystem>();
        _smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = _smoke.main;
        main.playOnAwake = false;
        main.loop = true;
        main.startLifetime = 2.4f;
        main.startSpeed = 0.55f;
        main.startSize = 0.4f;
        main.startColor = smokeColor;
        main.gravityModifier = -0.05f;                 // buoyant: the puffs rise
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;

        var emission = _smoke.emission;
        emission.rateOverTime = smokeRate;

        var shape = _smoke.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 12f;
        shape.radius = 0.1f;
        shape.rotation = new Vector3(-90f, 0f, 0f);    // cone points up (+Y) so smoke lifts off the exhaust

        var size = _smoke.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.5f), new Keyframe(1f, 2.2f)));   // each puff billows out as it rises

        var color = _smoke.colorOverLifetime;
        color.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(0.30f, 0.30f, 0.32f), 0f),
                    new GradientColorKey(new Color(0.14f, 0.14f, 0.16f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
        color.color = grad;                            // fade in, then dissipate

        var r = _smoke.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.material = SmokeMaterial();
        r.alignment = ParticleSystemRenderSpace.View;
    }

    // Sprites/Default is present in every render pipeline (incl. URP) and multiplies the soft-dot
    // texture by the per-particle vertex colour, so the colour-over-lifetime fade works.
    static Material _smokeMat;
    static Material SmokeMaterial()
    {
        if (_smokeMat != null) return _smokeMat;
        var shader = Shader.Find("Sprites/Default");
        _smokeMat = new Material(shader) { mainTexture = SoftDot() };
        return _smokeMat;
    }

    // A 32x32 soft radial dot (opaque centre fading to transparent edge) so each puff reads as a round,
    // feathered blob instead of a hard square.
    static Texture2D _softDot;
    static Texture2D SoftDot()
    {
        if (_softDot != null) return _softDot;
        const int n = 32;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        Vector2 c = new Vector2((n - 1) * 0.5f, (n - 1) * 0.5f);
        float rad = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c) / rad;   // 0 centre .. 1 edge
                float a = Mathf.Clamp01(1f - d);
                a = a * a;                                                // soft falloff
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    // Copies the samples in [startSec, endSec] of src into a fresh clip, so each door action can play
    // just its own foley hit out of the combined "Open and Close Door" recording.
    static AudioClip SubClip(AudioClip src, float startSec, float endSec, string name)
    {
        if (src == null) return null;
        int freq = src.frequency, ch = src.channels;
        int startS = Mathf.Clamp(Mathf.RoundToInt(startSec * freq), 0, src.samples);
        int endS   = Mathf.Clamp(Mathf.RoundToInt(endSec   * freq), startS, src.samples);
        int len = Mathf.Max(1, endS - startS);

        var data = new float[len * ch];
        src.GetData(data, startS);
        var clip = AudioClip.Create(name, len, ch, freq, false);
        clip.SetData(data, 0);
        return clip;
    }

    bool EPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }

    bool FPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.F);
#endif
    }

    bool GPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.G);
#endif
    }
}
