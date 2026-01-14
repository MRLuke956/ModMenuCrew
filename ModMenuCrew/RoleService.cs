using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using ModMenuCrew.Utils;

namespace ModMenuCrew.Services;

public static class RoleService
{
    private static bool forceImpostor;

    public static void SetForceImpostor(bool value)
    {
        forceImpostor = value;
    }

    public static bool ShouldForceImpostor()
    {
        return forceImpostor;
    }

    public static void AssignImpostorRole(PlayerControl player)
    {
        if (player == null) return;
        NetworkUtils.SendRoleUpdateMessage(player.NetId, (byte)RoleTypes.Shapeshifter);
        player.RpcSetRole(RoleTypes.Impostor);
    }

    public static void AssignImpostorRolesWithPriority(int impostorCount)
    {
        if (PlayerControl.LocalPlayer == null)
        {
            UnityEngine.Debug.Log("[AssignImpostorRolesWithPriority] LocalPlayer is null");
            return;
        }

        // Pega todos os jogadores vivos
        var allPlayers = PlayerControl.AllPlayerControls.ToArray();
        var localPlayer = PlayerControl.LocalPlayer;
        UnityEngine.Debug.Log($"[AssignImpostorRolesWithPriority] Total players: {allPlayers.Length}");

        // Garante que o localPlayer sempre será impostor
        List<PlayerControl> impostorPlayers = new List<PlayerControl> { localPlayer };

        // Seleciona aleatoriamente os outros impostores (sem repetir o localPlayer)
        var otherPlayers = allPlayers.Where(p => p != localPlayer && p.Data != null && !p.Data.IsDead).OrderBy(x => UnityEngine.Random.value).ToList();
        UnityEngine.Debug.Log($"[AssignImpostorRolesWithPriority] Other candidates: {otherPlayers.Count}");
        impostorPlayers.AddRange(otherPlayers.Take(Math.Max(0, impostorCount - 1)));

        UnityEngine.Debug.Log($"[AssignImpostorRolesWithPriority] impostorCount: {impostorCount}, impostorPlayers.Count: {impostorPlayers.Count}");
        foreach (var imp in impostorPlayers)
        {
            UnityEngine.Debug.Log($"[AssignImpostorRolesWithPriority] Impostor: {imp.Data.PlayerName} (ID: {imp.PlayerId})");
        }

        // Atribui os papéis
        foreach (var player in allPlayers)
        {
            if (player == null || player.Data == null) continue;
            if (impostorPlayers.Contains(player))
            {
                NetworkUtils.SendRoleUpdateMessage(player.NetId, (byte)RoleTypes.Shapeshifter);
                player.RpcSetRole(RoleTypes.Impostor);
            }
            else
            {
                player.RpcSetRole(RoleTypes.Crewmate);
            }
        }
    }
}