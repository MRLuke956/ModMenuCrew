using UnityEngine;
using ModMenuCrew.Features;
using ModMenuCrew.UI.Styles;
using BepInEx.Unity.IL2CPP.Utils.Collections;

namespace ModMenuCrew.UI.Managers
{
    public class CheatManager
    {
        public static CheatManager Instance { get; private set; }

        public bool TeleportWithCursor { get; set; }

        // Internal variables
        private bool showGeneralCheats = true;
        private float visionMultiplier = 1.0f;
        private float previousVisionMultiplier = 1.0f;

        public CheatManager()
        {
            Instance = this;
            TeleportWithCursor = ModMenuCrewPlugin.CfgTeleportWithCursor?.Value ?? false;
        }

        public void DrawCheatsTab()
        {
            GUILayout.BeginVertical(GuiStyles.SectionStyle);
            try
            {
                DrawGeneralCheatsSection();
            }
            finally
            {
                GUILayout.EndVertical();
            }
        }

        private void DrawGeneralCheatsSection()
        {
            showGeneralCheats = GUILayout.Toggle(showGeneralCheats, "Cheats ▼", GuiStyles.HeaderStyle);
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
                        HudManager.Instance.StartCoroutine(GameCheats.CompleteAllTasksWithDelay(0.2f).WrapToIl2Cpp());
                    else
                        GameCheats.CompleteAllTasks();
                }

                if (GUILayout.Button("Close Meeting", GuiStyles.ButtonStyle))
                    GameCheats.CloseMeeting();

                if (GUILayout.Button("Reveal Impostors", GuiStyles.ButtonStyle))
                    GameCheats.RevealImpostors();

                GUILayout.Space(10);

                bool previousTeleport = TeleportWithCursor;
                TeleportWithCursor = GUILayout.Toggle(TeleportWithCursor, "Teleport With Cursor", GuiStyles.ToggleStyle);
                if (previousTeleport != TeleportWithCursor && ModMenuCrewPlugin.CfgTeleportWithCursor != null)
                {
                    ModMenuCrewPlugin.CfgTeleportWithCursor.Value = TeleportWithCursor;
                }

                visionMultiplier = GUILayout.HorizontalSlider(visionMultiplier, 0.5f, 15f);
                GUILayout.Label($"Vision Multiplier: {visionMultiplier:F1}x", GuiStyles.HeaderStyle);
                GUILayout.Space(10);

                GUILayout.Label("<color=#FF6600>SHOWCASE VERSION</color>", GuiStyles.SubHeaderStyle);
                GUILayout.Label("Some features are disabled in this version.", GuiStyles.LabelStyle);
                GUILayout.Label("Visit crewcore.online for full access!", GuiStyles.LabelStyle);
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

            if (Mathf.Abs(visionMultiplier - previousVisionMultiplier) > 0.01f)
            {
                GameCheats.IncreaseVision(visionMultiplier);
                previousVisionMultiplier = visionMultiplier;
            }
        }
    }
}
