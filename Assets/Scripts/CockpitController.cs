using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The truck dashboard, drawn as a Screen-Space overlay OVER the live 3D view while driving
/// (Assets/Animation/Car POV/cockpit_kit/DRIVING.md). The cockpit shell has a transparent windshield
/// hole, so the forward driver camera behind it shows the real landscape; the rear-view mirror is a
/// <see cref="RawImage"/> fed by a real rear-facing camera's RenderTexture (see <see cref="TruckDriver"/>).
///
/// Everything is assembled procedurally in <see cref="Awake"/> from the sheet textures (sliced at runtime
/// by the DRIVING.md anchor table) into a fixed 260×180 "shell frame" pinned to the bottom of the screen
/// and scaled to width. Each layer is driven live from <see cref="DrivingRig"/>: the wheel + needles
/// rotate, the lamps toggle, the odometer counts, the charm swings, the mirror drains. The
/// <c>_nightmare</c> twins are swapped in on the dread flag (swap, never recolor — DRIVING.md §7).
/// </summary>
[RequireComponent(typeof(DrivingRig))]
public class CockpitController : MonoBehaviour
{
    const float FrameW = 260f, FrameH = 180f;   // shell-frame px; anchors are px, x right, y DOWN from top

    [Header("Cockpit sheets (home). The generator assigns these; self-wired in editor as a fallback.")]
    public Texture2D shellTex, gaugeSpeedTex, gaugeFuelTex, needleTex, warningTex, odometerTex, mirrorTex, charmTex, wheelTex;
    [Header("Cockpit sheets (_nightmare twins)")]
    public Texture2D shellNm, gaugeSpeedNm, gaugeFuelNm, needleNm, warningNm, odometerNm, mirrorNm, charmNm, wheelNm, passengerNm;

    public float maxWheelDeg = 135f;

    DrivingRig _rig;
    Canvas _canvas;
    RectTransform _frame;
    bool _nmState;

    // live layers
    Image _shell, _speedFace, _fuelFace, _speedNeedle, _fuelNeedle, _mirrorFrame, _drain, _passenger, _charm, _wheel;
    RawImage _mirrorGlass;
    Image[] _lamps = new Image[4];
    Image[] _odoDigits = new Image[5];

    // cached slices (home + nightmare)
    Sprite _sShell, _sShellNm, _sSpeed, _sSpeedNm, _sFuel, _sFuelNm, _sNeedle, _sNeedleNm;
    Sprite _sMirror, _sMirrorNm, _sCharm, _sCharmNm, _sWheel, _sWheelNm, _sPassenger;
    Sprite[] _sLampsUnlit = new Sprite[6], _sLampsLit = new Sprite[6];
    Sprite[] _sLampsUnlitNm = new Sprite[6], _sLampsLitNm = new Sprite[6];
    Sprite[] _sDigits = new Sprite[10], _sDigitsNm = new Sprite[10];

    float _charmA, _charmV;

    void Awake()
    {
        _rig = GetComponent<DrivingRig>();
#if UNITY_EDITOR
        SelfWire();
#endif
        SliceAll();
        BuildCanvas();
        ApplyRealm(_rig.nightmare, force: true);
        _canvas.enabled = false;   // hidden until we climb in
    }

    // -------------------------------------------------------------- public API (TruckDriver)
    public void Show() { FitFrame(); _canvas.enabled = true; }
    public void Hide() { _canvas.enabled = false; }
    public bool IsShown => _canvas != null && _canvas.enabled;
    public void SetRearTexture(RenderTexture rt)
    {
        if (_mirrorGlass == null) return;
        _mirrorGlass.texture = rt;
        _mirrorGlass.color = Color.white;   // show the rear feed at full brightness (drain overlay dims it)
    }

    // -------------------------------------------------------------- per-frame drive
    void LateUpdate()
    {
        if (_rig == null || _canvas == null || !_canvas.enabled) return;
        FitFrame();
        ApplyRealm(_rig.nightmare, force: false);
        DriveWheel();
        DriveGauges();
        DriveLamps();
        DriveOdometer();
        DriveCharm();
        DriveMirror();
    }

