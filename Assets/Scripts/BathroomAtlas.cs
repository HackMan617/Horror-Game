// BathroomAtlas.cs
// The cabin bathroom kit — hex penny tile, plank wainscot, the walk-in shower and its fixtures
// (see Assets/Animation/Interior Atlas/bathroom_kit/SHOWER.md). Companion to BathroomFixture.cs,
// and the sibling of InteriorStructureAtlas.cs (structure) and InteriorAtlas.cs (furniture).
//
// ONE texture for now: bathroom_colddusk.png (384x192). The nightmare pass ships later — EXCEPT
// the window, which already has its watcher variant baked into a separate 96x32 sheet
// (bathroom_window_watch.png, 3 frames) that WindowWatcher swaps in.
//
// Rects are transcribed from SHOWER.md (which agrees with the generator's own table in
// bathroomgen.js); origin is TOP-LEFT like the PNG, and frames run horizontally: frame f at
// (x + f*w, y). Slice() does the Y flip into Unity's bottom-left space.
//
// Import settings: Read/Write ON · Filter Point · Compression None.

using System.Collections.Generic;
using UnityEngine;

namespace Game.Interior
{
    public static class BathroomAtlas
    {
        public const int SHEET_W = 384, SHEET_H = 192;

        public static readonly Dictionary<string, AtlasItem> Items = new Dictionary<string, AtlasItem>
        {
            // ---- tileable surfaces (also extracted to standalone tiles for the shell materials) ----
            { "hexFloor",       new AtlasItem(  0,   0, 16, 16, 1) },
            { "hexFloorWorn",   new AtlasItem( 16,   0, 16, 16, 1) },
            { "wainscotWall",   new AtlasItem( 32,   0, 16, 16, 1) },
            { "wainscotCap",    new AtlasItem( 48,   0, 16, 16, 1) },   // the rail sits in the cell's TOP band
            { "plasterWall",    new AtlasItem( 64,   0, 16, 16, 1) },
            { "wetFloor",       new AtlasItem( 80,   0, 16, 16, 1) },   // sheen OVERLAY — fades in over the tile
            { "drainGrate",     new AtlasItem( 96,   0, 16, 16, 1) },
            { "soapClutter",    new AtlasItem(112,   0, 16, 16, 1) },
            // ---- 2-state bits ----
            { "mirror",         new AtlasItem(128,   0, 16, 16, 2) },   // 0 clear · 1 fogged
            { "valveHandle",    new AtlasItem(160,   0, 16, 16, 2) },   // 0 closed · 1 turned open
            { "showerHead",     new AtlasItem(192,   0, 16, 16, 2) },   // 0 off (one drip) · 1 running
            // ---- wall-hung / floor pieces ----
            { "plankShelf",     new AtlasItem(  0,  16, 32, 16, 1) },
            { "towelRack",      new AtlasItem( 32,  16, 32, 16, 1) },
            { "showerPan",      new AtlasItem( 64,  16, 32, 16, 1) },
            { "puddle",         new AtlasItem( 96,  16, 16, 16, 3) },   // still · drip lands · ripple
            { "curtainRail",    new AtlasItem(144,  16, 48, 16, 1) },
            // ---- fixtures ----
            { "pedestalSink",   new AtlasItem(  0,  32, 16, 32, 1) },
            { "toilet",         new AtlasItem( 16,  32, 16, 32, 1) },
            { "vanity",         new AtlasItem( 32,  32, 32, 32, 1) },
            { "pipeRiser",      new AtlasItem( 64,  32, 16, 32, 1) },
            { "waterStream",    new AtlasItem( 80,  32, 16, 32, 4) },
            { "window",         new AtlasItem(144,  32, 32, 32, 1) },
            // ---- shower shell / FX ----
            { "steam",          new AtlasItem(  0,  64, 32, 32, 4) },
            { "curtain",        new AtlasItem(  0,  96, 48, 48, 4) },   // closed · 1/3 · 2/3 · bunched open
            { "showerBackWall", new AtlasItem(192,  96, 32, 48, 1) },
            { "door",           new AtlasItem(224,  96, 32, 48, 4) },   // shut+latched · latch lifted · half · wide
        };

        /// <summary>The watcher sheet is its own 96x32 texture — the same window rect, three frames of
        /// red eyes outside the glass. Kept apart from <see cref="Items"/> because it is a different file.</summary>
        public const int WATCH_W = 32, WATCH_H = 32, WATCH_FRAMES = 3;

        /// <summary>Slice every frame of one piece into sprites, handling the top-left -> Unity
        /// (bottom-left) Y flip so multi-row cells land correctly.</summary>
        public static Sprite[] Slice(Texture2D atlas, string name, float ppu, Vector2 pivot)
        {
            if (atlas == null || !Items.TryGetValue(name, out AtlasItem it)) return null;
            var sprites = new Sprite[it.frames];
            for (int f = 0; f < it.frames; f++)
            {
                float uy = atlas.height - (it.y + it.h);
                var rect = new Rect(it.x + f * it.w, uy, it.w, it.h);
                sprites[f] = Sprite.Create(atlas, rect, pivot, ppu, 0, SpriteMeshType.FullRect);
                sprites[f].name = name + "_" + f;
            }
            return sprites;
        }

        /// <summary>Slice the three watcher frames off bathroom_window_watch.png (a plain 3x1 strip).</summary>
        public static Sprite[] SliceWatch(Texture2D sheet, float ppu, Vector2 pivot)
        {
            if (sheet == null) return null;
            var sprites = new Sprite[WATCH_FRAMES];
            for (int f = 0; f < WATCH_FRAMES; f++)
            {
                var rect = new Rect(f * WATCH_W, sheet.height - WATCH_H, WATCH_W, WATCH_H);
                sprites[f] = Sprite.Create(sheet, rect, pivot, ppu, 0, SpriteMeshType.FullRect);
                sprites[f].name = "windowWatch_" + f;
            }
            return sprites;
        }

        public static int FrameCount(string name) => Items.TryGetValue(name, out AtlasItem it) ? it.frames : 1;
    }
}
