// NeighborRobert.cs
// Robert Abernathy — the neighbor NPC. Drop on a GameObject with a SpriteRenderer.
//
// Handles the whole sprite the way the rest of the game does — no Animator Controller:
//   • rolls a fresh workwear palette at spawn (NeighborPalette) so he's never the same twice
//   • slices all 7-frame sheets — front / back / side, daytime + nightmare
//   • picks the sheet by FACING (side is mirrored for left, same as the player)
//   • runs an idle / walk / speak state machine over the shared frame layout
//   • swaps to the stretched NIGHTMARE form on the SAME dread flag as the dog & mountain
//   • gates his motion to PROXIMITY, so the wrongness only stirs when the player is near
//
// FRAME LAYOUT (every sheet, 224x32, cell 32x32, one row):
//   0 idle — neutral rest        1 idle — the smile held a beat too long
//   2 walk — contact (lead)      3 walk — passing (body rises)      4 walk — contact (other lead)
//   5 speak — mouth closed       6 speak — mouth open
//     · front/side speak: the mouth flaps (nightmare = the jaw unhinges into a void)
//     · back speak: no visible mouth — just a small nod (frames 5/6 ≈ idle)

using UnityEngine;

namespace Game.Neighbors
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class NeighborRobert : MonoBehaviour
    {
        const int IDLE_A = 0, IDLE_B = 1, WALK_A = 2, WALK_PASS = 3, WALK_B = 4, SPEAK_SHUT = 5, SPEAK_OPEN = 6;

        public enum State { Dormant, Idle, Walk, Speak }

        [Header("Sheets  (Read/Write ON · Point filter · Compression None)")]
        [Tooltip("neighbor_robert_front.png — recolored per roll.")]
        public Texture2D daytimeFront;
        public Texture2D daytimeBack;
        [Tooltip("neighbor_robert_side.png — faces RIGHT; mirrored for left.")]
        public Texture2D daytimeSide;
        [Header("...and the stretched nightmare set (never recolored)")]
        public Texture2D nightmareFront;
        public Texture2D nightmareBack;
        public Texture2D nightmareSide;

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
        public float nightmareThreshold = 0.5f;

        [Header("Proximity gate")]
        public Transform player;
        public float stirRange = 12f;
        public float speakRange = 3.5f;

        [Header("Timing")]
        public float walkFps = 8f;
        public float speakFps = 6.5f;
        public Vector2 idleHoldSecs = new Vector2(0.6f, 1.6f);
        public float idleBlinkSecs = 0.5f;

        SpriteRenderer sr;
        Sprite[] dayFront, dayBack, daySide, nightFront, nightBack, nightSide;
        bool nightmare;
        int facing;               // 0 down/front, 1 up/back, 2 left (side flipped), 3 right (side)
        State state = State.Idle;
        int frame, walkSeqPos;
        float timer, speakEndsAt = -1f;
        bool idleShowingB;

        static readonly int[] WalkSeq = { WALK_A, WALK_PASS, WALK_B, WALK_PASS };

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            if (rollOnAwake) look = RobertLook.Roll();
            Rebuild();
        }

        /// <summary>Recolor the daytime views + slice every sheet. Call again if the roll changes.</summary>
        public void Rebuild()
        {
            dayFront = SliceRecolored(daytimeFront);
            dayBack  = SliceRecolored(daytimeBack);
            daySide  = SliceRecolored(daytimeSide);
            nightFront = Slice(nightmareFront);
            nightBack  = Slice(nightmareBack);
            nightSide  = Slice(nightmareSide);
            frame = IDLE_A; timer = 0f;
            Apply();
        }

        Sprite[] SliceRecolored(Texture2D src)
        {
            if (src == null) return null;
            Texture2D tex = NeighborPalette.Recolor(src, look);
            return NeighborPalette.Slice(tex, frameWidth, frameHeight, pixelsPerUnit, pivot);
        }
        Sprite[] Slice(Texture2D src)
        {
            return src == null ? null : NeighborPalette.Slice(src, frameWidth, frameHeight, pixelsPerUnit, pivot);
        }

        Sprite[] ActiveSheet()
        {
            switch (facing)
            {
                case 1:  return nightmare ? (nightBack ?? nightFront) : (dayBack ?? dayFront);
                case 2:
                case 3:  return nightmare ? (nightSide ?? nightFront) : (daySide ?? dayFront);
                default: return nightmare ? nightFront : dayFront;
            }
        }

        void Apply()
        {
            var s = ActiveSheet();
            if (s == null || s.Length == 0) return;
            sr.flipX = (facing == 2);                         // side sheet faces right; mirror for left
            sr.sprite = s[Mathf.Clamp(frame, 0, s.Length - 1)];
        }

        // ---------- public control ----------

        public void Speak(float seconds = 2.5f)
        {
            if (player != null && (player.position - transform.position).sqrMagnitude > speakRange * speakRange) return;
            state = State.Speak; speakEndsAt = Time.time + seconds; frame = SPEAK_SHUT; timer = 0f;
        }

        /// <summary>Drive the walk cycle + facing from movement (world units/sec). Zero = idle.</summary>
        public void SetMovement(Vector2 velocity)
        {
            if (state == State.Speak) return;
            bool moving = velocity.sqrMagnitude > 0.0001f;
            if (moving)
            {
                // vertical dominates -> front/back; horizontal -> side (mirrored for left)
                if (Mathf.Abs(velocity.y) >= Mathf.Abs(velocity.x)) facing = velocity.y > 0f ? 1 : 0;
                else                                                facing = velocity.x < 0f ? 2 : 3;

                if (state != State.Walk) { state = State.Walk; walkSeqPos = 0; frame = WALK_A; timer = 0f; }
            }
            else if (state == State.Walk) { state = State.Idle; frame = IDLE_A; timer = 0f; }
        }

        /// <summary>Point him a direction while idle (0 front,1 back,2 left,3 right).</summary>
        public void FaceDirection(int f) { facing = Mathf.Clamp(f, 0, 3); Apply(); }

        // ---------- loop ----------

        void Update()
        {
            bool wantNightmare = DreadProgress >= nightmareThreshold;
            if (wantNightmare != nightmare) { nightmare = wantNightmare; Apply(); }

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
                case State.Speak:   TickSpeak(); break;
                case State.Walk:    TickWalk();  break;
                default:            TickIdle();  break;
            }
            Apply();
        }

        void TickIdle()
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            if (idleShowingB) { idleShowingB = false; frame = IDLE_A; timer = Random.Range(idleHoldSecs.x, idleHoldSecs.y); }
            else              { idleShowingB = true;  frame = IDLE_B; timer = idleBlinkSecs; }
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
