using UnityEngine;
using ModMenuCrew.Features;
using ModMenuCrew.UI.Styles;
using Assets.InnerNet;
using BepInEx.Unity.IL2CPP.Utils;

namespace ModMenuCrew.UI.Managers
{
    public class CheatManager
    {
        public static CheatManager Instance { get; private set; }

        public bool NoShapeshiftCooldown { get; set; }
        public bool EndlessVentTime { get; set; }
        public bool NoVentCooldown { get; set; }
        public bool EndlessShapeshiftDuration { get; set; }
        public bool NoVitalsCooldown { get; set; }
        public bool EndlessBattery { get; set; }
        public bool NoTrackingCooldown { get; set; }
        public bool EndlessTracking { get; set; }
        public bool AllowVenting { get; set; }
        public bool TeleportWithCursor { get; set; }
        public bool NoKillCooldown { get; set; }

        // Variáveis internas
        private bool showGeneralCheats = true;
        private bool showRoleCheats = true;
        private bool previousAllowVenting = false;
        private float visionMultiplier = 1.0f;
        private float previousVisionMultiplier = 1.0f;

        public CheatManager()
        {
            Instance = this;
        }

        public void DrawCheatsTab()
        {
            // Render without an internal scroll; DragWindow already wraps content in a scroll view.
            GUILayout.BeginVertical(GuiStyles.SectionStyle);
            try
            {
                DrawGeneralCheatsSection();
                GUILayout.Space(15);
                DrawRoleSpecificCheatsSection();
            }
            finally
            {
                GUILayout.EndVertical();
            }
        }

        private void DrawGeneralCheatsSection()
        {
            showGeneralCheats = GUILayout.Toggle(showGeneralCheats, "General Cheats ▼", GuiStyles.HeaderStyle);
            if (!showGeneralCheats) return;
            GUILayout.BeginVertical(GuiStyles.SectionStyle);
            try
            {
                GUILayout.Space(10);
                GUILayout.Label("Quick Actions", GuiStyles.HeaderStyle);
                GUILayout.Space(10);
                if (GUILayout.Button("Complete All Tasks", GuiStyles.ButtonStyle))
                {
                    if (HudManager.Instance != null)
                        HudManager.Instance.StartCoroutine(GameCheats.CompleteAllTasksWithDelay(0.2f));
                    else
                        GameCheats.CompleteAllTasks();
                }
                if (GUILayout.Button("Close Meeting", GuiStyles.ButtonStyle)) GameCheats.CloseMeeting();
                if (GUILayout.Button("Bypass Scanner", GuiStyles.ButtonStyle)) HudManager.Instance.StartCoroutine(GameCheats.BypassScannerWithTimeout(12f));
                if (GUILayout.Button("Reveal Impostors", GuiStyles.ButtonStyle)) GameCheats.RevealImpostors();
                GUILayout.Space(10);

                GUILayout.Label("Kill Options (HOST ONLY)", GuiStyles.HeaderStyle);
                GUILayout.Space(10);
                if (GUILayout.Button("Kill All Players", GuiStyles.ButtonStyle))
                {
                    if (IsHost()) GameCheats.KillAll(); else HudManager.Instance.Notifier.AddDisconnectMessage("You must be the host.");
                }
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Kill Crew Only", GuiStyles.ButtonStyle))
                {
                    if (IsHost()) GameCheats.KillAll(crewOnly: true); else HudManager.Instance.Notifier.AddDisconnectMessage("You must be the host.");
                }
                if (GUILayout.Button("Kill Impostors Only", GuiStyles.ButtonStyle))
                {
                    if (IsHost()) GameCheats.KillAll(impostorsOnly: true); else HudManager.Instance.Notifier.AddDisconnectMessage("You must be the host.");
                }
                GUILayout.EndHorizontal();

                AllowVenting = GUILayout.Toggle(AllowVenting, "Allow Venting (All Roles)", GuiStyles.ToggleStyle);
                TeleportWithCursor = GUILayout.Toggle(TeleportWithCursor, "Teleport With Cursor", GuiStyles.ToggleStyle);
                NoKillCooldown = GUILayout.Toggle(NoKillCooldown, "No Kill Cooldown (Impostor)", GuiStyles.ToggleStyle);

                visionMultiplier = GUILayout.HorizontalSlider(visionMultiplier, 0.5f, 15f);
                GUILayout.Label($"Vision Multiplier: {visionMultiplier:F1}x", GuiStyles.HeaderStyle);
                GUILayout.Space(10);
            }
            finally
            {
                GUILayout.EndVertical();
            }
        }

