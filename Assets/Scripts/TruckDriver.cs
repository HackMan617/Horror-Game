using UnityEngine;
using UnityEngine.Rendering.Universal;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Turns the truck into an in-world drivable vehicle (DRIVING.md, adapted to the real Exterior map). While
/// driving it hides the walking player + the truck's own body, mounts the main camera in the cab looking
/// forward, moves the truck through the 3D world via a <see cref="CharacterController"/>, and shows the
/// <see cref="CockpitController"/> dashboard overlay with a real rear-view camera behind the mirror. The
/// view is forward-locked; steering yaws the truck (and the world), not the camera.
///
/// Enter from <see cref="CarDoor"/> (press G at the running truck); exit by easing to a stop and pressing G
/// again (you get out where the truck stopped). Movement reads <see cref="DrivingRig.speed"/> /
/// <see cref="DrivingRig.steer"/>; the truck's <see cref="Billboard"/>/<see cref="DirectionalSprite"/> are
/// disabled while driving so this owns the transform (no billboard fight).
/// </summary>
[RequireComponent(typeof(DrivingRig))]
[RequireComponent(typeof(CockpitController))]
public class TruckDriver : MonoBehaviour
{
    [Header("Handling")]
    public float maxSpeed = 14f;        // world units/sec at full throttle
    public float turnRate = 80f;        // deg/sec at full steer while moving
    public float gravity = -20f;
    [Tooltip("Local eye offset for the driver camera (in the truck's forward frame). Higher Y = you sit up taller in the cab.")]
    public Vector3 eyeOffset = new Vector3(0f, 1.7f, 0.15f);
    [Tooltip("Compass heading the truck pulls away on when you climb in (180 = south, away from the cabin).")]
    public float startHeadingYaw = 180f;
    [Tooltip("Small lift (world units) so the truck's wheels rest ON the road rather than dipping into it.")]
    public float groundClearance = 0.05f;

    [Header("View — first/third person (V, like the player)")]
    [Tooltip("Start the drive in third-person (camera behind the truck) instead of the first-person cockpit.")]
    public bool startThirdPerson = false;
    [Tooltip("Third-person: how far behind the truck the camera trails.")]
    public float thirdPersonDistance = 6.5f;
    [Tooltip("Third-person: how high above the truck base the camera floats.")]
    public float thirdPersonHeight = 2.6f;
    [Tooltip("Third-person: the height on the truck the camera aims at.")]
    public float thirdPersonLookHeight = 1.5f;
    [Tooltip("Third-person: how far ahead of the truck the camera aims, so you see where you're going.")]
    public float thirdPersonLookAhead = 3f;
    [Tooltip("Third-person: camera follow smoothing (seconds). Higher = a laggier trail so the truck leads " +
             "in-frame when it accelerates/turns (reads as motion); 0 = rigid lock to the truck.")]
    public float thirdPersonFollowLag = 0.14f;

    [Header("Auto-drive (OutOfTown driver rig arrives already driving)")]
    public bool autoEnterOnStart = false;

    [Header("Audio — cabin driving loop")]
    [Tooltip("Inside Car Driving.wav — loops the whole time you're driving. Self-wired in the editor.")]
    public AudioClip drivingClip;
    [Range(0f, 1f)] public float drivingVolume = 0.8f;
    [Tooltip("Loop pitch scales from this (idle/stopped) up to +0.4 at full speed, for an engine-load feel.")]
    public float drivingBasePitch = 0.9f;

    public bool IsDriving { get; private set; }

    DrivingRig _rig;
    CockpitController _cockpit;
    AudioSource _driveLoop;
    CharacterController _cc;
    float _headingYaw, _vSpeed, _enterCooldown;
    bool _thirdPerson;

