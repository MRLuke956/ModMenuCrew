using Hazel;
using InnerNet;
using UnityEngine;
using Reactor.Networking;
using System;
using HarmonyLib;

namespace ModMenuCrew;

[HarmonyPatch(typeof(InnerNetClient))]
public static class GameEndManager
{
    private const byte CUSTOM_RPC_ID = 200;
    private const byte FORCE_END_GAME = 1;

    public static void ForceGameEnd(GameOverReason endReason, bool showAd = false)
    {
        if (AmongUsClient.Instance == null) return;

        try
        {
            MessageWriter writer = MessageWriter.Get(SendOption.Reliable);
            try
            {
                writer.StartMessage(5); // Use standard game message type
                writer.Write(AmongUsClient.Instance.GameId);
                writer.StartMessage(CUSTOM_RPC_ID);
                writer.Write(FORCE_END_GAME);
                writer.Write((byte)endReason);
                writer.Write(showAd);
                writer.EndMessage();

                if (AmongUsClient.Instance.AmHost)
                {
                    HandleForceEndGame(endReason, showAd);
                }
                else
                {
                    // Send to host only
                    AmongUsClient.Instance.SendOrDisconnect(writer);
                }
            }
            finally
            {
                writer.Recycle();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error in ForceGameEnd: {ex}");
        }
    }

    private static void HandleForceEndGame(GameOverReason endReason, bool showAd)
    {

        
            ShipStatus.Instance.enabled = false;
            GameManager.Instance.ShouldCheckForGameEnd = false;

            // Use the game's built-in end game system
            MessageWriter messageWriter = AmongUsClient.Instance.StartEndGame();
            try
            {
                messageWriter.Write((byte)endReason);
                messageWriter.Write(showAd);
                AmongUsClient.Instance.FinishEndGame(messageWriter);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in HandleForceEndGame: {ex}");
                messageWriter.Recycle();
            }
       
    }

    [HarmonyPatch(nameof(InnerNetClient.HandleMessage))]
    [HarmonyPrefix]
    public static bool HandleMessagePrefix(InnerNetClient __instance, [HarmonyArgument(0)] MessageReader reader)
    {
        if (reader.Tag == CUSTOM_RPC_ID)
        {
            byte subCommand = reader.ReadByte();
            if (subCommand == FORCE_END_GAME)
            {
                GameOverReason reason = (GameOverReason)reader.ReadByte();
                bool showAd = reader.ReadBoolean();

                if (__instance.AmHost)
                {
                    HandleForceEndGame(reason, showAd);
                }
                return false; // Don't let the original method handle this message
            }
        }
        return true; // Let other messages be handled normally
    }
}