using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// The house the player enters. As the player walks around it, it shows directional views
/// (front / back / east / west); a Billboard keeps the chosen view facing the camera. From the
/// front a "Press E to enter" prompt appears; pressing E plays the door-opening frames, fades to
/// black, and loads the interior scene.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class HousePortal : MonoBehaviour
{
    public Transform player;
    public Transform cameraTransform;       // used to pick the directional view
    public Sprite[] doorFrames;             // front: 0 = closed ... last = open
    public Sprite backSprite;
    public Sprite sideSprite;               // viewed from the east (+X)
    public Sprite sideMirrorSprite;         // viewed from the west (-X)
    public string interiorScene = "Sandbox3D";
    public float range = 4f;
    public float openFps = 8f;
    public float fadeDuration = 0.7f;

    SpriteRenderer _sr;
    bool _entering;
    float _fade;
    Texture2D _black;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        if (doorFrames != null && doorFrames.Length > 0) _sr.sprite = doorFrames[0];
        _black = new Texture2D(1, 1);
        _black.SetPixel(0, 0, Color.white);
        _black.Apply();
    }

    void Update()
    {
        if (_entering || player == null) return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

        Vector3 a = player.position; a.y = 0f;
        Vector3 b = transform.position; b.y = 0f;
        bool inFront = Vector3.Distance(a, b) <= range && player.position.z < transform.position.z;
        if (!inFront) return;

        if (DialogUI.Instance != null) DialogUI.Instance.ShowPrompt("Press E to enter");
        if (EnterPressed()) StartCoroutine(Enter());
    }

    void LateUpdate()
    {
        // pick the directional view by where the camera is around the house (the Billboard
        // component handles facing it toward the camera)
        if (_entering || cameraTransform == null || _sr == null) return;
        Vector3 toCam = cameraTransform.position - transform.position;
        toCam.y = 0f;
        if (toCam.sqrMagnitude < 0.0001f) return;
        _sr.sprite = PickView(toCam.normalized);
    }

    Sprite PickView(Vector3 d)
    {
        float south = -d.z;     // +1 = camera in front (the door side)
        float east  =  d.x;     // +1 = camera to the east
        if (Mathf.Abs(south) >= Mathf.Abs(east))
            return south >= 0f ? Front() : Or(backSprite);
        return east >= 0f ? Or(sideSprite) : Or(sideMirrorSprite);
    }

    Sprite Front() => (doorFrames != null && doorFrames.Length > 0) ? doorFrames[0] : null;
    Sprite Or(Sprite s) => s != null ? s : Front();

    IEnumerator Enter()
    {
        _entering = true;
        _sr.sprite = Front();

        if (doorFrames != null && doorFrames.Length > 0)
        {
            float t = 0f;
            int n = doorFrames.Length;
            int f = 0;
            while (f < n)
            {
                f = Mathf.FloorToInt(t * openFps);
                _sr.sprite = doorFrames[Mathf.Clamp(f, 0, n - 1)];
                t += Time.deltaTime;
                yield return null;
            }
            _sr.sprite = doorFrames[n - 1];
        }
        yield return new WaitForSeconds(0.25f);

        float ft = 0f;
        while (ft < fadeDuration)
        {
            ft += Time.deltaTime;
            _fade = Mathf.Clamp01(ft / fadeDuration);
            yield return null;
        }
        _fade = 1f;
        SceneManager.LoadScene(interiorScene);
    }

    void OnGUI()
    {
        if (_fade <= 0f) return;
        var prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, _fade);
        GUI.depth = -1000;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _black);
        GUI.color = prev;
    }

    bool EnterPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }
}
