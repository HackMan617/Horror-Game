// InteriorStructureAtlas.cs
// The cabin's STRUCTURE kit — stairs, wall decor, attic framing, basement concrete. Companion to
// InteriorProp.cs, and the sibling of InteriorAtlas.cs (which covers the furniture).
//
// Two textures share ONE layout (256x192), so the nightmare swap is the same rects on a different
// texture:
//   interior_structure_dusk.png       — the warm cabin pass
//   interior_structure_nightmare.png  — the dream-rot pass (flickers in on the dread flag)
//
// Rects are transcribed from Assets/Animation/INTERIOR_ADDITION.md; origin is TOP-LEFT (like the
// PNG) and frames run horizontally, frame f at (x + f*w, y).
//
// Import settings for both: Read/Write ON · Filter Point · Compression None.

using System.Collections.Generic;
using UnityEngine;

namespace Game.Interior
{
    public static class InteriorStructureAtlas
    {
        public const int SHEET_W = 256, SHEET_H = 192;

        public static readonly Dictionary<string, AtlasItem> Items = new Dictionary<string, AtlasItem>
        {
            // ---- basement (tileable concrete) ----
            { "concreteWall",      new AtlasItem(  0,   0, 16, 16, 1) },
            { "concreteWallCrack", new AtlasItem( 16,   0, 16, 16, 1) },
            { "concreteBase",      new AtlasItem( 32,   0, 16, 16, 1) },
            { "basementFloor",     new AtlasItem( 48,   0, 16, 16, 1) },
            { "supportPost",       new AtlasItem(  0,  32, 16, 48, 1) },
            // ---- attic / upper storey framing ----
            { "atticCeilSlopeL",   new AtlasItem( 64,   0, 16, 16, 1) },
            { "atticCeilSlopeR",   new AtlasItem( 80,   0, 16, 16, 1) },
            { "atticBeamH",        new AtlasItem( 96,   0, 16, 16, 1) },
            { "atticKneeWall",     new AtlasItem(112,   0, 16, 16, 1) },
            { "atticGableVent",    new AtlasItem(128,   0, 16, 16, 1) },
            { "atticBeamPost",     new AtlasItem( 16,  32, 16, 32, 1) },
            // ---- wall decor ----
            { "framedPortrait",    new AtlasItem(  0,  16, 16, 16, 4) },   // 0-2 eyes dart · 3 LUNGE (nightmare only)
            { "framedLandscape",   new AtlasItem(144,   0, 16, 16, 1) },
            { "wallSconce",        new AtlasItem( 64,  16, 16, 16, 2) },   // 0 off · 1 lit
            { "wallClock",         new AtlasItem(192,   0, 16, 16, 3) },   // pendulum L-C-R
            { "mountedShelf",      new AtlasItem( 96,  16, 32, 16, 1) },
            { "coatHooks",         new AtlasItem(128,  16, 32, 16, 1) },
            { "mirror",            new AtlasItem(160,  16, 16, 32, 1) },
            { "deerHead",          new AtlasItem( 32,  32, 32, 32, 1) },
            { "wreath",            new AtlasItem(160,   0, 16, 16, 1) },
            { "calendar",          new AtlasItem(176,   0, 16, 16, 1) },
            // ---- stairs (all side flights ascend LEFT -> RIGHT; mirror for the other hand) ----
            { "stairSideWood",     new AtlasItem(  0,  96, 48, 48, 3) },   // dust drift · a tread sags on 2
            { "stairSideCarpet",   new AtlasItem(144,  96, 48, 48, 1) },
            { "stairFront",        new AtlasItem(192,  96, 32, 48, 1) },   // climbs away up a back wall
            { "stairSideWorn",     new AtlasItem(  0, 144, 48, 48, 2) },   // upper steps flex — a slow creak
            { "stairStone",        new AtlasItem( 96, 144, 48, 48, 1) },
            { "stairDownHole",     new AtlasItem(144, 144, 48, 32, 1) },   // cut into the floor ABOVE a flight
        };

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

        public static int FrameCount(string name) => Items.TryGetValue(name, out AtlasItem it) ? it.frames : 1;
    }
}
