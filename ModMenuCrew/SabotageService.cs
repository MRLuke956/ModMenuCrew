using UnityEngine;

namespace ModMenuCrew;

/// <summary>
/// Door control service for showcase version.
/// Sabotage features removed - doors only.
/// </summary>
public static class SabotageService
{
    /// <summary>
    /// Closes all valid doors.
    /// </summary>
    public static void ToggleAllDoors(bool close = true)
    {
        if (!close) return;
        foreach (var system in GetValidDoorSystems())
            SystemManager.CloseDoorsOfType(system);
        ShowNotification("All main doors closed!");
    }

    private static SystemTypes[] GetValidDoorSystems() => new[]
    {
        SystemTypes.Electrical,
        SystemTypes.MedBay,
        SystemTypes.Security,
        SystemTypes.Storage,
        SystemTypes.Cafeteria,
        SystemTypes.UpperEngine,
        SystemTypes.LowerEngine
    };

    // Shows visual notification on HUD
    private static void ShowNotification(string message)
    {
        try
        {
            var hud = HudManager.Instance;
            if (hud != null && hud.Notifier != null)
            {
                hud.Notifier.AddDisconnectMessage(message);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error showing notification: {e}");
        }
    }
}