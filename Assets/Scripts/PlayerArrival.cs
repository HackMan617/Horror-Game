using UnityEngine;

/// <summary>
/// Drops the player at the arrival point a door requested before it loaded this scene
/// (HousePortal.overrideArrival), so stepping out of a building lands you back at that
/// building's door instead of the scene's default spawn. One-shot: it consumes the pending
/// arrival, so a fresh scene load (game start) leaves the default spawn untouched.
/// The CharacterController is toggled off around the teleport so it doesn't fight the move.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerArrival : MonoBehaviour
{
    void Start()
    {
        if (!HousePortal.HasArrival) return;
        HousePortal.HasArrival = false;

        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        transform.position = HousePortal.ArrivalPosition;
        if (cc != null) cc.enabled = true;
    }
}
