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
    [Tooltip("Local eye offset for the driver camera (in the truck's forward frame).")]
    public Vector3 eyeOffset = new Vector3(0f, 1.35f, 0.15f);
    [Tooltip("Compass heading the truck pulls away on when you climb in (180 = south, away from the cabin).")]
    public float startHeadingYaw = 180f;

    [Header("Auto-drive (OutOfTown driver rig arrives already driving)")]
    public bool autoEnterOnStart = false;

    public bool IsDriving { get; private set; }

    DrivingRig _rig;
    CockpitController _cockpit;
    CharacterController _cc;
    float _headingYaw, _vSpeed, _enterCooldown;

    // borrowed while driving, restored on exit
    Transform _player, _playerSprite, _camReturnParent;
    Vector3 _camReturnPos; Quaternion _camReturnRot;
    Camera _mainCam, _rearCam;
    RenderTexture _rearRT;
    Transform _seat;
    Behaviour _playerCtrl, _camRig, _billboard, _dirSprite, _carLights;
    Renderer _truckBody;
    CharacterController _playerCC;

    void Awake()
    {
        _rig = GetComponent<DrivingRig>();
        _cockpit = GetComponent<CockpitController>();
    }

    void Start() { if (autoEnterOnStart) EnterDrive(); }

    public void EnterDrive()
    {
        if (IsDriving) return;
        IsDriving = true;
        _headingYaw = startHeadingYaw;
        _enterCooldown = 0.4f;

        // --- hide the walking player, borrow its camera ---
        var pgo = GameObject.Find("Player");
        _player = pgo != null ? pgo.transform : null;
        if (_player != null)
        {
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

        // --- hide the truck's own body so we're "inside" it ---
        _truckBody = GetComponent<SpriteRenderer>();
        if (_truckBody != null) _truckBody.enabled = false;
        _billboard = Beh("Billboard"); _dirSprite = Beh("DirectionalSprite"); _carLights = Beh("CarLights");
        SetBeh(_billboard, false); SetBeh(_dirSprite, false); SetBeh(_carLights, false);

        // --- vehicle collider ---
        _cc = GetComponent<CharacterController>();
        if (_cc == null)
        {
            _cc = gameObject.AddComponent<CharacterController>();
            _cc.radius = 1.1f; _cc.height = 1.6f; _cc.center = new Vector3(0f, 0.8f, 0f);
        }
        _cc.enabled = true;
        transform.rotation = Quaternion.Euler(0f, _headingYaw, 0f);

        // --- driver camera in the cab, looking forward ---
        _seat = new GameObject("DriverSeat").transform;
        _seat.SetParent(transform, false);
        _seat.localPosition = eyeOffset;
        _seat.localRotation = Quaternion.identity;
        if (_mainCam != null)
        {
            var ct = _mainCam.transform;
            _camReturnParent = ct.parent; _camReturnPos = ct.localPosition; _camReturnRot = ct.localRotation;
            ct.SetParent(_seat, false);
            ct.localPosition = Vector3.zero; ct.localRotation = Quaternion.identity;
        }

        // --- rear-view camera -> RenderTexture for the mirror ---
        BuildRearCamera();
        _cockpit.SetRearTexture(_rearRT);
        _cockpit.Show();

        _rig.acceptInput = true;
    }

    public void ExitDrive()
    {
        if (!IsDriving) return;
        IsDriving = false;
        _rig.acceptInput = false;
        _rig.speed = 0f;

        _cockpit.Hide();

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

        // restore the truck body
        if (_truckBody != null) _truckBody.enabled = true;
        SetBeh(_billboard, true); SetBeh(_dirSprite, true); SetBeh(_carLights, true);

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
        if (_cc != null) _cc.enabled = false;   // park: stop being a mover
    }

    void Update()
    {
        if (!IsDriving) return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;
        if (_enterCooldown > 0f) _enterCooldown -= Time.deltaTime;

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

        Vector3 move = transform.forward * (_rig.speed * maxSpeed);
        if (_cc.isGrounded && _vSpeed < 0f) _vSpeed = -2f;
        _vSpeed += gravity * dt;
        _cc.Move((move + Vector3.up * _vSpeed) * dt);
    }

    void BuildRearCamera()
    {
        var go = new GameObject("RearCamera");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 1.4f, 0f);
        go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // look back down the road
        _rearCam = go.AddComponent<Camera>();
        _rearCam.nearClipPlane = 0.05f;
        _rearCam.fieldOfView = 70f;
        _rearCam.GetUniversalAdditionalCameraData().SetRenderer(1);    // the 3D renderer (see EnsureRenderer3D)
        _rearRT = new RenderTexture(256, 96, 16) { name = "RearViewRT" };
        _rearCam.targetTexture = _rearRT;
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
}
