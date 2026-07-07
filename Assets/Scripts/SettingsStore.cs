using UnityEngine;

/// <summary>
/// Persists player-adjustable game settings (sound volume, mouse sensitivity) across scenes and
/// sessions via <see cref="PlayerPrefs"/> — the same lightweight pattern as <see cref="CharacterStore"/>.
/// Values are clamped on read and write so a hand-edited pref can never push the game out of range.
/// The runtime application of these values (and the lockable-sound horror hook) lives in
/// <see cref="SettingsManager"/>.
/// </summary>
public static class SettingsStore
{
    const string Key = "HG_Settings_";

    public const float DefaultSoundVolume = 1f;
    public const float DefaultSensitivity = 0.12f;   // matches CameraRig / PlayerController3D defaults
    public const float MinSensitivity = 0.02f;
    public const float MaxSensitivity = 0.40f;

    /// <summary>Master listener volume the player has chosen, 0..1. Default full.</summary>
    public static float SoundVolume
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(Key + "sound", DefaultSoundVolume));
        set { PlayerPrefs.SetFloat(Key + "sound", Mathf.Clamp01(value)); PlayerPrefs.Save(); }
    }

    /// <summary>Mouse-look sensitivity fed to both the yaw (PlayerController3D) and pitch (CameraRig) fields.</summary>
    public static float MouseSensitivity
    {
        get => Mathf.Clamp(PlayerPrefs.GetFloat(Key + "sens", DefaultSensitivity), MinSensitivity, MaxSensitivity);
        set { PlayerPrefs.SetFloat(Key + "sens", Mathf.Clamp(value, MinSensitivity, MaxSensitivity)); PlayerPrefs.Save(); }
    }

    /// <summary>Restore every setting to its default.</summary>
    public static void ResetToDefaults()
    {
        SoundVolume = DefaultSoundVolume;
        MouseSensitivity = DefaultSensitivity;
    }
}