    // Keep the 260×180 shell pinned to the screen bottom and scaled to fill the width.
    void FitFrame()
    {
        float s = Screen.width / FrameW;
        _frame.localScale = new Vector3(s, s, 1f);
    }

    void DriveWheel()
    {
        float z = -_rig.steer * maxWheelDeg;
        if (_rig.nightmare) z += Mathf.Sin(Time.time * 0.9f) * 28f + Mathf.Sin(Time.time * 2.3f) * 11f;
        _wheel.rectTransform.localEulerAngles = new Vector3(0, 0, z);
    }

    static float Ang(float t) => -120f + Mathf.Clamp01(t) * 240f;   // −120°..+120° sweep
    void DriveGauges()
    {
        float sv = _rig.speed, fv = _rig.fuel;
        if (_rig.nightmare)
        {
            sv = Mathf.Clamp01(_rig.speed + Mathf.Sin(Time.time * 7f) * 0.28f);            // speedo spins
            float p = Mathf.Sin(Time.time * 0.7f);
            fv = p > 0.4f ? 1f : (p < -0.4f ? _rig.fuel * 0.35f : _rig.fuel);               // full…then empty
        }
        _speedNeedle.rectTransform.localEulerAngles = new Vector3(0, 0, Ang(sv));
        _fuelNeedle.rectTransform.localEulerAngles = new Vector3(0, 0, Ang(fv));
    }

    void DriveLamps()
    {
        var lit = _nmState ? _sLampsLitNm : _sLampsLit;
        var unlit = _nmState ? _sLampsUnlitNm : _sLampsUnlit;
        for (int i = 0; i < 4; i++)
        {
            bool on = _rig.nightmare ? true : DaytimeLamp(i);
            _lamps[i].sprite = on ? lit[i] : unlit[i];
        }
    }

    bool DaytimeLamp(int i)
    {
        switch (i)
        {
            case 0: return _rig.speed < 0.05f;      // oil pressure at idle
            case 1: return _rig.speed > 0.85f;      // temp when thrashed
            case 2: return _rig.fuel < 0.15f;       // battery / low fuel
            default: return false;                  // check-engine off in the home realm
        }
    }

    void DriveOdometer()
    {
        int odo = _rig.nightmare
            ? Mathf.Max(0, 99999 - Mathf.FloorToInt(_rig.distance * 3f) % 100000)   // backward
            : Mathf.FloorToInt(_rig.distance) % 100000;
        var digits = _nmState ? _sDigitsNm : _sDigits;
        for (int slot = 0; slot < 5; slot++)
        {
            int place = (int)Mathf.Pow(10, 4 - slot);
            _odoDigits[slot].sprite = digits[(odo / place) % 10];
        }
    }

    void DriveCharm()
    {
        float lateral = -_rig.steer * _rig.speed * 3.4f
                      - (_rig.nightmare ? Mathf.Sin(Time.time * 3f) * 0.4f : 0f);
        _charmV += (lateral - _charmA * 7f - _charmV * 2.2f) * Time.deltaTime;
        _charmA += _charmV * Time.deltaTime;
        _charm.rectTransform.localEulerAngles = new Vector3(0, 0, _charmA * Mathf.Rad2Deg);
    }

    void DriveMirror()
    {
        // The real rear view sits under a dark overlay that thickens as the rear view drains to black.
        float drain = Mathf.Clamp01(1f - _rig.rearFill);
        _drain.color = new Color(0f, 0f, 0f, drain * 0.9f);

        _passenger.enabled = _rig.nightmare;
        if (_rig.nightmare)
            _passenger.color = new Color(1, 1, 1, Mathf.Clamp01(0.55f + 0.4f * Mathf.Sin(Time.time * 1.3f)));
    }

