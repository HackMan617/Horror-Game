// BathroomFixture.cs
// One piece of the cabin BATHROOM kit (see Assets/Animation/Interior Atlas/bathroom_kit/SHOWER.md):
// the shower's concrete back wall, its curtain and rail, the pipe riser and head, the valve, the
// falling water, the steam, the vanity, the toilet, the mirror, the window, the door...
//
// The sibling of InteriorProp (structure) and InteriorObject (furniture): same three ideas, its own
// atlas. It slices its frames from BathroomAtlas at runtime, holds whichever frame it has been put
// on, and loops the ones that should move (water, steam, a dripping puddle) on their own clock.
//
// Two differences from its siblings, both deliberate:
//   - It does NOT drive its own state. ShowerStall and BathroomDoor own the curtain slide, the valve,
//     the fogging mirror and the door swing, because those frames are a sequence, not an idle loop.
//   - The nightmare pass has not been drawn yet (SHOWER.md ships the Cold Dusk grade only, apart from
//     the window's watcher variant). The nightmareAtlas slot and DreadProgress are wired now so that
//     dropping the second texture in is the whole of that job; until then a fixture simply never
//     flickers, because there is nothing to flicker to.
//
// Most bathroom pieces are FLAT: hung on a wall (fixed rotation) or laid on the floor (rotated onto
// its plane). Only the standing fixtures billboard, and even those stay put when they are built
// against a wall — a vanity that swings to face you reads as a cutout, not as a cabinet.

using UnityEngine;

namespace Game.Interior
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class BathroomFixture : MonoBehaviour
    {
        public enum Piece
        {
            HexFloor, HexFloorWorn, WainscotWall, WainscotCap, PlasterWall, WetFloor,
            DrainGrate, SoapClutter, Mirror, ValveHandle, ShowerHead,
            PlankShelf, TowelRack, ShowerPan, Puddle, CurtainRail,
            PedestalSink, Toilet, Vanity, PipeRiser, WaterStream, Window,
            Steam, Curtain, ShowerBackWall, Door,
        }

        static readonly string[] KEY =
        {
            "hexFloor","hexFloorWorn","wainscotWall","wainscotCap","plasterWall","wetFloor",
            "drainGrate","soapClutter","mirror","valveHandle","showerHead",
            "plankShelf","towelRack","showerPan","puddle","curtainRail",
            "pedestalSink","toilet","vanity","pipeRiser","waterStream","window",
            "steam","curtain","showerBackWall","door",
        };

        [Header("Which piece")]
        public Piece piece = Piece.Vanity;
        public bool flipX = false;

        [Header("Facing behaviour")]
        [Tooltip("Face the camera each frame (2.5D). Leave OFF for anything flat on a wall or on the " +
                 "floor — a fixture plumbed into a wall should stay on that wall.")]
        public bool billboard = false;
        [Tooltip("Viewer used to billboard toward. Defaults to Camera.main.")]
        public Transform viewer;

        [Header("Atlas  (Read/Write ON · Point · Compression None)")]
        public Texture2D dayAtlas;
        [Tooltip("The rotted twin. Not drawn yet — SHOWER.md ships the Cold Dusk grade only.")]
        public Texture2D nightmareAtlas;

        [Header("Sheet")]
        public float pixelsPerUnit = 16f;
        [Tooltip("Bottom-centre plants a standing fixture on the floor line; top-centre hangs a curtain " +
                 "or a rail from its rod; centre suits a tile laid flat.")]
        public Vector2 pivot = new Vector2(0.5f, 0f);

        [Header("Frame")]
        [Tooltip("Which frame to hold. ShowerStall / BathroomDoor drive this for the pieces that " +
                 "sequence; leave it at 0 for the ones that don't.")]
        public int frame = 0;
        [Tooltip("Run the frames as a loop instead of holding one — the falling water, the steam wisps, " +
                 "the puddle taking a drip.")]
        public bool autoLoop = false;
        public float loopFps = 8f;
        [Tooltip("Start the loop this many frames along, so two steam emitters side by side don't puff " +
                 "in lockstep.")]
        public int loopOffset = 0;

        /// <summary>Stop repainting the sprite, so something else can take the renderer over for a
        /// beat — WindowWatcher puts the eyes on the glass this way. Runtime only; never serialised.</summary>
        [System.NonSerialized] public bool suspended = false;

        [Header("Dread flag  (same source as the furniture, the decor, the bed)")]
        [Range(0f, 1f)] public float DreadProgress = 0f;

        SpriteRenderer _sr;
        Sprite[] _day, _night;
        float _animT;
        int _loop;
        bool _nmShown;
        float _flickT;

        /// <summary>The piece's renderer — ShowerStall fades the wet-floor sheen through its alpha.</summary>
        public SpriteRenderer Renderer => _sr != null ? _sr : (_sr = GetComponent<SpriteRenderer>());

        /// <summary>How many frames this piece actually has.</summary>
        public int FrameCount => _day != null ? _day.Length : 0;

        void Awake() { Rebuild(); }

        /// <summary>Re-slice this piece from the atlas (call after swapping a texture in the editor).</summary>
        public void Rebuild()
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            string key = KEY[(int)piece];
            _day   = BathroomAtlas.Slice(dayAtlas,       key, pixelsPerUnit, pivot);
            _night = BathroomAtlas.Slice(nightmareAtlas, key, pixelsPerUnit, pivot);
            if (_sr != null) _sr.flipX = flipX;
            _loop = loopOffset;
            Apply();
        }

        /// <summary>Hold a specific frame (the curtain's slide, the valve turning, the door's swing).</summary>
        public void SetFrame(int i)
        {
            frame = i;
            Apply();
        }

        void LateUpdate()
        {
            if (!billboard) return;
            Transform cam = viewer != null ? viewer : (Camera.main != null ? Camera.main.transform : null);
            if (cam == null) return;
            Vector3 toCam = cam.position - transform.position; toCam.y = 0f;
            if (toCam.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(toCam, Vector3.up);
        }

        void Update()
        {
            if (autoLoop && FrameCount > 1)
            {
                _animT += Time.deltaTime;
                if (_animT >= 1f / Mathf.Max(0.25f, loopFps)) { _animT = 0f; _loop++; }
            }

            // The nightmare flicker, ready for the day the rotted sheet exists. With no night atlas
            // sliced this settles on the day frames immediately and costs a comparison per frame.
            if (_night != null && _night.Length > 0)
            {
                float d = DreadProgress;
                _flickT -= Time.deltaTime;
                if (_flickT <= 0f)
                {
                    if (d <= 0f) { _nmShown = false; _flickT = 0.25f; }
                    else
                    {
                        _nmShown = Random.value < d;
                        _flickT = _nmShown ? Random.Range(0.05f, 0.05f + 0.15f * d)
                                           : Random.Range(0.14f, 0.14f + 0.52f * (1f - d));
                    }
                }
            }
            else _nmShown = false;

            Apply();
        }

        void Apply()
        {
            if (suspended) return;
            var set = (_nmShown && _night != null && _night.Length > 0) ? _night : _day;
            if (set == null || set.Length == 0 || _sr == null) return;
            int idx = autoLoop ? Mathf.Abs(_loop) % set.Length : frame;
            _sr.sprite = set[Mathf.Clamp(idx, 0, set.Length - 1)];
        }
    }
}
