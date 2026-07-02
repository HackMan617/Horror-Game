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

    [Header("Breed sets (index matches CharacterStore.DogNames); randomised at character creation")]
    public BreedFrames[] breeds;

    public float fps = 6f;
    public float followDistance = 2.5f;     // stop this far from the player (stays visible)
    public float speed = 4.5f;              // a little faster than the walk so it can keep up
    public float petRange = 3.5f;           // press P within this distance to pet
    public float petDuration = 3f;          // how long the hearts reaction plays

    [Header("Panting audio (intermittent — not a constant loop)")]
    public AudioClip pantClip;              // Dog Panting.wav
    public float pantMinGap = 3.5f;         // shortest wait between pants while trotting to the player
    public float pantMaxGap = 7f;           // longest wait between pants while trotting
    [Range(0f, 1f)] public float petPantChance = 0.4f;   // an occasional happy pant when petted
    [Range(0f, 1f)] public float pantVolume = 0.7f;

    SpriteRenderer _sr;
    AudioSource _audio;
    float _t;
    bool _hidden;
    float _petTimer;
    float _pantTimer;
    bool _wasWalking;
    PartnerController _partner;
    bool _partnerSearched;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _audio = GetComponent<AudioSource>();
        ApplyChosenBreed();
    }

    // Swap in the breed picked at character creation (CharacterStore.LoadDog). The baked
    // apricot frames stay as a fallback if breeds isn't populated (e.g. an un-rebuilt scene).
    void ApplyChosenBreed()
    {
        if (breeds == null || breeds.Length == 0) return;
        var b = breeds[Mathf.Clamp(CharacterStore.LoadDog(), 0, breeds.Length - 1)];
        if (b == null) return;
        if (b.idle  != null && b.idle.Length  > 0) idleFrames  = b.idle;
        if (b.walk  != null && b.walk.Length  > 0) walkFrames  = b.walk;
        if (b.heart != null && b.heart.Length > 0) heartFrames = b.heart;
        if (idleFrames != null && idleFrames.Length > 0) _sr.sprite = idleFrames[0];
    }

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

        // Intermittent panting while trotting toward the player: a short delay when it starts moving,
        // then a random gap between each pant so it never becomes a constant drone. Silent when sitting.
        if (walking)
        {
            if (!_wasWalking) _pantTimer = Random.Range(0.3f, pantMinGap);   // pant soon after setting off
            _pantTimer -= Time.deltaTime;
            if (_pantTimer <= 0f) { PlayPant(); _pantTimer = Random.Range(pantMinGap, pantMaxGap); }
        }
        _wasWalking = walking;

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
        if (Random.value < petPantChance) PlayPant();   // an occasional happy pant, not every pet
    }

    // Fire a single pant, unless one's already playing (keeps overlaps from stacking into a drone).
    void PlayPant()
    {
        if (pantClip == null || _audio == null || _audio.isPlaying) return;
        _audio.PlayOneShot(pantClip, pantVolume);
    }

    // One dog breed's animation frames (idle / walk / hearts). Wrapped in a class so Unity can
    // serialize an array of them (it won't serialize a bare Sprite[][]).
    [System.Serializable]
    public class BreedFrames
    {
        public string name;          // Apricot / Chocolate / Cream (label only)
        public Sprite[] idle;
        public Sprite[] walk;
        public Sprite[] heart;
    }
}
