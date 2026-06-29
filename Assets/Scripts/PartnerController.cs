using UnityEngine;

/// <summary>
/// Spawns the chosen partner (boy or girl) in the overworld and plays its idle loop.
/// The choice is read from CharacterStore (set on the character-select screen). The
/// other animations (speak / wave / talk / smile) are sliced and available for later.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(LoopSpriteAnimator))]
public class PartnerController : MonoBehaviour
{
    public Sprite[] boyIdle;
    public Sprite[] girlIdle;

    void Awake()
    {
        var frames = (CharacterStore.LoadPartner() == 1) ? girlIdle : boyIdle;   // 0 = boy, 1 = girl
        GetComponent<LoopSpriteAnimator>().frames = frames;
        if (frames != null && frames.Length > 0) GetComponent<SpriteRenderer>().sprite = frames[0];
    }
}
