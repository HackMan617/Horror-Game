using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Survival clock for the nightmare. Counts the 8 in-game hours across
/// survivalSeconds of real time; on completion the player "wakes" back to the
/// lobby. (No death path yet — the threat comes later.)
/// </summary>
public class NightmareClock : MonoBehaviour
{
    [Tooltip("Real seconds to survive the full set of in-game hours.")]
    public float survivalSeconds = 300f;
    public int totalHours = 8;
    public Text display;

    float _elapsed;
    bool _done;

    void Update()
    {
        if (_done) return;

        _elapsed += Time.deltaTime;
        float frac = Mathf.Clamp01(_elapsed / survivalSeconds);
        int hour = Mathf.Min(totalHours, Mathf.FloorToInt(frac * totalHours) + 1);
        if (display != null) display.text = $"Hour {hour} / {totalHours}";

        if (_elapsed >= survivalSeconds)
        {
            _done = true;
            if (GameManager.Instance != null)
                GameManager.Instance.ReturnToLobby(true);
        }
    }
}
