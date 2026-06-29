using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// The boy/girl partner companion. Plays its idle loop; reacts with a smile when the dog is
/// petted; and, when the player presses E nearby, plays its speaking animation while a line of
/// dialog shows at the bottom of the screen. The remaining row (wave) is sliced for later.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(LoopSpriteAnimator))]
public class PartnerController : MonoBehaviour
{
    public Transform player;
    public Sprite[] boyIdle;
    public Sprite[] girlIdle;
    public Sprite[] boySmile;
    public Sprite[] girlSmile;
    public Sprite[] boySpeak;
    public Sprite[] girlSpeak;
    public float smileDuration = 3f;
    public float talkRange = 2.6f;
    public float lineDuration = 4f;
    [TextArea]
    public string[] lines =
    {
        "Hey! It's good to see you.",
        "Stay close to me, okay?",
        "Did you give the dog a pat today?",
        "I had the strangest dream last night...",
        "I'm glad we're together.",
    };

    LoopSpriteAnimator _anim;
    SpriteRenderer _sr;
    Sprite[] _idle, _smile, _speak;
    float _tempTimer;
    int _line;

    void Awake()
    {
        _anim = GetComponent<LoopSpriteAnimator>();
        _sr = GetComponent<SpriteRenderer>();
        bool girl = CharacterStore.LoadPartner() == 1;          // 0 = boy, 1 = girl
        _idle  = girl ? girlIdle  : boyIdle;
        _smile = girl ? girlSmile : boySmile;
        _speak = girl ? girlSpeak : boySpeak;
        _anim.frames = _idle;
        if (_idle != null && _idle.Length > 0) _sr.sprite = _idle[0];
    }

    /// <summary>React happily for a few seconds (used when the dog is petted).</summary>
    public void Smile() => PlayTemp(_smile, smileDuration);

    void Update()
    {
        if (_tempTimer > 0f)
        {
            _tempTimer -= Time.deltaTime;
            if (_tempTimer <= 0f) _anim.frames = _idle;
        }

        if (player == null) return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

        Vector3 a = player.position; a.y = 0f;
        Vector3 b = transform.position; b.y = 0f;
        if (Vector3.Distance(a, b) > talkRange) return;

        if (DialogUI.Instance != null) DialogUI.Instance.ShowPrompt("Press E to talk");
        if (TalkPressed()) Talk();
    }

    void Talk()
    {
        PlayTemp(_speak, lineDuration);
        if (lines != null && lines.Length > 0 && DialogUI.Instance != null)
        {
            DialogUI.Instance.ShowDialog(lines[_line % lines.Length], lineDuration);
            _line++;
        }
    }

    void PlayTemp(Sprite[] frames, float dur)
    {
        if (frames == null || frames.Length == 0) return;
        _tempTimer = dur;
        _anim.frames = frames;
    }

    bool TalkPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }
}
