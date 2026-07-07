using UnityEngine;

/// <summary>
/// Keeps a far-range backdrop (the <see cref="MountainBackdrop"/> ring rig) centred on the camera's XZ so
/// it reads as infinitely distant while you drive the long road — the ridgeline never gets closer, only
/// the road and roadside props scroll past. Y is left alone so the horizon line stays put. Used in the
/// OutOfTown drive; the Exterior yard keeps its static backdrop.
/// </summary>
public class BackdropFollow : MonoBehaviour
{
    public bool followX = true, followZ = true;
    float _y;

    void Start() { _y = transform.position.y; }

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 p = transform.position, c = cam.transform.position;
        if (followX) p.x = c.x;
        if (followZ) p.z = c.z;
        p.y = _y;
        transform.position = p;
    }
}
