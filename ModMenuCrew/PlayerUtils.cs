using UnityEngine;
using System.Linq;
using System.Collections.Generic;

namespace ModMenuCrew.Utils;

public static class PlayerUtils
{
    public static float GetDistanceBetweenPlayers(PlayerControl source, PlayerControl target)
    {
        if (source == null || target == null) return float.MaxValue;
        return Vector2.Distance(source.GetTruePosition(), target.GetTruePosition());
    }

    public static PlayerControl GetClosestPlayer(PlayerControl source = null)
    {
        source ??= PlayerControl.LocalPlayer;
        return PlayerControl.AllPlayerControls
            .ToArray()
            .Where(p => p != source && !p.Data.IsDead)
            .OrderBy(p => GetDistanceBetweenPlayers(source, p))
            .FirstOrDefault();
    }

    public static bool IsInVent(PlayerControl player)
    {
        return player != null && player.inVent;
    }

    public static void TeleportTo(PlayerControl player, Vector2 position)
    {
        if (player == null) return;
        player.NetTransform.SnapTo(position);
    }

    public static void TeleportToPlayer(PlayerControl source, PlayerControl target)
    {
        if (source == null || target == null) return;
        TeleportTo(source, target.GetTruePosition());
    }
}