    // borrowed while driving, restored on exit
    Transform _player, _playerSprite, _camReturnParent;
    Vector3 _camReturnPos; Quaternion _camReturnRot;
    Camera _mainCam, _rearCam;
    RenderTexture _rearRT;
    Transform _seat;
    Behaviour _playerCtrl, _camRig, _billboard, _dirSprite, _carLights;
    DirectionalSprite _dir;
    Renderer _truckBody;
    CharacterController _playerCC;
    Vector3 _camVel;           // SmoothDamp velocity for the third-person chase follow
    Quaternion _playerRot0;    // player facing captured on enter, restored on exit

    void Awake()
    {
        _rig = GetComponent<DrivingRig>();
        _cockpit = GetComponent<CockpitController>();
#if UNITY_EDITOR
        if (drivingClip == null)
            drivingClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound Effects/Inside Car Driving.wav");
#endif
        // A dedicated 2D loop for the cabin interior (you're inside it), separate from CarDoor's positional
        // engine one-shot. Started in EnterDrive, stopped in ExitDrive.
        _driveLoop = gameObject.AddComponent<AudioSource>();
        _driveLoop.clip = drivingClip;
        _driveLoop.loop = true;
        _driveLoop.playOnAwake = false;
        _driveLoop.spatialBlend = 0f;
        _driveLoop.volume = drivingVolume;
    }

    void Start()
    {
        PlantOnGround();   // lift the hand-placed (centre-pivoted) truck so its wheels sit on the road, not in it
        if (autoEnterOnStart) EnterDrive();
    }

    public void EnterDrive()
    {
        if (IsDriving) return;
        IsDriving = true;
        _headingYaw = startHeadingYaw;
        _enterCooldown = 0.4f;
        _thirdPerson = startThirdPerson;

        // --- hide the walking player, borrow its camera ---
        var pgo = GameObject.Find("Player");
        _player = pgo != null ? pgo.transform : null;
        if (_player != null)
        {
            _playerRot0 = _player.rotation;   // restored on exit; yawed to the truck heading while driving (the compass reads it)
            _playerCtrl = _player.GetComponent<PlayerController3D>();
            if (_playerCtrl != null) _playerCtrl.enabled = false;
            _playerCC = _player.GetComponent<CharacterController>();
            if (_playerCC != null) _playerCC.enabled = false;
            var sprite = _player.Find("Sprite");
            _playerSprite = sprite;
            if (sprite != null) { var sr = sprite.GetComponent<Renderer>(); if (sr != null) sr.enabled = false; }
            _camRig = _player.GetComponentInChildren<CameraRig>();
            if (_camRig != null) _camRig.enabled = false;
        }

        _mainCam = Camera.main;
        if (_mainCam != null)
        {
            var ct = _mainCam.transform;
            _camReturnParent = ct.parent; _camReturnPos = ct.localPosition; _camReturnRot = ct.localRotation;
        }

        // --- the truck's own body: hidden in first person, shown (as a 2.5D billboard) in third ---
        _truckBody = GetComponent<SpriteRenderer>();
        _billboard = Beh("Billboard"); _dirSprite = Beh("DirectionalSprite"); _carLights = Beh("CarLights");
        _dir = _dirSprite as DirectionalSprite;

        // --- vehicle collider ---
        // Align the capsule bottom with the truck sprite's bottom edge so the grounded truck's billboard
        // rests ON the road while driving. The truck sprite is centre-pivoted, so a default capsule (bottom
        // near the origin) would ground the origin low and sink the billboard by its half-height.
        _cc = GetComponent<CharacterController>();
        if (_cc == null) _cc = gameObject.AddComponent<CharacterController>();
        float bottomBelowOrigin = SpriteBottomOffset();
        _cc.radius = 1.0f; _cc.height = 2.0f;
        _cc.center = new Vector3(0f, 1.0f - bottomBelowOrigin, 0f);   // capsule bottom = origin - bottomBelowOrigin = sprite bottom
        _cc.enabled = true;
        transform.rotation = Quaternion.Euler(0f, _headingYaw, 0f);

        // --- driver seat (first-person eye point in the cab) ---
        _seat = new GameObject("DriverSeat").transform;
        _seat.SetParent(transform, false);
        _seat.localPosition = eyeOffset;
        _seat.localRotation = Quaternion.identity;

        // --- rear-view camera -> RenderTexture for the mirror ---
        BuildRearCamera();
        _cockpit.SetRearTexture(_rearRT);

        ApplyView();            // parents the camera + shows/hides the cockpit for the current mode

        if (_driveLoop != null && drivingClip != null)
        {
            _driveLoop.volume = drivingVolume;
            _driveLoop.pitch = drivingBasePitch;
            _driveLoop.time = 0f;
            _driveLoop.Play();
        }

        _rig.acceptInput = true;
    }