        private void DrawRoleSpecificCheatsSection()
        {
            showRoleCheats = GUILayout.Toggle(showRoleCheats, "Role Cheats ▼", GuiStyles.HeaderStyle);
            if (!showRoleCheats) return;
            GUILayout.BeginVertical(GuiStyles.SectionStyle);
            try
            {
                GUILayout.Label("Role-Specific Cheats", GuiStyles.HeaderStyle);
                GUILayout.Space(10);
                EndlessVentTime = GUILayout.Toggle(EndlessVentTime, "Endless Vent Time (Engineer)", GuiStyles.ToggleStyle);
                NoVentCooldown = GUILayout.Toggle(NoVentCooldown, "No Vent Cooldown (Engineer)", GuiStyles.ToggleStyle);
                EndlessShapeshiftDuration = GUILayout.Toggle(EndlessShapeshiftDuration, "Endless Shapeshift Duration", GuiStyles.ToggleStyle);
                NoShapeshiftCooldown = GUILayout.Toggle(NoShapeshiftCooldown, "No Shapeshift Cooldown", GuiStyles.ToggleStyle);
                EndlessBattery = GUILayout.Toggle(EndlessBattery, "Endless Battery (Scientist)", GuiStyles.ToggleStyle);
                NoVitalsCooldown = GUILayout.Toggle(NoVitalsCooldown, "No Vitals Cooldown (Scientist)", GuiStyles.ToggleStyle);
                NoTrackingCooldown = GUILayout.Toggle(NoTrackingCooldown, "No Tracking Cooldown (Tracker)", GuiStyles.ToggleStyle);
                EndlessTracking = GUILayout.Toggle(EndlessTracking, "Endless Tracking (Tracker)", GuiStyles.ToggleStyle);
                GUILayout.Space(10);
            }
            finally
            {
                GUILayout.EndVertical();
            }
        }

        public void Update()
        {
            if (!PlayerControl.LocalPlayer) return;

            if (TeleportWithCursor != GameCheats.TeleportToCursorEnabled)
            {
                GameCheats.TeleportToCursorEnabled = TeleportWithCursor;
            }

            if (AllowVenting && !previousAllowVenting)
            {
                RoleCheats.EnableVentingForAll(DestroyableSingleton<HudManager>.Instance);
            }
            if (AllowVenting && PlayerControl.LocalPlayer != null)
            {
                DestroyableSingleton<HudManager>.Instance.ImpostorVentButton.gameObject.SetActive(true);
            }
            previousAllowVenting = AllowVenting;

            if (Mathf.Abs(visionMultiplier - previousVisionMultiplier) > 0.01f)
            {
                GameCheats.IncreaseVision(visionMultiplier);
                previousVisionMultiplier = visionMultiplier;
            }

            var playerRole = PlayerControl.LocalPlayer.Data?.Role;
            if (playerRole == null) return;

            try
            {
                if (playerRole is EngineerRole engineerRole && EndlessVentTime)
                    engineerRole.inVentTimeRemaining = RoleCheats.MAX_SAFE_VALUE;
                else if (playerRole is ShapeshifterRole shapeshifterRole && EndlessShapeshiftDuration)
                    shapeshifterRole.durationSecondsRemaining = RoleCheats.MAX_SAFE_VALUE;
                else if (playerRole is TrackerRole trackerRole && EndlessTracking)
                    trackerRole.durationSecondsRemaining = RoleCheats.MAX_SAFE_VALUE;

                // No Kill Cooldown for Impostors
                if (NoKillCooldown && playerRole.IsImpostor)
                {
                    PlayerControl.LocalPlayer.SetKillTimer(0f);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error updating continuous role cheats: {ex}");
            }
        }

        private static bool IsHost()
        {
            return AmongUsClient.Instance.AmHost;
        }
    }
}