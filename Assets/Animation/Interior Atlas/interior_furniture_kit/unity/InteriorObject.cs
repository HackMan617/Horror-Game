// InteriorObject.cs
// A single interactive piece of living-room furniture. Drop on a GameObject with a SpriteRenderer.
//
//   • slices the chosen object's frames from the DAY atlas (dusk or lavender) + the NIGHTMARE atlas
//   • animates it: TV loops / floor-lamp & TV toggle on use / dog breathes / the rest hold
//   • INTERACTION: SetHighlighted(true) on approach (a warm glow) + Activate() to use it
//   • NIGHTMARE FLICKER: as the dread flag climbs, the rotted sprite strobes in over the day one —
//     the room the player thinks is safe keeps blinking wrong, until they realize they never woke up
//
// FRAMES (see InteriorAtlas): tv 0 off · 1-2 on · 3 static ; floorLamp 0 off · 1 on ;
// couchDog 0-2 breathing ; everything else is a single frame.

using UnityEngine;

namespace Game.Interior
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class InteriorObject : MonoBehaviour
    {
        public enum Piece { Sofa, Couch, Armchair, CoffeeTable, Tv, Bookshelf, Rug, FloorLamp, CouchDog }

        [Header("Which object")]
        public Piece piece = Piece.Sofa;

        [Header("Atlases  (Read/Write ON · Point · Compression None)")]
        [Tooltip("The room's DAY atlas — assign interior_furniture_dusk OR _lavender.")]
        public Texture2D dayAtlas;
        public Texture2D nightmareAtlas;

        [Header("Sheet")]
        public float pixelsPerUnit = 16f;
        [Tooltip("Bottom-center keeps the piece planted on the floor line.")]
        public Vector2 pivot = new Vector2(0.5f, 0f);

        [Header("Interaction")]
        [Tooltip("TV & floor lamp start off and turn on when Activate() is called.")]
        public bool startsOn = false;
        [Tooltip("Tint multiplied in when SetHighlighted(true) — the 'you can use this' glow.")]
        public Color highlightTint = new Color(1.18f, 1.12f, 0.95f, 1f);

        [Header("Dread flag  (same source as the dog, mountain, houses)")]
        [Range(0f, 1f)] public float DreadProgress = 0f;

        [Header("Timing")]
        public float loopFps = 3.5f;      // tv "on" shimmer / breathing

        SpriteRenderer sr;
        Sprite[] day, night;
        string key;
        bool on, highlighted, nmShown;
        int frame; float animT, flickT;
        Color baseColor = Color.white;

        static readonly string[] KEY = { "sofa","couch","armchair","coffeeTable","tv","bookshelf","rug","floorLamp","couchDog" };

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            baseColor = sr.color;
            on = startsOn;
            Rebuild();
        }

        /// <summary>Re-slice both atlases for this piece. Call after changing piece/atlas at runtime.</summary>
        public void Rebuild()
        {
            key = KEY[(int)piece];
            day   = InteriorAtlas.Slice(dayAtlas,       key, pixelsPerUnit, pivot);
            night = InteriorAtlas.Slice(nightmareAtlas, key, pixelsPerUnit, pivot);
            frame = 0; Apply();
        }

        // ---------- interaction ----------

        /// <summary>Approach glow. Call from your proximity/aim check.</summary>
        public void SetHighlighted(bool v)
        {
            highlighted = v;
            sr.color = v ? new Color(baseColor.r*highlightTint.r, baseColor.g*highlightTint.g, baseColor.b*highlightTint.b, baseColor.a) : baseColor;
        }

        /// <summary>Use it. TV / floor lamp toggle on↔off; other pieces can hook their own reaction.</summary>
        public void Activate()
        {
            if (piece == Piece.Tv || piece == Piece.FloorLamp) { on = !on; frame = 0; animT = 0f; }
            // (extend: bookshelf pull, drawer open, etc.)
        }
        public bool IsOn => on;

        // ---------- per-frame animation index ----------
        int DayFrame()
        {
            switch (piece)
            {
                case Piece.Tv:
                    if (!on) return 0;                                  // off (dark glass)
                    return (frame % 2 == 0) ? 1 : 2;                    // on: shimmer between the two lit frames
                case Piece.FloorLamp:
                    return on ? 1 : 0;
                case Piece.CouchDog:
                    return (frame % 4 == 0) ? 1 : 0;                    // gentle breath
                default:
                    return 0;
            }
        }
        int NightFrame()
        {
            switch (piece)
            {
                case Piece.Tv:        return 3;                          // dead static + face
                case Piece.FloorLamp: return on ? 1 : 0;
                case Piece.CouchDog:  return (frame % 4 == 0) ? 1 : 0;
                default:              return 0;
            }
        }

        void Apply()
        {
            var set = (nmShown && night != null) ? night : day;
            if (set == null || set.Length == 0) return;
            int idx = (nmShown ? NightFrame() : DayFrame());
            sr.sprite = set[Mathf.Clamp(idx, 0, set.Length - 1)];
        }

        void Update()
        {
            // advance the slow loop clock (drives tv shimmer + breathing)
            animT += Time.deltaTime;
            if (animT >= 1f / Mathf.Max(0.5f, loopFps)) { animT = 0f; frame++; }

            // nightmare flicker — strobes in more often as dread climbs; ~solid at 1
            float d = DreadProgress;
            flickT -= Time.deltaTime;
            if (flickT <= 0f)
            {
                if (d <= 0f) { nmShown = false; flickT = 0.25f; }
                else if (d >= 1f)
                {
                    // mostly wrong, with brief lucid day-blinks
                    nmShown = !(Random.value < 0.14f);
                    flickT = nmShown ? Random.Range(0.16f, 0.34f) : Random.Range(0.04f, 0.11f);
                }
                else
                {
                    nmShown = Random.value < d;
                    flickT = nmShown ? Random.Range(0.05f, 0.05f + 0.15f * d)
                                     : Random.Range(0.14f, 0.14f + 0.52f * (1f - d));
                }
            }
            Apply();
        }
    }
}
