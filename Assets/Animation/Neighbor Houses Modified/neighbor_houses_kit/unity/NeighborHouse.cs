// NeighborHouse.cs
// A neighbor house (A / B / C) on the pre-Nightmare street. Drop on a GameObject with a
// SpriteRenderer (the assembled elevation) and add animated-strip decals as data.
//
//   • shows the assembled elevation (front/side/back) for the current view
//   • swaps home ↔ nightmare on the SAME dread flag as the dog & mountain — the block goes cold
//     and each house turns wrong its own way (the beat is baked into the _nightmare elevation)
//   • drives the animated overlay strips (window silhouette, candle/flashlight, chimney smoke,
//     rattling board, the front door) as child renderers at authored anchors, with wrong-timing
//   • gates the motion to PROXIMITY — the wrongness only stirs when the player is near
//
// Per-house nightmare beat (baked in the _nightmare elevation; drive strips to match):
//   A  door ajar to black; a figure in the one cold-lit upstairs room   (WinCandle on that window)
//   B  all windows black; a flashlight sweeps one; a pale face upstairs  (WinCandle sweep + baked face)
//   C  chimney dead cold; one downstairs window burns sick green         (no Smoke at night)

using System.Collections.Generic;
using UnityEngine;

namespace Game.Houses
{
    public enum HouseId { A, B, C }

    public class NeighborHouse : MonoBehaviour
    {
        [System.Serializable]
        public class Decal
        {
            public NeighborHouseTiles.StripId strip;
            [Tooltip("Top-left of the 24px strip cell, in source pixels from the elevation's top-left.")]
            public Vector2 pixelOffset;
            public int sortingOrderOffset = 1;
            [Tooltip("Only for door halves — grouped so DoorTop + DoorBottom swing together.")]
            public bool isDoor = false;
            [HideInInspector] public SpriteRenderer sr;
            [HideInInspector] public Sprite[] home, night;
            [HideInInspector] public float t; [HideInInspector] public int frame, seqPos;
        }

        [Header("Identity")]
        public HouseId id = HouseId.A;

        [Header("Assembled elevation  (Read/Write ON · Point · Compression None)")]
        public Texture2D homeElevation;
        public Texture2D nightmareElevation;

        [Header("Tile atlas — source of the animated strips")]
        public Texture2D tilesHome;
        public Texture2D tilesNightmare;

        [Header("Placement")]
        public float pixelsPerUnit = 24f;
        [Tooltip("Bottom-center: the house sits on its base line.")]
        public Vector2 pivot = new Vector2(0.5f, 0f);

        [Header("Animated strips (anchors)")]
        public List<Decal> decals = new List<Decal>();

        [Header("Dread flag  (same source as the dog & mountain)")]
        [Range(0f, 1f)] public float DreadProgress = 0f;
        public float nightmareThreshold = 0.5f;

        [Header("Proximity")]
        public Transform player;
        public float stirRange = 14f;

        SpriteRenderer sr;
        Sprite homeSprite, nightSprite;
        bool nightmare, near = true;
        bool doorRunning; float doorT; int doorSeqPos;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            Rebuild();
        }

        /// <summary>Build the elevation sprites + the decal child renderers.</summary>
        public void Rebuild()
        {
            homeSprite  = FullSprite(homeElevation);
            nightSprite = FullSprite(nightmareElevation);
            if (homeSprite != null) sr.sprite = homeSprite;

            float elevW = (homeElevation ? homeElevation.width  : 0);
            float elevH = (homeElevation ? homeElevation.height : 0);
            var center = new Vector2(0.5f, 0.5f);

            foreach (var d in decals)
            {
                if (d.sr == null)
                {
                    var go = new GameObject("decal_" + d.strip);
                    go.transform.SetParent(transform, false);
                    d.sr = go.AddComponent<SpriteRenderer>();
                    d.sr.sortingLayerID = sr.sortingLayerID;
                }
                d.sr.sortingOrder = sr.sortingOrder + d.sortingOrderOffset;
                d.home  = NeighborHouseTiles.SliceStrip(tilesHome,      d.strip, pixelsPerUnit, center);
                d.night = NeighborHouseTiles.SliceStrip(tilesNightmare, d.strip, pixelsPerUnit, center);
                // place: cell center, converted from top-left px to local units (elevation pivot = bottom-center)
                float cx = (d.pixelOffset.x + NeighborHouseTiles.CELL * 0.5f - elevW * 0.5f) / pixelsPerUnit;
                float cy = (elevH - (d.pixelOffset.y + NeighborHouseTiles.CELL * 0.5f)) / pixelsPerUnit;
                d.sr.transform.localPosition = new Vector3(cx, cy, 0f);
                d.frame = 0; d.seqPos = 0; d.t = 0f;
                ApplyDecal(d);
            }
        }

        Sprite FullSprite(Texture2D t)
        {
            if (t == null) return null;
            return Sprite.Create(t, new Rect(0, 0, t.width, t.height), pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        Sprite[] Set(Decal d) => (nightmare && d.night != null) ? d.night : d.home;
        void ApplyDecal(Decal d)
        {
            var s = Set(d); if (s == null || s.Length == 0) { return; }
            d.sr.sprite = s[Mathf.Clamp(d.frame, 0, s.Length - 1)];
        }

        /// <summary>Swing the front door (ping-pong). Call on interact, or let a trigger fire it.</summary>
        public void OpenDoor() { doorRunning = true; doorSeqPos = 0; doorT = 0f; }

        void Update()
        {
            bool wantNM = DreadProgress >= nightmareThreshold;
            if (wantNM != nightmare)
            {
                nightmare = wantNM;
                if (sr) sr.sprite = nightmare ? (nightSprite ?? homeSprite) : homeSprite;
            }

            if (player != null)
                near = (player.position - transform.position).sqrMagnitude <= stirRange * stirRange;

            // door ping-pong (shared clock across the door decals)
            if (doorRunning)
            {
                doorT += Time.deltaTime;
                if (doorT >= 0.3f) { doorT = 0f; doorSeqPos++; if (doorSeqPos >= NeighborHouseTiles.DoorSequence.Length) { doorRunning = false; doorSeqPos = 0; } }
            }
            int doorFrame = NeighborHouseTiles.DoorSequence[Mathf.Clamp(doorSeqPos, 0, NeighborHouseTiles.DoorSequence.Length - 1)];

            foreach (var d in decals)
            {
                if (d.sr == null) continue;
                bool doorStrip = d.isDoor || d.strip == NeighborHouseTiles.StripId.DoorTop || d.strip == NeighborHouseTiles.StripId.DoorBottom;

                if (!near) { d.frame = 0; d.sr.enabled = (d.strip == NeighborHouseTiles.StripId.Smoke) ? false : true; ApplyDecal(d); continue; }
                d.sr.enabled = !(nightmare && id == HouseId.C && d.strip == NeighborHouseTiles.StripId.Smoke); // C's chimney is dead cold at night

                if (doorStrip) { d.frame = doorFrame; ApplyDecal(d); continue; }

                // ambient strips loop with a slight per-frame jitter so the block never pulses in sync
                var def = NeighborHouseTiles.Def(d.strip);
                d.t += Time.deltaTime * 1000f;
                float step = def.ms * (0.8f + 0.5f * Mathf.PerlinNoise(d.seqPos * 0.7f, transform.position.x));
                if (d.t >= step) { d.t = 0f; d.seqPos++; d.frame = (d.frame + 1) % def.frames; }
                ApplyDecal(d);
            }
        }
    }
}
