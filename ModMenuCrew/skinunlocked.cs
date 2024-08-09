using HarmonyLib;
using Hazel;
using InnerNet;
using JetBrains.Annotations;
using TMPro;
using UnityEngine;

namespace ModMenuCrew
{
    internal class SkinUnlockPatch
    {
        [HarmonyPatch(typeof(HatManager), nameof(HatManager.Initialize))]
        internal class UnlockCosmetics
        {
            public static void Postfix(HatManager __instance)
            {
                foreach (var bundle in __instance.allBundles)
                { bundle.Free = true; }

                foreach (var featuredBundle in __instance.allFeaturedBundles)
                { featuredBundle.Free = true; }

                foreach (var featuredCube in __instance.allFeaturedCubes)
                { featuredCube.Free = true; }

                foreach (var featuredItem in __instance.allFeaturedItems)
                { featuredItem.Free = true; }

                foreach (var hat in __instance.allHats)
                { hat.Free = true; }

                foreach (var nameplate in __instance.allNamePlates)
                { nameplate.Free = true; }

                foreach (var pet in __instance.allPets)
                { pet.Free = true; }

                foreach (var skin in __instance.allSkins)
                { skin.Free = true; }

                foreach (var starBundle in __instance.allStarBundles)
                { starBundle.price = 0; }

                foreach (var visor in __instance.allVisors)
                { visor.Free = true; }
            }
        }

    }
    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.CanBan))]
    internal class InnerNetClient_CanBan_Patch
    {

        [HarmonyPrefix]
        public static bool Prefix(InnerNetClient __instance)
        {
            // Retorna true sempre, permitindo o ban para qualquer jogador
            return true;

        }


    }
    [HarmonyPatch(typeof(ChatController), nameof(ChatController.Update))]
    internal class test
    {

        public static void Postfix(ChatController __instance)
        {
            __instance.banButton.enabled = true;
            __instance.banButton.gameObject.SetActive(true);
            __instance.banButton.BanButton.gameObject.SetActive(true);
        }
    }

    [HarmonyPatch(typeof(EOSManager), nameof(EOSManager.Update))]
    internal class RainbowFriendCodePatch
    {

        public static void Postfix(EOSManager __instance)
        {

            string friendCodeText = $"<color=#f52626>Amy#Admin</color>";

            __instance.friendCode = friendCodeText;
            __instance.FriendCode = friendCodeText;
            __instance.friendsListKey = friendCodeText.ToLower();


            // PlayerControl.LocalPlayer.name = "ADM\n\nAMY";
            // PlayerControl.LocalPlayer.CmdCheckName("ADM\n\nAMY");
        }

    }

   
}