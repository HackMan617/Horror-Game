// NeighborRobert.cs
// Robert Abernathy — the neighbor NPC. Drop on a GameObject with a SpriteRenderer.
//
// Handles the whole sprite the way the rest of the game does — no Animator Controller:
//   • rolls a fresh workwear palette at spawn (NeighborPalette) so he's never the same twice
//   • slices both 7-frame sheets (daytime + nightmare)
//   • runs an idle / walk / speak state machine over the shared frame layout
//   • swaps to the stretched NIGHTMARE form on the SAME dread flag as the dog & mountain
//   • gates his motion to PROXIMITY, so the wrongness only stirs when the player is near
//
// FRAME LAYOUT (both sheets, 224x32, cell 32x32, one row):
//   0  idle — neutral rest pose (the frame he falls back to)
//   1  idle — the smile held a beat too long (1px wider). Alternate 0/1 on a SLOW, uneven
//            cadence with dead holds so he stands unnervingly still.
//   2  walk — contact, left foot leads (arms swing)
//   3  walk — passing; the body rises 1px (up-beat of the step)
//   4  walk — contact, right foot leads (arms swing opposite)
//   5  speak — mouth closed (the between-words beat)
//   6  speak — mouth open. DAYTIME: a small polite "O". NIGHTMARE: the jaw unhinges into a
//            black void down the throat — the scare.

using UnityEngine;

