using UnityEngine;

/// <summary>
/// A dropped log left behind when a <see cref="ChoppableTree"/> is felled (the <c>log_pickup</c>
/// sheet, see CHOPPING.md). Bobs between its two frames (idle + cut-end glint) and is collected when
/// the player walks up to it — a short arm delay stops it from being grabbed the instant it spawns
/// under the player who just chopped it. On pickup it puts the player into the hold-wood carry pose
/// via <see cref="AxeChopper.ShowCarry"/>, so they're shown holding the log in-hand.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class LogPickup : MonoBehaviour
{
    public Sprite[] frames;                 // 2 sliced cells (idle bob + glint)
    public float bobSeconds = 0.22f;        // time per frame of the bob
    public float pickupRadius = 1.2f;       // how close the player must get to collect it
    public float armDelay = 0.5f;           // ignore pickups for this long after spawning
    public string pickupMessage = "Picked up a log";

    Transform _player;
    SpriteRenderer _sr;
    float _t, _arm;
    int _f;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        if (frames != null && frames.Length > 0) _sr.sprite = frames[0];
        var pc = FindAnyObjectByType<PlayerController3D>();
        if (pc != null) _player = pc.transform;
    }

    void Update()
    {
        // slow two-frame bob / glint
        if (frames != null && frames.Length > 1)
        {
            _t += Time.deltaTime;
            if (_t >= bobSeconds) { _t = 0f; _f ^= 1; _sr.sprite = frames[_f]; }
        }

        _arm += Time.deltaTime;
        if (_arm < armDelay || _player == null) return;

        Vector3 a = transform.position; a.y = 0f;
        Vector3 b = _player.position; b.y = 0f;
        if ((a - b).sqrMagnitude <= pickupRadius * pickupRadius)
        {
            if (DialogUI.Instance != null) DialogUI.Instance.ShowDialog(pickupMessage, 1.5f);
            // Put the player into the hold-wood carry pose — they're now holding the log in-hand.
            var chopper = FindAnyObjectByType<AxeChopper>();
            if (chopper != null) chopper.ShowCarry(true);
            Destroy(gameObject);
        }
    }
}
