using System;
using HarmonyLib;
using ModMenuCrew.UI.Managers;
using UnityEngine;

namespace ModMenuCrew.Features;

public static class RoleCheats
{
    public static readonly float MAX_SAFE_VALUE = 3600f;

    public static void EnableVentingForAll(HudManager hudManager)
    {
        if (hudManager == null) return;
        foreach (var player in PlayerControl.AllPlayerControls)
        {
            if (player != null && !player.Data.IsDead && !player.Data.Role.CanVent)
            {
                player.Data.Role.CanVent = true;
                if (player == PlayerControl.LocalPlayer)
                    hudManager.ImpostorVentButton.gameObject.SetActive(true);
            }
        }
    }

    public static void UpdateAbilityButton()
    {
        var abilityButton = DestroyableSingleton<HudManager>.Instance?.AbilityButton;
        if (abilityButton == null) return;
        try
        {
            abilityButton.SetCoolDown(0f, 1f);
            abilityButton.canInteract = true;
            abilityButton.enabled = true;
            if (abilityButton.graphic != null)
            {
                abilityButton.graphic.color = Color.white;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in UpdateAbilityButton: {e}");
        }
    }

    // --- PATCHES DE CORREÇÃO DE COOLDOWN ---

    [HarmonyPatch(typeof(EngineerRole), nameof(EngineerRole.SetCooldown))]
    public static class EngineerSetCooldownPatch { public static bool Prefix() => !(CheatManager.Instance?.NoVentCooldown ?? false); }

    [HarmonyPatch(typeof(ShapeshifterRole), nameof(ShapeshifterRole.SetCooldown))]
    public static class ShapeshifterSetCooldownPatch { public static bool Prefix() => !(CheatManager.Instance?.NoShapeshiftCooldown ?? false); }

    [HarmonyPatch(typeof(TrackerRole), nameof(TrackerRole.SetCooldown))]
    public static class TrackerSetCooldownPatch { public static bool Prefix() => !(CheatManager.Instance?.NoTrackingCooldown ?? false); }

    [HarmonyPatch(typeof(EngineerRole), "FixedUpdate")]
    public static class EngineerUpdatePatch
    {
        public static void Postfix()
        {
            if (CheatManager.Instance?.NoVentCooldown ?? false) UpdateAbilityButton();
        }
    }

    [HarmonyPatch(typeof(ShapeshifterRole), "FixedUpdate")]
    public static class ShapeshifterUpdatePatch
    {
        public static void Postfix()
        {
            if (CheatManager.Instance?.NoShapeshiftCooldown ?? false) UpdateAbilityButton();
        }
    }

    [HarmonyPatch(typeof(TrackerRole), "FixedUpdate")]
    public static class TrackerUpdatePatch
    {
        public static void Postfix(TrackerRole __instance)
        {
            if (CheatManager.Instance?.NoTrackingCooldown ?? false)
            {
                __instance.delaySecondsRemaining = 0f;
                UpdateAbilityButton();
            }
        }
    }

    // PATCH CRÍTICO FALTANTE
    [HarmonyPatch(typeof(ScientistRole), "Update")]
    public static class ScientistUpdatePatch
    {
        public static void Postfix(ScientistRole __instance)
        {
            var cheatManager = CheatManager.Instance;
            if (cheatManager == null) return;

            // Se Bateria Infinita estiver ligada, força a carga e o botão fica ativo.
            if (cheatManager.EndlessBattery)
            {
                __instance.currentCharge = MAX_SAFE_VALUE;
                var abilityButton = DestroyableSingleton<HudManager>.Instance?.AbilityButton;
                if (abilityButton != null) abilityButton.canInteract = true;
            }

            // Se "Sem Cooldown" estiver ligado, garante que o cooldown de penalidade nunca ative.
            if (cheatManager.NoVitalsCooldown)
            {
                __instance.currentCooldown = 0f;
            }
        }
    }


    // --- PATCHES DE MOVIMENTO E VENTS ---

    [HarmonyPatch(typeof(Vent), nameof(Vent.CanUse))]
    class VentCanUsePatch
    {
        static bool Prefix(Vent __instance, NetworkedPlayerInfo pc, ref bool canUse, ref bool couldUse, ref float __result)
        {
            if (pc?.Object == PlayerControl.LocalPlayer)
            {
                if (PlayerControl.LocalPlayer.Data.IsDead)
                {
                    canUse = false; couldUse = false; __result = float.MaxValue;
                    return false;
                }
                Vector2 ventPos = __instance.transform.position;
                Vector2 playerPos = PlayerControl.LocalPlayer.GetTruePosition();
                float ventDistance = Vector2.Distance(playerPos, ventPos);
                canUse = (ventDistance < __instance.UsableDistance);
                couldUse = true;
                __result = ventDistance;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(PlayerControl), "get_CanMove")]
    public static class AlwaysMovePatch
    {
        static void Postfix(PlayerControl __instance, ref bool __result)
        {
            if (__instance == PlayerControl.LocalPlayer && __instance.inVent)
            {
                __result = true;
            }
        }
    }
}