namespace Game.Neighbors
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class NeighborRobert : MonoBehaviour
    {
        // ---- named frame indices (see the layout note above) ----
        const int IDLE_A = 0, IDLE_B = 1, WALK_A = 2, WALK_PASS = 3, WALK_B = 4, SPEAK_SHUT = 5, SPEAK_OPEN = 6;

        public enum State { Dormant, Idle, Walk, Speak }

        [Header("Sheets  (Read/Write ON · Point filter · Compression None)")]
        [Tooltip("neighbor_robert_front.png — the daytime man. Gets recolored per roll.")]
        public Texture2D daytimeSheet;
        [Tooltip("neighbor_robert_front_nightmare.png — the stretched form. Never recolored (drain overrides).")]
        public Texture2D nightmareSheet;

        [Header("Sheet layout")]
        public int frameWidth = 32;
        public int frameHeight = 32;
        public float pixelsPerUnit = 32f;
        [Tooltip("Bottom-center: the nightmare form grows UPWARD so he doesn't sink when the form swaps.")]
        public Vector2 pivot = new Vector2(0.5f, 0f);

        [Header("Workwear roll")]
        [Tooltip("ON = a fresh random assortment each load (design intent). OFF = use the look below.")]
        public bool rollOnAwake = true;
        public RobertLook look = RobertLook.Default;

        [Header("Dread flag  (same source as the dog & mountain)")]
        [Range(0f, 1f)] public float DreadProgress = 0f;
        [Tooltip("At/above this, Robert shows his stretched NIGHTMARE form.")]
        public float nightmareThreshold = 0.5f;

        [Header("Proximity gate")]
        [Tooltip("The player. Beyond 'stirRange' Robert is dormant (frozen on frame 0).")]
        public Transform player;
        public float stirRange = 12f;      // within this, he idles / lives
        public float speakRange = 3.5f;    // within this, calling Speak() actually plays

        [Header("Timing")]
        public float walkFps = 8f;
        public float speakFps = 6.5f;
        public Vector2 idleHoldSecs = new Vector2(0.6f, 1.6f);   // dead-hold range between idle blinks
        public float idleBlinkSecs = 0.5f;                        // how long IDLE_B (the too-long smile) holds

        SpriteRenderer sr;
        Sprite[] dayFrames, nightFrames;
        bool nightmare;
        State state = State.Idle;
        int frame;
        float timer;
        int walkSeqPos;
        bool idleShowingB;
        float speakEndsAt = -1f;

        static readonly int[] WalkSeq = { WALK_A, WALK_PASS, WALK_B, WALK_PASS };

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            if (rollOnAwake) look = RobertLook.Roll();
            Rebuild();
        }

        /// <summary>Recolor (daytime) + slice both sheets. Call again if you change the roll.</summary>
        public void Rebuild()
        {
            Texture2D dayTex = NeighborPalette.Recolor(daytimeSheet, look);
            dayFrames   = NeighborPalette.Slice(dayTex,        frameWidth, frameHeight, pixelsPerUnit, pivot);
            nightFrames = NeighborPalette.Slice(nightmareSheet, frameWidth, frameHeight, pixelsPerUnit, pivot);
            frame = IDLE_A; timer = 0f;
            Apply();
        }

        Sprite[] Sheet => (nightmare && nightFrames != null && nightFrames.Length > 0) ? nightFrames : dayFrames;

        void Apply()
        {
            var s = Sheet;
            if (s != null && s.Length > 0) sr.sprite = s[Mathf.Clamp(frame, 0, s.Length - 1)];
        }

        // ---------- public control ----------

        /// <summary>Make Robert talk for `seconds` (only takes if the player is within speakRange).</summary>
        public void Speak(float seconds = 2.5f)
        {
            if (player != null && (player.position - transform.position).sqrMagnitude > speakRange * speakRange) return;
            state = State.Speak; speakEndsAt = Time.time + seconds; frame = SPEAK_SHUT; timer = 0f;
        }

        /// <summary>Drive the walk cycle from your patrol/movement (world units/sec). Zero = idle.</summary>
        public void SetMovement(Vector2 velocity)
        {
            if (state == State.Speak) return;              // finish talking before walking
            bool moving = velocity.sqrMagnitude > 0.0001f;
            if (moving)
            {
                if (state != State.Walk) { state = State.Walk; walkSeqPos = 0; frame = WALK_A; timer = 0f; }
                if (Mathf.Abs(velocity.x) > 0.001f) sr.flipX = velocity.x < 0f;   // face travel direction
            }
            else if (state == State.Walk) { state = State.Idle; frame = IDLE_A; timer = 0f; }
        }

        // ---------- loop ----------

        void Update()
        {
            // form follows the dread flag
            bool wantNightmare = DreadProgress >= nightmareThreshold;
            if (wantNightmare != nightmare) { nightmare = wantNightmare; Apply(); }

            // proximity gate — dormant & frozen when the player is far
            if (player != null && state != State.Speak)
            {
                float d2 = (player.position - transform.position).sqrMagnitude;
                if (d2 > stirRange * stirRange)
                {
                    if (state != State.Walk) { state = State.Dormant; frame = IDLE_A; Apply(); return; }
                }
                else if (state == State.Dormant) { state = State.Idle; timer = 0f; }
            }

            switch (state)
            {
                case State.Dormant: return;
                case State.Speak:   TickSpeak();  break;
                case State.Walk:    TickWalk();   break;
                default:            TickIdle();   break;
            }
            Apply();
        }

        void TickIdle()
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            if (idleShowingB) { idleShowingB = false; frame = IDLE_A; timer = Random.Range(idleHoldSecs.x, idleHoldSecs.y); }
            else               { idleShowingB = true;  frame = IDLE_B; timer = idleBlinkSecs; }   // the smile, held too long
        }

        void TickWalk()
        {
            timer += Time.deltaTime;
            float step = 1f / Mathf.Max(1f, walkFps);
            while (timer >= step) { timer -= step; walkSeqPos = (walkSeqPos + 1) % WalkSeq.Length; frame = WalkSeq[walkSeqPos]; }
        }

        void TickSpeak()
        {
            if (Time.time >= speakEndsAt) { state = State.Idle; frame = IDLE_A; timer = 0f; idleShowingB = false; return; }
            timer += Time.deltaTime;
            float step = 1f / Mathf.Max(1f, speakFps);
            while (timer >= step) { timer -= step; frame = (frame == SPEAK_OPEN) ? SPEAK_SHUT : SPEAK_OPEN; }
        }
    }
}
