using UnityEngine;
using Game.Neighbors;

/// <summary>
/// Turns Robert to show the right view (front / back / side) based on where the camera is around him,
/// so his back and side sheets actually become visible as the player circles him — the kit's
/// <see cref="NeighborRobert"/> only changes facing while walking, so a stationary Robert needs this.
/// He keeps a fixed logical facing (his front points down the street); this reads the camera bearing
/// against it and calls FaceDirection. Kept as its own file so it's easy to retune independently.
/// </summary>
[RequireComponent(typeof(NeighborRobert))]
public class NeighborRobertFacing : MonoBehaviour
{
    [Tooltip("Camera/viewer used to choose the visible side. Defaults to Camera.main.")]
    public Transform viewer;
    [Tooltip("World direction Robert's FRONT faces (default -Z / south, toward the street & player).")]
    public Vector3 faceDir = new Vector3(0f, 0f, -1f);

    NeighborRobert _robert;
    int _last = -1;

    void Awake() { _robert = GetComponent<NeighborRobert>(); }

    void LateUpdate()
    {
        Transform cam = viewer != null ? viewer : (Camera.main != null ? Camera.main.transform : null);
        if (cam == null) return;

        Vector3 to = cam.position - transform.position; to.y = 0f;
        if (to.sqrMagnitude < 1e-4f) return;
        to.Normalize();
        Vector3 fwd = faceDir; fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.back;
        fwd.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, fwd);

        float f = Vector3.Dot(to, fwd);      // + => camera in front of Robert (see his front)
        float r = Vector3.Dot(to, right);    // + => camera to his right
        int view;
        if (f > 0.5f) view = 0;              // front
        else if (f < -0.5f) view = 1;        // back
        else view = (r < 0f) ? 2 : 3;        // left (mirrored) / right side

        if (view != _last) { _last = view; _robert.FaceDirection(view); }
    }
}
