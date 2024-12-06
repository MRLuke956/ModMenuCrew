using Hazel;
using InnerNet;
using UnityEngine;

namespace ModMenuCrew;

public static class SystemManager
{
    public static void CloseDoorsOfType(SystemTypes type)
    {
        if (!ShipStatus.Instance) return;

        // Send RPC directly to all clients including host
        MessageWriter messageWriter = AmongUsClient.Instance.StartRpcImmediately(ShipStatus.Instance.NetId, 27, SendOption.Reliable, AmongUsClient.Instance.HostId);
        messageWriter.Write((byte)type);
        AmongUsClient.Instance.FinishRpcImmediately(messageWriter);

        // Also close doors locally for immediate feedback
        ShipStatus.Instance.RpcCloseDoorsOfType(type);
    }
}