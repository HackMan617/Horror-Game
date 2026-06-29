using UnityEngine;

/// <summary>
/// Spawns the chosen partner (boy or girl) in the overworld and plays its idle loop.
/// When the player pets the dog (DogCompanion), the partner reacts with a smile for a
/// few seconds, then returns to idle. The remaining animations (speak / wave / talk) are
/// sliced and available for later.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(LoopSpriteAnimator))]
public class PartnerController : MonoBehaviour
{
    public Sprite[] boyIdle;
    public Sprite[] girlIdle;
    public Sprite[] boySmile;
    public Sprite[] girlSmile;
    public float smileDuration = 3f;

    LoopSpriteAnimator _anim;
    Sprite[] _idle, _smile;
    float _smileTimer;

    void Awake()
    {
        _anim = GetComponent<LoopSpriteAnimator>();
        bool girl = CharacterStore.LoadPartner() == 1;           // 0 = boy, 1 = girl
        _idle  = girl ? girlIdle  : boyIdle;
        _smile = girl ? girlSmile : boySmile;
        _anim.frames = _idle;
        if (_idle != null && _idle.Length > 0) GetComponent<SpriteRenderer>().sprite = _idle[0];
    }

    /// <summary>React happily for a few seconds, then return to the idle loop.</summary>
    public void Smile()
    {
        if (_smile == null || _smile.Length == 0) return;
        _smileTimer = smileDuration;
        _anim.frames = _smile;
    }

    void Update()
    {
        if (_smileTimer > 0f)
        {
            _smileTimer -= Time.deltaTime;
            if (_smileTimer <= 0f) _anim.frames = _idle;
        }
    }
}
