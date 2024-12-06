using UnityEngine;
using AmongUs.GameOptions;

namespace ModMenuCrew.Features;

public static class RoleCheats
{
    public static void HandleEngineerCheats(EngineerRole engineerRole, bool endlessVentTime, bool noVentCooldown)
    {
        if (endlessVentTime)
        {
            engineerRole.inVentTimeRemaining = float.MaxValue;
        }
        else if (engineerRole.inVentTimeRemaining > engineerRole.GetCooldown())
        {
            engineerRole.inVentTimeRemaining = engineerRole.GetCooldown();
        }

        if (noVentCooldown && engineerRole.cooldownSecondsRemaining > 0f)
        {
            engineerRole.cooldownSecondsRemaining = 0f;
            DestroyableSingleton<HudManager>.Instance.AbilityButton.ResetCoolDown();
            DestroyableSingleton<HudManager>.Instance.AbilityButton.SetCooldownFill(0f);
        }
    }

    public static void HandleShapeshifterCheats(ShapeshifterRole shapeshifterRole, bool endlessDuration)
    {
        if (endlessDuration)
        {
            shapeshifterRole.durationSecondsRemaining = float.MaxValue;
        }
        else if (shapeshifterRole.durationSecondsRemaining > GameManager.Instance.LogicOptions.GetShapeshifterDuration())
        {
            shapeshifterRole.durationSecondsRemaining = GameManager.Instance.LogicOptions.GetShapeshifterDuration();
        }
    }

    public static void HandleScientistCheats(ScientistRole scientistRole, bool noVitalsCooldown, bool endlessBattery)
    {
        if (noVitalsCooldown)
        {
            scientistRole.currentCooldown = 0f;
        }

        if (endlessBattery)
        {
            scientistRole.currentCharge = float.MaxValue;
        }
        else if (scientistRole.currentCharge > scientistRole.RoleCooldownValue)
        {
            scientistRole.currentCharge = scientistRole.RoleCooldownValue;
        }
    }

    public static void HandleTrackerCheats(TrackerRole trackerRole, bool noTrackingCooldown, bool noTrackingDelay, bool endlessTracking)
    {
        if (noTrackingCooldown)
        {
            trackerRole.cooldownSecondsRemaining = 0f;
            trackerRole.delaySecondsRemaining = 0f;
            DestroyableSingleton<HudManager>.Instance.AbilityButton.ResetCoolDown();
            DestroyableSingleton<HudManager>.Instance.AbilityButton.SetCooldownFill(0f);
        }

        if (noTrackingDelay)
        {
            MapBehaviour.Instance.trackedPointDelayTime = GameManager.Instance.LogicOptions.GetTrackerDelay();
        }

        if (endlessTracking)
        {
            trackerRole.durationSecondsRemaining = float.MaxValue;
        }
        else if (trackerRole.durationSecondsRemaining > GameManager.Instance.LogicOptions.GetTrackerDuration())
        {
            trackerRole.durationSecondsRemaining = GameManager.Instance.LogicOptions.GetTrackerDuration();
        }
    }

    public static void EnableVentingForAll(HudManager hudManager)
    {
      
            hudManager.ImpostorVentButton.gameObject.SetActive(true);
        
    }

    public static void AllowWalkingInVent()
    {
        

        PlayerControl.LocalPlayer.inVent = false;
        PlayerControl.LocalPlayer.moveable = true;
    }
}