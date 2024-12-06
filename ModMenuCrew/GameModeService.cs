using AmongUs.GameOptions;
using UnityEngine;

namespace ModMenuCrew;

public static class GameModeService
{
    public static void ToggleInfiniteVision(bool enabled)
    {
        if (PlayerControl.LocalPlayer == null) return;

    }

    public static void ToggleNoKillCooldown(bool enabled)
    {
        if (PlayerControl.LocalPlayer == null || !PlayerControl.LocalPlayer.Data.Role.IsImpostor) return;
        PlayerControl.LocalPlayer.SetKillTimer(enabled ? 0f : GameOptionsManager.Instance.CurrentGameOptions.GetFloat(FloatOptionNames.KillCooldown));
    }

    public static void CompleteAllTasks()
    {
        if (PlayerControl.LocalPlayer == null) return;
        foreach (var task in PlayerControl.LocalPlayer.myTasks)
        {
            if (!task.IsComplete)
            {
                PlayerControl.LocalPlayer.RpcCompleteTask(task.Id);
            }
        }
    }

    public static void RevealAllRoles()
    {
        foreach (var player in PlayerControl.AllPlayerControls)
        {
            if (player.Data.Role.IsImpostor)
            {
                player.name.Color(Color.red);
            }
        }
    }
}
