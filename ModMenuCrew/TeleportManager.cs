using System.Collections.Generic;
using UnityEngine;

namespace ModMenuCrew.UI.Managers;

public class TeleportManager
{
    private readonly Dictionary<SystemTypes, Vector2> locations;

    public TeleportManager()
    {
        locations = new Dictionary<SystemTypes, Vector2>
        {
            { SystemTypes.Electrical, new Vector2(-8.6f, -9.3f) },
            { SystemTypes.Security, new Vector2(-13.3f, -5.9f) },
            { SystemTypes.MedBay, new Vector2(-10.5f, -3.5f) },
            { SystemTypes.Storage, new Vector2(-3.5f, -11.9f) },
            { SystemTypes.Cafeteria, new Vector2(0f, 0f) },
            { SystemTypes.Admin, new Vector2(3.5f, -7.5f) }
        };
    }

    public IReadOnlyDictionary<SystemTypes, Vector2> Locations => locations;

    public void TeleportToLocation(SystemTypes location)
    {
        if (PlayerControl.LocalPlayer != null && locations.TryGetValue(location, out Vector2 position))
        {
            PlayerControl.LocalPlayer.transform.position = position;
        }
    }

    public void TeleportToPlayer(PlayerControl target)
    {
        if (PlayerControl.LocalPlayer != null && target != null)
        {
            PlayerControl.LocalPlayer.transform.position = target.transform.position;
        }
    }

    public void TeleportAllToMe()
    {
        if (!AmongUsClient.Instance.AmHost || PlayerControl.LocalPlayer == null) return;

        var myPosition = PlayerControl.LocalPlayer.GetTruePosition();
        foreach (var player in PlayerControl.AllPlayerControls)
        {
            if (player != PlayerControl.LocalPlayer)
            {
                player.transform.position = myPosition;
            }
        }
    }

    public PlayerControl GetClosestPlayer()
    {
        if (PlayerControl.LocalPlayer == null) return null;

        PlayerControl closest = null;
        float closestDistance = float.MaxValue;
        var myPosition = PlayerControl.LocalPlayer.GetTruePosition();

        foreach (var player in PlayerControl.AllPlayerControls)
        {
            if (player != PlayerControl.LocalPlayer && !player.Data.IsDead)
            {
                float distance = Vector2.Distance(player.GetTruePosition(), myPosition);
                if (distance < closestDistance)
                {
                    closest = player;
                    closestDistance = distance;
                }
            }
        }

        return closest;
    }
}