    // -------------------------------------------------------------- realm swap
    void ApplyRealm(bool nm, bool force)
    {
        if (!force && nm == _nmState) return;
        _nmState = nm;
        _shell.sprite = nm && _sShellNm ? _sShellNm : _sShell;
        _speedFace.sprite = nm && _sSpeedNm ? _sSpeedNm : _sSpeed;
        _fuelFace.sprite = nm && _sFuelNm ? _sFuelNm : _sFuel;
        _speedNeedle.sprite = nm && _sNeedleNm ? _sNeedleNm : _sNeedle;
        _fuelNeedle.sprite = nm && _sNeedleNm ? _sNeedleNm : _sNeedle;
        _mirrorFrame.sprite = nm && _sMirrorNm ? _sMirrorNm : _sMirror;
        _charm.sprite = nm && _sCharmNm ? _sCharmNm : _sCharm;
        _wheel.sprite = nm && _sWheelNm ? _sWheelNm : _sWheel;
    }

    // -------------------------------------------------------------- build
    void BuildCanvas()
    {
        var go = new GameObject("CockpitCanvas", typeof(Canvas), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);
        _canvas = go.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 150;   // above the world, below DialogUI (200) and the pause menu (1000)

        // A 260×180 frame pinned to the screen bottom-centre; localScale fits it to the screen width.
        // Bottom-centre pivot so it sits ON the screen bottom (and scales up from there), not half below it.
        _frame = NewRect("ShellFrame", _canvas.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(FrameW, FrameH), Vector2.zero);
        _frame.pivot = new Vector2(0.5f, 0f);

        _shell = Layer("Shell", _sShell, 130, 90, 260, 180);

        _speedFace = Layer("SpeedFace", _sSpeed, 100, 104, 60, 60);
        _fuelFace = Layer("FuelFace", _sFuel, 158, 108, 60, 60);
        _speedNeedle = Layer("SpeedNeedle", _sNeedle, 100, 104, 60, 60);
        _fuelNeedle = Layer("FuelNeedle", _sNeedle, 158, 108, 60, 60);
        _fuelNeedle.rectTransform.localScale = Vector3.one * (17f / 26f);   // fuel dial is smaller

        for (int i = 0; i < 4; i++)   // warning grid 2×2, centre (202,104), 15px pitch
        {
            float gx = 202f + ((i % 2) - 0.5f) * 15f;
            float gy = 104f + ((i / 2) - 0.5f) * 15f;
            _lamps[i] = Layer("Lamp" + i, _sLampsUnlit[i], gx, gy, 12, 12);
        }

        for (int s = 0; s < 5; s++)   // odometer window x86,y122, 9px pitch (9×13 digits)
            _odoDigits[s] = Layer("Odo" + s, _sDigits[0], 86f + s * 9f, 122f, 9, 13);

        // mirror at (130,22); glass rect 80×26. Glass (rear RT) -> drain -> passenger -> frame.
        _mirrorGlass = LayerRaw("MirrorGlass", 130, 22, 80, 26);
        _mirrorGlass.uvRect = new Rect(1f, 0f, -1f, 1f);   // mirror = horizontal flip
        _drain = Layer("MirrorDrain", null, 130, 22, 80, 26); _drain.color = new Color(0, 0, 0, 0);
        _passenger = Layer("Passenger", _sPassenger, 130, 22, 81, 27); _passenger.enabled = false;
        _mirrorFrame = Layer("MirrorFrame", _sMirror, 130, 22, 104, 40);

        _charm = Layer("Charm", _sCharm, 176, 34, 26, 52);
        _charm.rectTransform.pivot = new Vector2(0.5f, 1f);   // hangs from its top

        _wheel = Layer("Wheel", _sWheel, 130, 192, 140, 140);
    }

    // A child Image at shell-frame px (px right, py down-from-top) sized w×h px.
    Image Layer(string name, Sprite sprite, float px, float py, float w, float h)
    {
        var rt = NewRect(name, _frame, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(w, h), new Vector2(px - FrameW * 0.5f, FrameH - py));
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        if (sprite == null) img.color = Color.white;
        return img;
    }

    RawImage LayerRaw(string name, float px, float py, float w, float h)
    {
        var rt = NewRect(name, _frame, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(w, h), new Vector2(px - FrameW * 0.5f, FrameH - py));
        var raw = rt.gameObject.AddComponent<RawImage>();
        raw.color = new Color(0.16f, 0.15f, 0.19f);   // shows until a rear RT is assigned
        raw.raycastTarget = false;
        return raw;
    }

