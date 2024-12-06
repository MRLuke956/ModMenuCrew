using UnityEngine;
using Hazel;
using AmongUs.GameOptions;
using System.Linq;
using HarmonyLib;
using ModMenuCrew.Services;

namespace ModMenuCrew.UI.Managers;

public class ImpostorManager
{
    public void KillPlayer(PlayerControl target)
    {
        if (!PlayerControl.LocalPlayer?.Data.Role.IsImpostor ?? true) return;
        if (target == null || target.Data.IsDead) return;

        PlayerControl.LocalPlayer.RpcMurderPlayer(target, true);
    }

    public void SetKillCooldown(float cooldown)
    {
        if (!PlayerControl.LocalPlayer?.Data.Role.IsImpostor ?? true) return;
        PlayerControl.LocalPlayer.SetKillTimer(cooldown);
    }

        public void ForceImpostorRole()
    {
       
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(
           PlayerControl.LocalPlayer.NetId,
           (byte)RpcCalls.SetRole,
           SendOption.Reliable
            );
            writer.Write((byte)RoleTypes.Impostor);
            AmongUsClient.Instance.FinishRpcImmediately(writer);

            PlayerControl.LocalPlayer.RpcSetRole(RoleTypes.Impostor);
        
       
    }

    public void VentAt(Vector2 position)
    {
        if (!PlayerControl.LocalPlayer?.Data.Role.IsImpostor ?? true) return;

        var vent = Object.FindObjectsOfType<Vent>()
            .OrderBy(v => Vector2.Distance(v.transform.position, position))
            .FirstOrDefault();

        if (vent != null)
        {
           
        }
    }

    public void SabotageSystem(SystemTypes systemType)
    {
        if (!PlayerControl.LocalPlayer?.Data.Role.IsImpostor ?? true) return;
        if (!ShipStatus.Instance) return;

        ShipStatus.Instance.RpcUpdateSystem(systemType, 128);
    }
}