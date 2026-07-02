// TileStripQuad.cs
// Animates ONE quad (a window, a chimney smoke plume, a candle, a door half) by scrolling its
// material UVs through a strip in the neighbor-house tile atlas. This is how the flat 2D tileset
// gets "animation frames for windows and smoke" once the house is a 3D object: a small quad sits
// just in front of the baked window/chimney and cycles cells.
//
// Put it on a quad (GameObject > 3D Object > Quad) whose material samples the tile atlas. It sets
// mainTextureScale to one cell and moves mainTextureOffset per frame. Home/nightmare = a texture
// swap on the dread flag; motion is gated to the player's proximity (the street stays still until
// you're close), with the same wrong-timing as the rest of the game.
//
// Atlas: neighbor_{A,B,C}_tiles.png (+ _nightmare) — 192x144, 24-px cells, 8x6.

using UnityEngine;

namespace Game.Houses
{
    [RequireComponent(typeof(MeshRenderer))]
    public class TileStripQuad : MonoBehaviour
    {
        public enum Kind { WinSil, WinCandle, Smoke, LoosePlank, DoorTop, DoorBottom }

        [Header("Atlas")]
        public Texture2D tilesHome;
        public Texture2D tilesNightmare;
        public int atlasWidth = 192, atlasHeight = 144, tilePx = 24;

        [Header("Strip")]
        public Kind kind = Kind.WinSil;

        [Header("Dread flag  (same source as the dog, houses, interior)")]
        [Range(0f, 1f)] public float DreadProgress = 0f;
        public float nightmareThreshold = 0.5f;

        [Header("Proximity")]
        public Transform player;
        public float stirRange = 14f;
        [Tooltip("House C's chimney is dead cold at night — tick on a Smoke quad for house C.")]
        public bool deadColdAtNight = false;

        Material mat;
        MeshRenderer mr;
        int col0, row, frames, ms;
        int frame, seqPos; float t; bool nightmare;
        bool doorRunning; float doorT; int doorSeqPos;

        static readonly int[] DoorSeq = { 0,0,0,0,0,1,2,3,3,3,3,3,3,2,1 };

        void Awake()
        {
            mr = GetComponent<MeshRenderer>();
            mat = mr.material;                    // instance (so each window animates independently)
            Def();
            mat.mainTextureScale = new Vector2((float)tilePx / atlasWidth, (float)tilePx / atlasHeight);
            SwapTexture();
            Apply();
        }

        void Def()
        {
            switch (kind)
            {
                case Kind.WinSil:     col0=0; row=2; frames=6; ms=230; break;
                case Kind.WinCandle:  col0=0; row=3; frames=4; ms=190; break;
                case Kind.Smoke:      col0=0; row=4; frames=6; ms=150; break;
                case Kind.LoosePlank: col0=0; row=5; frames=4; ms=240; break;
                case Kind.DoorTop:    col0=4; row=3; frames=4; ms=300; break;
                default:              col0=4; row=5; frames=4; ms=300; break; // DoorBottom
            }
        }

        bool IsDoor => kind == Kind.DoorTop || kind == Kind.DoorBottom;

        /// <summary>Swing the door once (call on both DoorTop & DoorBottom quads together).</summary>
        public void OpenDoor() { doorRunning = true; doorSeqPos = 0; doorT = 0f; }

        void SwapTexture()
        {
            var tex = (nightmare && tilesNightmare) ? tilesNightmare : tilesHome;
            if (tex) { tex.filterMode = FilterMode.Point; mat.mainTexture = tex;
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex); }
        }

        void Apply()
        {
            int col = col0 + Mathf.Clamp(frame, 0, frames - 1);
            float u = (col * tilePx) / (float)atlasWidth;
            float v = 1f - ((row + 1) * tilePx) / (float)atlasHeight;    // Unity UV origin = bottom-left
            mat.mainTextureOffset = new Vector2(u, v);
        }

        void Update()
        {
            bool wantNM = DreadProgress >= nightmareThreshold;
            if (wantNM != nightmare) { nightmare = wantNM; SwapTexture(); }

            // C's chimney stops smoking at night
            if (kind == Kind.Smoke && deadColdAtNight && nightmare) { if (mr.enabled) mr.enabled = false; return; }
            else if (!mr.enabled) mr.enabled = true;

            bool near = player == null || (player.position - transform.position).sqrMagnitude <= stirRange * stirRange;
            if (!near && !IsDoor) { frame = 0; Apply(); return; }

            if (IsDoor)
            {
                if (doorRunning)
                {
                    doorT += Time.deltaTime;
                    if (doorT >= ms / 1000f) { doorT = 0f; doorSeqPos++; if (doorSeqPos >= DoorSeq.Length) { doorRunning = false; doorSeqPos = 0; } }
                }
                frame = DoorSeq[Mathf.Clamp(doorSeqPos, 0, DoorSeq.Length - 1)];
                Apply();
                return;
            }

            // ambient loop with per-quad jitter so the block never pulses in sync
            t += Time.deltaTime * 1000f;
            float step = ms * (0.8f + 0.5f * Mathf.PerlinNoise(seqPos * 0.6f, transform.position.x + transform.position.z));
            if (t >= step) { t = 0f; seqPos++; frame = (frame + 1) % frames; }
            Apply();
        }
    }
}
