using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// The house the player enters from the exterior. When the player is near it shows a prompt;
/// pressing E plays the door-opening frames once, fades to black, then loads the interior scene.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class HousePortal : MonoBehaviour
{
    public Transform player;
    public Sprite[] doorFrames;             // 0 = closed ... last = fully open
    public string interiorScene = "Sandbox3D";
    public float range = 3.5f;
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
        if (Vector3.Distance(a, b) > range) return;

        if (DialogUI.Instance != null) DialogUI.Instance.ShowPrompt("Press E to enter");
        if (EnterPressed()) StartCoroutine(Enter());
    }

    IEnumerator Enter()
    {
        _entering = true;

        // play the door-opening frames once
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

        // fade to black, then load the interior
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
