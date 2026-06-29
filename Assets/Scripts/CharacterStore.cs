using UnityEngine;
using Game.Characters;

/// <summary>
/// Persists the player's chosen CharacterLook (five small ints) across scenes and
/// sessions via PlayerPrefs, and defines a few ready-made preset looks for players
/// who don't want to build their own.
/// </summary>
public static class CharacterStore
{
    const string Key = "HG_CharacterLook_";

    public static void Save(CharacterLook look)
    {
        PlayerPrefs.SetInt(Key + "hair",  look.hair);
        PlayerPrefs.SetInt(Key + "skin",  look.skin);
        PlayerPrefs.SetInt(Key + "eyes",  look.eyes);
        PlayerPrefs.SetInt(Key + "shirt", look.shirt);
        PlayerPrefs.SetInt(Key + "pants", look.pants);
        PlayerPrefs.SetInt(Key + "set",   1);
        PlayerPrefs.Save();
    }

    public static CharacterLook Load()
    {
        if (PlayerPrefs.GetInt(Key + "set", 0) == 0) return CharacterLook.Default;
        var d = CharacterLook.Default;
        return new CharacterLook
        {
            hair  = PlayerPrefs.GetInt(Key + "hair",  d.hair),
            skin  = PlayerPrefs.GetInt(Key + "skin",  d.skin),
            eyes  = PlayerPrefs.GetInt(Key + "eyes",  d.eyes),
            shirt = PlayerPrefs.GetInt(Key + "shirt", d.shirt),
            pants = PlayerPrefs.GetInt(Key + "pants", d.pants),
        };
    }

    public struct Preset
    {
        public string name;
        public CharacterLook look;
        public Preset(string n, CharacterLook l) { name = n; look = l; }
    }

    // Indices map into the CharacterPalette option tables (Hair/Skin/Eyes/Shirt/Pants).
    public static readonly Preset[] Presets =
    {
        new Preset("Blonde Teal", new CharacterLook { hair = 1, skin = 1, eyes = 2, shirt = 4, pants = 1 }),
        new Preset("Crimson",     new CharacterLook { hair = 6, skin = 1, eyes = 5, shirt = 0, pants = 5 }),
        new Preset("Noir",        new CharacterLook { hair = 2, skin = 0, eyes = 6, shirt = 5, pants = 3 }),
    };
}
