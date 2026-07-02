// InteriorAtlas.cs
// The living-room furniture atlas — rects, palettes, and slicing. Companion to InteriorObject.cs.
//
// Three atlas textures share ONE layout (256x96), so a palette swap or the nightmare swap is
// just a different texture sampled with the same rects:
//   interior_furniture_dusk.png       — cool teal room, warm rust furniture
//   interior_furniture_lavender.png   — faded-purple room, dusty-rose furniture
//   interior_furniture_nightmare.png  — the dream-rot pass (flickers in on the dread flag)
//
// Import settings for all three: Read/Write ON · Filter Point · Compression None.

using System.Collections.Generic;
using UnityEngine;

namespace Game.Interior
{
    public enum InteriorPalette { Dusk = 0, Lavender = 1, Nightmare = 2 }

    /// <summary>One object's slot in the atlas (frame 0 rect, laid out horizontally).</summary>
    public struct AtlasItem
    {
        public int x, y, w, h, frames;
        public AtlasItem(int x, int y, int w, int h, int frames) { this.x = x; this.y = y; this.w = w; this.h = h; this.frames = frames; }
    }

    public static class InteriorAtlas
    {
        public const int SHEET_W = 256, SHEET_H = 128;

        // name -> frame-0 rect (TOP-LEFT origin, like the PNG) + frame count.
        // Frames are laid out horizontally: frame f is at (x + f*w, y).
        // Solid pieces carry three facings (FRONT / <name>Back / <name>Side) so they can sit against
        // any wall; the SIDE profile is narrower. See Assets/Animation/INTERIOR_UPDATE.md.
        public static readonly Dictionary<string, AtlasItem> Items = new Dictionary<string, AtlasItem>
        {
            // row 0 — sofa trio · loveseat trio
            { "sofa",          new AtlasItem(  0,   0, 48, 32, 1) },
            { "sofaBack",      new AtlasItem( 48,   0, 48, 32, 1) },
            { "sofaSide",      new AtlasItem( 96,   0, 30, 32, 1) },
            { "couch",         new AtlasItem(126,   0, 32, 32, 1) },   // loveseat
            { "couchBack",     new AtlasItem(158,   0, 32, 32, 1) },
            { "couchSide",     new AtlasItem(190,   0, 30, 32, 1) },
            // row 1 — armchair trio · bookshelf trio · lamp
            { "armchair",      new AtlasItem(  0,  32, 32, 32, 1) },
            { "armchairBack",  new AtlasItem( 32,  32, 32, 32, 1) },
            { "armchairSide",  new AtlasItem( 64,  32, 30, 32, 1) },
            { "bookshelf",     new AtlasItem( 94,  32, 32, 32, 1) },
            { "bookshelfBack", new AtlasItem(126,  32, 32, 32, 1) },
            { "bookshelfSide", new AtlasItem(158,  32, 18, 32, 1) },
            { "floorLamp",     new AtlasItem(176,  32, 16, 32, 2) },   // 0 off · 1 on
            // row 2 — tv trio (front is the 4-frame clip)
            { "tv",            new AtlasItem(  0,  64, 32, 32, 4) },   // 0 off · 1-2 on · 3 static
            { "tvBack",        new AtlasItem(128,  64, 32, 32, 1) },
            { "tvSide",        new AtlasItem(160,  64, 26, 32, 1) },
            // row 3 — combo · table · rug
            { "couchDog",      new AtlasItem(  0,  96, 48, 32, 3) },   // dog asleep on the sofa · 0-2 breathing
            { "coffeeTable",   new AtlasItem(144,  96, 32, 16, 1) },
            { "rug",           new AtlasItem(144, 112, 48, 16, 1) },   // draw UNDER furniture
        };

        /// <summary>Slice every frame of one object into sprites. Handles the top-left→Unity
        /// (bottom-left) Y flip, so multi-row atlas cells land correctly.</summary>
        public static Sprite[] Slice(Texture2D atlas, string name, float ppu, Vector2 pivot)
        {
            if (atlas == null || !Items.TryGetValue(name, out AtlasItem it)) return null;
            var sprites = new Sprite[it.frames];
            for (int f = 0; f < it.frames; f++)
            {
                float uy = atlas.height - (it.y + it.h);                 // flip Y (PNG is top-left origin)
                var rect = new Rect(it.x + f * it.w, uy, it.w, it.h);
                sprites[f] = Sprite.Create(atlas, rect, pivot, ppu, 0, SpriteMeshType.FullRect);
                sprites[f].name = name + "_" + f;
            }
            return sprites;
        }

        public static int FrameCount(string name) => Items.TryGetValue(name, out AtlasItem it) ? it.frames : 1;
    }
}
