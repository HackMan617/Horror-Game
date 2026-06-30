// CharacterAnimator.cs
// Drop this on a GameObject with a SpriteRenderer.
// It recolors the master sheets once at spawn, slices them into frames,
// and cycles the walk animation. No Animator Controller needed.
//
// Drive it from your movement script by calling SetMovement(velocity) each frame
// (or set `velocity` directly if this component reads your Rigidbody2D).

using UnityEngine;

namespace Game.Characters
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class CharacterAnimator : MonoBehaviour
    {
        [Header("Master sheets")]
        [Tooltip("Import settings: Read/Write Enabled = ON, Filter = Point, Compression = None.")]
        public Texture2D masterFront;          // character_master.png       (Male)
        public Texture2D masterBack;           // character_master_back.png  (Male)
        [Tooltip("Long-hair (Female) variants. Same baked colors — leave empty to fall back to the Male sheets.")]
        public Texture2D masterFrontLong;      // character_master_long.png       (Female)
        public Texture2D masterBackLong;       // character_master_back_long.png  (Female)

        [Header("Look — the 5 saved choices per player")]
        public CharacterLook look = CharacterLook.Default;

        [Header("Sheet layout")]
        public int frameWidth = 32;
        public int frameHeight = 32;
        public float pixelsPerUnit = 32f;
        [Tooltip("Bottom-center keeps the feet planted at the transform position.")]
        public Vector2 pivot = new Vector2(0.5f, 0f);

        [Header("Animation")]
        public float framesPerSecond = 8f;

        SpriteRenderer sr;
        Sprite[] front, back;

        // 0 = down/front, 1 = up/back, 2 = left, 3 = right
        int facing;
        bool moving;
        int frame;
        float timer;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            Rebuild();
        }

        /// <summary>Recolor + reslice. Call again whenever `look` changes (e.g. from the creation menu).</summary>
        public void Rebuild()
        {
            bool female = look.body == BodyType.Female;
            Texture2D srcFront = (female && masterFrontLong != null) ? masterFrontLong : masterFront;
            Texture2D srcBack  = (female && masterBackLong  != null) ? masterBackLong  : masterBack;

            Texture2D fTex = CharacterPalette.Recolor(srcFront, look);
            front = CharacterPalette.Slice(fTex, frameWidth, frameHeight, pixelsPerUnit, pivot);

            if (srcBack != null)
            {
                Texture2D bTex = CharacterPalette.Recolor(srcBack, look);
                back = CharacterPalette.Slice(bTex, frameWidth, frameHeight, pixelsPerUnit, pivot);
            }
            else { back = null; }

            frame = 0;
            timer = 0f;
            if (front != null && front.Length > 0) sr.sprite = front[0];
        }

        /// <summary>Call from your controller with the current move velocity (world units/sec).</summary>
        public void SetMovement(Vector2 velocity)
        {
            moving = velocity.sqrMagnitude > 0.0001f;
            if (!moving) return;

            if (Mathf.Abs(velocity.y) >= Mathf.Abs(velocity.x))
                facing = velocity.y > 0f ? 1 : 0;     // up -> back sheet, down -> front sheet
            else
                facing = velocity.x < 0f ? 2 : 3;     // left / right (front sheet, mirrored for left)
        }

        void Update()
        {
            // up uses the back sheet; everything else uses the front sheet
            Sprite[] sheet = (facing == 1 && back != null && back.Length > 0) ? back : front;
            if (sheet == null || sheet.Length == 0) return;

            // mirror horizontally when walking left
            sr.flipX = (facing == 2);

            if (moving)
            {
                timer += Time.deltaTime;
                float step = 1f / Mathf.Max(1f, framesPerSecond);
                while (timer >= step)
                {
                    timer -= step;
                    frame = (frame + 1) % sheet.Length;
                }
            }
            else
            {
                frame = 0;   // idle pose = first frame
            }

            sr.sprite = sheet[Mathf.Clamp(frame, 0, sheet.Length - 1)];
        }
    }
}
