using UnityEngine;

/// <summary>
/// Makes a sprite face the camera (2.5D billboard). Y-axis-only keeps it upright
/// like a Doom/PSX-style sprite standing on the ground.
/// </summary>
[ExecuteAlways]
public class Billboard : MonoBehaviour
{
    public bool yAxisOnly = true;

    Transform _cam;

    void LateUpdate()
    {
        if (_cam == null)
        {
            var c = Camera.main;
            if (c == null) return;
            _cam = c.transform;
        }

        Vector3 toCam = _cam.position - transform.position;
        if (yAxisOnly) toCam.y = 0f;
        if (toCam.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.LookRotation(toCam, Vector3.up);
    }
}
