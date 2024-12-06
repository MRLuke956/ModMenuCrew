using UnityEngine;
using ModMenuCrew.Features;
using ModMenuCrew.UI.Styles;

namespace ModMenuCrew.UI.Managers;

public class CheatManager
{
    private Vector2 scrollPosition;
    private bool showGeneralCheats = true;
    private bool showRoleCheats = true;

    // Role-specific toggles
    public bool EndlessVentTime { get; set; }
    public bool NoVentCooldown { get; set; }
    public bool EndlessShapeshiftDuration { get; set; }
    public bool NoVitalsCooldown { get; set; }
    public bool EndlessBattery { get; set; }
    public bool NoTrackingCooldown { get; set; }
    public bool NoTrackingDelay { get; set; }
    public bool EndlessTracking { get; set; }

    // General cheats
    public bool AllowVenting { get; set; }
    public bool WalkInVent { get; set; }
    public bool TeleportWithCursor { get; set; }
    public bool SpeedBoost { get; set; }

    public void DrawCheatsTab()
    {
        // Calculate scroll view height based on screen size
        float scrollViewHeight = Screen.height * 0.7f;
        scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(scrollViewHeight));

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
            GUILayout.EndScrollView();
        }
    }

    private void DrawGeneralCheatsSection()
    {
        showGeneralCheats = GUILayout.Toggle(showGeneralCheats, "General Cheats ▼", GuiStyles.HeaderStyle);
        if (!showGeneralCheats) return;

        GUILayout.BeginVertical(GuiStyles.SectionStyle);
        try
        {
            // Quick Actions
            GUILayout.Label("Quick Actions", GuiStyles.HeaderStyle);

            if (GUILayout.Button("Complete All Tasks", GuiStyles.ButtonStyle))
            {
                GameCheats.CompleteAllTasks();
            }

            if (GUILayout.Button("Close Meeting", GuiStyles.ButtonStyle))
            {
                GameCheats.CloseMeeting();
            }
            if (GUILayout.Button("Scanner", GuiStyles.ButtonStyle))
            {
                HudManager.Instance.Notifier.BroadcastMessage("LocalScanner");
                HudManager.Instance.Notifier.AddDisconnectMessage("LocalScanner");
                GameCheats.BypassScanner();
            }

            GUILayout.Space(10);
            GUILayout.Label("Kill Options", GuiStyles.HeaderStyle);

            if (GUILayout.Button("Kill All Players", GuiStyles.ButtonStyle))
            {
                GameCheats.KillAll();
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Kill Crew Only", GuiStyles.ButtonStyle))
            {
                GameCheats.KillAll(crewOnly: true);
            }
            if (GUILayout.Button("Kill Impostors Only", GuiStyles.ButtonStyle))
            {
                GameCheats.KillAll(impostorsOnly: true);
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Kick All From Vents", GuiStyles.ButtonStyle))
            {
                GameCheats.KickAllFromVents();
            }

            GUILayout.Space(15);
            GUILayout.Label("Toggle Options", GuiStyles.HeaderStyle);

            TeleportWithCursor = GUILayout.Toggle(TeleportWithCursor, "Teleport to Cursor (Right Click)", GuiStyles.ToggleStyle);
            GUILayout.Space(5);
            SpeedBoost = GUILayout.Toggle(SpeedBoost, "Speed Boost (2x)", GuiStyles.ToggleStyle);
            GUILayout.Space(5);
            AllowVenting = GUILayout.Toggle(AllowVenting, "Allow All Roles to Vent", GuiStyles.ToggleStyle);
            GUILayout.Space(5);
            WalkInVent = GUILayout.Toggle(WalkInVent, "Walk While in Vent", GuiStyles.ToggleStyle);
        }
        finally
        {
            GUILayout.EndVertical();
        }
    }

    private void DrawRoleSpecificCheatsSection()
    {
        showRoleCheats = GUILayout.Toggle(showRoleCheats, "Role-Specific Cheats ▼", GuiStyles.HeaderStyle);
        if (!showRoleCheats) return;

        GUILayout.BeginVertical(GuiStyles.SectionStyle);
        try
        {
            DrawRoleSection("Engineer", () => {
                EndlessVentTime = GUILayout.Toggle(EndlessVentTime, "Endless Vent Time", GuiStyles.ToggleStyle);
                GUILayout.Space(5);
                NoVentCooldown = GUILayout.Toggle(NoVentCooldown, "No Vent Cooldown", GuiStyles.ToggleStyle);
            });

            DrawRoleSection("Shapeshifter", () => {
                EndlessShapeshiftDuration = GUILayout.Toggle(EndlessShapeshiftDuration, "Endless Shapeshift Duration", GuiStyles.ToggleStyle);
            });

            DrawRoleSection("Scientist", () => {
                NoVitalsCooldown = GUILayout.Toggle(NoVitalsCooldown, "No Vitals Cooldown", GuiStyles.ToggleStyle);
                GUILayout.Space(5);
                EndlessBattery = GUILayout.Toggle(EndlessBattery, "Endless Battery", GuiStyles.ToggleStyle);
            });

            DrawRoleSection("Tracker", () => {
                NoTrackingCooldown = GUILayout.Toggle(NoTrackingCooldown, "No Tracking Cooldown", GuiStyles.ToggleStyle);
                GUILayout.Space(5);
                NoTrackingDelay = GUILayout.Toggle(NoTrackingDelay, "No Tracking Delay", GuiStyles.ToggleStyle);
                GUILayout.Space(5);
                EndlessTracking = GUILayout.Toggle(EndlessTracking, "Endless Tracking", GuiStyles.ToggleStyle);
            });
        }
        finally
        {
            GUILayout.EndVertical();
        }
    }

    private void DrawRoleSection(string roleName, System.Action drawContent)
    {
        GUILayout.Space(10);
        GUILayout.Label(roleName, GuiStyles.HeaderStyle);
        GUILayout.BeginVertical(GuiStyles.SectionStyle);
        try
        {
            drawContent();
        }
        finally
        {
            GUILayout.EndVertical();
        }
    }

    public void Update()
    {
        if (!PlayerControl.LocalPlayer) return;

        // Handle general cheats
        if (TeleportWithCursor)
        {
            GameCheats.TeleportToCursor();
        }

        if (SpeedBoost)
        {
            GameCheats.HandleSpeedBoost();
        }

        if (AllowVenting)
        {
            RoleCheats.EnableVentingForAll(DestroyableSingleton<HudManager>.Instance);
        }

        if (WalkInVent)
        {
            RoleCheats.AllowWalkingInVent();
        }

        // Handle role-specific cheats
        var playerRole = PlayerControl.LocalPlayer.Data?.Role;
        if (playerRole == null) return;

        try
        {
            if (playerRole is EngineerRole engineerRole)
            {
                RoleCheats.HandleEngineerCheats(engineerRole, EndlessVentTime, NoVentCooldown);
            }
            else if (playerRole is ShapeshifterRole shapeshifterRole)
            {
                RoleCheats.HandleShapeshifterCheats(shapeshifterRole, EndlessShapeshiftDuration);
            }
            else if (playerRole is ScientistRole scientistRole)
            {
                RoleCheats.HandleScientistCheats(scientistRole, NoVitalsCooldown, EndlessBattery);
            }
            else if (playerRole is TrackerRole trackerRole)
            {
                RoleCheats.HandleTrackerCheats(trackerRole, NoTrackingCooldown, NoTrackingDelay, EndlessTracking);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error updating role cheats: {ex}");
        }
    }
}