// CharacterPalette.cs
// Runtime palette-swap for the player character.
// Keeps ONE master sheet per facing and recolors a per-character copy at spawn.
// Eyes are baked to a magenta sentinel (255,0,255) in the master sheets so they
// swap uniformly with everything else (they share black with the outline otherwise).
//
// Per-player save data is just a CharacterLook (five small ints) — never a generated image.

using System.Collections.Generic;
using UnityEngine;

namespace Game.Characters
{
    /// <summary>The five customization choices stored per player.</summary>
    [System.Serializable]
    public struct CharacterLook
    {
        public int hair;
        public int skin;
        public int eyes;
        public int shirt;
        public int pants;

        public static CharacterLook Default => new CharacterLook { hair = 0, skin = 1, eyes = 0, shirt = 0, pants = 0 };
    }

    public static class CharacterPalette
    {
        // ---- A two-tone clothing/hair/skin option (base + shadow) ----
        public struct Ramp
        {
            public string name;
            public string baseHex;
            public string shadowHex;
            public Ramp(string n, string b, string s) { name = n; baseHex = b; shadowHex = s; }
        }

        // ---- A single-color option (eyes) ----
        public struct Tone
        {
            public string name;
            public string hex;
            public Tone(string n, string h) { name = n; hex = h; }
        }

        // =====================================================================
        // Source colors baked into the master sheet — DO NOT change these.
        // They are the exact pixels the swap matches against.
        // =====================================================================
        static readonly Color32 SkinBase  = C(240, 184, 144), SkinShadow  = C(192, 122, 76);
        static readonly Color32 HairBase  = C(156, 90, 38),   HairShadow  = C(94, 52, 16);
        static readonly Color32 ShirtBase = C(216, 48, 48),   ShirtShadow = C(152, 32, 24);
        static readonly Color32 PantsBase = C(58, 91, 208),   PantsShadow = C(38, 57, 140);
        static readonly Color32 EyeKey    = C(255, 0, 255);   // sentinel baked into master sheets

        // =====================================================================
        // Selectable options — these mirror the in-tool Character Creator palette.
        // Add/remove freely; indices are what you store per player.
        // =====================================================================
        public static readonly Ramp[] Hair =
        {
            new Ramp("Brunette", "#9c5a26", "#5e3410"),
            new Ramp("Blonde",   "#e6bd63", "#b0863a"),
            new Ramp("Black",    "#2c2c33", "#161619"),
            new Ramp("Auburn",   "#a8442a", "#6e2818"),
            new Ramp("Chestnut", "#6b4423", "#3f2614"),
            new Ramp("Ash Grey", "#b9b9c4", "#83838f"),
            new Ramp("Crimson",  "#c0303a", "#7e1c24"),
            new Ramp("Mint",     "#7fd0a8", "#4f9e78"),
        };

        public static readonly Ramp[] Skin =
        {
            new Ramp("Porcelain", "#f6cda8", "#d49a6e"),
            new Ramp("Light",     "#f0b890", "#c07a4c"),
            new Ramp("Tan",       "#d99a5c", "#a86a34"),
            new Ramp("Brown",     "#a86a40", "#714525"),
            new Ramp("Deep",      "#7a4a2c", "#4f2e18"),
            new Ramp("Rich",      "#553018", "#371d0e"),
        };

        public static readonly Tone[] Eyes =
        {
            new Tone("Brown", "#4a2e1a"),
            new Tone("Black", "#1a1410"),
            new Tone("Blue",  "#2f5bd0"),
            new Tone("Green", "#2f7d4f"),
            new Tone("Hazel", "#8a6a2a"),
            new Tone("Amber", "#c07a1a"),
            new Tone("Grey",  "#6a7480"),
        };

        public static readonly Ramp[] Shirt =
        {
            new Ramp("Red",      "#d83030", "#982018"),
            new Ramp("Blue",     "#2a5bd0", "#1c3c8c"),
            new Ramp("Green",    "#2f9d52", "#1d6a36"),
            new Ramp("Purple",   "#7a3fb0", "#512876"),
            new Ramp("Teal",     "#2aa0a0", "#1c6e6e"),
            new Ramp("Charcoal", "#34343c", "#1c1c22"),
            new Ramp("Gold",     "#e0b020", "#a87e14"),
            new Ramp("Cream",    "#e8e0c8", "#b8ac8c"),
        };

