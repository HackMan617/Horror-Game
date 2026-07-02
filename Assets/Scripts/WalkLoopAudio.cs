using UnityEngine;

/// <summary>
/// A looping footfall bed that rises while the player walks and eases back to silence when they stop —
/// used for the house interior's wooden floor, where the sound is one continuous walking loop rather
/// than per-step clips (so it doesn't fit <see cref="FootstepAudio"/>). The clip loops continuously and
/// is gated purely by volume, so starting/stopping never clicks. Pairs with disabling the per-step
/// FootstepAudio on the same player indoors.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class WalkLoopAudio : MonoBehaviour
{
    public PlayerController3D player;
    public CharacterController controller;
    public AudioClip loopClip;
    [Range(0f, 1f)] public float volume = 0.6f;
    [Tooltip("Volume units per second eased in/out as the player starts and stops moving.")]
    public float fade = 6f;

    AudioSource _src;

    void Awake()
    {
        _src = GetComponent<AudioSource>();
        _src.clip = loopClip;
        _src.loop = true;
        _src.playOnAwake = false;
        _src.spatialBlend = 0f;      // the player's own footsteps: 2D
        _src.volume = 0f;
        if (player == null) player = GetComponentInParent<PlayerController3D>();
        if (controller == null) controller = GetComponentInParent<CharacterController>();
        if (loopClip != null) _src.Play();   // runs continuously; volume does the gating
    }

    void Update()
    {
        bool paused = GameManager.Instance != null && GameManager.Instance.IsPaused;
        bool grounded = controller == null || controller.isGrounded;
        bool moving = player != null && player.MoveInput.sqrMagnitude > 0.01f;
        float target = (!paused && grounded && moving && loopClip != null) ? volume : 0f;
        _src.volume = Mathf.MoveTowards(_src.volume, target, fade * Time.deltaTime);
    }
}