    // Configure everything that differs between the first-person cockpit and the third-person chase view.
    // First person: mount the camera in the cab, show the dashboard overlay, hide the truck body. Third
    // person: show the truck body as a 2.5D billboard, hide the overlay, and drive the camera in world
    // space (LateUpdate) behind the heading — it can't be parented to the truck because the Billboard spins
    // the truck transform to face the camera, which would drag a parented camera with it.
    void ApplyView()
    {
        bool third = _thirdPerson;

        if (third) _cockpit.Hide(); else _cockpit.Show();
        if (_truckBody != null) _truckBody.enabled = third;
        SetBeh(_billboard, third);
        SetBeh(_dirSprite, third);
        SetBeh(_carLights, third);
        if (_rearCam != null) _rearCam.enabled = !third;   // mirror feed only matters in first person

        if (_mainCam == null) return;
        var ct = _mainCam.transform;
        if (third)
        {
            ct.SetParent(null, true);                       // placed each frame in LateUpdate
            _camVel = Vector3.zero;                         // snap to the chase pose so it doesn't swing in
            ct.position = ThirdPersonCamTarget();
            ct.rotation = Quaternion.LookRotation(ThirdPersonLookTarget() - ct.position, Vector3.up);
        }
        else
        {
            ct.SetParent(_seat, false);
            ct.localPosition = Vector3.zero;
            ct.localRotation = Quaternion.identity;
        }
    }

    public void ExitDrive()
    {
        if (!IsDriving) return;
        IsDriving = false;
        _rig.acceptInput = false;
        _rig.speed = 0f;

        _cockpit.Hide();
        if (_driveLoop != null) _driveLoop.Stop();
        if (_player != null) _player.rotation = _playerRot0;   // back to the facing you climbed in with

        // restore the camera to the player rig
        if (_mainCam != null)
        {
            var ct = _mainCam.transform;
            ct.SetParent(_camReturnParent, false);
            ct.localPosition = _camReturnPos; ct.localRotation = _camReturnRot;
        }
        if (_seat != null) Destroy(_seat.gameObject);
        if (_rearCam != null) Destroy(_rearCam.gameObject);
        if (_rearRT != null) { _rearRT.Release(); Destroy(_rearRT); _rearRT = null; }

        // restore the truck body; the parked truck now points the way you left it
        if (_truckBody != null) _truckBody.enabled = true;
        SetBeh(_billboard, true); SetBeh(_dirSprite, true); SetBeh(_carLights, true);
        if (_dir != null) _dir.noseYaw = _headingYaw;

        // park: stop being a mover, then re-plant the billboard on the ground (undo the CharacterController's
        // drive-time Y drift so the parked truck's wheels sit on the road, not in it).
        if (_cc != null) _cc.enabled = false;
        PlantOnGround();

        // drop the player out beside the (now parked) truck
        if (_player != null)
        {
            Vector3 outPos = transform.position;
            if (_playerCC != null)
            {
                _playerCC.enabled = false;
                _player.position = outPos;
                _playerCC.enabled = true;
            }
            else _player.position = outPos;
            if (_playerSprite != null) { var sr = _playerSprite.GetComponent<Renderer>(); if (sr != null) sr.enabled = true; }
            if (_camRig != null) _camRig.enabled = true;
            if (_playerCtrl != null) _playerCtrl.enabled = true;
        }
    }

