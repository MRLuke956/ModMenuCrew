using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;
using System;

namespace ModMenuCrew.Patches;

[HarmonyPatch(typeof(PlayerControl))]
public static class ScannerPatch
{
    private const byte RPC_SET_SCANNER = 15;
    private static readonly System.Random random = new System.Random();

    [HarmonyPatch(nameof(PlayerControl.RpcSetScanner))]
    [HarmonyPrefix]
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] bool value)
    {
        try
        {
            if (__instance == null || AmongUsClient.Instance == null) return false;

            // Generate a realistic scanner count increment
            byte scannerCount = (byte)(__instance.scannerCount + random.Next(1, 3));
            __instance.scannerCount = scannerCount;

            // Apply locally first
            __instance.SetScanner(value, scannerCount);

            // Broadcast to all clients, not just host
            MessageWriter writer = AmongUsClient.Instance.StartRpc(__instance.NetId, RPC_SET_SCANNER, SendOption.Reliable);
            writer.Write(value);
            writer.Write(scannerCount);
            writer.Write(DateTime.UtcNow.Ticks);
            writer.EndMessage();

            // Also send direct to host for validation
            if (!AmongUsClient.Instance.AmHost)
            {
                MessageWriter hostWriter = AmongUsClient.Instance.StartRpcImmediately(
                    __instance.NetId,
                    RPC_SET_SCANNER,
                    SendOption.Reliable,
                    AmongUsClient.Instance.HostId
                );
                hostWriter.Write(value);
                hostWriter.Write(scannerCount);
                hostWriter.Write(DateTime.UtcNow.Ticks);
                AmongUsClient.Instance.FinishRpcImmediately(hostWriter);
            }

            return false;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in ScannerPatch.Prefix: {e}");
            return true;
        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
    [HarmonyPostfix]
    public static void HandleRpcPostfix(PlayerControl __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        try
        {
            if (callId == RPC_SET_SCANNER)
            {
                bool scanValue = reader.ReadBoolean();
                byte scanCount = reader.ReadByte();
                long timestamp = reader.ReadInt32();

                // Validate timestamp to prevent replay attacks
                if (Math.Abs(DateTime.UtcNow.Ticks - timestamp) > TimeSpan.FromSeconds(5).Ticks)
                {
                    return;
                }

                // Apply scanner state to all clients
                __instance.SetScanner(scanValue, scanCount);

                // If host, rebroadcast to ensure all clients are synced
                if (AmongUsClient.Instance.AmHost && __instance.PlayerId != PlayerControl.LocalPlayer.PlayerId)
                {
                    MessageWriter writer = AmongUsClient.Instance.StartRpc(__instance.NetId, RPC_SET_SCANNER, SendOption.Reliable);
                    writer.Write(scanValue);
                    writer.Write(scanCount);
                    writer.Write(timestamp);
                    writer.EndMessage();
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in HandleRpcPostfix: {e}");
        }
    }
}