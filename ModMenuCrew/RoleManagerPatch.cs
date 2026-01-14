using HarmonyLib;
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
        // Volta à lógica original, não interfere na seleção padrão do jogo.
        // Se desejar, pode remover todo o conteúdo deste método ou deixar vazio.
    }
}