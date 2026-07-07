using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Instrument + input state for the truck cockpit (Assets/Animation/Car POV/cockpit_kit/DRIVING.md §3).
/// One MonoBehaviour owns every number the dashboard reads — speed, steer, fuel, distance — integrated
/// each frame from held input while <see cref="acceptInput"/> is on. <see cref="CockpitController"/> reads
/// these to drive the gauges/wheel/charm/mirror; <see cref="TruckDriver"/> reads <see cref="speed"/> and
/// <see cref="steer"/> to actually move the truck through the world. Empty fuel zeroes the throttle
/// target — you coast to a stop and can't pull away until <see cref="Refuel"/>.
/// </summary>
public class DrivingRig : MonoBehaviour
{
    [Range(0, 1)] public float speed;      // 0..1  (×80 = mph)
    [Range(-1, 1)] public float steer;     // -1 left .. +1 right
    [Range(0, 1)] public float fuel = 1f;  // 1 full .. 0 empty
    public float distance;                 // world "miles" travelled — odometer driver
    public float rearFill = 1f;            // 1 full .. 0 drained (rear-view mirror darkening)
    public bool nightmare;

    [Header("tuning (DRIVING.md §8)")]
    public float accel = 1.6f, dragIdle = 0.28f, dragBrake = 1.6f;
    public float fuelBurn = 0.018f, milesPerSec = 22f;

    [Tooltip("Held gas/steer input is read here only while true (TruckDriver toggles it on entering the cab).")]
    public bool acceptInput = false;

    float throttle, brake, steerIn;   // held input, integrated in Update

    public void Refuel() => fuel = 1f;

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

        if (acceptInput) ReadInput();
        else { throttle = brake = steerIn = 0f; }

        float dt = Time.deltaTime;
        bool hasFuel = fuel > 0.001f;
        float target = (throttle > 0 && hasFuel) ? 1f : 0f;

        speed += (target - speed) * (throttle > 0 && hasFuel ? accel : 0f) * dt;
        speed -= (brake > 0 ? dragBrake : dragIdle) * speed * dt;
        speed = Mathf.Clamp01(speed);

        steer += (steerIn - steer) * Mathf.Min(1, dt * 8f);

        fuel = Mathf.Max(0, fuel - speed * dt * fuelBurn);
        distance += speed * dt * milesPerSec;

        // Rear-view drains toward black with speed ("no turning back", DRIVING.md §5).
        rearFill = Mathf.MoveTowards(rearFill, 1f - speed * 0.92f, dt * 2.5f);
    }

    // WASD/arrows drive: W/Up = gas (held), S/Down = brake, A/D or Left/Right = steer.
    void ReadInput()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) { throttle = brake = steerIn = 0f; return; }
        throttle = (kb.wKey.isPressed || kb.upArrowKey.isPressed) ? 1f : 0f;
        brake = (kb.sKey.isPressed || kb.downArrowKey.isPressed) ? 1f : 0f;
        float s = 0f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) s -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) s += 1f;
        steerIn = s;
#else
        throttle = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) ? 1f : 0f;
        brake = (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) ? 1f : 0f;
        steerIn = Input.GetAxisRaw("Horizontal");
#endif
    }
}
