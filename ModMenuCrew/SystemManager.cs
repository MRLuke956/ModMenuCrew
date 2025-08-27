using Hazel;
using UnityEngine;

namespace ModMenuCrew;

public static class SystemManager
{
    /// <summary>
    /// Fecha as portas de um sistema específico, se aplicável.
    /// </summary>
    public static void CloseDoorsOfType(SystemTypes type)
    {
        if (!ShipStatus.Instance)
        {
            ShowNotification("Erro: ShipStatus não está disponível!");
            return;
        }
        if (!IsDoorSystem(type))
        {
            ShowNotification($"O sistema {type} não possui portas para fechar.");
            return;
        }
        try
        {
            // Envia RPC para todos, incluindo host
            MessageWriter messageWriter = AmongUsClient.Instance.StartRpcImmediately(
                ShipStatus.Instance.NetId, 27, SendOption.Reliable, AmongUsClient.Instance.HostId);
            messageWriter.Write((byte)type);
            AmongUsClient.Instance.FinishRpcImmediately(messageWriter);

            // Fecha localmente para feedback imediato
            ShipStatus.Instance.RpcCloseDoorsOfType(type);

            ShowNotification($"Portas de {type} fechadas!");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Erro ao fechar portas de {type}: {e}");
            ShowNotification($"Erro ao fechar portas de {type}!");
        }
    }

    /// <summary>
    /// Verifica se o tipo de sistema possui portas.
    /// </summary>
    private static bool IsDoorSystem(SystemTypes type)
    {
        switch (type)
        {
            case SystemTypes.Electrical:
            case SystemTypes.MedBay:
            case SystemTypes.Security:
            case SystemTypes.Storage:
            case SystemTypes.Cafeteria:
            case SystemTypes.UpperEngine:
            case SystemTypes.LowerEngine:
                return true;
            default:
                return false;
        }
    }

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