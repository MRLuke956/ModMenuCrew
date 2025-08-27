using System;
using System.Collections.Generic;
using AmongUs.Data;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace ModMenuCrew.Features
{
    [HarmonyPatch]
    public static class ChatCommandsPatch
    {
        private static readonly Dictionary<byte, DateTime> lastPingTime = new Dictionary<byte, DateTime>();
        private static DateTime lastCommandTime = DateTime.MinValue;

        private const string PING_COMMAND = "/ping";
        private const float COMMAND_COOLDOWN_SECONDS = 5f;

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.StartGame))]
        [HarmonyPostfix]
        public static void Postfix_StartGame()
        {
            lastPingTime.Clear();
            lastCommandTime = DateTime.MinValue;
        }

        [HarmonyPatch(typeof(ChatController), nameof(ChatController.SendChat))]
        [HarmonyPrefix]
        public static bool Prefix_SendChat(ChatController __instance)
        {
            if (string.IsNullOrWhiteSpace(__instance.freeChatField.Text) || __instance.quickChatField.Visible)
            {
                return true;
            }

            string text = __instance.freeChatField.Text;
            string[] args = text.Split(' ');

            if (args[0].Equals(PING_COMMAND, StringComparison.OrdinalIgnoreCase))
            {
                __instance.freeChatField.Clear();
                __instance.timeSinceLastMessage = 3f;

                HandlePingCommand();

                return false;
            }

            return true;
        }

        private static void HandlePingCommand()
        {
            if (!AmongUsClient.Instance.AmConnected)
            {
                SendMessage("<color=#ff0000>Erro: Você não está conectado a um lobby.</color>");
                return;
            }

            if ((DateTime.UtcNow - lastCommandTime).TotalSeconds < COMMAND_COOLDOWN_SECONDS)
            {
                int secondsLeft = (int)Math.Ceiling(COMMAND_COOLDOWN_SECONDS - (DateTime.UtcNow - lastCommandTime).TotalSeconds);
                SendMessage($"<color=#ffff00>Aguarde {secondsLeft}s para usar o comando novamente.</color>");
                return;
            }
            lastCommandTime = DateTime.UtcNow;

            string pingMessage = BuildPingMessage();
            SendMessage(pingMessage);
        }

        private static string BuildPingMessage()
        {
            var messageBuilder = new System.Text.StringBuilder();
            messageBuilder.AppendLine("<b><color=#00ffff>Informações de Conexão:</color></b>");

            string region = GetRegionName();
            bool hasOtherPlayers = false;

            if (AmongUsClient.Instance.IsGameStarted && PlayerControl.AllPlayerControls.Count > 0)
            {
                hasOtherPlayers = PlayerControl.AllPlayerControls.Count > 1;

                Dictionary<byte, ClientData> clientsLookup = new Dictionary<byte, ClientData>();
                for (int i = 0; i < AmongUsClient.Instance.allClients.Count; i++)
                {
                    ClientData client = AmongUsClient.Instance.allClients[i];
                    if (client.Character != null)
                    {
                        clientsLookup[client.Character.PlayerId] = client;
                    }
                }

                List<PlayerControl> sortedPlayers = new List<PlayerControl>();
                for (int i = 0; i < PlayerControl.AllPlayerControls.Count; i++)
                {
                    sortedPlayers.Add(PlayerControl.AllPlayerControls[i]);
                }
                sortedPlayers.Sort((p1, p2) => p1.PlayerId.CompareTo(p2.PlayerId));

                for (int i = 0; i < sortedPlayers.Count; i++)
                {
                    var player = sortedPlayers[i];
                    if (player.Data == null) continue;
                    AppendPlayerInfo(messageBuilder, player.PlayerId, player.Data.PlayerName, player.AmOwner, clientsLookup, null);
                }
            }
            else if (LobbyBehaviour.Instance != null)
            {
                hasOtherPlayers = AmongUsClient.Instance.allClients.Count > 1;

                List<ClientData> sortedClients = new List<ClientData>();
                for (int i = 0; i < AmongUsClient.Instance.allClients.Count; i++)
                {
                    sortedClients.Add(AmongUsClient.Instance.allClients[i]);
                }
                sortedClients.Sort((c1, c2) => c1.Id.CompareTo(c2.Id));

                for (int i = 0; i < sortedClients.Count; i++)
                {
                    var client = sortedClients[i];
                    if (client.Character == null) continue;
                    AppendPlayerInfo(messageBuilder, client.Character.PlayerId, client.PlayerName, client.Id == AmongUsClient.Instance.ClientId, null, client);
                }
            }
            else
            {
                AppendPlayerInfo(messageBuilder, PlayerControl.LocalPlayer.PlayerId, DataManager.Player.Customization.Name, true, null, null);
                messageBuilder.AppendLine("<i>Você não está em um lobby.</i>");
            }

            messageBuilder.Append($"\nRegião: {region}");
            if (hasOtherPlayers)
            {
                messageBuilder.Append("\n<size=70%><i>*Ping de outros jogadores é uma simulação.</i></size>");
            }

            return messageBuilder.ToString();
        }

        private static void AppendPlayerInfo(System.Text.StringBuilder builder, byte playerId, string playerName, bool isLocal, Dictionary<byte, ClientData> clientsLookup, ClientData directClient)
        {
            string pingText;
            if (isLocal)
            {
                int realPing = AmongUsClient.Instance.Ping;
                string pingColor = realPing < 100 ? "00ff00" : (realPing < 200 ? "ffff00" : "ff0000");
                pingText = $"<color=#{pingColor}>{realPing}ms</color>";
            }
            else
            {
                int simulatedPing = GetSimulatedPing(playerId);
                pingText = simulatedPing == -1 ? "<color=#c0c0c0>N/A</color>" : $"<color=#c0c0c0>~{simulatedPing}ms*</color>";
            }

            string platform = "PC";
            ClientData clientData = directClient;
            if (clientData == null && clientsLookup != null)
            {
                clientsLookup.TryGetValue(playerId, out clientData);
            }

            if (clientData != null)
            {
                platform = GetPlatformName(clientData.PlatformData.Platform);
            }

            builder.AppendLine($"{playerName}: {pingText} | Plataforma: {platform}");
        }

        private static int GetSimulatedPing(byte playerId)
        {
            if (lastPingTime.TryGetValue(playerId, out var lastTime))
            {
                int ping = (int)(DateTime.UtcNow - lastTime).TotalMilliseconds;
                lastPingTime[playerId] = DateTime.UtcNow;
                return ping;
            }

            lastPingTime[playerId] = DateTime.UtcNow;
            return -1;
        }

        private static string GetRegionName()
        {
            if (DestroyableSingleton<ServerManager>.InstanceExists)
            {
                return DestroyableSingleton<ServerManager>.Instance.CurrentRegion.Name;
            }
            return "Desconhecida";
        }

        private static string GetPlatformName(Platforms platform)
        {
            switch (platform)
            {
                case Platforms.IPhone:
                case Platforms.Android:
                    return "Mobile";
                case Platforms.Switch:
                    return "Switch";
                case Platforms.Xbox:
                    return "Xbox";
                case Platforms.Playstation:
                    return "PlayStation";
                case Platforms.StandaloneSteamPC:
                    return "PC (Steam)";
                case Platforms.StandaloneEpicPC:
                    return "PC (Epic)";
                case Platforms.StandaloneItch:
                    return "PC (Itch)";
                case Platforms.StandaloneWin10:
                    return "PC (MS Store)";
                default:
                    return "PC";
            }
        }

        private static void SendMessage(string message)
        {
            if (HudManager.Instance != null && HudManager.Instance.Chat != null)
            {
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, message, false);
            }
        }
    }
}