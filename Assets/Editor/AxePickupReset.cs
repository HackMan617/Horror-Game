using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor helpers for the axe-in-stump pickup (see AxePickup / AXE_STUMP.md).
///
/// <para>The pickup is persisted: once taken, <see cref="CharacterStore"/> keeps the
/// <c>cabin_axe</c> flag (this stump stays empty) and the <c>has_axe</c> flag (the player keeps
/// their axe) across doors and reloads. That's correct for play, but during testing it means the
/// stump loads <b>already empty</b>. Use <b>Tools ▸ Horror Game ▸ Reset Axe Pickup</b> to put the
/// axe back in the stump and re-lock woodcutting.</para>
/// </summary>
public static class AxePickupReset
{
    // Kept in sync with AxePickup.pickupId (default) and AxeChopper.AxeFlag.
    const string CabinAxeFlag = "cabin_axe";

    [MenuItem("Tools/Horror Game/Reset Axe Pickup")]
    static void ResetAxePickup()
    {
        // Clearing to false is equivalent to unset: CharacterStore.GetFlag treats 0 as "not taken".
        CharacterStore.SetFlag(CabinAxeFlag, false);   // stump shows the axe again
        CharacterStore.SetFlag(AxeChopper.AxeFlag, false);   // player no longer has an axe
        PlayerPrefs.Save();

        Debug.Log($"[AxePickupReset] Cleared '{CabinAxeFlag}' and '{AxeChopper.AxeFlag}'. " +
                  "The axe will be back in the stump on next play, and chopping is re-locked until it's taken.");
    }

    [MenuItem("Tools/Horror Game/Report Axe Pickup State")]
    static void ReportAxePickupState()
    {
        bool taken   = CharacterStore.GetFlag(CabinAxeFlag);
        bool hasAxe  = CharacterStore.GetFlag(AxeChopper.AxeFlag);
        Debug.Log($"[AxePickupReset] cabin_axe taken = {taken}; player has_axe = {hasAxe}. " +
                  (taken ? "Stump loads EMPTY — run Reset Axe Pickup to restore it." : "Stump loads WITH the axe."));
    }
}
