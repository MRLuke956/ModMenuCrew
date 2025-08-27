using Il2CppSystem;
using Il2CppSystem.Collections.Generic;
using UnityEngine;

namespace ModMenuCrew;

public static class SabotageService
{
    private const byte DOOR_RPC = 81;
    private static readonly Dictionary<SystemTypes, float> lastDoorAttempts = new();
    private static readonly TimeSpan DOOR_COOLDOWN = TimeSpan.FromSeconds(0.5f);

    /// <summary>
    /// Dispara a sabotagem do reator.
    /// </summary>
    public static void TriggerReactorMeltdown()
        => TriggerSabotage(SystemTypes.Reactor, "Sabotagem: Reator ativada!");

    /// <summary>
    /// Dispara a sabotagem de oxigênio.
    /// </summary>
    public static void TriggerOxygenDepletion()
        => TriggerSabotage(SystemTypes.LifeSupp, "Sabotagem: Oxigênio ativada!");

    /// <summary>
    /// Dispara a sabotagem das luzes.
    /// </summary>
    public static void TriggerLightsOut()
        => TriggerSabotage(SystemTypes.Electrical, "Sabotagem: Luzes apagadas!");

    /// <summary>
    /// Dispara todas as sabotagens principais.
    /// </summary>
    public static void TriggerAllSabotages()
    {
        if (!ShipStatus.Instance) return;
        foreach (var system in GetSabotageSystems())
            TriggerSabotage(system);
        ShowNotification("Todas as sabotagens principais ativadas!");
    }

    /// <summary>
    /// Dispara sabotagem para um sistema específico.
    /// </summary>
    public static void TriggerSabotage(SystemTypes system, string feedbackMsg = null)
    {
        if (!ShipStatus.Instance) return;
        ShipStatus.Instance.RpcUpdateSystem(system, 128);
        if (!string.IsNullOrEmpty(feedbackMsg))
            ShowNotification(feedbackMsg);
    }

    /// <summary>
    /// Fecha todas as portas válidas.
    /// </summary>
    public static void ToggleAllDoors(bool close = true)
    {
        if (!close) return;
        foreach (var system in GetValidDoorSystems())
            SystemManager.CloseDoorsOfType(system);
        ShowNotification("Todas as portas principais fechadas!");
    }

    private static SystemTypes[] GetSabotageSystems() => new[]
    {
        SystemTypes.Reactor,
        SystemTypes.LifeSupp,
        SystemTypes.Electrical,
        SystemTypes.Comms,
        SystemTypes.Laboratory
    };

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

    // Exibe notificação visual no HUD de forma segura
    private static void ShowNotification(string message)
    {
        try
        {
            var hud = HudManager.Instance;
            if (hud != null && hud.Notifier != null)
            {
                hud.Notifier.AddDisconnectMessage(message);
            }
            else
            {
                Debug.LogWarning("HudManager.Instance ou Notifier está nulo, não foi possível mostrar a notificação.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Erro ao mostrar notificação: {e}");
        }
    }
}