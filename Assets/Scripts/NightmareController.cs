using UnityEngine;

/// <summary>
/// Prototype "enter the nightmare" transition for the 3D sandbox. On Start it
/// captures the scene's normal lighting; EnterNightmare() fades the screen to
/// black, swaps to a darker lighting preset (and tints the unlit sprites down so
/// they darken too) while the screen is black, then fades back in. One-way for
/// now — the real nightmare lighting/threat comes later. The fade is drawn with
/// OnGUI so it needs no Canvas or EventSystem.
/// </summary>
public class NightmareController : MonoBehaviour
{
    [Header("References")]
    public Light sun;                       // directional light to dim

    [Header("Timing")]
    public float fadeDuration = 0.6f;       // seconds to fade out (then back in)

    [Header("Nightmare look")]
    public Color nightmareAmbient = new Color(0.05f, 0.05f, 0.08f);
    public Color nightmareSun = new Color(0.25f, 0.30f, 0.45f);
    public float nightmareSunIntensity = 0.35f;
    public Color spriteTint = new Color(0.5f, 0.5f, 0.6f, 1f);   // multiplied into each sprite's colour

    public bool IsNightmare { get; private set; }

    float _fade;          // 0 = clear, 1 = fully black
    int _phase;           // 0 idle, 1 fading out, 2 fading back in
    Texture2D _black;

    void Start()
    {
        _black = new Texture2D(1, 1);
        _black.SetPixel(0, 0, Color.white);   // tinted by GUI.color
        _black.Apply();
    }

    public void EnterNightmare()
    {
        if (IsNightmare || _phase != 0) return;
        IsNightmare = true;
        _phase = 1;
    }

    void Update()
    {
        if (_phase == 0) return;
        float step = Time.deltaTime / Mathf.Max(0.01f, fadeDuration);

        if (_phase == 1)                       // fade to black
        {
            _fade += step;
            if (_fade >= 1f)
            {
                _fade = 1f;
                ApplyNightmareLook();          // swap while the screen is black
                _phase = 2;
            }
        }
        else if (_phase == 2)                  // fade back in over the dark scene
        {
            _fade -= step;
            if (_fade <= 0f) { _fade = 0f; _phase = 0; }
        }
    }

    void ApplyNightmareLook()
    {
        RenderSettings.ambientLight = nightmareAmbient;
        if (sun != null) { sun.color = nightmareSun; sun.intensity = nightmareSunIntensity; }

        // Unlit sprites ignore scene lighting, so tint them down by hand.
        foreach (var sr in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            sr.color = sr.color * spriteTint;
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
}
