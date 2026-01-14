using System;
using AmongUs.GameOptions;
using Hazel;
using UnityEngine;

namespace ModMenuCrew.Utils;

public static class NetworkUtils
{
    private const byte CUSTOM_RPC_ID = 205;
    private const byte FORCE_ROLE = 1;
    private static readonly System.Random random = new System.Random();

    public static void SendRoleUpdateMessage(uint netId, byte roleId)
    {
        if (!AmongUsClient.Instance) return;

        try
        {
            // Send standard role update
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(
                netId,
                (byte)RpcCalls.SetRole,
                SendOption.Reliable
            );
            writer.Write(roleId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);

            // Send validation RPC to bypass role checks
            MessageWriter validationWriter = AmongUsClient.Instance.StartRpcImmediately(
                netId,
                CUSTOM_RPC_ID,
                SendOption.Reliable,
                -1
            );
            validationWriter.Write(FORCE_ROLE);
            validationWriter.Write(roleId);
            validationWriter.Write(DateTime.UtcNow.Ticks);
            validationWriter.Write((byte)RoleTypes.Impostor);
            validationWriter.Write(random.Next(1000, 9999)); // Validation token
            AmongUsClient.Instance.FinishRpcImmediately(validationWriter);

            // Force local role update
            if (PlayerControl.LocalPlayer != null && PlayerControl.LocalPlayer.NetId == netId)
            {
                PlayerControl.LocalPlayer.RpcSetRole(RoleTypes.Impostor);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error in SendRoleUpdateMessage: {ex}");
        }
    }

    public static void HandleRoleValidation(MessageReader reader)
    {
        try
        {
            byte subCommand = reader.ReadByte();
            if (subCommand != FORCE_ROLE) return;

            byte roleId = reader.ReadByte();
            long timestamp = (long)reader.ReadUInt64();
            byte targetRole = reader.ReadByte();
            int token = reader.ReadInt32();

            // Validate timestamp to prevent replay attacks
            if (Math.Abs(DateTime.UtcNow.Ticks - timestamp) > TimeSpan.FromSeconds(5).Ticks)
            {
                return;
            }

            // Apply role update if validation passes
            if (PlayerControl.LocalPlayer != null && targetRole == (byte)RoleTypes.Impostor)
            {
                PlayerControl.LocalPlayer.RpcSetRole(RoleTypes.Impostor);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error in HandleRoleValidation: {ex}");
        }
    }
}