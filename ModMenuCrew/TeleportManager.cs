using System;
using System.Collections.Generic;
using System.Linq;
using Hazel;
using UnityEngine;

namespace ModMenuCrew.UI.Managers;

public class TeleportManager
{
    private const byte TELEPORT_RPC = 230;
    private const byte TELEPORT_SYNC = 231;
    private const float MAX_TELEPORT_DISTANCE = 50f;
    private const float MIN_TELEPORT_INTERVAL = 0.5f;
    private const int MAX_ATTEMPTS = 3;
    private const int VERIFICATION_TOKEN_LENGTH = 32;

    private readonly Dictionary<SystemTypes, Vector2> locations;
    private readonly Dictionary<byte, DateTime> lastPlayerTeleports;
    private readonly System.Random random;
    private float lastTeleportTime;
    private byte lastTeleportId;

    public TeleportManager()
    {
        random = new System.Random();
        lastPlayerTeleports = new Dictionary<byte, DateTime>();

        locations = new Dictionary<SystemTypes, Vector2>
        {
            { SystemTypes.Electrical, new Vector2(-8.6f, -8.3f) },
            { SystemTypes.Security, new Vector2(-13.3f, -5.9f) },
            { SystemTypes.MedBay, new Vector2(-10.5f, -3.5f) },
            { SystemTypes.Storage, new Vector2(-3.5f, -11.9f) },
            { SystemTypes.Cafeteria, new Vector2(0f, 0f) },
            { SystemTypes.Admin, new Vector2(3.5f, -7.5f) },
            { SystemTypes.Weapons, new Vector2(4.5f, 4f) },
            { SystemTypes.Nav, new Vector2(16.7f, -4.8f) },
            { SystemTypes.Shields, new Vector2(9.3f, -11.3f) }
        };
    }

    public IReadOnlyDictionary<SystemTypes, Vector2> Locations => locations;

