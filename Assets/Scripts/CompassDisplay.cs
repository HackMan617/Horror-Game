using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A corner-of-screen HUD compass (see Assets/Animation/COMPASS.md). Each frame it reads the
/// player's world heading (0° = north / +Z, 90° = east / +X, matching the sky's north) and picks
/// the matching cell from a 32-frame dial sheet, so the dial turns with the direction the player
/// faces. It's a screen-space UI Image, so it stays pinned to the corner regardless of the camera
/// (first/third person, look-behind).
/// </summary>
public class CompassDisplay : MonoBehaviour
{
    [Tooltip("Whose facing to show. Auto-finds the 'Player' object if left empty.")]
    public Transform player;
    [Tooltip("The HUD image to swap. Auto-uses this object's Image if left empty.")]
    public Image image;
    [Tooltip("The 32 sliced dial frames, index 0 = north.")]
    public Sprite[] frames;

    [Header("Calibration")]
    [Tooltip("Flip the spin direction if the dial turns the wrong way.")]
    public bool invert = false;
    [Tooltip("Nudge which frame counts as north, if the art's frame 0 isn't dead north.")]
    public int frameOffset = 0;

    void Start()
    {
        if (image == null) image = GetComponent<Image>();
        AcquirePlayer();
    }

    // Re-find the scene's player. Runs on start and again whenever the reference goes null — e.g.
    // when a persistent HUD carries between the exterior and interior and the old player is gone.
    void AcquirePlayer()
    {
        if (player != null) return;
        var p = GameObject.Find("Player");
        if (p != null) player = p.transform;
    }

    void Update()
    {
        if (player == null) AcquirePlayer();
        if (player == null || image == null || frames == null || frames.Length == 0) return;

        float heading = player.eulerAngles.y;          // world yaw, 0..360 (0 = north / +Z)
        if (invert) heading = 360f - heading;

        int n = frames.Length;
        int i = (Mathf.RoundToInt(heading / 360f * n) + frameOffset) % n;
        if (i < 0) i += n;
        image.sprite = frames[i];
    }
}