    static RectTransform NewRect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 size, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size; rt.anchoredPosition = pos;
        return rt;
    }

    // -------------------------------------------------------------- slicing (runtime, by anchor table)
    void SliceAll()
    {
        _sShell = Whole(shellTex); _sShellNm = Whole(shellNm);
        _sSpeed = Whole(gaugeSpeedTex); _sSpeedNm = Whole(gaugeSpeedNm);
        _sFuel = Whole(gaugeFuelTex); _sFuelNm = Whole(gaugeFuelNm);
        _sNeedle = Whole(needleTex); _sNeedleNm = Whole(needleNm);
        _sMirror = Whole(mirrorTex); _sMirrorNm = Whole(mirrorNm);
        _sCharm = Whole(charmTex); _sCharmNm = Whole(charmNm);
        _sWheel = Whole(wheelTex); _sWheelNm = Whole(wheelNm);
        _sPassenger = Whole(passengerNm);
        SliceWarning(warningTex, _sLampsUnlit, _sLampsLit);
        SliceWarning(warningNm, _sLampsUnlitNm, _sLampsLitNm);
        SliceDigits(odometerTex, _sDigits);
        SliceDigits(odometerNm, _sDigitsNm);
    }

    const float SlicePpu = 100f;
    static Sprite Whole(Texture2D t) =>
        t == null ? null : Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), SlicePpu, 0, SpriteMeshType.FullRect);

    // Warning sheet 144×48 = 6 cols × 2 rows of 24 (row 0 unlit, row 1 lit). Texture Y is bottom-up.
    static void SliceWarning(Texture2D t, Sprite[] unlit, Sprite[] lit)
    {
        if (t == null) return;
        const int cell = 24;
        for (int c = 0; c < 6; c++)
        {
            unlit[c] = Sprite.Create(t, new Rect(c * cell, t.height - cell, cell, cell), new Vector2(0.5f, 0.5f), SlicePpu, 0, SpriteMeshType.FullRect);
            lit[c] = Sprite.Create(t, new Rect(c * cell, t.height - 2 * cell, cell, cell), new Vector2(0.5f, 0.5f), SlicePpu, 0, SpriteMeshType.FullRect);
        }
    }

    // Odometer strip 90×13 = digits 0–9 at (d*9,0,9,13).
    static void SliceDigits(Texture2D t, Sprite[] digits)
    {
        if (t == null) return;
        const int cw = 9, ch = 13;
        for (int d = 0; d < 10; d++)
            digits[d] = Sprite.Create(t, new Rect(d * cw, 0, cw, ch), new Vector2(0.5f, 0.5f), SlicePpu, 0, SpriteMeshType.FullRect);
    }

#if UNITY_EDITOR
    void SelfWire()
    {
        const string dir = "Assets/Animation/Car POV/cockpit_kit/sprites/";
        T(ref shellTex, dir + "cockpit_shell.png"); T(ref shellNm, dir + "cockpit_shell_nightmare.png");
        T(ref gaugeSpeedTex, dir + "gauge_speed.png"); T(ref gaugeSpeedNm, dir + "gauge_speed_nightmare.png");
        T(ref gaugeFuelTex, dir + "gauge_fuel.png"); T(ref gaugeFuelNm, dir + "gauge_fuel_nightmare.png");
        T(ref needleTex, dir + "needle.png"); T(ref needleNm, dir + "needle_nightmare.png");
        T(ref warningTex, dir + "warning_lights.png"); T(ref warningNm, dir + "warning_lights_nightmare.png");
        T(ref odometerTex, dir + "odometer_digits.png"); T(ref odometerNm, dir + "odometer_digits_nightmare.png");
        T(ref mirrorTex, dir + "mirror.png"); T(ref mirrorNm, dir + "mirror_nightmare.png");
        T(ref charmTex, dir + "charm.png"); T(ref charmNm, dir + "charm_nightmare.png");
        T(ref wheelTex, dir + "steering_wheel.png"); T(ref wheelNm, dir + "steering_wheel_nightmare.png");
        T(ref passengerNm, dir + "mirror_passenger_nightmare.png");
    }

    static void T(ref Texture2D t, string path)
    { if (t == null) t = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path); }
#endif
}
