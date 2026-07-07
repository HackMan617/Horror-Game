using UnityEngine;

/// <summary>
/// Makes the drivable road loop: a patch of road at the far end that, when the DRIVING truck rolls onto
/// it, seamlessly teleports the truck back to <see cref="returnPosition"/> (the loop's start, beside the
/// town sign) instead of loading a scene. Keeps the truck's heading and speed, so driving on just wraps
/// you back past the sign — an endless road that always comes home to the sign. Detection mirrors
/// <see cref="SceneExitTrigger"/> (an XZ-box test against the active <see cref="TruckDriver"/>).
/// </summary>
public class DriveLoopTrigger : MonoBehaviour
{
    [Tooltip("Half-size of the detection box in X (across road) and Z (along road). Y is ignored.")]
    public Vector2 halfExtents = new Vector2(6f, 3f);
    [Tooltip("World spot the truck is placed at when it wraps (the loop start, by the town sign).")]
    public Vector3 returnPosition;
    [Tooltip("Seconds after a wrap before this can fire again, so the truck clears the box first.")]
    public float cooldown = 1.5f;

    TruckDriver _truck;
    float _cd;

    void Start() { _truck = FindObjectOfType<TruckDriver>(); }

    void Update()
    {
        if (_cd > 0f) { _cd -= Time.deltaTime; return; }
        if (_truck == null) { _truck = FindObjectOfType<TruckDriver>(); if (_truck == null) return; }
        if (!_truck.IsDriving) return;

        Vector3 d = _truck.transform.position - transform.position;
        if (Mathf.Abs(d.x) <= halfExtents.x && Mathf.Abs(d.z) <= halfExtents.y)
        {
            _truck.TeleportTo(returnPosition);
            _cd = cooldown;
        }
    }
}
