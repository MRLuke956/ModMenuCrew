using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AmongUs.Data;
using AmongUs.GameOptions;
using BepInEx.Unity.IL2CPP.Utils;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using Epic.OnlineServices.KWS;
using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;

namespace ModMenuCrew.Features
{
    public static class GameCheats
    {
        public const byte RPC_SET_SCANNER = 15;
        public const byte RPC_SET_INVISIBILITY = 51;
        private static readonly System.Random random = new System.Random();
        public const byte CUSTOM_RPC_PHANTOM_POOF = 112;
        public static bool IsRainbowNameActive { get; set; }
        public const byte CHECK_COLOR_RPC = 7; // Corrigido para o valor real do CheckColor
        public static readonly byte[] HOST_COLOR_IDS = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18 };
        public static bool ForceColorForEveryone = false;

        // Flag para controle do zoom
        private static bool zoomOutEnabled = false;
        private static float customZoomValue = 10.0f; // Valor padrão de zoom out

        // Flag para controle do teleporte para cursor
        public static bool TeleportToCursorEnabled = false;

        // Log para monitoramento
        private static void LogCheat(string message) => Debug.Log($"[Cheat] {message}");

        // Gera um token falso para bypass de detecção
        private static string GenerateFakeToken(byte playerId = 0)
        {
            long timestamp = DateTime.UtcNow.Ticks;
            string gameId = AmongUsClient.Instance?.GameId.ToString() ?? "0";
            string rawData = $"{playerId}-{timestamp}-{gameId}";
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(rawData));
                return Convert.ToBase64String(hashBytes);
            }
        }


        // Gera o token de verificação para o scanner
        private static string GenerateVerificationToken(byte playerId, byte scanCount, long timestamp)
        {
            return $"{playerId}-{scanCount}-{timestamp}-MODMENUCREW";
        }

        // Valida o token recebido no RPC do scanner
        private static bool ValidateToken(string token, byte playerId, byte scanCount, long timestamp)
        {
            string expectedToken = GenerateVerificationToken(playerId, scanCount, timestamp);
            return token == expectedToken && Math.Abs(DateTime.UtcNow.Ticks - timestamp) < 50000000L;
        }
        #region Cheats de Reunião
        /// <summary>
        /// Fecha a reunião atual ou tela de exílio, restaurando a jogabilidade.
        /// </summary>
        public static void CloseMeeting()
        {
            try
            {
                if (MeetingHud.Instance)
                {
                    MeetingHud.Instance.DespawnOnDestroy = false;
                    UnityEngine.Object.Destroy(MeetingHud.Instance.gameObject);
                    DestroyableSingleton<HudManager>.Instance.StartCoroutine(
                        DestroyableSingleton<HudManager>.Instance.CoFadeFullScreen(Color.black, Color.clear, 0.2f, false));
                    PlayerControl.LocalPlayer.SetKillTimer(GameManager.Instance.LogicOptions.GetKillCooldown());
                    ShipStatus.Instance.EmergencyCooldown = GameManager.Instance.LogicOptions.GetEmergencyCooldown();
                    Camera.main.GetComponent<FollowerCamera>().Locked = false;
                    DestroyableSingleton<HudManager>.Instance.SetHudActive(true);
                    LogCheat("Reunião fechada com sucesso.");
                }
                else if (ExileController.Instance != null)
                {
                    ExileController.Instance.ReEnableGameplay();
                    ExileController.Instance.WrapUp();
                    LogCheat("Exílio encerrado.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error in CloseMeeting: {e}");
            }
        }
        #endregion








        #region Cheats de Tarefas
        /// <summary>
        /// Completa todas as tarefas do jogador local com bypass de RPC.
        /// </summary>
        public static void CompleteAllTasks()
        {
            if (PlayerControl.LocalPlayer == null || PlayerControl.LocalPlayer.Data == null)
            {
                Debug.LogWarning("Jogador local não encontrado.");
                return;
            }
            if (AmongUsClient.Instance == null)
            {
                Debug.LogWarning("AmongUsClient não inicializado.");
                return;
            }
            try
            {
                var taskList = PlayerControl.LocalPlayer.Data.Tasks;
                if (taskList == null || taskList.Count == 0)
                {
                    Debug.LogWarning("Nenhuma tarefa encontrada para completar.");
                    return;
                }

                // Executa com pequenas pausas entre cada tarefa
                HudManager.Instance.StartCoroutine(CompleteAllTasksWithDelay(0.2f));
            }
            catch (Exception e)
            {
                Debug.LogError($"Erro ao completar tarefas: {e}");
            }
        }

        /// <summary>
        /// Completa todas as tarefas com um pequeno delay entre cada conclusão.
        /// </summary>
        public static IEnumerator CompleteAllTasksWithDelay(float perTaskDelay = 0.2f)
        {
            if (PlayerControl.LocalPlayer == null || PlayerControl.LocalPlayer.Data == null || AmongUsClient.Instance == null)
                yield break;

            bool isHost = AmongUsClient.Instance.AmHost;
            var taskList = PlayerControl.LocalPlayer.Data.Tasks;
            if (taskList == null || taskList.Count == 0)
                yield break;

            // Snapshot dos IDs para evitar modificar a coleção durante iteração
            var taskInfosSnapshot = taskList.ToArray();
            var idsToComplete = new List<int>();
            foreach (var ti in taskInfosSnapshot)
                if (ti != null && !ti.Complete)
                    idsToComplete.Add((int)ti.Id);

            // Completa cada TaskInfo por ID com intervalos, consultando a lista atual a cada passo
            foreach (var id in idsToComplete)
            {
                var currentList = PlayerControl.LocalPlayer.Data.Tasks;
                if (currentList == null) break;

                object match = null;
                for (int i = 0, c = currentList.Count; i < c; i++)
                {
                    var cur = currentList[i];
                    if (cur != null && (int)cur.Id == id) { match = cur; break; }
                }
                if (match == null) continue;

                // Envia RPC e marca localmente
                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(
                    PlayerControl.LocalPlayer.NetId, (byte)RpcCalls.CompleteTask, SendOption.Reliable, -1);
                writer.WritePacked(id);
                AmongUsClient.Instance.FinishRpcImmediately(writer);

                // Marca Complete se ainda existir
                try { ((dynamic)match).Complete = true; } catch { }
                LogCheat($"Tarefa {id} completada. (Host: {isHost})");

                yield return new WaitForSeconds(Mathf.Max(0.05f, perTaskDelay));
            }

            // Garante tasks multi-step com intervalos curtos
            var myTasksSnapshot = PlayerControl.LocalPlayer.myTasks.ToArray();
            foreach (var task in myTasksSnapshot)
            {
                if (task == null || task.IsComplete) continue;

                if (task is NormalPlayerTask normalTask)
                {
                    while (normalTask.TaskStep < normalTask.MaxStep)
                    {
                        normalTask.NextStep();
                        yield return new WaitForSeconds(0.05f);
                    }
                }
                task.Complete();
                yield return new WaitForSeconds(0.05f);
            }

            PlayerControl.LocalPlayer.Data.MarkDirty();
            LogCheat(isHost
                ? "Todas as tarefas completadas (com delay) e sincronizadas."
                : "Todas as tarefas completadas localmente (com delay). Sincronização pode variar.");
        }
        #endregion

        #region Cheats de Impostor
        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
        public static class ImpostorForcer1Patch
        {
            public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] Hazel.MessageReader reader)
            {
                if (callId == (byte)RpcCalls.SetRole && AmongUsClient.Instance != null && !AmongUsClient.Instance.AmHost)
                {
                    byte playerId = reader.ReadByte();
                    byte roleId = reader.ReadByte();
                    if (roleId == (byte)RoleTypes.Impostor)
                    {
                        var players = PlayerControl.AllPlayerControls.ToArray().ToList();
                        PlayerControl targetPlayer = players.FirstOrDefault(p => p.PlayerId == playerId);
                        if (targetPlayer != null)
                        {
                            targetPlayer.RpcSetRole(RoleTypes.Impostor);
                            LogCheat($"Jogador {playerId} forçado a ser impostor.");
                        }
                    }
                }
            }
        }

        public static class Impostor1Forcer
        {
            /// <summary>
            /// Solicita o papel de impostor com bypass de RPC.
            /// </summary>
            public static void RequestImpostorRole()
            {
                if (PlayerControl.LocalPlayer == null)
                {
                    Debug.LogWarning("Jogador local não encontrado.");
                    return;
                }
                if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
                {
                    Debug.LogWarning("Você precisa ser o host para usar este cheat.");
                    return;
                }
                try
                {
                    MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(
                        PlayerControl.LocalPlayer.NetId, (byte)RpcCalls.SetRole, SendOption.Reliable, -1);
                    writer.Write(PlayerControl.LocalPlayer.PlayerId);
                    writer.Write((byte)RoleTypes.Impostor);
                    writer.Write(GenerateFakeToken(PlayerControl.LocalPlayer.PlayerId));
                    AmongUsClient.Instance.FinishRpcImmediately(writer);
                    LogCheat("Papel de impostor solicitado com bypass.");
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error in RequestImpostorRole: {e}");
                }
            }
        }
        #endregion

        #region Cheats de Movimento
        /// <summary>
        /// Teletransporta o jogador para a posição do cursor com exploit Hazel.
        /// </summary>
        public static void TeleportToCursor()
        {
            if (PlayerControl.LocalPlayer == null || PlayerControl.LocalPlayer.NetTransform == null || Camera.main == null)
            {
                Debug.LogWarning("Jogador local ou câmera não encontrada.");
                return;
            }
            try
            {
                Vector2 mousePos = Input.mousePosition;
                Vector2 worldPos = Camera.main.ScreenToWorldPoint(mousePos);
                if (!Physics2D.OverlapPoint(worldPos, Constants.ShipAndAllObjectsMask))
                {
                    PlayerControl.LocalPlayer.NetTransform.SnapTo(worldPos);
                    LogCheat($"Teleportado localmente para {worldPos}.");

                    MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(
                        PlayerControl.LocalPlayer.NetId, (byte)RpcCalls.SnapTo, SendOption.Reliable, -1);
                    writer.Write(worldPos.x);
                    writer.Write(worldPos.y);
                    writer.Write(GenerateFakeToken(PlayerControl.LocalPlayer.PlayerId));
                    AmongUsClient.Instance.FinishRpcImmediately(writer);
                    LogCheat($"Tentativa de sincronizar teleporte para {worldPos}.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error in TeleportToCursor: {e}");
            }
        }


        /// <summary>
        /// Verifica o input do mouse para teleporte quando habilitado.
        /// Deve ser chamado no Update() independente do estado do menu.
        /// </summary>
        public static void CheckTeleportInput()
        {
            if (TeleportToCursorEnabled && Input.GetMouseButtonDown(1))
            {
                TeleportToCursor();
            }
        }


        public static void KickAllFromVents()
        {
            if (ShipStatus.Instance == null)
            {
                Debug.LogWarning("ShipStatus não inicializado.");
                return;
            }
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
            {
                Debug.LogWarning("Você precisa ser o host para usar este cheat.");
                return;
            }
            try
            {
                foreach (var vent in ShipStatus.Instance.AllVents)
                {
                    VentilationSystem.Update(VentilationSystem.Operation.BootImpostors, vent.Id);
                }
                LogCheat("Todos expulsos dos ventiladores.");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error in KickAllFromVents: {e}");
            }
        }
        #endregion

        #region Cheats de Scanner
        public static void BypassScanner(bool value)
        {
            try
            {
                if (PlayerControl.LocalPlayer == null || AmongUsClient.Instance == null)
                    return;

                // Sempre incrementa o scannerCount para cada chamada
                byte scannerCount = (byte)(PlayerControl.LocalPlayer.scannerCount + 1);
                PlayerControl.LocalPlayer.scannerCount = scannerCount;
                PlayerControl.LocalPlayer.SetScanner(value, scannerCount);

                foreach (var p in PlayerControl.AllPlayerControls)
                {
                    var writer = AmongUsClient.Instance.StartRpcImmediately(
                        PlayerControl.LocalPlayer.NetId, 15, SendOption.Reliable, p.OwnerId);

                    writer.Write(value);
                    writer.Write(scannerCount);

                    AmongUsClient.Instance.FinishRpcImmediately(writer);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("BypassScanner error: " + e.Message);
            }
        }

        public static void HandleScannerRPC(PlayerControl instance, byte callId, MessageReader reader)
        {
            if (callId != 15) return;

            try
            {
                bool scanValue = reader.ReadBoolean();
                byte scanCount = reader.ReadByte();

                instance.SetScanner(scanValue, scanCount);
            }
            catch (Exception e)
            {
                Debug.LogError("HandleScannerRPC error: " + e.Message);
            }
        }

        public static IEnumerator BypassScannerWithTimeout(float duration)
        {
            BypassScanner(true); // Ativa com novo scannerCount
            yield return new WaitForSeconds(duration);
            BypassScanner(false); // Desativa com scannerCount + 1 (ainda válido)
        }

        #endregion
        #region Scanner – visual local-only (sem RPC)

        /* Chame este método a partir do botão no Cheat-Manager  */
        public static void LocalVisualScanForEveryone(float duration = 2f)
        {
            if (PlayerControl.LocalPlayer == null) return;

            // liga animação localmente para todos
            foreach (var p in PlayerControl.AllPlayerControls)
                LocalToggleScanner(p, true);

            // desliga após o delay
            HudManager.Instance.StartCoroutine(LocalDisableScansAfterDelay(duration).WrapToIl2Cpp());
        }

        /* ------------------------------------------------------------------ */
        /* Helpers privados                                                   */
        /* ------------------------------------------------------------------ */

        private static void LocalToggleScanner(PlayerControl player, bool on)
        {
            try
            {
                byte newCnt = (byte)(player.scannerCount + 1); // mantém contador coerente
                player.scannerCount = newCnt;
                player.SetScanner(on, newCnt);                 // só afeta este cliente
            }
            catch (Exception e)
            {
                Debug.LogError($"LocalToggleScanner error ({player?.name}): {e}");
            }
        }

        private static IEnumerator LocalDisableScansAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);

            foreach (var p in PlayerControl.AllPlayerControls)
                LocalToggleScanner(p, false);

          
        }

        #endregion





        #region Cheats de Matança
        /// <summary>
        /// Mata todos os jogadores com bypass de RPC e atrasos aleatórios.
        /// </summary>
        public static void KillAll(bool crewOnly = false, bool impostorsOnly = false)
        {
            if (ShipStatus.Instance == null || PlayerControl.LocalPlayer == null)
            {
                Debug.LogWarning("ShipStatus ou jogador local não encontrado.");
                return;
            }
            if (AmongUsClient.Instance == null)
            {
                Debug.LogWarning("AmongUsClient não encontrado.");
                return;
            }
            try
            {
                bool isFreePlay = !AmongUsClient.Instance.IsGameStarted;
                List<PlayerControl> targets = PlayerControl.AllPlayerControls.ToArray()
                    .Where(target => target != PlayerControl.LocalPlayer && IsValidKillTarget(target, crewOnly, impostorsOnly))
                    .ToList();
                float baseDelay = 0f;
                foreach (var target in targets)
                {
                    if (isFreePlay)
                    {
                        ExecuteKillBypass(PlayerControl.LocalPlayer, target, baseDelay);
                    }
                    else
                    {
                        BroadcastKillBypass(PlayerControl.LocalPlayer, target, baseDelay);
                    }
                    baseDelay += random.Next(100, 300) / 1000f;
                }
                LogCheat("Matança massiva concluída com bypass superior.");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error in KillAll: {e}");
            }
        }

        private static bool IsValidKillTarget(PlayerControl target, bool crewOnly, bool impostorsOnly)
        {
            if (target == null || target.Data?.Role == null || target.Data.IsDead) return false;
            if (crewOnly) return target.Data.Role.TeamType == RoleTeamTypes.Crewmate;
            if (impostorsOnly) return target.Data.Role.TeamType == RoleTeamTypes.Impostor;
            return true;
        }

        // Kill local e sincroniza estado morto (bypass para lobbies e singleplayer)
        private static void ExecuteKillBypass(PlayerControl killer, PlayerControl target, float delay)
        {
            if (target == null || killer == null)
            {
                Debug.LogWarning("Target ou killer não encontrado.");
                return;
            }
            if (delay > 0)
            {
                HudManager.Instance.StartCoroutine(DelayedKillBypass(killer, target, delay));
                return;
            }
            killer.MurderPlayer(target, MurderResultFlags.Succeeded);
            if (target.Data != null)
                target.Data.IsDead = true;
            LogCheat($"[Bypass] {target.PlayerId} morto localmente.");
        }

        // Kill via RPC para todos (bypass superior)
        private static void BroadcastKillBypass(PlayerControl killer, PlayerControl target, float delay)
        {
            if (AmongUsClient.Instance == null || killer == null || target == null)
            {
                Debug.LogWarning("AmongUsClient/killer/target não encontrado.");
                return;
            }
            if (delay > 0)
            {
                HudManager.Instance.StartCoroutine(DelayedKillBypass(killer, target, delay, true));
                return;
            }
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == PlayerControl.LocalPlayer)
                    continue;

                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(
                    killer.NetId,
                    (byte)RpcCalls.MurderPlayer,
                    SendOption.Reliable,
                    player.OwnerId
                );
                writer.WriteNetObject(target);
                writer.Write((int)MurderResultFlags.Succeeded);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
            }
            if (target.Data != null)
                target.Data.IsDead = true;
            LogCheat($"[Bypass] Morte de {target.PlayerId} transmitida para todos.");
        }

        private static IEnumerator DelayedKillBypass(PlayerControl killer, PlayerControl target, float delay, bool broadcast = false)
        {
            yield return new WaitForSeconds(delay);
            if (killer == null || target == null || AmongUsClient.Instance == null)
            {
                Debug.LogWarning("killer/target/AmongUsClient não encontrado.");
                yield break;
            }
            if (broadcast)
                BroadcastKillBypass(killer, target, 0f);
            else
                ExecuteKillBypass(killer, target, 0f);

            LogCheat($"[Bypass] Morte de {target.PlayerId} transmitida após delay de {delay}s.");
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
        public static class MurderBypassPatch
        {
            public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] Hazel.MessageReader reader)
            {
                if (callId != (byte)RpcCalls.MurderPlayer)
                    return true; // Continue com o método original para outros RPCs

                try
                {
                    // Salva a posição atual
                    int oldPosition = reader.Position;

                    // Tenta ler com segurança
                    PlayerControl target = null;
                    try
                    {
                        // Tenta ler o NetId do alvo
                        uint netId = reader.ReadPackedUInt32();

                        // Percorre todos os jogadores para encontrar o com o mesmo NetId
                        foreach (var player in PlayerControl.AllPlayerControls)
                        {
                            if (player != null && player.NetId == netId)
                            {
                                target = player;
                                break;
                            }
                        }
                    }
                    catch
                    {
                        // Redefine a posição se falhar
                        reader.Position = oldPosition;
                        Debug.LogWarning("[KillBypassPatch] Falha ao ler o NetId do alvo");
                        return true; // Continue normalmente
                    }

                    // Se conseguir ler o alvo
                    if (target != null && target.Data != null && !target.Data.IsDead)
                    {
                        // Marca como morto localmente
                        target.Data.IsDead = true;

                        // Adiciona efeitos visuais para melhor experiência
                        if (target.cosmetics != null)
                        {


                            // Ativa o modo fantasma
                            target.gameObject.layer = LayerMask.NameToLayer("Ghost");
                        }

                        // Se for o jogador local que morreu, mostrar animação de morte
                        if (target.AmOwner && DestroyableSingleton<HudManager>.Instance != null)
                        {
                            // Tentar fechar tarefas se estiver fazendo alguma
                            if (Minigame.Instance)
                            {
                                try
                                {
                                    Minigame.Instance.Close();
                                }
                                catch
                                {
                                    // Ignora erro se não conseguir fechar
                                }
                            }

                            // Tenta mostrar tela de morte se possível
                            try
                            {
                                // Tenta encontrar o assassino pelo RPC
                                PlayerControl killer = __instance;
                                DestroyableSingleton<HudManager>.Instance.KillOverlay.ShowKillAnimation(killer.Data, target.Data);
                            }
                            catch
                            {
                                // Se não conseguir mostrar a animação, pelo menos mostra que está morto
                                target.cosmetics.SetNameMask(false);
                            }
                        }

                        Debug.Log($"[KillBypassPatch] Bypass: {target.Data.PlayerName} morto via patch com efeitos visuais.");
                    }

                    // Redefine a posição para o jogo processar normalmente
                    reader.Position = oldPosition;
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[KillBypassPatch] Exceção: {ex}");
                    return true; // Em caso de erro, continue com o método original
                }
            }
        }
       
        #endregion

        #region Cheats Visuais
        /// <summary>
        /// Revela impostores marcando seus nomes em vermelho.
        /// </summary>
        public static void RevealImpostors()
        {
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player.Data?.Role != null)
                {
                    Color roleColor = player.Data.Role.IsImpostor ? Color.red : Color.white;
                    string roleName = player.Data.Role.Role.ToString();

                    string roleHex = ColorUtility.ToHtmlStringRGB(roleColor);
                    string roleLine = $"<size=65%><color=#{roleHex}><i>{roleName}</i></color></size>";
                    string display = roleLine + "\n" + player.Data.PlayerName;

                    player.cosmetics.nameText.color = Color.white;
                    player.cosmetics.nameText.text = display;
                }
            }
        }

        /// <summary>
        /// Altera o nome de um jogador e sincroniza para que TODOS os jogadores vejam.
        /// </summary>
        /// <param name="player">O jogador cujo nome será alterado.</param>
        /// <param name="newName">O novo nome a ser definido.</param>
        public static void ChangePlayerName(PlayerControl player, string newName)
        {
            if (player == null)
            {
                Debug.LogWarning("Jogador não encontrado.");
                return;
            }
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
            {
                Debug.LogWarning("Você precisa ser o host para usar este cheat.");
                return;
            }
            try
            {
                player.Data.PlayerName = newName;
                player.Data.MarkDirty();
                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(
                    player.NetId, (byte)RpcCalls.SetName, SendOption.Reliable, -1);
                writer.Write(newName);
                writer.Write(GenerateFakeToken(player.PlayerId));
                AmongUsClient.Instance.FinishRpcImmediately(writer);
                LogCheat($"Nome de {player.PlayerId} alterado para {newName} e sincronizado para todos.");
            }
            catch (Exception e)
            {
                Debug.LogError($"Erro ao mudar o nome: {e}");
            }
        }

        public static void RainbowName()
        {
            if (PlayerControl.LocalPlayer == null || PlayerControl.LocalPlayer.cosmetics.nameText == null)
            {
                Debug.LogWarning("Jogador local ou nome não encontrado.");
                return;
            }
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
            {
                Debug.LogWarning("Você precisa ser o host para usar este cheat.");
                return;
            }
            IsRainbowNameActive = true;
            HudManager.Instance.StartCoroutine(HackerNameCoroutine());
            LogCheat("Efeito de nome arco-íris ativado.");
        }

        private static readonly string[] hackerChars = { "⌭", "◊", "⚡", "↯", "⊗", "⊝", "†", "∆", "▲", "░", "▓", "█" };
        private static readonly string[] glitchChars = { "ᗩ", "ᗷ", "ᑕ", "ᗪ", "ᗴ", "ᖴ", "Ǥ", "ᕼ", "Ī", "ᒎ", "ᛕ", "ᒪ", "ᗰ", "ᑎ", "ᗝ", "ᑭ", "ᑫ", "ᖇ", "ᔕ", "T", "ᑌ", "ᐯ", "ᗯ", "᙭", "Y", "ᘔ" };

        private static IEnumerator HackerNameCoroutine()
        {
            string originalName = PlayerControl.LocalPlayer.Data?.PlayerName ?? "HACKER";
            float h = 0;
            System.Random rnd = new System.Random();

            while (IsRainbowNameActive)
            {
                string glitchName = "";
                for (int i = 0; i < originalName.Length; i++)
                {
                    if (rnd.Next(100) < 20)
                        glitchName += hackerChars[rnd.Next(hackerChars.Length)];
                    else if (rnd.Next(100) < 30)
                    {
                        char c = originalName[i];
                        if (char.IsLetter(c))
                        {
                            int index = char.ToUpper(c) - 'A';
                            if (index >= 0 && index < glitchChars.Length)
                                glitchName += glitchChars[index];
                            else
                                glitchName += c;
                        }
                        else
                            glitchName += c;
                    }
                    else
                        glitchName += originalName[i];
                }

                if (rnd.Next(100) < 40)
                    glitchName = hackerChars[rnd.Next(hackerChars.Length)] + glitchName + hackerChars[rnd.Next(hackerChars.Length)];

                PlayerControl.LocalPlayer.cosmetics.nameText.color = Color.HSVToRGB(h, 1, 1);
                PlayerControl.LocalPlayer.cosmetics.nameText.text = glitchName;

                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(
                    PlayerControl.LocalPlayer.NetId, (byte)RpcCalls.SetName, SendOption.Reliable, -1);
                writer.Write(glitchName);
                writer.Write(GenerateFakeToken(PlayerControl.LocalPlayer.PlayerId));
                AmongUsClient.Instance.FinishRpcImmediately(writer);

                h = (h + (0.001f + (float)rnd.NextDouble() * 0.01f)) % 1;
                yield return new WaitForSeconds(0.05f + (float)rnd.NextDouble() * 0.1f);
            }
        }
        #endregion

        #region Novos Cheats
        /// <summary>
        /// Alterna a invisibilidade do jogador com exploit Hazel.
        /// </summary>
        private static Coroutine invisibilityCoroutine;

        public static void ToggleInvisibility(bool enable)
        {
            if (PlayerControl.LocalPlayer == null || AmongUsClient.Instance == null)
            {
                Debug.LogWarning("Invisibilidade não ativada: jogador local não encontrado.");
                return;
            }
            try
            {
                if (enable)
                {
                    if (invisibilityCoroutine != null)
                        HudManager.Instance.StopCoroutine(invisibilityCoroutine);
                    invisibilityCoroutine = HudManager.Instance.StartCoroutine(SendFakePositionCoroutine());

                    // Apenas cosméticos, não mova o player local!
                    PlayerControl.LocalPlayer.cosmetics.nameText.gameObject.SetActive(false);
                    PlayerControl.LocalPlayer.cosmetics.hat.gameObject.SetActive(false);
                    PlayerControl.LocalPlayer.cosmetics.skin.gameObject.SetActive(false);

                    Debug.Log("Invisibilidade ativada (apenas local, sem host).");
                }
                else
                {
                    if (invisibilityCoroutine != null)
                    {
                        HudManager.Instance.StopCoroutine(invisibilityCoroutine);
                        invisibilityCoroutine = null;
                    }
                    // Não precisa enviar posição real para todos, apenas restaure cosméticos
                    PlayerControl.LocalPlayer.cosmetics.nameText.gameObject.SetActive(true);
                    PlayerControl.LocalPlayer.cosmetics.hat.gameObject.SetActive(true);
                    PlayerControl.LocalPlayer.cosmetics.skin.gameObject.SetActive(true);

                    Debug.Log("Invisibilidade desativada (apenas local, sem host).");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Erro ao alternar invisibilidade: {e}");
            }
        }

        private static IEnumerator SendFakePositionCoroutine()
        {
            while (true)
            {
                Vector2 fakePosition = new Vector2(1000f, 1000f);
                // Só envia para si mesmo, não depende de host e não afeta outros jogadores
                PlayerControl.LocalPlayer.NetTransform.SnapTo(fakePosition);
                yield return new WaitForSeconds(0.2f);
            }
        }

        /// <summary>
        /// Amplia o campo de visão do jogador.
        /// </summary>
        public static void IncreaseVision(float multiplier = 2.0f)
        {
            zoomOutEnabled = true;
            customZoomValue = multiplier;
            Debug.Log($"[Cheat] Zoom Out ativado: {customZoomValue}");
        }

        /// <summary>
        /// Restaura a visão padrão (desativa o Zoom Out).
        /// </summary>
        public static void ResetVision()
        {
            zoomOutEnabled = false;
            Debug.Log("[Cheat] Zoom Out desativado");
        }

        // Harmony Patch para modificar o zoom da câmera do jogador
        [HarmonyPatch(typeof(FollowerCamera), nameof(FollowerCamera.Update))]
        public static class ZoomOutPatch
        {
            static void Postfix(FollowerCamera __instance)
            {
                if (zoomOutEnabled && __instance != null)
                {
                    var cam = __instance.GetComponent<Camera>();
                    if (cam != null)
                    {
                        cam.orthographicSize = customZoomValue;
                    }
                }
            }
        }

        /// <summary>
        /// Executa uma matança massiva segura de crewmates com bypass.
        /// </summary>
        public static void SafeMassKill()
        {
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
            {
                Debug.LogWarning("Você precisa ser o host para usar este cheat.");
                return;
            }
            HudManager.Instance.StartCoroutine(KillAllCoroutine(true, false));
            LogCheat("Matança massiva segura de crewmates iniciada.");
        }

        private static IEnumerator KillAllCoroutine(bool crewOnly = false, bool impostorsOnly = false)
        {
            if (ShipStatus.Instance == null || PlayerControl.LocalPlayer == null || AmongUsClient.Instance == null || !AmongUsClient.Instance.IsGameStarted || !AmongUsClient.Instance.AmHost)
                yield break;

            List<PlayerControl> targets = PlayerControl.AllPlayerControls.ToArray()
                .Where(target => target != PlayerControl.LocalPlayer && IsValidKillTarget(target, crewOnly, impostorsOnly))
                .ToList();

            float baseDelay = 0f;
            foreach (var target in targets)
            {
                yield return new WaitForSeconds(baseDelay);
                BroadcastKillBypass(PlayerControl.LocalPlayer, target, 0f);
                baseDelay += random.Next(100, 300) / 1000f;
            }
        }

        #endregion

        #region Utilitários
        private static bool ValidatePlayer(byte playerId) => PlayerControl.AllPlayerControls.ToArray().Any(p => p.PlayerId == playerId);
        #endregion


        #region Patches
        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
        public static class PlayerControlHandleRpcPatch
        {
            public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] Hazel.MessageReader reader)
            {


                if (callId == RPC_SET_SCANNER)
                {
                    HandleScannerRPC(__instance, callId, reader);
                    return;
                }

                if (callId == RPC_SET_INVISIBILITY)
                {
                   
                }

                if (callId == CUSTOM_RPC_PHANTOM_POOF)
                {
                    // ... existing code ...
                }
            }
        }
        #endregion
        public static class MapCheats
        {
            public static void DestroyMap()
            {
                if (!AmongUsClient.Instance.AmHost)
                {
                    Debug.LogWarning("[MapCheats] Apenas o host pode remover o mapa ou lobby.");
                    return;
                }

                // Remove o LobbyBehaviour (no lobby)
                var lobby = LobbyBehaviour.Instance;
                if (lobby != null)
                {
                    (lobby as InnerNetObject)?.Despawn();
                    LobbyBehaviour.Instance = null; // Limpa o singleton!
                    Debug.Log("[MapCheats] LobbyBehaviour despawned e singleton limpo.");
                }

                // Remove o ShipStatus (em partida)
                var ship = ShipStatus.Instance;
                if (ship != null)
                {
                    (ship as InnerNetObject)?.Despawn();
                    ShipStatus.Instance = null;
                    Debug.Log("[MapCheats] ShipStatus despawned e singleton limpo.");
                }
            }

            public static void VoteKickPlayer(PlayerControl target, bool exploit = false)
            {
                if (target == null || AmongUsClient.Instance == null)
                {
                    Debug.LogWarning("Jogador alvo ou AmongUsClient não encontrado.");
                    return;
                }

                if (!exploit)
                {
                    // Modo normal: host vota para banir
                    VoteBanSystem.Instance.CmdAddVote(target.OwnerId);
                }
                else
                {
                    // Exploit: força todos a votarem para banir o alvo
                    foreach (var p in PlayerControl.AllPlayerControls)
                    {
                        var writer = AmongUsClient.Instance.StartRpcImmediately(
                            VoteBanSystem.Instance.NetId,
                            (byte)RpcCalls.AddVote,
                            SendOption.None,
                            AmongUsClient.Instance.HostId // Envia para o host
                        );
                        writer.Write(p.OwnerId);        // Quem está "votando"
                        writer.Write(target.OwnerId);   // Quem é o alvo
                        AmongUsClient.Instance.FinishRpcImmediately(writer);
                    }
                }
            }
            public static void SpawnLobby()
            {
                if (!AmongUsClient.Instance.AmHost)
                {
                    Debug.LogWarning("[MapCheats] Apenas o host pode criar o lobby.");
                    return;
                }

                if (LobbyBehaviour.Instance == null)
                {
                    var prefab = DestroyableSingleton<GameStartManager>.Instance.LobbyPrefab;
                    if (prefab != null)
                    {
                        LobbyBehaviour.Instance = UnityEngine.Object.Instantiate<LobbyBehaviour>(prefab);
                        AmongUsClient.Instance.Spawn(LobbyBehaviour.Instance, -2, SpawnFlags.None);
                        Debug.Log("[MapCheats] LobbyBehaviour spawned via prefab.");
                    }
                    else
                    {
                        Debug.LogWarning("[MapCheats] LobbyPrefab não encontrado em GameStartManager.");
                    }
                }
                else
                {
                    Debug.LogWarning("[MapCheats] LobbyBehaviour já existe.");
                }
            }

        }

        public static void RotatePlayersInSpiralDance(float spiralTurns = 1.2f, float spiralSpacing = 0.38f, float speed = 1.1f, float duration = 4.5f)
        {
            HudManager.Instance.StartCoroutine(RotatePlayersSpiralDanceCoroutine(spiralTurns, spiralSpacing, speed, duration));
        }

        private static IEnumerator RotatePlayersSpiralDanceCoroutine(float spiralTurns, float spiralSpacing, float speed, float duration)
        {
            var players = PlayerControl.AllPlayerControls.ToArray();
            int count = players.Length;
            if (count == 0) yield break;

            // Salvar posições originais
            Vector2[] originalPositions = new Vector2[count];
            Vector2 center = Vector2.zero;

            for (int i = 0; i < count; i++)
            {
                if (players[i] == null) continue;
                originalPositions[i] = players[i].transform.position;
                center += (Vector2)players[i].transform.position;
            }
            center /= count;

            float elapsed = 0f;
            float smoothFactor = 0.85f; // Aumentado para movimento mais suave
            float spiralDuration = duration * 0.5f; // metade para ir, metade para voltar

            // Movimento de ida pela espiral - Fase de expansão
            while (elapsed < spiralDuration)
            {
                float t = elapsed / spiralDuration;
                float pulseEffect = 1.0f + 0.05f * Mathf.Sin(t * 12f); // Efeito de pulso suave

                for (int i = 0; i < count; i++)
                {
                    if (players[i] == null || players[i].NetTransform == null) continue;

                    // Atraso personalizado para cada jogador
                    float playerDelay = (float)i / (float)count * 0.5f;
                    float progress = Mathf.Clamp01((t - playerDelay) / (1.0f - playerDelay));

                    // Cálculo da posição na espiral com efeito de pulso
                    float angle = spiralTurns * 2f * Mathf.PI * progress;
                    float radius = spiralSpacing * angle * pulseEffect;
                    Vector2 targetPos = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

                    // Movimento suave com aceleração/desaceleração natural
                    float dynamicSmoothFactor = smoothFactor * (0.9f + 0.2f * Mathf.Sin(progress * Mathf.PI));
                    Vector2 lerped = Vector2.Lerp(players[i].transform.position, targetPos, dynamicSmoothFactor);
                    players[i].NetTransform.RpcSnapTo(lerped);
                }

                // Velocidade variável para efeito mais natural
                float dynamicSpeed = speed * (0.8f + 0.4f * Mathf.Pow(t, 0.5f));
                elapsed += Time.deltaTime * dynamicSpeed;
                yield return null;
            }

            // Pequena pausa no ápice da espiral
            float pauseTime = 0.3f;
            yield return new WaitForSeconds(pauseTime);

            // Movimento de volta pela espiral - Fase de contração
            elapsed = 0f;
            while (elapsed < spiralDuration)
            {
                float t = elapsed / spiralDuration;
                float pulseEffect = 1.0f + 0.05f * Mathf.Sin(t * 12f); // Efeito de pulso suave

                for (int i = 0; i < count; i++)
                {
                    if (players[i] == null || players[i].NetTransform == null) continue;

                    // Atraso personalizado para cada jogador (invertido para o retorno)
                    float playerDelay = (float)(count - i - 1) / (float)count * 0.5f;
                    float progress = 1.0f - Mathf.Clamp01((t - playerDelay) / (1.0f - playerDelay));

                    // Cálculo da posição na espiral com efeito de pulso
                    float angle = spiralTurns * 2f * Mathf.PI * progress;
                    float radius = spiralSpacing * angle * pulseEffect;
                    Vector2 targetPos = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

                    // Movimento suave com aceleração/desaceleração natural
                    float dynamicSmoothFactor = smoothFactor * (0.9f + 0.2f * Mathf.Sin(progress * Mathf.PI));
                    Vector2 lerped = Vector2.Lerp(players[i].transform.position, targetPos, dynamicSmoothFactor);
                    players[i].NetTransform.RpcSnapTo(lerped);
                }

                // Velocidade variável para efeito mais natural
                float dynamicSpeed = speed * (0.8f + 0.4f * Mathf.Pow(1 - t, 0.5f));
                elapsed += Time.deltaTime * dynamicSpeed;
                yield return null;
            }

            // Restaurar posições originais com transição suave
            float restoreTime = 0.4f; // Aumentado para transição mais suave
            elapsed = 0f;
            while (elapsed < restoreTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / restoreTime);
                for (int i = 0; i < count; i++)
                {
                    if (players[i] == null || players[i].NetTransform == null) continue;

                    // Posição
                    Vector2 lerped = Vector2.Lerp(players[i].transform.position, originalPositions[i], t);
                    players[i].NetTransform.RpcSnapTo(lerped);
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Garantir que todos voltem exatamente às posições originais
            for (int i = 0; i < count; i++)
            {
                if (players[i] != null && players[i].NetTransform != null)
                {
                    players[i].NetTransform.RpcSnapTo(originalPositions[i]);
                }
            }
        }


    }
}