    void Update()
    {
        if (!IsDriving) return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;
        if (_enterCooldown > 0f) _enterCooldown -= Time.deltaTime;

        // V flips first/third person mid-drive, exactly like the on-foot camera.
        if (ToggleViewPressed()) { _thirdPerson = !_thirdPerson; ApplyView(); }

        // get out once eased to a near-stop (small cooldown so the enter-press doesn't instantly exit)
        if (_enterCooldown <= 0f && _rig.speed < 0.06f && GetOutPressed())
        {
            if (DialogUI.Instance != null) DialogUI.Instance.ShowPrompt("");
            ExitDrive();
            return;
        }
        if (_enterCooldown <= 0f && _rig.speed < 0.06f && DialogUI.Instance != null)
            DialogUI.Instance.ShowPrompt("Press G to get out");

        float dt = Time.deltaTime;
        // steer yaws the heading (turns more the faster you go, a little even when crawling)
        _headingYaw += _rig.steer * turnRate * (0.15f + 0.85f * _rig.speed) * dt;
        transform.rotation = Quaternion.Euler(0f, _headingYaw, 0f);

        // Yaw the hidden player to the truck heading so the HUD compass (which reads the player's facing)
        // turns as you drive; restored to its entry facing on exit.
        if (_player != null) _player.rotation = Quaternion.Euler(0f, _headingYaw, 0f);

        // In third person the Billboard/DirectionalSprite are live: keep the truck's "nose" heading synced
        // to the driven yaw so it shows the correct side as the chase camera trails behind it.
        if (_thirdPerson && _dir != null) _dir.noseYaw = _headingYaw;

        // Cabin loop leans on the throttle: idle pitch at a stop, rising with speed for an engine-load feel.
        if (_driveLoop != null && _driveLoop.isPlaying) _driveLoop.pitch = drivingBasePitch + 0.4f * _rig.speed;

        Vector3 move = transform.forward * (_rig.speed * maxSpeed);
        if (_cc.isGrounded && _vSpeed < 0f) _vSpeed = -2f;
        _vSpeed += gravity * dt;
        _cc.Move((move + Vector3.up * _vSpeed) * dt);
    }

    // Place the third-person chase camera in world space behind the heading (not parented — the Billboard
    // spins the truck transform to face the camera, so a parented camera would chase its own tail). The
    // follow is smoothed so the truck LEADS in-frame as it accelerates/turns — that trailing lag is what
    // reads as motion (a rigid lock pins the truck dead-centre and looks frozen even while it's driving).
    void LateUpdate()
    {
        if (!IsDriving || !_thirdPerson || _mainCam == null) return;
        var ct = _mainCam.transform;
        ct.position = thirdPersonFollowLag > 0f
            ? Vector3.SmoothDamp(ct.position, ThirdPersonCamTarget(), ref _camVel, thirdPersonFollowLag)
            : ThirdPersonCamTarget();
        ct.rotation = Quaternion.LookRotation(ThirdPersonLookTarget() - ct.position, Vector3.up);
    }

    Vector3 HeadingDir() => Quaternion.Euler(0f, _headingYaw, 0f) * Vector3.forward;
    Vector3 ThirdPersonCamTarget() =>
        transform.position - HeadingDir() * thirdPersonDistance + Vector3.up * thirdPersonHeight;
    Vector3 ThirdPersonLookTarget() =>
        transform.position + HeadingDir() * thirdPersonLookAhead + Vector3.up * thirdPersonLookHeight;

