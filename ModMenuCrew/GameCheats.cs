using UnityEngine;
using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using InnerNet;
using System;

namespace ModMenuCrew.Features;

public static class GameCheats
{
    private const byte RPC_SET_SCANNER = 15;
    private const byte CUSTOM_RPC_ID = 200;
    private const byte FORCE_END_GAME = 1;
    private static readonly System.Random random = new System.Random();

    public static void CloseMeeting()
    {
        if (MeetingHud.Instance)
        {
            MeetingHud.Instance.DespawnOnDestroy = false;
            UnityEngine.Object.Destroy(MeetingHud.Instance.gameObject);

            DestroyableSingleton<HudManager>.Instance.StartCoroutine(
                DestroyableSingleton<HudManager>.Instance.CoFadeFullScreen(Color.black, Color.clear, 0.2f, false)
            );
            PlayerControl.LocalPlayer.SetKillTimer(GameManager.Instance.LogicOptions.GetKillCooldown());
            ShipStatus.Instance.EmergencyCooldown = GameManager.Instance.LogicOptions.GetEmergencyCooldown();
            Camera.main.GetComponent<FollowerCamera>().Locked = false;
            DestroyableSingleton<HudManager>.Instance.SetHudActive(true);
            ControllerManager.Instance.CloseAndResetAll();
        }
        else if (ExileController.Instance != null)
        {
            ExileController.Instance.ReEnableGameplay();
            ExileController.Instance.WrapUp();
        }
    }

    public static void CompleteAllTasks()
    {
        if (!PlayerControl.LocalPlayer) return;

        foreach (var task in PlayerControl.LocalPlayer.myTasks)
        {
            task.Complete();
        }
    }

    public static void KillAll(bool crewOnly = false, bool impostorsOnly = false)
    {
        if (!ShipStatus.Instance || !PlayerControl.LocalPlayer) return;

        try
        {
            bool isFreePlay = AmongUsClient.Instance == null || !AmongUsClient.Instance.IsGameStarted;

            foreach (var target in PlayerControl.AllPlayerControls)
            {
                if (target == PlayerControl.LocalPlayer) continue;

                bool shouldKill = true;
                if (crewOnly)
                {
                    shouldKill = target.Data.Role.TeamType == RoleTeamTypes.Crewmate;
                }
                else if (impostorsOnly)
                {
                    shouldKill = target.Data.Role.TeamType == RoleTeamTypes.Impostor;
                }

                if (!shouldKill) continue;

                if (isFreePlay)
                {
                    PlayerControl.LocalPlayer.MurderPlayer(target, MurderResultFlags.Succeeded);
                }
                else
                {
                    foreach (var player in PlayerControl.AllPlayerControls)
                    {
                        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(
                            PlayerControl.LocalPlayer.NetId,
                            (byte)RpcCalls.MurderPlayer,
                            SendOption.None,
                            AmongUsClient.Instance.GetClientIdFromCharacter(player)
                        );
                        writer.WriteNetObject(target);
                        writer.Write((int)MurderResultFlags.Succeeded);
                        AmongUsClient.Instance.FinishRpcImmediately(writer);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error in KillAll: {ex}");
        }
    }

    public static void TeleportToCursor()
    {
        if (!PlayerControl.LocalPlayer) return;
        if (Input.GetMouseButtonDown(1))
        {
            Vector2 worldPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            PlayerControl.LocalPlayer.NetTransform.RpcSnapTo(worldPosition);
        }
    }

    public static void HandleSpeedBoost(float multiplier = 2.0f)
    {
        if (!PlayerControl.LocalPlayer) return;

        const float defaultSpeed = 2.5f;
        const float defaultGhostSpeed = 3f;

        PlayerControl.LocalPlayer.MyPhysics.Speed = defaultSpeed * multiplier;
        PlayerControl.LocalPlayer.MyPhysics.GhostSpeed = defaultGhostSpeed * multiplier;
    }

    public static void KickAllFromVents()
    {
        if (!ShipStatus.Instance) return;

        foreach (var vent in ShipStatus.Instance.AllVents)
        {
            VentilationSystem.Update(VentilationSystem.Operation.BootImpostors, vent.Id);
        }
    }

    public static void BypassScanner(bool value = true)
    {
        try
        {
            if (PlayerControl.LocalPlayer == null || AmongUsClient.Instance == null) return;

            byte scannerCount = (byte)(PlayerControl.LocalPlayer.scannerCount + random.Next(1, 3));
            PlayerControl.LocalPlayer.scannerCount = scannerCount;

            PlayerControl.LocalPlayer.SetScanner(value, scannerCount);

            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(
                PlayerControl.LocalPlayer.NetId,
                RPC_SET_SCANNER,
                SendOption.Reliable,
                AmongUsClient.Instance.HostId
            );

            writer.Write(value);
            writer.Write(scannerCount);
            writer.Write(DateTime.UtcNow.Ticks);

            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in BypassScanner: {e}");
        }
    }

    public static void ForceGameEnd(GameOverReason endReason, bool showAd = false)
    {
        if (AmongUsClient.Instance == null) return;

        try
        {
            MessageWriter writer = MessageWriter.Get(SendOption.Reliable);
            try
            {
                writer.StartMessage(5);
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
        if (!AmongUsClient.Instance.AmHost) return;

        try
        {
            ShipStatus.Instance.enabled = false;
            GameManager.Instance.ShouldCheckForGameEnd = false;

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
        catch (Exception ex)
        {
            Debug.LogError($"Error in HandleForceEndGame outer block: {ex}");
        }
    }

    public static void HandleScannerRPC(PlayerControl instance, byte callId, MessageReader reader)
    {
        try
        {
            if (callId == RPC_SET_SCANNER && AmongUsClient.Instance.AmHost)
            {
                bool scanValue = reader.ReadBoolean();
                byte scanCount = reader.ReadByte();
                long timestamp = (long)reader.ReadUInt64();

                if (Math.Abs(DateTime.UtcNow.Ticks - timestamp) > TimeSpan.FromSeconds(5).Ticks)
                {
                    return;
                }

                instance.SetScanner(scanValue, scanCount);
            }
            else if (callId == CUSTOM_RPC_ID)
            {
                byte subCommand = reader.ReadByte();
                if (subCommand == FORCE_END_GAME && AmongUsClient.Instance.AmHost)
                {
                    GameOverReason reason = (GameOverReason)reader.ReadByte();
                    bool showAd = reader.ReadBoolean();
                    HandleForceEndGame(reason, showAd);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in HandleScannerRPC: {e}");
        }
    }
}