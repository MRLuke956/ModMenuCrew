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

        NetworkUtils.SendRoleUpdateMessage(player.NetId, (byte)RoleTypes.Impostor);
        player.RpcSetRole(RoleTypes.Impostor);
    }
}