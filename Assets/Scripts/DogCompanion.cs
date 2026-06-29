using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Apricot dog companion for the overworld. Trails the player on the XZ plane,
/// stopping a short distance away so it stays on-screen, plays a walk cycle while
/// moving and a sitting idle loop while stopped. Hidden during the nightmare.
/// Press P while near the dog to pet it: the dog plays its hearts reaction (and sits
/// still) while the partner smiles, then both return to normal.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class DogCompanion : MonoBehaviour
{
    public Transform player;
    public NightmareController nightmare;   // hide while the nightmare is active
    public Sprite[] idleFrames;
    public Sprite[] walkFrames;
    public Sprite[] heartFrames;            // bottom row: happy "being petted" hearts
    public float fps = 6f;
    public float followDistance = 2.5f;     // stop this far from the player (stays visible)
    public float speed = 4.5f;              // a little faster than the walk so it can keep up
    public float petRange = 3.5f;           // press P within this distance to pet
    public float petDuration = 3f;          // how long the hearts reaction plays

    SpriteRenderer _sr;
    float _t;
    bool _hidden;
    float _petTimer;
    PartnerController _partner;
    bool _partnerSearched;

    void Awake() { _sr = GetComponent<SpriteRenderer>(); }

    void Update()
    {
        // Overworld only: vanish during the nightmare.
        if (nightmare != null && nightmare.IsNightmare)
        {
            if (!_hidden) { _sr.enabled = false; _hidden = true; }
            return;
        }
        if (_hidden) { _sr.enabled = true; _hidden = false; }

        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;
        if (player == null) return;

        if (_petTimer > 0f) _petTimer -= Time.deltaTime;
        bool petting = _petTimer > 0f;

        Vector3 to = player.position - transform.position;
        to.y = 0f;
        float dist = to.magnitude;

        // Follow on the ground plane, but sit still while being petted.
        bool walking = !petting && dist > followDistance;
        if (walking)
        {
            float move = Mathf.Min(speed * Time.deltaTime, dist - followDistance);
            transform.position += (to / dist) * move;
        }

        // Pet the dog (P) when the player is close.
        if (dist <= petRange && PetPressed()) Pet();

        // Hearts while petting, otherwise the walk cycle / sitting idle.
        Sprite[] frames = (petting && heartFrames != null && heartFrames.Length > 0)
            ? heartFrames
            : (walking ? walkFrames : idleFrames);
        if (frames != null && frames.Length > 0)
        {
            _t += Time.deltaTime * fps;
            _sr.sprite = frames[((int)_t) % frames.Length];
        }
    }

    bool PetPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.P);
#endif
    }

    void Pet()
    {
        _petTimer = petDuration;
        if (!_partnerSearched) { _partner = FindAnyObjectByType<PartnerController>(); _partnerSearched = true; }
        if (_partner != null) _partner.Smile();
    }
}