        public static readonly Ramp[] Pants =
        {
            new Ramp("Blue",   "#3a5bd0", "#26398c"),
            new Ramp("Slate",  "#5a5a64", "#3a3a42"),
            new Ramp("Khaki",  "#a8895a", "#6e5832"),
            new Ramp("Black",  "#2c2c33", "#161619"),
            new Ramp("Forest", "#3f6a3f", "#284628"),
            new Ramp("Maroon", "#8a2f3a", "#5a1c24"),
        };

        // =====================================================================
        // Core: build the color-key map for a given look.
        // =====================================================================
        public static Dictionary<int, Color32> BuildMap(CharacterLook look)
        {
            Ramp hair  = Hair[Mathf.Clamp(look.hair, 0, Hair.Length - 1)];
            Ramp skin  = Skin[Mathf.Clamp(look.skin, 0, Skin.Length - 1)];
            Ramp shirt = Shirt[Mathf.Clamp(look.shirt, 0, Shirt.Length - 1)];
            Ramp pants = Pants[Mathf.Clamp(look.pants, 0, Pants.Length - 1)];
            Tone eye   = Eyes[Mathf.Clamp(look.eyes, 0, Eyes.Length - 1)];

            var map = new Dictionary<int, Color32>
            {
                { Key(SkinBase),  Hex(skin.baseHex)   }, { Key(SkinShadow),  Hex(skin.shadowHex)  },
                { Key(HairBase),  Hex(hair.baseHex)   }, { Key(HairShadow),  Hex(hair.shadowHex)  },
                { Key(ShirtBase), Hex(shirt.baseHex)  }, { Key(ShirtShadow), Hex(shirt.shadowHex) },
                { Key(PantsBase), Hex(pants.baseHex)  }, { Key(PantsShadow), Hex(pants.shadowHex) },
                { Key(EyeKey),    Hex(eye.hex)        },
            };
            return map;
        }

        /// <summary>Produce a recolored copy of a master sheet for the given look.</summary>
        public static Texture2D Recolor(Texture2D master, CharacterLook look)
        {
            if (master == null) return null;
            if (!master.isReadable)
            {
                Debug.LogError("[CharacterPalette] Master texture '" + master.name +
                               "' is not readable. Tick 'Read/Write Enabled' in its import settings.");
                return master;
            }

            Dictionary<int, Color32> map = BuildMap(look);
            Color32[] src = master.GetPixels32();
            var dst = new Color32[src.Length];

            for (int i = 0; i < src.Length; i++)
            {
                Color32 p = src[i];
                if (p.a < 200) { dst[i] = p; continue; }          // keep transparent / outline antialias
                if (map.TryGetValue(Key(p), out Color32 rep))
                {
                    rep.a = p.a;
                    dst[i] = rep;
                }
                else
                {
                    dst[i] = p;                                    // outline black, shoe grey, etc. untouched
                }
            }

            var tex = new Texture2D(master.width, master.height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = master.name + "_recolored"
            };
            tex.SetPixels32(dst);
            tex.Apply();
            return tex;
        }

        /// <summary>Slice a single-row sheet into one sprite per frame.</summary>
        public static Sprite[] Slice(Texture2D tex, int frameW, int frameH, float ppu, Vector2 pivot)
        {
            int count = Mathf.Max(1, tex.width / frameW);
            var sprites = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                sprites[i] = Sprite.Create(
                    tex,
                    new Rect(i * frameW, 0, frameW, frameH),
                    pivot, ppu, 0, SpriteMeshType.FullRect);
                sprites[i].name = tex.name + "_" + i;
            }
            return sprites;
        }

        // ---- helpers ----
        static Color32 C(byte r, byte g, byte b) => new Color32(r, g, b, 255);
        static int Key(Color32 p) => (p.r << 16) | (p.g << 8) | p.b;
        static Color32 Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return (Color32)c;
        }
    }
}