    // Raise/lower the truck so its sprite's bottom edge (the wheels) rests on the ground under it, plus a
    // small clearance. Robust to the sprite's centre pivot — reads the actual rendered bounds. Used for the
    // parked truck (Start) and after exiting a drive.
    void PlantOnGround()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return;
        if (!TryGroundY(transform.position, out float g)) return;
        float bottomBelowOrigin = transform.position.y - sr.bounds.min.y;   // origin → sprite bottom (≈ half-height)
        var p = transform.position;
        p.y = g + bottomBelowOrigin + groundClearance;                      // sprite bottom = ground + clearance
        transform.position = p;
    }

    // How far the truck sprite's bottom edge sits below the transform origin (centre-pivoted ≈ half-height).
    float SpriteBottomOffset()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null) return Mathf.Max(0f, transform.position.y - sr.bounds.min.y);
        return 0.84f;   // fallback: truck half-height (27px / 16 ppu / 2)
    }

    // Raycast down for the ground surface under a point (ignoring the truck's own colliders and anything
    // above the cab). Used to plant the truck on the road so it doesn't sink.
    bool TryGroundY(Vector3 pos, out float y)
    {
        y = 0f;
        var hits = Physics.RaycastAll(pos + Vector3.up * 5f, Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore);
        bool found = false; float best = 0f;
        foreach (var h in hits)
        {
            var ht = h.collider.transform;
            if (ht == transform || ht.IsChildOf(transform)) continue;   // ignore ourselves
            if (h.point.y > pos.y + 1.0f) continue;                     // ignore ceilings / overhead props
            if (!found || h.point.y > best) { best = h.point.y; found = true; }
        }
        y = best;
        return found;
    }

    void BuildRearCamera()
    {
        var go = new GameObject("RearCamera");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 1.5f, -0.3f);
        go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // look back down the road
        _rearCam = go.AddComponent<Camera>();

        // Clone the main camera's exact (working) config — clear flags, culling mask, HDR/MSAA, background,
        // and the URP renderer it uses — so the rear feed renders the same 3D world into its RenderTexture.
        // A bare runtime camera can silently render nothing (wrong clear/renderer); copying avoids that.
        if (_mainCam != null)
        {
            _rearCam.CopyFrom(_mainCam);
            var mainData = _mainCam.GetUniversalAdditionalCameraData();
            var rearData = _rearCam.GetUniversalAdditionalCameraData();
            rearData.renderType = CameraRenderType.Base;
            int idx = RendererIndexOf(mainData);
            if (idx >= 0) rearData.SetRenderer(idx);
        }
        _rearCam.nearClipPlane = 0.05f;
        _rearCam.fieldOfView = 70f;

        _rearRT = new RenderTexture(256, 96, 16) { name = "RearViewRT" };
        _rearRT.Create();
        _rearCam.targetTexture = _rearRT;   // CopyFrom nulled this to match main; point it back at the mirror RT
        _rearCam.depth = (_mainCam != null ? _mainCam.depth : 0f) - 1f;   // render before the main view
        _rearCam.enabled = true;
    }

    // Read a UniversalAdditionalCameraData's renderer index (no public getter) so the rear camera can use the
    // exact renderer the main camera does. Falls back to the known 3D renderer index (1) if reflection fails.
    static int RendererIndexOf(UnityEngine.Rendering.Universal.UniversalAdditionalCameraData data)
    {
        if (data == null) return 1;
        var f = typeof(UnityEngine.Rendering.Universal.UniversalAdditionalCameraData)
            .GetField("m_RendererIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (f != null && f.GetValue(data) is int i) return i;
        return 1;
    }

    Behaviour Beh(string type)
    {
        foreach (var c in GetComponents<Behaviour>())
            if (c.GetType().Name == type) return c;
        return null;
    }
    static void SetBeh(Behaviour b, bool on) { if (b != null) b.enabled = on; }

    bool GetOutPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.G);
#endif
    }

    bool ToggleViewPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.V);
#endif
    }
}
