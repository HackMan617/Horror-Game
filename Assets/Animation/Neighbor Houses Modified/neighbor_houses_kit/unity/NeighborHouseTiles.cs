// NeighborHouseTiles.cs
// The neighbor-house tile atlas — cells, animated strips, slicing. Companion to NeighborHouse.cs.
//
// Atlas: neighbor_{A,B,C}_tiles.png + _nightmare.png — 192x144, 8x6, 24-px cells.
// Static rows build the house; rows 2-5 are the animated strips overlaid on the assembled
// elevation (a shape crossing a window, candle/flashlight flicker, chimney smoke, a rattling
// board, the front door swinging). Home and nightmare share the layout — swap the texture.
//
// Import (all house textures): Read/Write ON · Filter Point · Compression None · PPU 24.

using UnityEngine;

namespace Game.Houses
{
    public enum HousePalette { Home = 0, Nightmare = 1 }

    public static class NeighborHouseTiles
    {
        public const int CELL = 24;

        // ---- static tiles (col,row) — for building/repairing houses from the atlas ----
        // Row 0: wallA(0) wallB(1) postV(2) beamH(3) foundation(4) doorTop(5) doorBottom(6) window(7)
        // Row 1: roofField(0) roofRakeL(1) roofRakeR(2) eave(3) chimney(4) chimneyTop(5)

        /// <summary>The animated overlay strips (rows 2-5).</summary>
        public enum StripId { WinSil, WinCandle, Smoke, LoosePlank, DoorTop, DoorBottom }

        public struct Strip { public int col, row, frames, ms; public Strip(int c,int r,int f,int m){col=c;row=r;frames=f;ms=m;} }

        public static Strip Def(StripId s)
        {
            switch (s)
            {
                case StripId.WinSil:     return new Strip(0, 2, 6, 230);  // shape crosses the glass
                case StripId.WinCandle:  return new Strip(0, 3, 4, 190);  // candle / (night) flashlight
                case StripId.Smoke:      return new Strip(0, 4, 6, 150);  // chimney smoke (dead wisp at night)
                case StripId.LoosePlank: return new Strip(0, 5, 4, 240);  // a board rattles / a shadow crosses
                case StripId.DoorTop:    return new Strip(4, 3, 4, 300);  // door swing — top half
                default:                 return new Strip(4, 5, 4, 300);  // DoorBottom — bottom half
            }
        }

        // doorOpen plays as a ping-pong (never a metronome): closed → open → hold → close.
        public static readonly int[] DoorSequence = { 0,0,0,0,0,1,2,3,3,3,3,3,3,2,1 };

        /// <summary>Slice one static cell (col,row) → a sprite. Handles the top-left→Unity Y flip.</summary>
        public static Sprite SliceCell(Texture2D atlas, int col, int row, float ppu, Vector2 pivot)
        {
            if (atlas == null) return null;
            float uy = atlas.height - (row + 1) * CELL;
            return Sprite.Create(atlas, new Rect(col * CELL, uy, CELL, CELL), pivot, ppu, 0, SpriteMeshType.FullRect);
        }

        /// <summary>Slice every frame of an animated strip → sprites (pivot center by default).</summary>
        public static Sprite[] SliceStrip(Texture2D atlas, StripId id, float ppu, Vector2 pivot)
        {
            if (atlas == null) return null;
            Strip d = Def(id);
            var sprites = new Sprite[d.frames];
            for (int f = 0; f < d.frames; f++)
            {
                float uy = atlas.height - (d.row + 1) * CELL;
                sprites[f] = Sprite.Create(atlas, new Rect((d.col + f) * CELL, uy, CELL, CELL), pivot, ppu, 0, SpriteMeshType.FullRect);
                sprites[f].name = id + "_" + f;
            }
            return sprites;
        }
    }
}
