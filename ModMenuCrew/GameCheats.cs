using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;

namespace ModMenuCrew.Features
{
    /// <summary>
    /// Simplified GameCheats for Showcase Version
    /// </summary>
    public static class GameCheats
    {
        public const byte RPC_SET_SCANNER = 15;
        private static readonly System.Random random = new System.Random();
        public static bool TeleportToCursorEnabled = false;

        private static void LogCheat(string message) => Debug.Log($"[Cheat] {message}");

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
        /// Completa todas as tarefas do jogador local.
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
                HudManager.Instance.StartCoroutine(CompleteAllTasksWithDelay(0.2f).WrapToIl2Cpp());
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

            var taskInfosSnapshot = taskList.ToArray();
            var idsToComplete = new List<int>();
            foreach (var ti in taskInfosSnapshot)
                if (ti != null && !ti.Complete)
                    idsToComplete.Add((int)ti.Id);

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

                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(
                    PlayerControl.LocalPlayer.NetId, (byte)RpcCalls.CompleteTask, SendOption.Reliable, -1);
                writer.WritePacked(id);
                AmongUsClient.Instance.FinishRpcImmediately(writer);

                try { ((dynamic)match).Complete = true; } catch { }
                LogCheat($"Tarefa {id} completada. (Host: {isHost})");

                yield return new WaitForSeconds(Mathf.Max(0.05f, perTaskDelay));
            }

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
            LogCheat("Todas as tarefas completadas.");
        }
        #endregion

        #region Cheats de Movimento
        /// <summary>
        /// Teletransporta o jogador para a posição do cursor.
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
                    AmongUsClient.Instance.FinishRpcImmediately(writer);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error in TeleportToCursor: {e}");
            }
        }

        /// <summary>
        /// Verifica o input do mouse para teleporte quando habilitado.
        /// </summary>
        public static void CheckTeleportInput()
        {
            if (TeleportToCursorEnabled && Input.GetMouseButtonDown(1))
            {
                TeleportToCursor();
            }
        }
        #endregion

        #region Cheats Visuais
        /// <summary>
        /// Revela impostores marcando seus nomes com a cor da role.
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
            LogCheat("Impostores revelados.");
        }

        /// <summary>
        /// Amplia o campo de visão do jogador.
        /// </summary>
        public static void IncreaseVision(float multiplier)
        {
            if (Camera.main != null)
            {
                Camera.main.orthographicSize = 3.0f * multiplier;
                LogCheat($"Visão ampliada para {multiplier}x.");
            }
        }

        /// <summary>
        /// Restaura a visão padrão.
        /// </summary>
        public static void ResetVision()
        {
            if (Camera.main != null)
            {
                Camera.main.orthographicSize = 3.0f;
                LogCheat("Visão restaurada ao padrão.");
            }
        }
        #endregion
    }
}
