// InteriorProp.cs
// One piece of the cabin's STRUCTURE kit (see InteriorStructureAtlas / Assets/Animation/INTERIOR_ADDITION.md):
// a stair flight, a hung portrait, a sconce, the wall clock, the mirror, the deer head...
//
// The sibling of InteriorObject (furniture): same three ideas, different atlas.
//   • slices its frames from the DUSK atlas + the NIGHTMARE atlas at runtime
//   • animates what should move — the clock's pendulum, the portrait's darting eyes, the stairs'
//     drifting dust — and holds a single frame for everything else
//   • NIGHTMARE FLICKER: as the dread flag climbs the rotted twin strobes in over the day sprite,
//     and the portrait starts throwing its LUNGE frame, which only exists in the dream
//
// Hung decor mounts flat on a wall (fixed rotation); free-standing pieces billboard to the camera.

using UnityEngine;

namespace Game.Interior
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class InteriorProp : MonoBehaviour
    {
        public enum Piece
        {
            StairSideWood, StairSideCarpet, StairSideWorn, StairStone, StairFront, StairDownHole,
            FramedPortrait, FramedLandscape, WallSconce, WallClock, MountedShelf, CoatHooks,
            Mirror, DeerHead, Wreath, Calendar,
            SupportPost, AtticBeamPost, AtticBeamH, AtticKneeWall, AtticGableVent,
            AtticCeilSlopeL, AtticCeilSlopeR,
            ConcreteWall, ConcreteWallCrack, ConcreteBase, BasementFloor,
        }

        static readonly string[] KEY =
        {
            "stairSideWood","stairSideCarpet","stairSideWorn","stairStone","stairFront","stairDownHole",
            "framedPortrait","framedLandscape","wallSconce","wallClock","mountedShelf","coatHooks",
            "mirror","deerHead","wreath","calendar",
            "supportPost","atticBeamPost","atticBeamH","atticKneeWall","atticGableVent",
            "atticCeilSlopeL","atticCeilSlopeR",
            "concreteWall","concreteWallCrack","concreteBase","basementFloor",
        };

        [Header("Which piece")]
        public Piece piece = Piece.FramedPortrait;
        [Tooltip("Mirror horizontally — side stair flights are drawn ascending left->right; flip for the other hand.")]
        public bool flipX = false;

        [Header("Facing behaviour")]
        [Tooltip("Face the camera each frame (2.5D). Leave OFF for anything hung flat on a wall — decor " +
                 "should stay on its wall, not swing round to the player.")]
        public bool billboard = false;
        [Tooltip("Viewer used to billboard toward. Defaults to Camera.main.")]
        public Transform viewer;

        [Header("Atlases  (Read/Write ON · Point · Compression None)")]
        public Texture2D dayAtlas;
        public Texture2D nightmareAtlas;

        [Header("Sheet")]
        public float pixelsPerUnit = 16f;
        [Tooltip("Bottom-center plants a stair flight on the floor line; center suits hung decor.")]
        public Vector2 pivot = new Vector2(0.5f, 0f);

        [Header("Interaction")]
        [Tooltip("Sconces start lit or dark; Activate() toggles them.")]
        public bool startsOn = false;

        [Header("Dread flag  (same source as the furniture, the dog, the mountain)")]
        [Range(0f, 1f)] public float DreadProgress = 0f;

        [Header("Timing")]
        [Tooltip("Frames per second of the idle loop — the pendulum swing, the drifting stair dust.")]
        public float loopFps = 1.6f;

        SpriteRenderer _sr;
        Sprite[] _day, _night;
        bool _on, _nmShown;
        int _frame;
        float _animT, _flickT, _dartT;
        int _dart;          // which eye position the portrait currently holds

        void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _on = startsOn;
            Rebuild();
        }

        /// <summary>Re-slice this piece from the atlases (call after swapping a texture in the editor).</summary>
        public void Rebuild()
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();   // callable before Awake (editor tooling)
            string key = KEY[(int)piece];
            _day   = InteriorStructureAtlas.Slice(dayAtlas,       key, pixelsPerUnit, pivot);
            _night = InteriorStructureAtlas.Slice(nightmareAtlas, key, pixelsPerUnit, pivot);
            if (_sr != null) _sr.flipX = flipX;
            _frame = 0;
            Apply();
        }

        /// <summary>Use it. Sconces toggle lit/dark; other pieces can hook their own reaction.</summary>
        public void Activate()
        {
            if (piece == Piece.WallSconce) { _on = !_on; Apply(); }
        }
        public bool IsOn => _on;

        void LateUpdate()
        {
            if (!billboard) return;
            Transform cam = viewer != null ? viewer : (Camera.main != null ? Camera.main.transform : null);
            if (cam == null) return;
            Vector3 toCam = cam.position - transform.position; toCam.y = 0f;
            if (toCam.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(toCam, Vector3.up);
        }

        // ---------- per-frame animation index ----------
        int DayFrame()
        {
            switch (piece)
            {
                case Piece.WallSconce:      return _on ? 1 : 0;
                case Piece.WallClock:       return PENDULUM[_frame % PENDULUM.Length];   // L-C-R-C swing
                case Piece.FramedPortrait:  return _dart;                                // eyes hold, then dart
                case Piece.StairSideWood:   return _frame % 3;                           // dust drift + a sagging tread
                case Piece.StairSideWorn:   return _frame % 2;                           // the slow creak
                default:                    return 0;
            }
        }

        // The dream keeps the same motion but the portrait gains its fourth frame: the LUNGE, which
        // only ever exists on this side. It fires rarely, and more often the deeper the dread.
        int NightFrame()
        {
            if (piece == Piece.FramedPortrait)
                return Random.value < 0.05f + 0.20f * DreadProgress ? 3 : _dart;
            return DayFrame();
        }

        static readonly int[] PENDULUM = { 0, 1, 2, 1 };

        void Apply()
        {
            var set = (_nmShown && _night != null && _night.Length > 0) ? _night : _day;
            if (set == null || set.Length == 0) return;
            int idx = _nmShown ? NightFrame() : DayFrame();
            _sr.sprite = set[Mathf.Clamp(idx, 0, set.Length - 1)];
        }

        void Update()
        {
            // the idle loop clock (pendulum / stair dust)
            _animT += Time.deltaTime;
            if (_animT >= 1f / Mathf.Max(0.25f, loopFps)) { _animT = 0f; _frame++; }

            // the portrait's eyes hold still for a beat, then flick to a new position — never a
            // steady loop, so you are never quite sure it moved
            if (piece == Piece.FramedPortrait)
            {
                _dartT -= Time.deltaTime;
                if (_dartT <= 0f)
                {
                    _dart = Random.Range(0, 3);
                    _dartT = Random.Range(0.9f, 3.2f) * (1f - 0.5f * DreadProgress);
                }
            }

            // nightmare flicker — strobes the rotted twin in as dread climbs (mirrors InteriorObject/Bed)
            float d = DreadProgress;
            _flickT -= Time.deltaTime;
            if (_flickT <= 0f)
            {
                if (d <= 0f) { _nmShown = false; _flickT = 0.25f; }
                else if (d >= 1f)
                {
                    _nmShown = !(Random.value < 0.14f);
                    _flickT = _nmShown ? Random.Range(0.16f, 0.34f) : Random.Range(0.04f, 0.11f);
                }
                else
                {
                    _nmShown = Random.value < d;
                    _flickT = _nmShown ? Random.Range(0.05f, 0.05f + 0.15f * d)
                                       : Random.Range(0.14f, 0.14f + 0.52f * (1f - d));
                }
            }

            Apply();
        }
    }
}
