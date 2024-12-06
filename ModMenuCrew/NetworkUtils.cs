using Hazel;
using InnerNet;

namespace ModMenuCrew.Utils;

public static class NetworkUtils
{
    public static void SendRoleUpdateMessage(uint netId, byte roleId)
    {
        if (!AmongUsClient.Instance) return;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(
            netId,
            (byte)RpcCalls.SetRole,
            SendOption.Reliable
        );
        writer.Write(roleId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
}