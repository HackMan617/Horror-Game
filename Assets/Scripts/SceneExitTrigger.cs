using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// A patch of road that, when the DRIVING truck rolls onto it, fades to black and loads another scene —
/// the road running "off the map" into town, and the road home. Placed on the extended road strip just
/// inside the world boundary so the truck never reaches the hard wall. Detection is a simple XZ-box test
/// against the active <see cref="TruckDriver"/> (robust regardless of CharacterController trigger quirks).
///
/// If <see cref="arriveOnFoot"/> is set, the load hands <see cref="HousePortal"/> an arrival point and the
/// target scene's <see cref="PlayerArrival"/> drops the player there on foot (used for the drive home —
/// you end back at the cabin). Otherwise the target scene brings up its own driver rig (auto-enter).
/// </summary>
public class SceneExitTrigger : MonoBehaviour
{
    [Tooltip("Scene to load when the driving truck enters this patch.")]
    public string targetScene = "OutOfTown";
    [Tooltip("Half-size of the detection box in X (across road) and Z (along road). Y is ignored.")]
    public Vector2 halfExtents = new Vector2(4f, 2.5f);
    public float fadeDuration = 0.7f;

    [Header("Arrival in the target scene")]
    [Tooltip("If on, drop the player on foot at 'arrivalPosition' in the target scene (the drive-home end).")]
    public bool arriveOnFoot = false;
    public Vector3 arrivalPosition;

    TruckDriver _truck;
    bool _firing;
    Texture2D _black;

    void Start() { _truck = FindObjectOfType<TruckDriver>(); }

    void Update()
    {
        if (_firing) return;
        if (_truck == null) { _truck = FindObjectOfType<TruckDriver>(); if (_truck == null) return; }
        if (!_truck.IsDriving) return;

        Vector3 d = _truck.transform.position - transform.position;
        if (Mathf.Abs(d.x) <= halfExtents.x && Mathf.Abs(d.z) <= halfExtents.y)
            StartCoroutine(Fire());
    }

    IEnumerator Fire()
    {
        _firing = true;
        float t = 0f, fade = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime; fade = Mathf.Clamp01(t / fadeDuration);
            _fade = fade; yield return null;
        }
        _fade = 1f;

        if (arriveOnFoot)
        {
            HousePortal.HasArrival = true;
            HousePortal.ArrivalPosition = arrivalPosition;
        }
        SceneManager.LoadScene(targetScene);
    }

    float _fade;
    void OnGUI()
    {
        if (_fade <= 0f) return;
        if (_black == null) { _black = new Texture2D(1, 1); _black.SetPixel(0, 0, Color.white); _black.Apply(); }
        var prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, _fade);
        GUI.depth = -1000;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _black);
        GUI.color = prev;
    }
}