    public void TeleportToLocation(SystemTypes location)
    {
        try
        {
            if (!ValidateGameState() || !ValidateTeleportCooldown()) return;

            if (locations.TryGetValue(location, out Vector2 position))
            {
                ExecuteTeleportWithRetry(position);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[TeleportManager] Location teleport error: {e}");
        }
    }

    public void TeleportToPlayer(PlayerControl target)
    {
        try
        {
            if (!ValidateGameState() || !ValidateTeleportCooldown() || !ValidateTargetPlayer(target)) return;

            Vector2 targetPosition = target.GetTruePosition();
            if (ValidateTeleportDistance(targetPosition))
            {
                ExecuteTeleportWithRetry(targetPosition);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[TeleportManager] Player teleport error: {e}");
        }
    }

    public void TeleportAllToMe()
    {
        try
        {
            if (!ValidateGameState() || !AmongUsClient.Instance.AmHost) return;

            var myPosition = PlayerControl.LocalPlayer.GetTruePosition();
            var teleportToken = GenerateSecureToken();

            foreach (var player in PlayerControl.AllPlayerControls.ToArray())
            {
                if (ValidateTargetPlayer(player) && !IsPlayerTeleportOnCooldown(player.PlayerId))
                {
                    SendMassTeleportRpc(player.PlayerId, myPosition, teleportToken);
                    lastPlayerTeleports[player.PlayerId] = DateTime.UtcNow;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[TeleportManager] Mass teleport error: {e}");
        }
    }

    public PlayerControl GetClosestPlayer()
    {
        try
        {
            if (!ValidateGameState()) return null;

            var localPosition = PlayerControl.LocalPlayer.GetTruePosition();
            return PlayerControl.AllPlayerControls
                .ToArray()
                .Where(p => ValidateTargetPlayer(p))
                .OrderBy(p => Vector2.Distance(p.GetTruePosition(), localPosition))
                .FirstOrDefault();
        }
        catch (Exception e)
        {
            Debug.LogError($"[TeleportManager] GetClosestPlayer error: {e}");
            return null;
        }
    }

    private void ExecuteTeleportWithRetry(Vector2 position)
    {
        int attempts = 0;
        bool success = false;

        while (!success && attempts < MAX_ATTEMPTS)
        {
            try
            {
                ExecuteTeleport(position);
                SyncTeleportWithServer(position);
                success = true;
                lastTeleportTime = Time.time;
                lastTeleportId = (byte)((lastTeleportId + 1) % 255);
            }
            catch (Exception e)
            {
                attempts++;
                if (attempts >= MAX_ATTEMPTS)
                {
                    Debug.LogError($"[TeleportManager] Failed to execute teleport after {MAX_ATTEMPTS} attempts: {e}");
                    return;
                }
                System.Threading.Thread.Sleep(50); // Short delay between attempts
            }
        }
    }

    private bool ValidateGameState()
    {
        if (PlayerControl.LocalPlayer == null || AmongUsClient.Instance == null)
        {
            Debug.LogWarning("[TeleportManager] Invalid game state: Player or client is null");
            return false;
        }
        return AmongUsClient.Instance.AmConnected;
    }

    private bool ValidateTeleportCooldown() =>
        Time.time - lastTeleportTime >= MIN_TELEPORT_INTERVAL;

    private bool IsPlayerTeleportOnCooldown(byte playerId)
    {
        if (lastPlayerTeleports.TryGetValue(playerId, out DateTime lastTeleport))
        {
            return (DateTime.UtcNow - lastTeleport).TotalSeconds < MIN_TELEPORT_INTERVAL;
        }
        return false;
    }

    private bool ValidateTargetPlayer(PlayerControl target) =>
        target != null &&
        target != PlayerControl.LocalPlayer &&
        !target.Data.IsDead &&
        !target.Data.Disconnected;

    private bool ValidateTeleportDistance(Vector2 targetPosition) =>
        Vector2.Distance(PlayerControl.LocalPlayer.GetTruePosition(), targetPosition) <= MAX_TELEPORT_DISTANCE;

    private void ExecuteTeleport(Vector2 position)
    {
        if (PlayerControl.LocalPlayer.inVent)
        {
            PlayerControl.LocalPlayer.MyPhysics.ExitAllVents();
        }
        PlayerControl.LocalPlayer.NetTransform.SnapTo(position);
    }

    private void SyncTeleportWithServer(Vector2 position)
    {
        if (!AmongUsClient.Instance.AmClient) return;

        var token = GenerateSecureToken();
        SendTeleportRpc(PlayerControl.LocalPlayer.PlayerId, position, token);
        SendBackupTeleportSync(position, token);
    }

    private string GenerateSecureToken()
    {
        var tokenBytes = new byte[VERIFICATION_TOKEN_LENGTH];
        random.NextBytes(tokenBytes);
        return Convert.ToBase64String(tokenBytes);
    }

    private void SendTeleportRpc(byte playerId, Vector2 position, string token)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(
            PlayerControl.LocalPlayer.NetId,
            TELEPORT_RPC,
            SendOption.Reliable,
            -1
        );
        writer.Write(playerId);
        writer.Write(position.x);
        writer.Write(position.y);
        writer.Write(DateTime.UtcNow.Ticks);
        writer.Write(token);
        writer.Write(lastTeleportId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    private void SendBackupTeleportSync(Vector2 position, string token)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(
            PlayerControl.LocalPlayer.NetId,
            TELEPORT_SYNC,
            SendOption.Reliable,
            -1
        );
        writer.Write(PlayerControl.LocalPlayer.PlayerId);
        writer.Write(position.x);
        writer.Write(position.y);
        writer.Write(token);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    private void SendMassTeleportRpc(byte targetId, Vector2 position, string token)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(
            PlayerControl.LocalPlayer.NetId,
            TELEPORT_RPC,
            SendOption.Reliable,
            -1
        );
        writer.Write(targetId);
        writer.Write(position.x);
        writer.Write(position.y);
        writer.Write(DateTime.UtcNow.Ticks);
        writer.Write(token);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
}
