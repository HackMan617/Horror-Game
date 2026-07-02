// NeighborPalette.cs
// Runtime workwear recolor for Robert Abernathy's DAYTIME sheet.
//
// The daytime sheet (neighbor_robert_front.png) bakes his clothing in flat, region-keyed
// colors. This swaps four regions — coveralls, turtleneck, gloves, boots — against curated
// "worn" palettes, deriving each region's shadow from its base so the ramps stay coherent.
//
// Per the design, Robert should roll a FRESH assortment every time the game loads, so he's
// never quite the same man twice — call RobertLook.Roll() at spawn. Skin, grey hair, the
// glasses and the shears are IDENTITY and stay fixed. The nightmare sheet is a separate,
// drained texture that overrides his colors entirely, so it is never recolored.
//
// Mirrors CharacterPalette.cs in structure and matches the web preview's shade factors
// exactly, so a rolled look reads identically in-engine and in the .dc.html reference.

using System.Collections.Generic;
using UnityEngine;

namespace Game.Neighbors
{
    /// <summary>Robert's rolled workwear choices. Four small ints — or just call Roll().</summary>
    [System.Serializable]
    public struct RobertLook
    {
        public int coveralls;
        public int turtleneck;
        public int gloves;
        public int boots;

        public static RobertLook Default => new RobertLook { coveralls = 0, turtleneck = 0, gloves = 0, boots = 0 };

        /// <summary>A fresh random assortment. Call once at spawn/load.</summary>
        public static RobertLook Roll()
        {
            return new RobertLook
            {
                coveralls  = Random.Range(0, NeighborPalette.Coveralls.Length),
                turtleneck = Random.Range(0, NeighborPalette.Turtleneck.Length),
                gloves     = Random.Range(0, NeighborPalette.Gloves.Length),
                boots      = Random.Range(0, NeighborPalette.Boots.Length),
            };
        }
    }

    public static class NeighborPalette
    {
        // =====================================================================
        // Source colors baked into neighbor_robert_front.png — DO NOT change.
        // These are the exact pixels the swap matches against.
        // =====================================================================
        static readonly Color32 CovBase   = C(95, 116, 136), CovShadow  = C(66, 82, 95), Strap = C(60, 72, 85);
        static readonly Color32 NeckBase  = C(43, 47, 54),    NeckShadow = C(24, 27, 32);
        static readonly Color32 GloveBase = C(216, 194, 78),  GloveShadow = C(166, 146, 58);
        static readonly Color32 BootBase  = C(58, 51, 44),    BootShadow = C(36, 31, 26);

        // Shade factors — must match the .dc.html preview so rolls look the same everywhere.
        const float CovShadeF = 0.72f, StrapF = 0.58f, NeckShadeF = 0.58f, GloveShadeF = 0.76f, BootShadeF = 0.60f;

        // =====================================================================
        // Curated "worn" pools. Indices are what RobertLook stores.
        // Base colors only — the shadow is derived by darkening.
        // =====================================================================
        public static readonly string[] Coveralls =
        {
            "#5f7488", // denim slate (default)
            "#6b6a52", // olive drab
            "#7a5a44", // rust brown
            "#5a6b5f", // grey-green
            "#736347", // khaki
            "#4f5a6b", // steel blue
            "#6e4f57", // oxblood mauve
            "#556655", // moss
        };
        public static readonly string[] Turtleneck =
        {
            "#2b2f36", // black (the Jobs rollneck, default)
            "#2e2733", // dark plum
            "#233029", // dark green
            "#33291f", // dark brown
            "#252a30", // charcoal
        };
        public static readonly string[] Gloves =
        {
            "#d8c24e", // yellow rubber (default)
            "#4f8fb0", // blue
            "#b04f4f", // red
            "#5aa06a", // green
            "#c8792e", // orange
            "#9a5fa0", // violet
        };
        public static readonly string[] Boots =
        {
            "#3a332c", // dark brown (default)
            "#2f2f33", // black
            "#4a3f30", // tan-brown
            "#413229", // umber
        };

        // =====================================================================
        // Build the color-key map for a rolled look.
        // =====================================================================
        public static Dictionary<int, Color32> BuildMap(RobertLook look)
        {
            Color32 cov   = Hex(Coveralls [Mathf.Clamp(look.coveralls,  0, Coveralls.Length  - 1)]);
            Color32 neck  = Hex(Turtleneck[Mathf.Clamp(look.turtleneck, 0, Turtleneck.Length - 1)]);
            Color32 glove = Hex(Gloves    [Mathf.Clamp(look.gloves,     0, Gloves.Length     - 1)]);
            Color32 boot  = Hex(Boots     [Mathf.Clamp(look.boots,      0, Boots.Length      - 1)]);

            return new Dictionary<int, Color32>
            {
                { Key(CovBase),    cov                    }, { Key(CovShadow),  Darken(cov, CovShadeF)  },
                { Key(Strap),      Darken(cov, StrapF)    },
                { Key(NeckBase),   neck                   }, { Key(NeckShadow), Darken(neck, NeckShadeF) },
                { Key(GloveBase),  glove                  }, { Key(GloveShadow),Darken(glove, GloveShadeF) },
                { Key(BootBase),   boot                   }, { Key(BootShadow), Darken(boot, BootShadeF) },
            };
        }

        /// <summary>Produce a recolored copy of the DAYTIME sheet for a rolled look.</summary>
        public static Texture2D Recolor(Texture2D daytimeSheet, RobertLook look)
        {
            if (daytimeSheet == null) return null;
            if (!daytimeSheet.isReadable)
            {
                Debug.LogError("[NeighborPalette] '" + daytimeSheet.name +
                               "' is not readable. Tick 'Read/Write Enabled' in its import settings.");
                return daytimeSheet;
            }

            Dictionary<int, Color32> map = BuildMap(look);
            Color32[] src = daytimeSheet.GetPixels32();
            var dst = new Color32[src.Length];

            for (int i = 0; i < src.Length; i++)
            {
                Color32 p = src[i];
                if (p.a < 200) { dst[i] = p; continue; }            // keep transparent / outline
                if (map.TryGetValue(Key(p), out Color32 rep)) { rep.a = p.a; dst[i] = rep; }
                else dst[i] = p;                                    // skin/hair/glasses/shears/outline untouched
            }

            var tex = new Texture2D(daytimeSheet.width, daytimeSheet.height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = daytimeSheet.name + "_rolled"
            };
            tex.SetPixels32(dst);
            tex.Apply();
            return tex;
        }

        /// <summary>Slice a single-row sheet into one sprite per 32px frame.</summary>
        public static Sprite[] Slice(Texture2D tex, int frameW, int frameH, float ppu, Vector2 pivot)
        {
            if (tex == null) return null;
            int count = Mathf.Max(1, tex.width / frameW);
            var sprites = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                sprites[i] = Sprite.Create(tex, new Rect(i * frameW, 0, frameW, frameH), pivot, ppu, 0, SpriteMeshType.FullRect);
                sprites[i].name = tex.name + "_" + i;
            }
            return sprites;
        }

        // ---- helpers ----
        static Color32 C(byte r, byte g, byte b) => new Color32(r, g, b, 255);
        static int Key(Color32 p) => (p.r << 16) | (p.g << 8) | p.b;
        static Color32 Darken(Color32 c, float f) =>
            new Color32((byte)Mathf.RoundToInt(c.r * f), (byte)Mathf.RoundToInt(c.g * f), (byte)Mathf.RoundToInt(c.b * f), 255);
        static Color32 Hex(string hex) { ColorUtility.TryParseHtmlString(hex, out Color c); return (Color32)c; }
    }
}
