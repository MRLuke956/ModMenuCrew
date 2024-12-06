using HarmonyLib;
using LibCpp2IL.Elf;
using ModMenuCrew.Services;

namespace ModMenuCrew.Patches;

[HarmonyPatch(typeof(RoleManager), nameof(RoleManager.SelectRoles))]
public static class RoleManagerPatch
{
    public static void SetForceImpostor(bool value)
    {
        RoleService.SetForceImpostor(value);
    }

    public static void Prefix(RoleManager __instance)
    {
        if (!RoleService.ShouldForceImpostor() || !PlayerControl.LocalPlayer) return;
        RoleService.AssignImpostorRole(PlayerControl.LocalPlayer);
   
    }

}